using Robust.Shared.Serialization;

namespace Content.Shared._Core.AudioDirector.Eui;

[Serializable, NetSerializable]
public sealed class AudioTrackEuiData
{
    public int Id { get; }
    public string Name { get; }
    public string Path { get; }
    public float Volume { get; }
    public bool Loop { get; }
    public bool Paused { get; }
    public float Length { get; }
    public float PlaybackPosition { get; }

    public AudioTrackEuiData(
        int id,
        string name,
        string path,
        float volume,
        bool loop,
        bool paused,
        float length,
        float playbackPosition)
    {
        Id = id;
        Name = name;
        Path = path;
        Volume = volume;
        Loop = loop;
        Paused = paused;
        Length = length;
        PlaybackPosition = playbackPosition;
    }
}
