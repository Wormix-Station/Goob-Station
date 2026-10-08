using Content.Shared._Core.AudioDirector.Eui;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._Core.AudioDirector.UI.Controls;

public sealed class AudioTrackControl : BoxContainer
{
    private readonly int _groupId;
    private readonly int _trackId;
    private readonly float _length;

    private float _position;
    private bool _paused;
    private bool _loop;
    private float _sliderLockTimer;

    private readonly Slider _playbackSlider;
    private readonly Label _durationLabel;

    public event Action<int, int>? OnDeleteTrackPressed;
    public event Action<int, int, float, bool>? OnUpdateTrackPressed;
    public event Action<int, int, bool>? OnSetTrackPausedPressed;
    public event Action<int, int, float>? OnSetTrackTimePressed;
    public event Action<int, int, float, bool>? OnFadeTrackPressed;

    public AudioTrackControl(
        int groupId,
        AudioTrackEuiData track)
    {
        _groupId = groupId;
        _trackId = track.Id;
        _length = track.Length;
        _position = track.PlaybackPosition;
        _paused = track.Paused;
        _loop = track.Loop;

        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;

        var header = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true
        };

        var title = new Label
        {
            Text = track.Name,
            HorizontalExpand = true
        };

        var fadeButton = new Button
        {
            Text = "Fade",
            MinWidth = 72
        };

        fadeButton.OnPressed += _ =>
        {
            var window = new FadeAudioTrackWindow(_paused);

            window.OnFadeConfirmed += (duration, fadeIn) =>
            {
                OnFadeTrackPressed?.Invoke(
                    _groupId,
                    _trackId,
                    duration,
                    fadeIn);
            };

            window.OpenCentered();
        };

        var loopButton = new Button
        {
            Text = "Loop",
            MinWidth = 72,
            ToggleMode = true,
            Pressed = track.Loop
        };

        var deleteButton = new Button
        {
            Text = "-",
            MinWidth = 32,
            StyleClasses = { "negative" }
        };

        deleteButton.OnPressed += _ =>
            OnDeleteTrackPressed?.Invoke(
                _groupId,
                _trackId);

        header.AddChild(title);
        header.AddChild(loopButton);
        header.AddChild(fadeButton);
        header.AddChild(deleteButton);

        var path = new Label
        {
            Text = track.Path,
            HorizontalExpand = true
        };

        var volumeRow = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true
        };

        var volumeLabel = new Label
        {
            Text = $"Volume: {VolumeToPercent(track.Volume)}%",
            MinWidth = 105
        };

        var volumeSlider = new Slider
        {
            MinValue = 0f,
            MaxValue = 100f,
            Value = VolumeToPercent(track.Volume),
            MinWidth = 160,
            HorizontalExpand = false
        };

        volumeSlider.OnReleased += slider =>
        {
            volumeLabel.Text = $"Volume: {MathF.Round(slider.Value)}%";

            OnUpdateTrackPressed?.Invoke(
                _groupId,
                _trackId,
                PercentToVolume(slider.Value),
                _loop);
        };

        volumeSlider.OnValueChanged += args =>
        {
            volumeLabel.Text = $"Volume: {MathF.Round(args.Value)}%";
        };

        loopButton.OnPressed += _ =>
        {
            _loop = loopButton.Pressed;

            OnUpdateTrackPressed?.Invoke(
                _groupId,
                _trackId,
                PercentToVolume(volumeSlider.Value),
                _loop);
        };

        volumeRow.AddChild(volumeLabel);
        volumeRow.AddChild(volumeSlider);

        var playbackRow = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true
        };

        var pauseButton = new Button
        {
            Text = "Pause",
            MinWidth = 72,
            ToggleMode = true,
            Pressed = track.Paused
        };

        pauseButton.OnPressed += _ =>
        {
            _paused = pauseButton.Pressed;

            OnSetTrackPausedPressed?.Invoke(
                _groupId,
                _trackId,
                _paused);
        };

        _playbackSlider = new Slider
        {
            MinValue = 0f,
            MaxValue = Math.Max(_length, 0.01f),
            Value = Math.Clamp(_position, 0f, Math.Max(_length, 0.01f)),
            HorizontalExpand = true
        };

        _playbackSlider.OnReleased += slider =>
        {
            _position = slider.Value;
            _sliderLockTimer = 0.5f;

            OnSetTrackTimePressed?.Invoke(
                _groupId,
                _trackId,
                slider.Value);
        };

        _durationLabel = new Label
        {
            MinWidth = 95
        };

        playbackRow.AddChild(pauseButton);
        playbackRow.AddChild(_playbackSlider);
        playbackRow.AddChild(_durationLabel);

        AddChild(header);
        AddChild(path);
        AddChild(volumeRow);
        AddChild(playbackRow);

        UpdateDurationLabel();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_sliderLockTimer > 0f)
            _sliderLockTimer -= args.DeltaSeconds;

        if (!_paused && !_playbackSlider.Grabbed)
        {
            _position += args.DeltaSeconds;

            if (_length > 0f)
            {
                if (_loop)
                {
                    _position %= _length;
                }
                else
                {
                    _position = Math.Min(
                        _position,
                        _length);
                }
            }
        }

        if (!_playbackSlider.Grabbed && _sliderLockTimer <= 0f)
        {
            _playbackSlider.SetValueWithoutEvent(
                Math.Clamp(
                    _position,
                    0f,
                    Math.Max(_length, 0.01f)));
        }

        UpdateDurationLabel();
    }

    private void UpdateDurationLabel()
    {
        _durationLabel.Text =
            $"{FormatTime(_position)} / {FormatTime(_length)}";
    }

    private static float VolumeToPercent(float volume)
    {
        return MathF.Round(
            Math.Clamp(volume, 0f, 1f) * 100f);
    }

    private static float PercentToVolume(float percent)
    {
        return Math.Clamp(
            percent,
            0f,
            100f) / 100f;
    }

    private static string FormatTime(float seconds)
    {
        if (seconds < 0f
            || float.IsNaN(seconds)
            || float.IsInfinity(seconds))
        {
            seconds = 0f;
        }

        return TimeSpan.FromSeconds(seconds)
            .ToString(@"mm\:ss");
    }
}
