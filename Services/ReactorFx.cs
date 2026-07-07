using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace WinForge.Services;

/// <summary>
/// 反應堆視覺特效（純內建 Composition）· Reactor visual FX using only in-box Microsoft.UI.Composition
/// (no Win2D). Cherenkov core glow, steam sprites, screen-shake, alarm strobe and a render clock that
/// pushes simulation snapshots into a shared property set so visuals never touch mutating sim state.
/// All helpers are static and reusable from both the in-tab page and the dedicated window.
/// </summary>
public static class ReactorFx
{
    /// <summary>把一個 Composition 容器掛上 XAML 元素 · Bind a Composition container to a XAML host.</summary>
    public static (Compositor c, ContainerVisual root) Bind(UIElement host)
    {
        var visual = ElementCompositionPreview.GetElementVisual(host);
        var c = visual.Compositor;
        var root = c.CreateContainerVisual();
        ElementCompositionPreview.SetElementChildVisual(host, root);
        return (c, root);
    }

    /// <summary>切連科夫輝光（藍光核芯）· Cherenkov radial glow; caller drives Scale/Opacity from power.</summary>
    public static SpriteVisual CherenkovGlow(Compositor c, float radius)
    {
        var brush = c.CreateRadialGradientBrush();
        brush.EllipseCenter = new Vector2(0.5f, 0.5f);
        brush.EllipseRadius = new Vector2(0.5f, 0.5f);
        brush.ColorStops.Add(c.CreateColorGradientStop(0.0f, Color.FromArgb(255, 0x6F, 0xC8, 0xFF)));
        brush.ColorStops.Add(c.CreateColorGradientStop(0.35f, Color.FromArgb(220, 0x1B, 0x6C, 0xFF)));
        brush.ColorStops.Add(c.CreateColorGradientStop(0.75f, Color.FromArgb(90, 0x1B, 0x6C, 0xFF)));
        brush.ColorStops.Add(c.CreateColorGradientStop(1.0f, Color.FromArgb(0, 0x1B, 0x6C, 0xFF)));

        var sprite = c.CreateSpriteVisual();
        sprite.Size = new Vector2(radius * 2, radius * 2);
        sprite.AnchorPoint = new Vector2(0.5f, 0.5f);
        sprite.Brush = brush;
        sprite.Opacity = 0.0f;
        return sprite;
    }

    /// <summary>一個上升蒸汽精靈池 · A pool of rising, fading steam sprites.</summary>
    public sealed class SteamPool
    {
        private readonly Compositor _c;
        private readonly ContainerVisual _parent;
        private readonly List<SpriteVisual> _sprites = new();
        private readonly Random _rng = new();
        private int _next;
        private double _accum;

        public SteamPool(Compositor c, ContainerVisual parent, int count)
        {
            _c = c; _parent = parent;
            var brush = c.CreateColorBrush(Color.FromArgb(120, 0xDD, 0xEE, 0xFF));
            for (int i = 0; i < count; i++)
            {
                var s = c.CreateSpriteVisual();
                s.Size = new Vector2(14, 14);
                s.AnchorPoint = new Vector2(0.5f, 0.5f);
                s.Brush = brush;
                s.Opacity = 0;
                _parent.Children.InsertAtTop(s);
                _sprites.Add(s);
            }
        }

