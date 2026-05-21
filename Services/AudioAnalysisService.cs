using System.Runtime.InteropServices;
using System.Threading;

namespace XboxMetroLauncher.Services;

public sealed class AudioAnalysisService : IDisposable
{
    private const int ClsctxAll = 23;
    private const int AudclntSharemodeShared = 0;
    private const int AudclntStreamflagsLoopback = 0x00020000;
    private const int WaveFormatIeeeFloat = 3;
    private const int WaveFormatPcm = 1;
    private const int WaveFormatExtensible = 0xFFFE;

    private static readonly Guid AudioClientGuid = new("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
    private static readonly Guid AudioCaptureClientGuid = new("C8ADBD64-E71E-48a0-A4DE-185C395CD317");
    private static readonly Guid KsdSubTypeIeeeFloat = new("00000003-0000-0010-8000-00aa00389b71");
    private static readonly Guid KsdSubTypePcm = new("00000001-0000-0010-8000-00aa00389b71");

    private readonly object _sync = new();
    private readonly float[] _analysisBuffer = new float[4096];
    private Thread? _thread;
    private CancellationTokenSource? _cts;
    private int _bufferWriteIndex;
    private double _bass;
    private double _mid;
    private double _treble;
    private double _loudness;
    private double _peak;
    private double _lastBass;
    private double _lastLoudness;

    public event EventHandler<AudioAnalysisFrame>? FrameReady;

    public void Start()
    {
        lock (_sync)
        {
            if (_thread?.IsAlive == true)
            {
                return;
            }

            _cts = new CancellationTokenSource();
            _thread = new Thread(() => CaptureLoop(_cts.Token))
            {
                IsBackground = true,
                Name = "Metro audio analyzer"
            };
            _thread.SetApartmentState(ApartmentState.MTA);
            _thread.Start();
        }
    }

    public void Stop()
    {
        CancellationTokenSource? cts;
        Thread? thread;
        lock (_sync)
        {
            cts = _cts;
            thread = _thread;
            _cts = null;
            _thread = null;
        }

        cts?.Cancel();
        if (thread?.IsAlive == true)
        {
            thread.Join(250);
        }

        cts?.Dispose();
    }

    public void Dispose() => Stop();

    private void CaptureLoop(CancellationToken token)
    {
        IMMDeviceEnumerator? enumerator = null;
        IMMDevice? device = null;
        IAudioClient? audioClient = null;
        IAudioCaptureClient? captureClient = null;
        IntPtr waveFormatPtr = IntPtr.Zero;

        try
        {
            enumerator = (IMMDeviceEnumerator)(object)new MMDeviceEnumerator();
            Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(0, 0, out device));
            var audioClientGuid = AudioClientGuid;
            Marshal.ThrowExceptionForHR(device.Activate(ref audioClientGuid, ClsctxAll, IntPtr.Zero, out var audioClientObject));
            audioClient = (IAudioClient)audioClientObject;
            Marshal.ThrowExceptionForHR(audioClient.GetMixFormat(out waveFormatPtr));

            var format = AudioFormat.FromPointer(waveFormatPtr);
            Marshal.ThrowExceptionForHR(audioClient.Initialize(AudclntSharemodeShared, AudclntStreamflagsLoopback, 1_000_000, 0, waveFormatPtr, Guid.Empty));
            var captureClientGuid = AudioCaptureClientGuid;
            Marshal.ThrowExceptionForHR(audioClient.GetService(ref captureClientGuid, out var captureClientObject));
            captureClient = (IAudioCaptureClient)captureClientObject;
            Marshal.ThrowExceptionForHR(audioClient.Start());

            while (!token.IsCancellationRequested)
            {
                Marshal.ThrowExceptionForHR(captureClient.GetNextPacketSize(out var packetFrames));
                while (packetFrames > 0)
                {
                    Marshal.ThrowExceptionForHR(captureClient.GetBuffer(out var data, out var frames, out var flags, out _, out _));
                    try
                    {
                        if ((flags & 0x2) != 0)
                        {
                            PushSilence((int)frames);
                        }
                        else
                        {
                            ReadSamples(data, (int)frames, format);
                        }
                    }
                    finally
                    {
                        Marshal.ThrowExceptionForHR(captureClient.ReleaseBuffer(frames));
                    }

                    Analyze(format.SampleRate);
                    Marshal.ThrowExceptionForHR(captureClient.GetNextPacketSize(out packetFrames));
                }

                Thread.Sleep(8);
            }
        }
        catch
        {
            // Audio capture is optional. If WASAPI loopback is not available,
            // the visualizer falls back to its internal idle motion.
        }
        finally
        {
            try
            {
                audioClient?.Stop();
            }
            catch
            {
            }

            if (waveFormatPtr != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(waveFormatPtr);
            }

            ReleaseCom(captureClient);
            ReleaseCom(audioClient);
            ReleaseCom(device);
            ReleaseCom(enumerator);
        }
    }

