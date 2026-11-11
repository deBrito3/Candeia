using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Candeia.Execution.Isolation;

// The runner's process on Windows. It is created suspended, with:
//   - a copy of the game's token lowered to low integrity, so it cannot write to the player's folders
//     or open the game's process;
//   - only its three pipe handles inherited, and a minimal environment;
//   - a Job Object that kills it when the game closes, forbids child processes, limits its memory and
//     processor time, blocks clipboard, desktop and system settings, and suppresses crash dialogs;
// and only resumed once it is inside the job. The Job Object structures follow Projeto Aurora's
// ProcessBudget, extended with the process-count, time and user-interface limits.
[SupportedOSPlatform("windows")]
sealed class IsolatedProcess : IDisposable
{
    readonly IntPtr job, process;
    bool disposed;

    public Stream Input { get; }
    public Stream Output { get; }
    public Stream Error { get; }
    public int Id { get; }

    IsolatedProcess(IntPtr job, IntPtr process, int id, Stream input, Stream output, Stream error)
    {
        this.job = job; this.process = process; Id = id; Input = input; Output = output; Error = error;
    }

    public static IsolatedProcess Start(string executable, IReadOnlyList<string> arguments, RunLimits limits, bool lowIntegrity, IReadOnlyDictionary<string, string> environment)
    {
        IntPtr job = CreateJob(limits), token = IntPtr.Zero, attributes = IntPtr.Zero, handles = IntPtr.Zero, block = IntPtr.Zero;
        IntPtr inRead = IntPtr.Zero, inWrite = IntPtr.Zero, outRead = IntPtr.Zero, outWrite = IntPtr.Zero, errRead = IntPtr.Zero, errWrite = IntPtr.Zero;
        bool attributesReady = false, started = false;
        var info = default(ProcessInformation);
        try
        {
            (inRead, inWrite) = Pipe(childEndIsRead: true);
            (outRead, outWrite) = Pipe(childEndIsRead: false);
            (errRead, errWrite) = Pipe(childEndIsRead: false);

            IntPtr size = IntPtr.Zero;
            InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
            attributes = Marshal.AllocHGlobal(size);
            Check(InitializeProcThreadAttributeList(attributes, 1, 0, ref size), "InitializeProcThreadAttributeList");
            attributesReady = true;
            handles = Marshal.AllocHGlobal(IntPtr.Size * 3);
            Marshal.WriteIntPtr(handles, 0, inRead);
            Marshal.WriteIntPtr(handles, IntPtr.Size, outWrite);
            Marshal.WriteIntPtr(handles, IntPtr.Size * 2, errWrite);
            Check(UpdateProcThreadAttribute(attributes, 0, (IntPtr)ProcThreadAttributeHandleList, handles, (IntPtr)(IntPtr.Size * 3), IntPtr.Zero, IntPtr.Zero), "UpdateProcThreadAttribute");

            var startup = new StartupInfoEx { lpAttributeList = attributes };
            startup.StartupInfo.cb = Marshal.SizeOf<StartupInfoEx>();
            startup.StartupInfo.dwFlags = StartfUseStdHandles;
            startup.StartupInfo.hStdInput = inRead;
            startup.StartupInfo.hStdOutput = outWrite;
            startup.StartupInfo.hStdError = errWrite;

            var commandLine = new StringBuilder(Quote(executable));
            foreach (var argument in arguments) commandLine.Append(' ').Append(Quote(argument));
            block = Marshal.StringToHGlobalUni(string.Concat(environment.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase).Select(p => $"{p.Key}={p.Value}\0")) + "\0");
            uint flags = CreateSuspended | CreateNoWindow | ExtendedStartupInfoPresent | CreateUnicodeEnvironment;
            string directory = Path.GetDirectoryName(executable) ?? AppContext.BaseDirectory;

            if (lowIntegrity)
            {
                token = LowIntegrityToken();
                Check(CreateProcessAsUserW(token, executable, commandLine, IntPtr.Zero, IntPtr.Zero, true, flags, block, directory, ref startup, out info), "CreateProcessAsUser");
            }
            else Check(CreateProcessW(executable, commandLine, IntPtr.Zero, IntPtr.Zero, true, flags, block, directory, ref startup, out info), "CreateProcess");
            started = true;
            Check(AssignProcessToJobObject(job, info.hProcess), "AssignProcessToJobObject");
            if (ResumeThread(info.hThread) == uint.MaxValue) throw new Win32Exception(Marshal.GetLastWin32Error(), "ResumeThread");
            CloseHandle(info.hThread);
            info.hThread = IntPtr.Zero;

            // The child's ends now belong to the child.
            CloseHandle(inRead); CloseHandle(outWrite); CloseHandle(errWrite);
            inRead = outWrite = errWrite = IntPtr.Zero;
            return new IsolatedProcess(job, info.hProcess, info.dwProcessId,
                new FileStream(new SafeFileHandle(inWrite, ownsHandle: true), FileAccess.Write, 1),
                new FileStream(new SafeFileHandle(outRead, ownsHandle: true), FileAccess.Read, 4096),
                new FileStream(new SafeFileHandle(errRead, ownsHandle: true), FileAccess.Read, 4096));
        }
        catch
        {
            if (started)
            {
                TerminateJobObject(job, 1);
                TerminateProcess(info.hProcess, 1);
                if (info.hThread != IntPtr.Zero) CloseHandle(info.hThread);
                CloseHandle(info.hProcess);
            }
            foreach (var handle in new[] { inRead, inWrite, outRead, outWrite, errRead, errWrite }) if (handle != IntPtr.Zero) CloseHandle(handle);
            CloseHandle(job);
            throw;
        }
        finally
        {
            if (attributesReady) DeleteProcThreadAttributeList(attributes);
            if (attributes != IntPtr.Zero) Marshal.FreeHGlobal(attributes);
            if (handles != IntPtr.Zero) Marshal.FreeHGlobal(handles);
            if (block != IntPtr.Zero) Marshal.FreeHGlobal(block);
            if (token != IntPtr.Zero) CloseHandle(token);
        }
    }

    public bool WaitForExit(int milliseconds) => WaitForSingleObject(process, (uint)Math.Max(0, milliseconds)) == 0;

    public int? ExitCode => GetExitCodeProcess(process, out uint code) && code != StillActive ? unchecked((int)code) : null;

    public long PeakMemory
    {
        get
        {
            var limits = default(ExtendedLimits);
            return QueryInformationJobObject(job, ExtendedLimitInformation, ref limits, Marshal.SizeOf<ExtendedLimits>(), IntPtr.Zero)
                ? (long)limits.PeakProcessMemoryUsed : 0;
        }
    }

    public void Kill() => TerminateJobObject(job, 1);

    // The integrity level of the running process (0x1000 = low, 0x2000 = medium), read from its token.
    internal int? IntegrityLevel
    {
        get
        {
            if (!OpenProcessToken(process, TokenQuery, out var token)) return null;
            try
            {
                GetTokenInformation(token, TokenIntegrityLevel, IntPtr.Zero, 0, out int size);
                var buffer = Marshal.AllocHGlobal(size);
                try
                {
                    if (!GetTokenInformation(token, TokenIntegrityLevel, buffer, size, out _)) return null;
                    var sid = Marshal.ReadIntPtr(buffer);
                    int count = Marshal.ReadByte(GetSidSubAuthorityCount(sid));
                    return Marshal.ReadInt32(GetSidSubAuthority(sid, (uint)(count - 1)));
                }
                finally { Marshal.FreeHGlobal(buffer); }
            }
            finally { CloseHandle(token); }
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        TerminateJobObject(job, 1);
        Input.Dispose(); Output.Dispose(); Error.Dispose();
        CloseHandle(process);
        CloseHandle(job);
    }

    static IntPtr CreateJob(RunLimits limits)
    {
        var job = CreateJobObjectW(IntPtr.Zero, null);
        if (job == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateJobObject");
        var extended = new ExtendedLimits
        {
            Basic = new BasicLimits
            {
                LimitFlags = LimitKillOnJobClose | LimitDieOnUnhandledException | LimitActiveProcess | LimitProcessMemory | LimitProcessTime,
                ActiveProcessLimit = 1,
                PerProcessUserTimeLimit = limits.CpuMilliseconds * 10_000L,
            },
            ProcessMemoryLimit = (UIntPtr)(ulong)limits.MemoryBytes,
        };
        var ui = new UiRestrictions { UIRestrictionsClass = UiLimitAll };
        if (!SetInformationJobObject(job, ExtendedLimitInformation, ref extended, Marshal.SizeOf<ExtendedLimits>())
            || !SetInformationJobObject(job, BasicUiRestrictions, ref ui, Marshal.SizeOf<UiRestrictions>()))
        {
            int error = Marshal.GetLastWin32Error();
            CloseHandle(job);
            throw new Win32Exception(error, "SetInformationJobObject");
        }
        return job;
    }

    static IntPtr LowIntegrityToken()
    {
        Check(OpenProcessToken(GetCurrentProcess(), TokenDuplicate | TokenQuery | TokenAdjustDefault | TokenAssignPrimary, out var current), "OpenProcessToken");
        IntPtr sid = IntPtr.Zero;
        try
        {
            Check(DuplicateTokenEx(current, 0, IntPtr.Zero, SecurityImpersonation, TokenPrimary, out var token), "DuplicateTokenEx");
            Check(ConvertStringSidToSidW("S-1-16-4096", out sid), "ConvertStringSidToSid");
            var label = new TokenMandatoryLabel { Label = new SidAndAttributes { Sid = sid, Attributes = SeGroupIntegrity } };
            if (!SetTokenInformation(token, TokenIntegrityLevel, ref label, Marshal.SizeOf<TokenMandatoryLabel>() + GetLengthSid(sid)))
            {
                int error = Marshal.GetLastWin32Error();
                CloseHandle(token);
                throw new Win32Exception(error, "SetTokenInformation");
            }
            return token;
        }
        finally
        {
            if (sid != IntPtr.Zero) LocalFree(sid);
            CloseHandle(current);
        }
    }

    // A pipe whose child end is inheritable and whose game end is not.
    static (IntPtr Read, IntPtr Write) Pipe(bool childEndIsRead)
    {
        var security = new SecurityAttributes { nLength = Marshal.SizeOf<SecurityAttributes>(), bInheritHandle = 1 };
        Check(CreatePipe(out var read, out var write, ref security, 0), "CreatePipe");
        Check(SetHandleInformation(childEndIsRead ? write : read, HandleFlagInherit, 0), "SetHandleInformation");
        return (read, write);
    }

    static string Quote(string argument) =>
        argument.Length > 0 && argument.IndexOfAny([' ', '\t', '"']) < 0 ? argument : "\"" + argument.Replace("\"", "\\\"") + "\"";

    static void Check(bool ok, string what)
    {
        if (!ok) throw new Win32Exception(Marshal.GetLastWin32Error(), what);
    }

    const uint CreateSuspended = 0x4, CreateNoWindow = 0x08000000, ExtendedStartupInfoPresent = 0x80000, CreateUnicodeEnvironment = 0x400;
    const int StartfUseStdHandles = 0x100;
    const uint HandleFlagInherit = 0x1, StillActive = 259;
    const int ProcThreadAttributeHandleList = 0x20002;
    const uint LimitProcessTime = 0x2, LimitActiveProcess = 0x8, LimitProcessMemory = 0x100, LimitDieOnUnhandledException = 0x400, LimitKillOnJobClose = 0x2000;
    const int BasicUiRestrictions = 4, ExtendedLimitInformation = 9;
    const uint UiLimitAll = 0xFF;
    const uint TokenAssignPrimary = 0x1, TokenDuplicate = 0x2, TokenQuery = 0x8, TokenAdjustDefault = 0x80;
    const int SecurityImpersonation = 2, TokenPrimary = 1, TokenIntegrityLevel = 25;
    const uint SeGroupIntegrity = 0x20;

    [StructLayout(LayoutKind.Sequential)] struct SecurityAttributes { public int nLength; public IntPtr lpSecurityDescriptor; public int bInheritHandle; }
    [StructLayout(LayoutKind.Sequential)]
    struct StartupInfo
    {
        public int cb;
        public IntPtr lpReserved, lpDesktop, lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }
    [StructLayout(LayoutKind.Sequential)] struct StartupInfoEx { public StartupInfo StartupInfo; public IntPtr lpAttributeList; }
    [StructLayout(LayoutKind.Sequential)] struct ProcessInformation { public IntPtr hProcess, hThread; public int dwProcessId, dwThreadId; }
    [StructLayout(LayoutKind.Sequential)]
    struct BasicLimits
    {
        public long PerProcessUserTimeLimit, PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize, MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass, SchedulingClass;
    }
    [StructLayout(LayoutKind.Sequential)] struct IoCounters { public ulong A, B, C, D, E, F; }
    [StructLayout(LayoutKind.Sequential)]
    struct ExtendedLimits
    {
        public BasicLimits Basic;
        public IoCounters Io;
        public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
    }
    [StructLayout(LayoutKind.Sequential)] struct UiRestrictions { public uint UIRestrictionsClass; }
    [StructLayout(LayoutKind.Sequential)] struct SidAndAttributes { public IntPtr Sid; public uint Attributes; }
    [StructLayout(LayoutKind.Sequential)] struct TokenMandatoryLabel { public SidAndAttributes Label; }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern IntPtr CreateJobObjectW(IntPtr attributes, string? name);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool SetInformationJobObject(IntPtr job, int infoClass, ref ExtendedLimits info, int length);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool SetInformationJobObject(IntPtr job, int infoClass, ref UiRestrictions info, int length);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool QueryInformationJobObject(IntPtr job, int infoClass, ref ExtendedLimits info, int length, IntPtr returned);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool TerminateJobObject(IntPtr job, uint exitCode);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool TerminateProcess(IntPtr process, uint exitCode);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool CreatePipe(out IntPtr read, out IntPtr write, ref SecurityAttributes attributes, int size);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool SetHandleInformation(IntPtr handle, uint mask, uint flags);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, int flags, ref IntPtr size);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags, IntPtr attribute, IntPtr value, IntPtr size, IntPtr previous, IntPtr returnSize);
    [DllImport("kernel32.dll")] static extern void DeleteProcThreadAttributeList(IntPtr list);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CreateProcessW(string application, StringBuilder commandLine, IntPtr processAttributes, IntPtr threadAttributes, bool inheritHandles,
        uint flags, IntPtr environment, string directory, ref StartupInfoEx startup, out ProcessInformation information);
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CreateProcessAsUserW(IntPtr token, string application, StringBuilder commandLine, IntPtr processAttributes, IntPtr threadAttributes, bool inheritHandles,
        uint flags, IntPtr environment, string directory, ref StartupInfoEx startup, out ProcessInformation information);
    [DllImport("kernel32.dll", SetLastError = true)] static extern uint ResumeThread(IntPtr thread);
    [DllImport("kernel32.dll", SetLastError = true)] static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool GetExitCodeProcess(IntPtr process, out uint code);
    [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr memory);
    [DllImport("advapi32.dll", SetLastError = true)] static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
    [DllImport("advapi32.dll", SetLastError = true)] static extern bool DuplicateTokenEx(IntPtr token, uint access, IntPtr attributes, int level, int type, out IntPtr duplicate);
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern bool ConvertStringSidToSidW(string sid, out IntPtr result);
    [DllImport("advapi32.dll", SetLastError = true)] static extern bool SetTokenInformation(IntPtr token, int infoClass, ref TokenMandatoryLabel info, int length);
    [DllImport("advapi32.dll")] static extern int GetLengthSid(IntPtr sid);
    [DllImport("advapi32.dll", SetLastError = true)] static extern bool GetTokenInformation(IntPtr token, int infoClass, IntPtr info, int length, out int returned);
    [DllImport("advapi32.dll")] static extern IntPtr GetSidSubAuthorityCount(IntPtr sid);
    [DllImport("advapi32.dll")] static extern IntPtr GetSidSubAuthority(IntPtr sid, uint index);
}
