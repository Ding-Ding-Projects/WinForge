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
/// an ambient reactor hum loop, pump rumble, turbine whine, steam-relief hiss, radiation clicks, a
/// SCRAM klaxon, an annunciator buzzer, an ANSI Temporal-3 evacuation tone, relay clicks and
/// acknowledge beeps. No bundled audio assets, no ffmpeg, no AudioEngineService.
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
    public volatile float Flow;                         // 0..1 — drives RCP / coolant-rumble layer
    public volatile float TurbineRpm;                   // 0..~1800 — drives turbine-generator whine
    public volatile float Radiation;                    // normalized display value — drives geiger-like clicks
    public volatile bool Scram;
    public volatile bool Meltdown;
    public volatile bool SteamRelief;                   // PORV / safety / MSSV relief hiss

    private volatile bool _humOn;
    private volatile bool _klaxonOn;
    private volatile bool _buzzerOn;
    private volatile bool _evacOn;
    private volatile bool _ringbackOn;   // ISA-18.1 ringback chime (cleared-but-not-reset windows)

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
    private double _phPump1, _phPump2, _phTurbine1, _phTurbine2, _phRadClick;
    private double _phRing;                       // ringback chime oscillator phase
    private double _ringTime;                     // seconds within the 1 s ringback gate cycle
    private double _noiseLp, _steamLp;
    private double _humAmp, _whineAmp, _pumpAmp, _turbineAmp, _steamAmp; // smoothed amplitudes
    private double _radClickEnv;
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
    public void Ringback(bool on) => _ringbackOn = on;

    public void Beep(bool accept)
    {
        _beepAccept = accept;
        Interlocked.Increment(ref _beepRequest);
    }

    public void RelayClick() => Interlocked.Increment(ref _clickRequest);

    /// <summary>停止所有發聲（離開頁面）· Stop all voices (page unload) — keeps the graph alive.</summary>
    public void StopVoices()
    {
        _humOn = _klaxonOn = _buzzerOn = _evacOn = _ringbackOn = false;
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
        double flow = Flow; if (flow < 0) flow = 0; if (flow > 1.5) flow = 1.5;
        double rpm = TurbineRpm; if (rpm < 0) rpm = 0; if (rpm > 2400) rpm = 2400;
        double rpmNorm = Math.Min(1.35, rpm / 1800.0);
        double rad = Radiation; if (rad < 0) rad = 0; if (rad > 50) rad = 50;
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

                // ---- RCP / coolant rumble: low rotating mass + broadband pipe turbulence ----
                double targetPump = _humOn ? Math.Clamp(flow, 0.0, 1.0) : 0.0;
                _pumpAmp += (targetPump - _pumpAmp) * 0.0012;
                if (_pumpAmp > 0.0005)
                {
                    double f1 = 24.0 + 42.0 * Math.Clamp(flow, 0.0, 1.0);
                    double f2 = 2.0 * f1 + 5.0;
                    _phPump1 += TwoPi * f1 * dt;
                    _phPump2 += TwoPi * f2 * dt;
                    double white = _rng.NextDouble() * 2.0 - 1.0;
                    _noiseLp += (white - _noiseLp) * 0.025;
                    double turbulence = _noiseLp * (0.03 + 0.05 * Math.Clamp(flow, 0.0, 1.0));
                    s += (Math.Sin(_phPump1) * 0.11 + Math.Sin(_phPump2) * 0.035 + turbulence) * _pumpAmp;
                }

                // ---- turbine-generator whine: ramps with actual shaft speed, not power demand ----
                double targetTurbine = _humOn ? Math.Clamp(rpmNorm, 0.0, 1.2) : 0.0;
                _turbineAmp += (targetTurbine - _turbineAmp) * 0.0010;
                if (_turbineAmp > 0.0005)
                {
                    double f = 260.0 + 1260.0 * rpmNorm;
                    _phTurbine1 += TwoPi * f * dt;
                    _phTurbine2 += TwoPi * (f * 1.985) * dt;
                    double beat = 0.85 + 0.15 * Math.Sin(_phLfo * 0.37);
                    s += (Math.Sin(_phTurbine1) * 0.045 + Math.Sin(_phTurbine2) * 0.020) * _turbineAmp * beat;
                }

                // ---- steam relief / safety valve hiss: shaped high-pass noise with a slow flutter ----
                double targetSteam = SteamRelief ? 1.0 : 0.0;
                _steamAmp += (targetSteam - _steamAmp) * (targetSteam > _steamAmp ? 0.006 : 0.0012);
                if (_steamAmp > 0.0005)
                {
                    double white = _rng.NextDouble() * 2.0 - 1.0;
                    _steamLp += (white - _steamLp) * 0.012;
                    double hiss = white - _steamLp;
                    double flutter = 0.75 + 0.25 * Math.Sin(TwoPi * 11.0 * _evacTimeCounter);
                    s += hiss * 0.24 * _steamAmp * flutter;
                }

                // ---- radiation monitor texture: sparse clicks that become a chatter under high readings ----
                double clickRate = Math.Min(140.0, 1.5 + rad * 8.0 + (Meltdown ? 35.0 : 0.0));
                if (_rng.NextDouble() < clickRate * dt) { _radClickEnv = 1.0; _phRadClick = 0.0; }
                if (_radClickEnv > 0.001)
                {
                    _phRadClick += TwoPi * 3200.0 * dt;
                    s += Math.Sin(_phRadClick) * _radClickEnv * 0.11;
                    _radClickEnv *= 0.92;
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

                // ---- ISA-18.1 ringback chime: soft 988 Hz two-tone bell, gated ~1 Hz (0.45 s on) ----
                // Tonally distinct from (and quieter than) the 660 Hz alarm horn so the operator can hear
                // the difference between a NEW alarm and a CLEARED window awaiting RESET.
                if (_ringbackOn)
                {
                    double cyc = _ringTime;
                    _ringTime += dt;
                    if (_ringTime >= 1.0) _ringTime -= 1.0;
                    if (cyc < 0.45)
                    {
                        _phRing += TwoPi * 988.0 * dt;
                        double env = Math.Sin(Math.PI * (cyc / 0.45)); // smooth raised-cosine within the on-window
                        s += (Math.Sin(_phRing) * 0.7 + Math.Sin(_phRing * 2.0) * 0.2) * 0.10 * env;
                    }
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
