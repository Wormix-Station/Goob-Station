using System.Diagnostics.CodeAnalysis;
using Content.Server._Core.AudioDirector.Targets;

namespace Content.Server._Core.AudioDirector;

public sealed class AudioGroup
{
    private readonly Dictionary<int, AudioTrack> _tracks = new();

    private int _nextTrackId = 1;

    public int Id { get; }

    public string Name { get; private set; }

    public AudioGroupTarget Target { get; private set; }

    public IReadOnlyCollection<AudioTrack> Tracks =>
        _tracks.Values;

    public AudioGroup(
        int id,
        string name,
        AudioGroupTarget target)
    {
        Id = id;
        Name = name;
        Target = target;
    }

    internal void Rename(string name)
    {
        Name = name;
    }

    internal void ChangeTarget(AudioGroupTarget target)
    {
        Target = target;
    }

    internal AudioTrack AddTrack(
        string name,
        string path,
        float volume,
        bool loop,
        float length)
    {
        var id = _nextTrackId;
        _nextTrackId++;

        var track = new AudioTrack(
            id,
            name,
            path,
            volume,
            loop,
            length);

        _tracks.Add(id, track);
        return track;
    }

    internal bool TryGetTrack(
        int trackId,
        [NotNullWhen(true)] out AudioTrack? track)
    {
        return _tracks.TryGetValue(
            trackId,
            out track);
    }

    internal bool TryRemoveTrack(int trackId)
    {
        return _tracks.Remove(trackId);
    }
}
