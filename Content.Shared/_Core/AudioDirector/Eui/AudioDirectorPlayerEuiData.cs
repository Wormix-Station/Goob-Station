using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Core.AudioDirector.Eui;

[Serializable, NetSerializable]
public sealed class AudioDirectorPlayerEuiData
{
    public NetUserId UserId { get; }
    public string Name { get; }

    public AudioDirectorPlayerEuiData(
        NetUserId userId,
        string name)
    {
        UserId = userId;
        Name = name;
    }
}
