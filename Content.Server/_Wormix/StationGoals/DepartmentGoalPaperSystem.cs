// SPDX-FileCopyrightText: 2026 FelixOnly <62942680+felixonly@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.Fax;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Shared._CorvaxGoob.CCCVars;
using Content.Shared.Fax.Components;
using Content.Shared.GameTicking;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Wormix.StationGoals;

public sealed class DepartmentGoalPaperSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly FaxSystem _fax = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IEntityManager _entity = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
    }

    private void OnRoundStarted(RoundStartedEvent ev)
    {
        if (!_cfg.GetCVar(CCCVars.StationGoal))
            return;


        var ticker = _entity.System<GameTicker>();
        bool isGoalMode = ticker?.CurrentPreset?.ID == "DepartmentGoals";



        var query = EntityQueryEnumerator<DepartmentGoalComponent>();
        while (query.MoveNext(out var uid, out var station))
        {
            var tempGoals = new List<DepartmentGoalPrototype>();

            foreach (var rawGoal in station.Goals)
            {
                tempGoals.Add(_proto.Index(rawGoal));
            }


            var selGoal = new List<DepartmentGoalPrototype>();
            while (tempGoals.Count > 0)
            {

                // SEC
                var secGoal = _random.Pick(
                    tempGoals.Where(x => x.Department == 1).ToList());
                selGoal.Add(secGoal);

                // MED
                var medGoal = _random.Pick(
                    tempGoals.Where(x => x.Department == 2).ToList());
                selGoal.Add(medGoal);

                // Cargo
                var cargoGoal = _random.Pick(
                    tempGoals.Where(x => x.Department == 4).ToList());
                selGoal.Add(cargoGoal);

                // Eng
                var engGoal = _random.Pick(
                    tempGoals.Where(x => x.Department == 5).ToList());
                selGoal.Add(engGoal);

                // SERVICE
                var servGoal = _random.Pick(
                    tempGoals.Where(x => x.Department == 6).ToList());
                selGoal.Add(servGoal);


                break;
            }

            if (selGoal.Count == 0)
                return;

            if (!isGoalMode)
            {
                if (SendStationTransit(uid))
                {
                    Log.Info($"Goal has been sent to station {MetaData(uid).EntityName}");
                }
                return;
            }

            if (SendStationGoal(uid, selGoal))
            {
                Log.Info($"Goal has been sent to station {MetaData(uid).EntityName}");
            }
        }
    }

    public bool SendStationTransit(EntityUid ent)
    {
        var printout = new FaxPrintout(
            Loc.GetString("department-goal-greenshift", ("station", MetaData(ent).EntityName)),
            Loc.GetString("station-goal-fax-paper-name"),
            null,
            null,
            "paper_stamp-centcom",
            [
                new()
                {
                    StampedName = Loc.GetString("stamp-component-stamped-name-centcom"),
                    StampedColor = Color.FromHex("#006600")
                }
            ]
        );

        var wasSent = false;
        var query = EntityQueryEnumerator<FaxMachineComponent>();
        while (query.MoveNext(out var faxUid, out var fax))
        {
            if (!fax.ReceiveAllStationGoals && !(fax.ReceiveStationGoal && _station.GetOwningStation(faxUid) == ent))
                continue;

            _fax.Receive(faxUid, printout, null, fax);

            wasSent |= fax.ReceiveStationGoal;
        }

        return wasSent;
    }

    /// <summary>
    ///     Send a station goal on selected station to all faxes which are authorized to receive it.
    /// </summary>
    /// <returns>True if at least one fax received paper</returns>
    public bool SendStationGoal(EntityUid ent, List<DepartmentGoalPrototype> goal)
    {
        var goalText = string.Empty;

        goalText += Loc.GetString("department-goal-start", ("station", MetaData(ent).EntityName));

        foreach (var departGoal in goal)
        {
            goalText += "\n\n";
            goalText += Loc.GetString(departGoal.Text);
        }

        goalText += "\n\n";
        goalText += Loc.GetString("department-goal-end");
        goalText += "\n\n\n\n\n\n\n";

        var printout = new FaxPrintout(
            goalText,
            Loc.GetString("station-goal-fax-paper-name"),
            null,
            null,
            "paper_stamp-centcom",
            [
                new()
                {
                    StampedName = Loc.GetString("stamp-component-stamped-name-centcom"),
                    StampedColor = Color.FromHex("#006600")
                }
            ]
        );

        var wasSent = false;
        var query = EntityQueryEnumerator<FaxMachineComponent>();
        while (query.MoveNext(out var faxUid, out var fax))
        {
            if (!fax.ReceiveAllStationGoals && !(fax.ReceiveStationGoal && _station.GetOwningStation(faxUid) == ent))
                continue;

            _fax.Receive(faxUid, printout, null, fax);

            foreach (var departGoal in goal)
            {
                foreach (var spawnEnt in departGoal.Spawns)
                {
                    SpawnAtPosition(spawnEnt, Transform(faxUid).Coordinates);
                }
            }

            wasSent |= fax.ReceiveStationGoal;
        }

        return wasSent;
    }
}


