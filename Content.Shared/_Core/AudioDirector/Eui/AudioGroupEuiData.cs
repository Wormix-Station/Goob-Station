using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Core.AudioDirector.Eui;

[Serializable, NetSerializable]
public sealed class AudioGroupEuiData
{
    public int Id { get; }
    public string Name { get; }
    public AudioGroupTargetType TargetType { get; }

    public string? GridNetEntityId { get; }
    public string? MapId { get; }
    public NetUserId[] Players { get; }
    public AudioTrackEuiData[] Tracks { get; }

    public AudioGroupEuiData(
        int id,
        string name,
        AudioGroupTargetType targetType,
        string? gridNetEntityId = null,
        string? mapId = null,
        NetUserId[]? players = null,
        AudioTrackEuiData[]? tracks = null)
    {
        Id = id;
        Name = name;
        TargetType = targetType;
        GridNetEntityId = gridNetEntityId;
        MapId = mapId;
        Players = players ?? [];
        Tracks = tracks ?? [];
    }
}
