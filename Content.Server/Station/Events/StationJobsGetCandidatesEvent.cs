// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Wormix.Players;
using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server.Station.Events;

[ByRefEvent]
public readonly record struct StationJobsGetCandidatesEvent(NetUserId Player, List<ProtoId<JobPrototype>> Jobs, List<CharacterWhitelistRoleWithUser> ProfilesRestrictions ); // Wormix
