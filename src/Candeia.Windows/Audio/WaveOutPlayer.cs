using System.Runtime.InteropServices;
using Candeia.Audio;

namespace Candeia.Audio;

// Plays the mixer through the Windows waveOut library: 44.1 kHz stereo, 16 bits, four buffers of 25 ms
// (about 100 ms from a sound being started to it being heard). A thread of its own waits for each buffer
// to come back, fills it from the mixer and sends it again. Without a sound device, the game stays silent
// and Available is false.
sealed class WaveOutPlayer : IDisposable
{
    const int Buffers = 4, Frames = 1102; // 25 ms each
    const int WaveMapper = -1, CallbackEvent = 0x50000, Done = 1;

    readonly Mixer mixer;
    readonly AutoResetEvent ready = new(false);
    readonly IntPtr[] headers = new IntPtr[Buffers], data = new IntPtr[Buffers];
    readonly int headerSize = Marshal.SizeOf<WaveHeader>();
    readonly float[] mix = new float[Frames * 2];
    readonly short[] pcm = new short[Frames * 2];
    IntPtr device;
    Thread? thread;
    volatile bool running;

    public WaveOutPlayer(Mixer mixer) => this.mixer = mixer;

    public bool Available { get; private set; }

    public void Start()
    {
        var format = new WaveFormat
        {
            FormatTag = 1,
            Channels = 2,
            SamplesPerSec = Synth.Rate,
            AvgBytesPerSec = Synth.Rate * 4,
            BlockAlign = 4,
            BitsPerSample = 16,
        };
        if (waveOutOpen(out device, WaveMapper, ref format, ready.SafeWaitHandle.DangerousGetHandle(), IntPtr.Zero, CallbackEvent) != 0) return;
        for (int i = 0; i < Buffers; i++)
        {
            data[i] = Marshal.AllocHGlobal(Frames * 4);
            headers[i] = Marshal.AllocHGlobal(headerSize);
            Marshal.StructureToPtr(new WaveHeader { Data = data[i], BufferLength = Frames * 4 }, headers[i], false);
            waveOutPrepareHeader(device, headers[i], headerSize);
        }
        Available = true;
        running = true;
        thread = new Thread(Run) { IsBackground = true, Name = "Candeia sound", Priority = ThreadPriority.AboveNormal };
        thread.Start();
    }

    void Run()
    {
        for (int i = 0; i < Buffers; i++) Send(i);
        while (running)
        {
            ready.WaitOne(50);
            for (int i = 0; i < Buffers && running; i++)
                if ((Marshal.ReadInt32(headers[i], Flags) & Done) != 0) Send(i);
        }
    }

    static readonly int Flags = (int)Marshal.OffsetOf<WaveHeader>(nameof(WaveHeader.Flags));

    // Fills one buffer from the mixer and queues it.
    void Send(int i)
    {
        mixer.Mix(mix);
        for (int s = 0; s < mix.Length; s++) pcm[s] = (short)Math.Clamp(mix[s] * 32767f, -32768f, 32767f);
        Marshal.Copy(pcm, 0, data[i], pcm.Length);
        waveOutWrite(device, headers[i], headerSize);
    }

    public void Dispose()
    {
        if (!Available) return;
        running = false;
        thread?.Join(500);
        waveOutReset(device);
        for (int i = 0; i < Buffers; i++)
        {
            waveOutUnprepareHeader(device, headers[i], headerSize);
            Marshal.FreeHGlobal(headers[i]);
            Marshal.FreeHGlobal(data[i]);
        }
        waveOutClose(device);
        ready.Dispose();
        Available = false;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct WaveFormat
    {
        public short FormatTag;
        public short Channels;
        public int SamplesPerSec;
        public int AvgBytesPerSec;
        public short BlockAlign;
        public short BitsPerSample;
        public short Size;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct WaveHeader
    {
        public IntPtr Data;
        public int BufferLength;
        public int BytesRecorded;
        public IntPtr User;
        public int Flags;
        public int Loops;
        public IntPtr Next;
        public IntPtr Reserved;
    }

    [DllImport("winmm.dll")] static extern int waveOutOpen(out IntPtr device, int deviceId, ref WaveFormat format, IntPtr callback, IntPtr instance, int flags);
    [DllImport("winmm.dll")] static extern int waveOutPrepareHeader(IntPtr device, IntPtr header, int size);
    [DllImport("winmm.dll")] static extern int waveOutUnprepareHeader(IntPtr device, IntPtr header, int size);
    [DllImport("winmm.dll")] static extern int waveOutWrite(IntPtr device, IntPtr header, int size);
    [DllImport("winmm.dll")] static extern int waveOutReset(IntPtr device);
    [DllImport("winmm.dll")] static extern int waveOutClose(IntPtr device);
}