    private void ReadSamples(IntPtr data, int frames, AudioFormat format)
    {
        var channels = Math.Max(1, format.Channels);
        for (var frame = 0; frame < frames; frame++)
        {
            var sum = 0d;
            for (var channel = 0; channel < channels; channel++)
            {
                var offset = frame * format.BlockAlign + channel * format.BytesPerSample;
                sum += ReadSample(data, offset, format);
            }

            PushSample((float)(sum / channels));
        }
    }

    private static double ReadSample(IntPtr data, int offset, AudioFormat format)
    {
        if (format.IsFloat)
        {
            return Marshal.PtrToStructure<float>(IntPtr.Add(data, offset));
        }

        if (format.BitsPerSample == 16)
        {
            return Marshal.ReadInt16(data, offset) / 32768d;
        }

        if (format.BitsPerSample == 24)
        {
            var b0 = Marshal.ReadByte(data, offset);
            var b1 = Marshal.ReadByte(data, offset + 1);
            var b2 = Marshal.ReadByte(data, offset + 2);
            var value = b0 | (b1 << 8) | (b2 << 16);
            if ((value & 0x800000) != 0)
            {
                value |= unchecked((int)0xFF000000);
            }

            return value / 8388608d;
        }

        if (format.BitsPerSample == 32)
        {
            return Marshal.ReadInt32(data, offset) / 2147483648d;
        }

        return 0;
    }

    private void PushSilence(int frames)
    {
        for (var i = 0; i < frames; i++)
        {
            PushSample(0);
        }
    }

    private void PushSample(float sample)
    {
        _analysisBuffer[_bufferWriteIndex] = Math.Clamp(sample, -1f, 1f);
        _bufferWriteIndex = (_bufferWriteIndex + 1) % _analysisBuffer.Length;
    }

    private void Analyze(int sampleRate)
    {
        if (sampleRate <= 0)
        {
            sampleRate = 48000;
        }

        var rms = 0d;
        for (var i = 0; i < _analysisBuffer.Length; i++)
        {
            var value = _analysisBuffer[i];
            rms += value * value;
        }

        rms = Math.Sqrt(rms / _analysisBuffer.Length);

        var bassRaw = BandEnergy(sampleRate, 45, 65, 85, 110, 145);
        var midRaw = BandEnergy(sampleRate, 260, 420, 720, 1100, 1800);
        var trebleRaw = BandEnergy(sampleRate, 3200, 5200, 7600, 10200);

        var bass = NormalizeBand(bassRaw, 42);
        var mid = NormalizeBand(midRaw, 24);
        var treble = NormalizeBand(trebleRaw, 42);
        var loudness = NormalizeBand(rms, 12);
        var peak = Math.Clamp(Math.Max(bass - _lastBass * 0.72, loudness - _lastLoudness * 0.78) * 6.2, 0, 1);

        _bass = Envelope(_bass, bass, 0.86, 0.16);
        _mid = Envelope(_mid, mid, 0.58, 0.14);
        _treble = Envelope(_treble, treble, 0.72, 0.22);
        _loudness = Envelope(_loudness, loudness, 0.62, 0.12);
        _peak = Envelope(_peak, peak, 0.96, 0.11);
        _lastBass = _bass;
        _lastLoudness = _loudness;

        FrameReady?.Invoke(this, new AudioAnalysisFrame(_bass, _mid, _treble, _loudness, _peak));
    }

