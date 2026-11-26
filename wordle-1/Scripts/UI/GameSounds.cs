using System;
using Godot;

namespace WordleResearch.UI;

internal sealed class GameSounds
{
    private const int SampleRate = 24_000;
    private const double TailSeconds = 0.08;

    private static readonly Vector2[] Letter = Synthesize(
        new Note(0, 0.065, 987.77, 0.19));
    private static readonly Vector2[] Delete = Synthesize(
        new Note(0, 0.085, 392.00, 0.18));
    private static readonly Note[] AcceptedChord =
    {
        new(0, 0.155, 523.25, 0.09),
        new(0, 0.155, 659.25, 0.07),
        new(0, 0.18, 783.99, 0.06)
    };
    private static readonly Vector2[] Accepted = Synthesize(AcceptedChord);
    private static readonly Vector2[] Victory = Synthesize(
        AcceptedChord[0], AcceptedChord[1], AcceptedChord[2],
        new Note(0.17, 0.14, 659.25, 0.16),
        new Note(0.26, 0.16, 830.61, 0.14),
        new Note(0.35, 0.22, 987.77, 0.16));

    private readonly AudioStreamPlayer _player;
    private readonly Timer _stopTimer;
    private bool _enabled;

    public GameSounds(AudioStreamPlayer player, bool enabled)
    {
        _player = player;
        _enabled = enabled;
        _player.Stream = new AudioStreamGenerator
        {
            MixRateMode = AudioStreamGenerator.AudioStreamGeneratorMixRate.Custom,
            MixRate = SampleRate,
            BufferLength = 0.75f
        };
        _stopTimer = new Timer { OneShot = true, IgnoreTimeScale = true };
        _player.AddChild(_stopTimer);
        _stopTimer.Timeout += _player.Stop;
    }

    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        if (!enabled)
        {
            _stopTimer.Stop();
            _player.Stop();
        }
    }

    public void PlayLetter() => Play(Letter);
    public void PlayDelete() => Play(Delete);

    // The final valid guess plays its confirmation before the rising victory arpeggio.
    public void PlayAcceptedGuess(bool won) => Play(won ? Victory : Accepted);

    private void Play(Vector2[] frames)
    {
        if (!_enabled)
            return;

        _player.Stop();
        _player.Play();
        if (_player.GetStreamPlayback() is not AudioStreamGeneratorPlayback playback ||
            !playback.PushBuffer(frames))
        {
            _stopTimer.Stop();
            _player.Stop();
            return;
        }

        _stopTimer.Start((double)frames.Length / SampleRate + TailSeconds);
    }

    private static Vector2[] Synthesize(params Note[] notes)
    {
        double duration = 0;
        foreach (var note in notes)
            duration = Math.Max(duration, note.Start + note.Duration);
        var frames = new Vector2[(int)Math.Ceiling(duration * SampleRate)];

        foreach (var note in notes)
        {
            int start = (int)Math.Round(note.Start * SampleRate);
            int count = Math.Min((int)Math.Round(note.Duration * SampleRate), frames.Length - start);
            for (int i = 0; i < count; i++)
            {
                double time = (double)i / SampleRate;
                double phase = Math.Tau * note.Frequency * time;
                double attack = Math.Min(1, time / 0.007);
                double decay = 1 - (double)i / (count - 1);
                float sample = (float)(note.Gain * attack * decay * decay *
                    (Math.Sin(phase) + 0.16 * Math.Sin(2 * phase)));
                frames[start + i] += new Vector2(sample, sample);
            }
        }
        return frames;
    }

    private readonly record struct Note(double Start, double Duration, double Frequency, double Gain);
}
