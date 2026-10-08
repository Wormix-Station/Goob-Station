using Content.Shared._Core.AudioDirector;

namespace Content.Server._Core.AudioDirector.Targets;

public sealed class GridAudioGroupTarget : AudioGroupTarget
{
    public override AudioGroupTargetType Type =>
        AudioGroupTargetType.Grid;

    public EntityUid GridUid { get; }

    public GridAudioGroupTarget(EntityUid gridUid)
    {
        GridUid = gridUid;
    }
}
