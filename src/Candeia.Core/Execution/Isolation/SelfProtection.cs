using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Candeia.Execution.Isolation;

// A low-integrity process cannot write to the game's process, but Windows still lets it terminate it:
// PROCESS_TERMINATE belongs to the "execute" rights, and a process only refuses read and write from lower
// integrity levels by default. This relabels the game's own process at its current integrity level with
// "no read up, no write up, no execute up", so the runner can neither open nor end the game. Relabelling a
// process at or below its own level needs no special privilege.
[SupportedOSPlatform("windows")]
static class SelfProtection
{
    static readonly object Sync = new();
    static bool applied;

    public static void Apply()
    {
        lock (Sync)
        {
            if (applied) return;
            int level = CurrentIntegrity() ?? 0x2000;
            string sddl = $"S:(ML;;NWNRNX;;;S-1-16-{level})";
            if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(sddl, 1, out var descriptor, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "ConvertStringSecurityDescriptorToSecurityDescriptor");
            try
            {
                if (!GetSecurityDescriptorSacl(descriptor, out bool present, out var sacl, out _) || !present)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "GetSecurityDescriptorSacl");
                int result = SetSecurityInfo(GetCurrentProcess(), KernelObject, LabelSecurityInformation, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, sacl);
                if (result != 0) throw new Win32Exception(result, "SetSecurityInfo");
                applied = true;
            }
            finally { LocalFree(descriptor); }
        }
    }

    static int? CurrentIntegrity()
    {
        if (!OpenProcessToken(GetCurrentProcess(), 0x8, out var token)) return null;
        try
        {
            GetTokenInformation(token, 25, IntPtr.Zero, 0, out int size);
            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                if (!GetTokenInformation(token, 25, buffer, size, out _)) return null;
                var sid = Marshal.ReadIntPtr(buffer);
                int count = Marshal.ReadByte(GetSidSubAuthorityCount(sid));
                return Marshal.ReadInt32(GetSidSubAuthority(sid, (uint)(count - 1)));
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        finally { CloseHandle(token); }
    }

    const int KernelObject = 6;
    const uint LabelSecurityInformation = 0x10;

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(string sddl, uint revision, out IntPtr descriptor, IntPtr size);
    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool GetSecurityDescriptorSacl(IntPtr descriptor, out bool present, out IntPtr sacl, out bool defaulted);
    [DllImport("advapi32.dll")]
    static extern int SetSecurityInfo(IntPtr handle, int type, uint information, IntPtr owner, IntPtr group, IntPtr dacl, IntPtr sacl);
    [DllImport("advapi32.dll", SetLastError = true)] static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
    [DllImport("advapi32.dll", SetLastError = true)] static extern bool GetTokenInformation(IntPtr token, int infoClass, IntPtr info, int length, out int returned);
    [DllImport("advapi32.dll")] static extern IntPtr GetSidSubAuthorityCount(IntPtr sid);
    [DllImport("advapi32.dll")] static extern IntPtr GetSidSubAuthority(IntPtr sid, uint index);
    [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr memory);
}
