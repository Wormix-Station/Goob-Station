using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Core.AudioDirector.Eui;

[Serializable, NetSerializable]
public sealed class AudioDirectorEuiState : EuiStateBase
{
    public AudioGroupEuiData[] Groups { get; }
    public AudioDirectorPlayerEuiData[] Players { get; }

    public AudioDirectorEuiState(
        AudioGroupEuiData[] groups,
        AudioDirectorPlayerEuiData[] players)
    {
        Groups = groups;
        Players = players;
    }
}
