using Content.Shared._Core.AudioDirector;
using Robust.Shared.Map;

namespace Content.Server._Core.AudioDirector.Targets;

public sealed class MapAudioGroupTarget : AudioGroupTarget
{
    public override AudioGroupTargetType Type =>
        AudioGroupTargetType.Map;

    public MapId MapId { get; }

    public MapAudioGroupTarget(MapId mapId)
    {
        MapId = mapId;
    }
}
