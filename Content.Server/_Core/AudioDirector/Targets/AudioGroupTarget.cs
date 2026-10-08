using Content.Shared._Core.AudioDirector;

namespace Content.Server._Core.AudioDirector.Targets;

public abstract class AudioGroupTarget
{
    public abstract AudioGroupTargetType Type { get; }
}
