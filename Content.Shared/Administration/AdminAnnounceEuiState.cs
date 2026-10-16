// SPDX-License-Identifier: MIT

using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared.Administration
{
    public enum AdminAnnounceType
    {
        All,
        Map,
        Server,
    }

    [Serializable, NetSerializable]
    public sealed class AdminAnnounceEuiState : EuiStateBase
    {
        public readonly List<AdminAnnounceTargetEntry> Targets;

        public AdminAnnounceEuiState(List<AdminAnnounceTargetEntry> targets)
        {
            Targets = targets;
        }
    }

    [Serializable, NetSerializable]
    public readonly record struct AdminAnnounceTargetEntry(string Name, NetEntity Grid);

    public readonly record struct AdminAnnounceTargetSelection(AdminAnnounceType Type, NetEntity? Grid)
    {
        public static readonly AdminAnnounceTargetSelection All = new(AdminAnnounceType.All, null);
        public static readonly AdminAnnounceTargetSelection Server = new(AdminAnnounceType.Server, null);
    }

    public static class AdminAnnounceEuiMsg
    {
        [Serializable, NetSerializable]
        public sealed class DoAnnounce : EuiMessageBase
        {
            public bool CloseAfter;
            public string Announcer = default!;
            public string Announcement = default!;
            public AdminAnnounceType AnnounceType;
            public string Voice = default!; // CorvaxGoob-TTS
            // DS14-announce-start
            public NetEntity? TargetGrid;
            public string ColorHex = "1d8bad";
            public string SoundPath = "/Audio/_CorvaxGoob/Announcements/centcomm.ogg";
            public float SoundVolume = 5f;
            public string Sender = "";
            // DS14-announce-end
        }
    }
}
