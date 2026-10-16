// SPDX-License-Identifier: MIT

using Content.Shared.Actions;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Mobs;
using Content.Shared.Toggleable;
using Robust.Shared.Prototypes;

namespace Content.Shared.Wagging;

public sealed class WaggingSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WaggingComponent, MapInitEvent>(OnWaggingMapInit);
        SubscribeLocalEvent<WaggingComponent, ComponentShutdown>(OnWaggingShutdown);
        SubscribeLocalEvent<WaggingComponent, ToggleActionEvent>(OnWaggingToggle);
        SubscribeLocalEvent<WaggingComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnWaggingMapInit(EntityUid uid, WaggingComponent component, MapInitEvent args)
    {
        _actions.AddAction(uid, ref component.ActionEntity, component.Action, uid);
        Dirty(uid, component);
    }

    private void OnWaggingShutdown(EntityUid uid, WaggingComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.ActionEntity);
    }

    private void OnWaggingToggle(EntityUid uid, WaggingComponent component, ToggleActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryToggleWagging(uid, wagging: component))
            return;

        args.Handled = true;
    }

    private void OnMobStateChanged(EntityUid uid, WaggingComponent component, MobStateChangedEvent args)
    {
        if (component.Wagging)
            TryToggleWagging(uid, wagging: component);
    }

    public bool TryToggleWagging(EntityUid uid, WaggingComponent? wagging = null, HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref wagging, ref humanoid))
            return false;

        if (!humanoid.MarkingSet.Markings.TryGetValue(MarkingCategories.Tail, out var markings))
            return false;

        if (markings.Count == 0)
            return false;

        wagging.Wagging = !wagging.Wagging;
        Dirty(uid, wagging);

        var changed = false;

        for (var idx = 0; idx < markings.Count; idx++)
        {
            var currentMarking = markings[idx];
            var currentMarkingId = currentMarking.MarkingId;
            string newMarkingId;

            if (wagging.Wagging)
            {
                newMarkingId = $"{currentMarkingId}{wagging.Suffix}";
            }
            else
            {
                if (currentMarkingId.EndsWith(wagging.Suffix))
                {
                    newMarkingId = currentMarkingId[..^wagging.Suffix.Length];
                }
                else
                {
                    newMarkingId = currentMarkingId;
                    Log.Warning($"Unable to revert wagging for {currentMarkingId}");
                }
            }

            if (!_prototype.HasIndex<MarkingPrototype>(newMarkingId))
            {
                Log.Warning($"{ToPrettyString(uid)} tried toggling wagging but {newMarkingId} marking doesn't exist");
                continue;
            }

            markings[idx] = new Marking(newMarkingId, currentMarking.MarkingColors)
            {
                Forced = currentMarking.Forced,
            };
            changed = true;
        }

        if (changed)
            Dirty(uid, humanoid);

        return true;
    }
}
