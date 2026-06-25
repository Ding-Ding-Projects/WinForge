using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media;
using Windows.Media.Audio;
using Windows.Media.MediaProperties;
using Windows.Media.Render;

namespace WinForge.Services;

/// <summary>
/// 控制室合成音效引擎（純 C#，AudioGraph）· Synthesized control-room audio (pure managed, AudioGraph).
///
/// Generates every sound procedurally as 48 kHz stereo float PCM in the AudioGraph render callback:
/// an ambient reactor hum loop, a SCRAM klaxon, an annunciator buzzer, an ANSI Temporal-3 evacuation
/// tone, relay clicks and acknowledge beeps. No bundled audio assets, no ffmpeg, no AudioEngineService.
/// Degrades silently (all methods become no-ops) when there is no audio device.
///
/// SAFETY for the audio thread: the QuantumStarted handler performs NO allocation, NO locking and
/// throws NO exceptions. The UI sets volatile fields (Power/Scram/Meltdown + per-voice gates) and the
/// render thread reads them lock-free.
/// </summary>
public sealed class ReactorAudioEngine : IDisposable
{
    public static ReactorAudioEngine I { get; } = new();

    // ---- public lock-free control surface (set from UI) ----
    public bool Enabled { get; set; } = true;          // master mute (persisted reactor.audio)
    public volatile float Power;                        // 0..1 — drives hum fundamental + turbine whine
    public volatile bool Scram;
    public volatile bool Meltdown;

    private volatile bool _humOn;
    private volatile bool _klaxonOn;
    private volatile bool _buzzerOn;
    private volatile bool _evacOn;

    // One-shot triggers (consumed by the render thread).
    private long _beepRequest;       // packed: bit0 = accept(1)/reject(0); incremented each request
    private long _beepConsumed;
    private long _clickRequest;
    private long _clickConsumed;

    private AudioGraph? _graph;
    private AudioFrameInputNode? _input;
    private AudioDeviceOutputNode? _output;
    private volatile bool _dead;     // no device / failed → silent
    private volatile bool _starting;
    private bool _started;

    private const int SampleRate = 48000;
    private const int Channels = 2;

    // ---- persistent voice phases (render-thread only) ----
    private double _phHum1, _phHum2, _phHum3, _phHum4, _phWhine, _phKlaxon, _phLfo, _phBuzzer, _phEvac;
    private double _noiseLp;
    private double _humAmp, _whineAmp;          // smoothed amplitudes
    private double _evacTime;                    // seconds within the evac pattern cycle
    // one-shot envelopes
    private double _beepPhase, _beepEnv; private bool _beepAccept; private bool _beepActive;
    private double _clickEnv; private double _clickPhase; private bool _clickActive;
    private readonly Random _rng = new();

    private ReactorAudioEngine() { }

    /// <summary>延遲建立音訊圖（首次發聲時）· Lazily create the graph; degrade silently on failure.</summary>
    public async Task EnsureStartedAsync()
    {
        if (_started || _dead || _starting) return;
        _starting = true;
        try
        {
            // Respect persisted mute preference.
            Enabled = SettingsStore.Get("reactor.audio", "True") == "True";

            var settings = new AudioGraphSettings(AudioRenderCategory.SoundEffects)
            {
                QuantumSizeSelectionMode = QuantumSizeSelectionMode.LowestLatency,
            };
            var result = await AudioGraph.CreateAsync(settings);
            if (result.Status != AudioGraphCreationStatus.Success || result.Graph is null)
            {
                _dead = true; return;
            }
            _graph = result.Graph;

            var outResult = await _graph.CreateDeviceOutputNodeAsync();
            if (outResult.Status != AudioDeviceNodeCreationStatus.Success || outResult.DeviceOutputNode is null)
            {
                _dead = true; _graph.Dispose(); _graph = null; return;
            }
            _output = outResult.DeviceOutputNode;

            var props = AudioEncodingProperties.CreatePcm((uint)SampleRate, (uint)Channels, 32);
            props.Subtype = MediaEncodingSubtypes.Float;
            _input = _graph.CreateFrameInputNode(props);
            _input.AddOutgoingConnection(_output);
            _input.QuantumStarted += OnQuantumStarted;

            _graph.UnrecoverableErrorOccurred += (_, _) => { _dead = true; };
            _graph.Start();
            _started = true;
        }
        catch
        {
            _dead = true;
        }
        finally
        {
            _starting = false;
        }
    }