    private double BandEnergy(int sampleRate, params double[] frequencies)
    {
        var total = 0d;
        foreach (var frequency in frequencies)
        {
            total += Goertzel(sampleRate, frequency);
        }

        return total / frequencies.Length;
    }

    private double Goertzel(int sampleRate, double frequency)
    {
        var coeff = 2 * Math.Cos(2 * Math.PI * frequency / sampleRate);
        var s0 = 0d;
        var s1 = 0d;
        var s2 = 0d;

        for (var i = 0; i < _analysisBuffer.Length; i++)
        {
            var index = (_bufferWriteIndex + i) % _analysisBuffer.Length;
            var window = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (_analysisBuffer.Length - 1));
            s0 = _analysisBuffer[index] * window + coeff * s1 - s2;
            s2 = s1;
            s1 = s0;
        }

        var power = s1 * s1 + s2 * s2 - coeff * s1 * s2;
        return Math.Sqrt(Math.Max(0, power)) / (_analysisBuffer.Length * 0.5);
    }

    private static double NormalizeBand(double value, double gain)
        => Math.Clamp(1 - Math.Exp(-Math.Max(0, value) * gain), 0, 1);

    private static double Envelope(double current, double target, double attack, double release)
        => current + (target - current) * (target > current ? attack : release);

    private static void ReleaseCom(object? instance)
    {
        if (instance is not null && Marshal.IsComObject(instance))
        {
            Marshal.ReleaseComObject(instance);
        }
    }

    private readonly record struct AudioFormat(int Channels, int SampleRate, int BitsPerSample, int BlockAlign, bool IsFloat)
    {
        public int BytesPerSample => Math.Max(1, BitsPerSample / 8);

        public static AudioFormat FromPointer(IntPtr pointer)
        {
            var tag = (int)(ushort)Marshal.ReadInt16(pointer);
            var channels = (int)(ushort)Marshal.ReadInt16(pointer, 2);
            var sampleRate = Marshal.ReadInt32(pointer, 4);
            var blockAlign = (int)(ushort)Marshal.ReadInt16(pointer, 12);
            var bits = (int)(ushort)Marshal.ReadInt16(pointer, 14);
            var isFloat = tag == WaveFormatIeeeFloat;

            if (tag == WaveFormatExtensible)
            {
                var subFormat = Marshal.PtrToStructure<Guid>(IntPtr.Add(pointer, 24));
                isFloat = subFormat == KsdSubTypeIeeeFloat;
                if (bits == 0 && subFormat == KsdSubTypePcm)
                {
                    bits = 16;
                }
            }
            else if (tag != WaveFormatPcm && tag != WaveFormatIeeeFloat)
            {
                isFloat = true;
            }

            if (bits <= 0)
            {
                bits = isFloat ? 32 : 16;
            }

            return new AudioFormat(Math.Max(1, channels), Math.Max(8000, sampleRate), bits, Math.Max(1, blockAlign), isFloat);
        }
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private sealed class MMDeviceEnumerator
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        int RegisterEndpointNotificationCallback(IntPtr client);
        int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    private interface IMMDevice
    {
        int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);
        int OpenPropertyStore(int access, out IntPtr properties);
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        int GetState(out int state);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2")]
    private interface IAudioClient
    {
        int Initialize(int shareMode, int streamFlags, long bufferDuration, long periodicity, IntPtr format, Guid audioSessionGuid);
        int GetBufferSize(out uint bufferFrames);
        int GetStreamLatency(out long latency);
        int GetCurrentPadding(out uint paddingFrames);
        int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closestMatch);
        int GetMixFormat(out IntPtr deviceFormat);
        int GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);
        int Start();
        int Stop();
        int Reset();
        int SetEventHandle(IntPtr eventHandle);
        int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317")]
    private interface IAudioCaptureClient
    {
        int GetBuffer(out IntPtr data, out uint frames, out int flags, out long devicePosition, out long qpcPosition);
        int ReleaseBuffer(uint frames);
        int GetNextPacketSize(out uint frames);
    }
}
