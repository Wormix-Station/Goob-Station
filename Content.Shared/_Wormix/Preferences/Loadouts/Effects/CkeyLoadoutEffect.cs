using System.Diagnostics.CodeAnalysis;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Preferences.Loadouts.Effects;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Shared._Wormix.Preferences.Loadouts.Effects;

/// <summary>
/// Позволяет реализовать либо спонсорские предметы, либо даже для квент.
/// </summary>
public sealed partial class CkeyLoadoutEffect : LoadoutEffect
{
    [DataField(required: true)]
    public List<string> Players = new();   // CKey-и игроков которые имеют доступ к предмету

    public override bool Validate(HumanoidCharacterProfile profile,
        RoleLoadout loadout,
        LoadoutPrototype proto,
        ICommonSession? session,
        IDependencyCollection collection,
        [NotNullWhen(false)] out FormattedMessage? reason)
    {
        reason = null;

        if (session == null)
            return true;

        if (!Players.Contains(session.Name))
        {
            reason = FormattedMessage.FromMarkupOrThrow(Loc.GetString("loadout-ckey-denied"));
            return false;
        }

        return true;
    }
}