    // ---- public voice control (no-ops when dead/disabled) ----
    public void Hum(bool on) => _humOn = on;
    public void Klaxon(bool on) => _klaxonOn = on;
    public void Buzzer(bool on) => _buzzerOn = on;
    public void EvacTone(bool on) => _evacOn = on;

    public void Beep(bool accept)
    {
        _beepAccept = accept;
        Interlocked.Increment(ref _beepRequest);
    }

    public void RelayClick() => Interlocked.Increment(ref _clickRequest);

    /// <summary>停止所有發聲（離開頁面）· Stop all voices (page unload) — keeps the graph alive.</summary>
    public void StopVoices()
    {
        _humOn = _klaxonOn = _buzzerOn = _evacOn = false;
    }

    public void SetEnabled(bool on)
    {
        Enabled = on;
        SettingsStore.Set("reactor.audio", on ? "True" : "False");
    }

    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }

    private unsafe void OnQuantumStarted(AudioFrameInputNode sender, FrameInputNodeQuantumStartedEventArgs args)
    {
        if (_dead) return;
        int n = args.RequiredSamples;
        if (n <= 0) return;

        try
        {
            uint bytes = (uint)(n * Channels * sizeof(float));
            var frame = new AudioFrame(bytes);
            using (var buffer = frame.LockBuffer(AudioBufferAccessMode.Write))
            using (var reference = buffer.CreateReference())
            {
                ((IMemoryBufferByteAccess)reference).GetBuffer(out byte* dataInBytes, out uint capacity);
                float* dst = (float*)dataInBytes;
                FillSamples(dst, n);
            }
            sender.AddFrame(frame);
        }
        catch
        {
            _dead = true;
        }
    }

    private const double TwoPi = Math.PI * 2.0;

    private unsafe void FillSamples(float* dst, int n)
    {
        bool master = Enabled;
        // Consume one-shot triggers (cheap; no allocation).
        long br = Interlocked.Read(ref _beepRequest);
        if (br != _beepConsumed) { _beepConsumed = br; _beepActive = true; _beepEnv = 0; _beepPhase = 0; }
        long cr = Interlocked.Read(ref _clickRequest);
        if (cr != _clickConsumed) { _clickConsumed = cr; _clickActive = true; _clickEnv = 1.0; _clickPhase = 0; }

        double pwr = Power; if (pwr < 0) pwr = 0; if (pwr > 1.5) pwr = 1.5;
        double dt = 1.0 / SampleRate;

        for (int i = 0; i < n; i++)
        {
            double s = 0;

            if (master)
            {
                // ---- ambient hum (60/120/180/240 Hz) + filtered noise + slow tremolo ----
                double targetHum = _humOn ? 1.0 : 0.0;
                _humAmp += (targetHum - _humAmp) * 0.0008;
                if (_humAmp > 0.0005)
                {
                    double humFund = 60.0 * (1.0 + 0.04 * pwr);
                    _phHum1 += TwoPi * humFund * dt;
                    _phHum2 += TwoPi * humFund * 2 * dt;
                    _phHum3 += TwoPi * humFund * 3 * dt;
                    _phHum4 += TwoPi * humFund * 4 * dt;
                    double hum = Math.Sin(_phHum1) * 1.0
                               + Math.Sin(_phHum2) * 0.5
                               + Math.Sin(_phHum3) * 0.25
                               + Math.Sin(_phHum4) * 0.12;
                    _phLfo += TwoPi * 0.2 * dt;
                    double tremolo = 1.0 + 0.05 * Math.Sin(_phLfo);
                    double white = _rng.NextDouble() * 2.0 - 1.0;
                    _noiseLp += (white - _noiseLp) * 0.05;
                    s += (hum * 0.15 * tremolo + _noiseLp * 0.03) * _humAmp;

                    // turbine whine scales pitch + amplitude with power.
                    double whineFreq = 6000.0 + 4000.0 * pwr;
                    _phWhine += TwoPi * whineFreq * dt;
                    double targetWhine = _humOn ? (0.02 + 0.05 * pwr) : 0;
                    _whineAmp += (targetWhine - _whineAmp) * 0.0008;
                    s += Math.Sin(_phWhine) * _whineAmp;
                }

                // ---- SCRAM klaxon: square warbling 600↔1200 Hz via 3 Hz triangle LFO ----
                if (_klaxonOn)
                {
                    double tri = TriangleWave(3.0, ref _phKlaxon, dt); // -1..1
                    double f = 900.0 + 300.0 * tri;
                    // square via sign of an integrated phase
                    double sq = Math.Sign(Math.Sin(IntegratePhase(ref _phBuzzer, f, dt)));
                    s += sq * 0.4;
                }

                // ---- annunciator buzzer: 660 Hz square pulsed at 6 Hz ----
                if (_buzzerOn)
                {
                    double gate = (Math.Sin(TwoPi * 6.0 * _evacTimeCounter) > 0) ? 1 : 0;
                    double sq = Math.Sign(Math.Sin(IntegratePhase(ref _phEvac, 660.0, dt)));
                    s += sq * 0.18 * gate;
                }

                // ---- evacuation Temporal-3 (3100 Hz) only during meltdown ----
                if (_evacOn)
                {
                    _evacTime += dt;
                    double cyc = _evacTime % 3.0; // [0.5 on,0.5 off]×3, 1.5 off  (3.0 s cycle)
                    bool on = (cyc < 0.5) || (cyc >= 1.0 && cyc < 1.5);
                    if (on)
                    {
                        double sq = Math.Sign(Math.Sin(IntegratePhase(ref _phWhine, 3100.0, dt)));
                        s += sq * 0.25;
                    }
                }

                // ---- relay click (impulse + damped 250 Hz) ----
                if (_clickActive)
                {
                    double noise = (_rng.NextDouble() * 2 - 1);
                    _clickPhase += TwoPi * 250.0 * dt;
                    s += (noise * 0.5 + Math.Sin(_clickPhase) * 0.5) * _clickEnv * 0.35;
                    _clickEnv *= 0.9985; // ~5 ms decay
                    if (_clickEnv < 0.02) _clickActive = false;
                }

                // ---- acknowledge beep (90 ms sine, raised-cosine, rising/falling) ----
                if (_beepActive)
                {
                    _beepEnv += dt;
                    double dur = 0.09;
                    if (_beepEnv >= dur) { _beepActive = false; }
                    else
                    {
                        double f = _beepAccept
                            ? 800.0 + 300.0 * (_beepEnv / dur)
                            : 1100.0 - 300.0 * (_beepEnv / dur);
                        _beepPhase += TwoPi * f * dt;
                        double w = 0.5 - 0.5 * Math.Cos(TwoPi * _beepEnv / dur); // window
                        s += Math.Sin(_beepPhase) * 0.3 * w;
                    }
                }
            }

            // soft-limit
            double outv = Math.Tanh(s);
            float f32 = (float)outv;
            dst[i * 2] = f32;
            dst[i * 2 + 1] = f32;
            _evacTimeCounter += dt;
        }
    }

    private double _evacTimeCounter;

    private static double IntegratePhase(ref double phase, double freq, double dt)
    {
        phase += TwoPi * freq * dt;
        if (phase > TwoPi * 1e6) phase -= TwoPi * 1e6;
        return phase;
    }

    private static double TriangleWave(double freq, ref double phase, double dt)
    {
        phase += freq * dt;
        if (phase >= 1.0) phase -= 1.0;
        return 4.0 * Math.Abs(phase - 0.5) - 1.0; // -1..1
    }

    public void Dispose()
    {
        try
        {
            if (_input is not null) _input.QuantumStarted -= OnQuantumStarted;
            _graph?.Stop();
            _graph?.Dispose();
        }
        catch { /* best effort */ }
        _graph = null; _input = null; _output = null;
        _started = false; _dead = true;
    }
}
