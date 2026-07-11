using System;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace WinForge.Services;

/// <summary>Brief high-contrast state confirmation shown after a Video Conference Mute hotkey.</summary>
public static class VideoConferenceMuteOverlay
{
    private static Window? _window;
    private static TextBlock? _title;
    private static TextBlock? _detail;
    private static int _generation;

    public static void Show(ConferenceMuteState state)
    {
        if (_window is null)
        {
            var title = new TextBlock { FontSize = 22, FontWeight = FontWeights.SemiBold };
            var detail = new TextBlock { TextWrapping = TextWrapping.Wrap };
            var panel = new StackPanel { Spacing = 6 };
            panel.Children.Add(title);
            panel.Children.Add(detail);
            var window = new Window
            {
                Title = Loc.I.Pick("Video Conference Mute", "視像會議靜音"),
                Content = new Border { Child = panel, Padding = new Thickness(20), CornerRadius = new CornerRadius(10) },
            };
            window.Closed += OnClosed;
            var presenter = OverlappedPresenter.Create();
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            window.AppWindow.SetPresenter(presenter);
            window.AppWindow.Resize(new SizeInt32(360, 132));
            _window = window;
            _title = title;
            _detail = detail;
        }

        bool fullyMuted = (!state.MicrophoneAvailable || state.MicrophoneMuted) &&
                          (!state.CameraPrivacyControlEnabled || state.CameraMuted);
        _title!.Text = fullyMuted ? Loc.I.Pick("Conference muted", "會議已靜音") : Loc.I.Pick("Conference active", "會議啟用中");
        var microphone = !state.MicrophoneAvailable
            ? Loc.I.Pick("Microphone unavailable", "咪未能使用")
            : state.MicrophoneMuted ? Loc.I.Pick("Microphone muted", "咪已靜音") : Loc.I.Pick("Microphone live", "咪開啟中");
        var camera = !state.CameraPrivacyControlEnabled
            ? Loc.I.Pick("Camera privacy unchanged", "鏡頭私隱未更改")
            : state.CameraMuted ? Loc.I.Pick("Camera blocked", "鏡頭已封鎖") : Loc.I.Pick("Camera allowed", "鏡頭已允許");
        _detail!.Text = $"{microphone} · {camera}";
        _window.Activate();
        int generation = ++_generation;
        _ = HideAfterAsync(generation, App.Shell?.DispatcherQueue);
    }

    private static async Task HideAfterAsync(int generation, DispatcherQueue? dispatcher)
    {
        await Task.Delay(TimeSpan.FromSeconds(2));
        if (dispatcher is not null)
            dispatcher.TryEnqueue(() => CloseIfCurrent(generation));
    }

    private static void CloseIfCurrent(int generation)
    {
        if (generation != _generation || _window is null) return;
        try { _window.Close(); }
        catch { }
    }

    private static void OnClosed(object sender, WindowEventArgs args)
    {
        if (ReferenceEquals(sender, _window))
        {
            _window = null;
            _title = null;
            _detail = null;
        }
    }
}
