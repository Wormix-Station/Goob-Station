using Content.Shared._Core.AudioDirector;
using Robust.Shared.Network;

namespace Content.Server._Core.AudioDirector.Targets;

public sealed class PlayersAudioGroupTarget : AudioGroupTarget
{
    public override AudioGroupTargetType Type =>
        AudioGroupTargetType.Players;

    private readonly HashSet<NetUserId> _players;

    public IReadOnlySet<NetUserId> Players =>
        _players;

    public PlayersAudioGroupTarget(IEnumerable<NetUserId> players)
    {
        _players = new HashSet<NetUserId>(players);
    }
}