        /// <summary>按蒸汽壓力比例噴發 · Spawn steam at a rate proportional to pressure (0..1).</summary>
        public void Spawn(double rate01, float baseX, float baseY, double dt)
        {
            if (_sprites.Count == 0) return;
            _accum += rate01 * 18.0 * dt; // spawns/sec at full pressure
            while (_accum >= 1.0)
            {
                _accum -= 1.0;
                var s = _sprites[_next];
                _next = (_next + 1) % _sprites.Count;
                float x = baseX + (float)(_rng.NextDouble() * 40 - 20);
                s.Offset = new Vector3(x, baseY, 0);
                s.Opacity = 0.7f;
                s.Scale = new Vector3(0.5f, 0.5f, 1);

                var rise = _c.CreateVector3KeyFrameAnimation();
                rise.InsertKeyFrame(1f, new Vector3(x + (float)(_rng.NextDouble() * 30 - 15), baseY - 90, 0));
                rise.Duration = TimeSpan.FromSeconds(2.2);
                s.StartAnimation("Offset", rise);

                var fade = _c.CreateScalarKeyFrameAnimation();
                fade.InsertKeyFrame(0f, 0.7f);
                fade.InsertKeyFrame(1f, 0f);
                fade.Duration = TimeSpan.FromSeconds(2.2);
                s.StartAnimation("Opacity", fade);

                var grow = _c.CreateVector3KeyFrameAnimation();
                grow.InsertKeyFrame(1f, new Vector3(1.8f, 1.8f, 1));
                grow.Duration = TimeSpan.FromSeconds(2.2);
                s.StartAnimation("Scale", grow);
            }
        }
    }

    /// <summary>畫面震動（熔毀加劇）· Looping randomized screen-shake; amplitude grows with severity.</summary>
    public static void ScreenShake(Visual root, float amplitude)
    {
        var c = root.Compositor;
        if (amplitude <= 0.01f) { root.StopAnimation("Offset"); root.Offset = Vector3.Zero; return; }
        var anim = c.CreateVector3KeyFrameAnimation();
        var rng = new Random();
        for (int i = 0; i <= 8; i++)
        {
            float fx = (float)(rng.NextDouble() * 2 - 1) * amplitude;
            float fy = (float)(rng.NextDouble() * 2 - 1) * amplitude;
            anim.InsertKeyFrame(i / 8f, new Vector3(fx, fy, 0));
        }
        anim.Duration = TimeSpan.FromMilliseconds(220);
        anim.IterationBehavior = AnimationIterationBehavior.Forever;
        root.StartAnimation("Offset", anim);
    }

    /// <summary>紅色警示閃爍 · Sharp red strobe (opacity keyframes).</summary>
    public static void RedStrobe(Visual overlay, bool on)
    {
        var c = overlay.Compositor;
        if (!on) { overlay.StopAnimation("Opacity"); overlay.Opacity = 0; return; }
        var anim = c.CreateScalarKeyFrameAnimation();
        var linear = c.CreateLinearEasingFunction();
        anim.InsertKeyFrame(0.0f, 0.0f, linear);
        anim.InsertKeyFrame(0.49f, 0.0f, linear);
        anim.InsertKeyFrame(0.50f, 0.45f, linear);
        anim.InsertKeyFrame(0.99f, 0.45f, linear);
        anim.InsertKeyFrame(1.0f, 0.0f, linear);
        anim.Duration = TimeSpan.FromMilliseconds(700);
        anim.IterationBehavior = AnimationIterationBehavior.Forever;
        overlay.StartAnimation("Opacity", anim);
    }

    /// <summary>
    /// 60 fps 渲染時鐘 · A render clock wrapping CompositionTarget.Rendering, subscribed only while the
    /// reactor view is active and explicitly stopped on unload (so it never wastes power in the
    /// background).
    /// </summary>
    public sealed class RenderClock
    {
        private EventHandler<object>? _handler;
        private bool _running;
        private DateTime _last = DateTime.UtcNow;

        public void Start(Action<double> onFrame)
        {
            if (_running) return;
            _running = true;
            _last = DateTime.UtcNow;
            _handler = (_, _) =>
            {
                var now = DateTime.UtcNow;
                double dt = (now - _last).TotalSeconds;
                _last = now;
                if (dt <= 0 || dt > 0.25) dt = 1.0 / 60.0;
                try { onFrame(dt); } catch { /* never let a render fault crash the app */ }
            };
            CompositionTarget.Rendering += _handler;
        }

        public void Stop()
        {
            if (!_running) return;
            _running = false;
            if (_handler is not null) CompositionTarget.Rendering -= _handler;
            _handler = null;
        }
    }
}
