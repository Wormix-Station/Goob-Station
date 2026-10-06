using Content.Goobstation.Common.Grab;
using Content.Goobstation.Common.MartialArts;
using Content.Goobstation.Shared.GrabIntent;
using Content.Goobstation.Shared.MartialArts.Components;
using Content.Goobstation.Shared.MartialArts.Events;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Bed.Sleep;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Events;
using Content.Shared.Execution;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Projectiles;
using Content.Shared.Speech;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Audio;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Goobstation.Shared.MartialArts;

public partial class SharedMartialArtsSystem
{
    private void InitializeCombatives()
    {
        // Combo Subscribers
        SubscribeLocalEvent<CanPerformComboComponent, CombativesRestrainPerformedEvent>(OnCombativesRestrain);
        SubscribeLocalEvent<CanPerformComboComponent, CombativesThrowPerformedEvent>(OnCombativesThrow);
        SubscribeLocalEvent<CanPerformComboComponent, CombativesChokePerformedEvent>(OnCombativesChoke);
        SubscribeLocalEvent<CanPerformComboComponent, CombativesSlitThroatPerformedEvent>(OnCombativesSlitThroat);
        SubscribeLocalEvent<CanPerformComboComponent, CombativesKnockdownPerformedEvent>(OnCombativesKnockdown);
        SubscribeLocalEvent<CanPerformComboComponent, CombativesWeakeningPerformedEvent>(OnCombativesWeakening);
        SubscribeLocalEvent<CanPerformComboComponent, CombativesPummelPerformedEvent>(OnCombativesPummel);
        SubscribeLocalEvent<CanPerformComboComponent, CombativesDisarmPerformedEvent>(OnCombativesDisarm);

        // Utility & Item Grant
        SubscribeLocalEvent<GrantCombativesComponent, UseInHandEvent>(OnGrantCombativesUse);
        SubscribeLocalEvent<GrantCombativesComponent, MapInitEvent>(OnGrantCombativesMapInit);

        // RestrainComponent Subscribers
        SubscribeLocalEvent<CombativesRestrainComponent, InteractionAttemptEvent>(OnRestrainCancelInteraction);
        SubscribeLocalEvent<CombativesRestrainComponent, UseAttemptEvent>(OnRestrainCancelUse);
        SubscribeLocalEvent<CombativesRestrainComponent, PickupAttemptEvent>(OnRestrainCancelPickup);
        SubscribeLocalEvent<CombativesRestrainComponent, BeforeReleaseEvent>(OnRestrainReleaseAttempt);
        SubscribeLocalEvent<CombativesRestrainComponent, AttackAttemptEvent>(OnRestrainAttackAttempt);
        SubscribeLocalEvent<CombativesRestrainComponent, SpeakAttemptEvent>(OnRestrainSpeakAttempt);
        SubscribeLocalEvent<CombativesRestrainComponent, PreventCollideEvent>(OnRestrainPreventCollide);
        SubscribeLocalEvent<CombativesRestrainComponent, KnockDownAttemptEvent>(OnKnockDownAttempt);
        SubscribeLocalEvent<CombativesRestrainComponent, KnockedDownEvent>(OnKnockedDown);

        SubscribeLocalEvent<CombativesRestrainComponent, StoodEvent>(OnRestrainStood);
        SubscribeLocalEvent<CombativesRestrainComponent, PullStoppedMessage>(OnRestrainStopped);
    }

    private static ProtoId<TagPrototype> _combatKnife = "CombatKnife";
    private static ProtoId<TagPrototype>[] _allowedWeaponTags = { _combatKnife, "Sidearm" };
    #region Generic Methods

    private bool IsWeaponAllowedForCombatives(EntityUid user, EntityUid weapon)
    {
        return weapon == user || _tag.HasAnyTag(weapon, _allowedWeaponTags);
    }

    private void OnGrantCombativesMapInit(Entity<GrantCombativesComponent> ent, ref MapInitEvent args)
    {
        if (!HasComp<MobStateComponent>(ent))
            return;

        TryGrantMartialArt(ent, ent.Comp);
    }

    private void OnGrantCombativesUse(EntityUid ent, GrantCombativesComponent comp, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!_netManager.IsServer)
            return;

        if (!TryGrantMartialArt(args.User, comp))
            return;

        var coords = Transform(args.User).Coordinates;
        _audio.PlayPvs(comp.SoundOnUse, coords);

        if (comp.MultiUse)
            return;

        QueueDel(ent);
        if (comp.SpawnedProto != null)
            Spawn(comp.SpawnedProto, coords);
    }

    private void OnCombativesAttackPerformed(Entity<MartialArtsKnowledgeComponent> ent, ref ComboAttackPerformedEvent args)
    {
        if (args.Weapon != args.Performer || args.Target == args.Performer)
            return;

        switch (args.Type)
        {
            case ComboAttackType.Grab:
                if (!TryComp<PullerComponent>(ent, out var puller)
                    || !TryComp<GrabIntentComponent>(ent, out var grabIntent)
                    || !TryComp<PullableComponent>(args.Target, out var pullable)
                    || !TryComp<GrabbableComponent>(args.Target, out var grabbable))
                    return;
                grabbable.NextEscapeAttempt = _timing.CurTime.Add(TimeSpan.FromSeconds(2));
                break;
            case ComboAttackType.Harm:
                // Leg sweep
                if (!TryComp<StandingStateComponent>(ent.Owner, out var standing)
                    || standing.Standing
                    || !TryComp<StandingStateComponent>(args.Target, out var targetStanding)
                    || !targetStanding.Standing
                    || HasComp<ArmbarredComponent>(ent.Owner)
                    )
                    break;

                _stun.TryKnockdown(args.Target, TimeSpan.FromSeconds(5), true, drop: true);
                ComboPopup(ent, args.Target, "Leg Sweep");
                break;
        }

    }

    private void OnCombativesMeleeAttackRate(Entity<MartialArtsKnowledgeComponent> ent, ref GetMeleeAttackRateEvent args)
    {
        if (args.User != args.Weapon)
            return;

        if (!TryComp<HandsComponent>(args.User, out var hands))
            return;

        var helding = _hands.EnumerateHeld((args.User, hands)).ToList();

        if (TryComp<PullerComponent>(args.User, out var puller)
            && HasComp<CombativesRestrainComponent>(puller.Pulling))
            return;

        args.Multipliers *= 3f;
    }


    private void OnCombativesGrabEvent(Entity<MartialArtsKnowledgeComponent> end, ref CheckGrabOverridesEvent args)
    {
        if (args.Stage > GrabStage.No)
            args.Stage = GrabStage.Hard;
    }


    #endregion

    #region Combo Methods

    private void OnCombativesRestrain(Entity<CanPerformComboComponent> ent, ref CombativesRestrainPerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out _)
            || !TryComp<PullableComponent>(target, out var pullable)
            || pullable.Puller != ent
            || !TryComp<GrabbableComponent>(target, out var grabbable)
            || grabbable.GrabStage != GrabStage.Hard
            || !TryComp<StandingStateComponent>(target, out var standingState)
            || !standingState.Standing)
            return;

        if (!HasComp<CombativesRestrainComponent>(target))
        {
            AddComp<CombativesRestrainComponent>(target).Puller = ent;
        }

        if (TryComp<HandsComponent>(target, out var hands))
        {
            foreach (var hand in hands.Hands.Keys)
            {
                _virtualItem.TrySpawnVirtualItemInHand(ent, target);
            }
        }

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Weapons/genhit2.ogg"), target);
        ComboPopup(ent, target, proto.ID);
        ent.Comp.LastAttacks.Clear();
    }

    private void OnCombativesThrow(Entity<CanPerformComboComponent> ent, ref CombativesThrowPerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out var downed)
            || downed
            || !TryComp<PullableComponent>(target, out var pullable)
            || !TryComp<PullerComponent>(ent, out var puller)
            || !TryComp<GrabIntentComponent>(ent, out var grabIntent))
            return;

        var knockdownTime = TimeSpan.FromSeconds(proto.ParalyzeTime);

        var ev = new BeforeStaminaDamageEvent(1f);
        RaiseLocalEvent(target, ref ev);

        knockdownTime *= ev.Value;

        if (TryComp<CombativesRestrainComponent>(target, out var restrain))
        {
            Unrestrain((target, restrain));
        }

        bool isRestrained = restrain != null;

        _stamina.TakeStaminaDamage(target, proto.StaminaDamage * (isRestrained ? 3 : 1), applyResistances: true);
        _stun.TryKnockdown(target, knockdownTime, true, false, isRestrained);


        _pulling.TryStopPull(target, pullable, ent, true);

        var entPos = _transform.GetMapCoordinates(ent).Position;
        var targetPos = _transform.GetMapCoordinates(target).Position;
        var direction = targetPos - entPos; // vector from ent to target

        _grabThrowing.Throw(target, ent, direction, 5, behavior: proto.DropItems);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Weapons/genhit3.ogg"), target);
        ComboPopup(ent, target, proto.ID); // CorvaxGoob-Localization // proto.Name -> proto.ID
        ent.Comp.LastAttacks.Clear();
    }

    private void OnCombativesChoke(Entity<CanPerformComboComponent> ent, ref CombativesChokePerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out _)
            || !TryComp<CombativesRestrainComponent>(target, out var restrain)
            || !TryComp<PullerComponent>(ent, out var puller)
            || !TryComp<GrabIntentComponent>(ent, out var grabIntent)
            || !TryComp<PullableComponent>(target, out var pullable)
            || !TryComp<GrabbableComponent>(target, out var grabbable))
            return;

        if (TryComp<StaminaComponent>(target, out var stamina) && stamina.Critical)
        {
            _newStatus.TryAddStatusEffectDuration(target, "StatusEffectForcedSleeping", out _, TimeSpan.FromSeconds(30));
        }
        else
        {
            _stamina.TakeStaminaDamage(target, proto.StaminaDamage, source: ent, applyResistances: false);
        }

        restrain.HandledForcedStand = true;
        _standingState.Stand(target, force: true);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Weapons/genhit2.ogg"), target);
        ComboPopup(ent, target, proto.ID);
        ent.Comp.LastAttacks.Clear();
    }

    private void OnCombativesSlitThroat(Entity<CanPerformComboComponent> ent, ref CombativesSlitThroatPerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out var downed)
            || !TryComp<PullableComponent>(target, out var pullable)
            || !TryComp<CombativesRestrainComponent>(target, out var restrain)
            || restrain.Puller != ent.Owner
            || !TryComp<TargetingComponent>(ent, out var targeting)
            || targeting.Target != TargetBodyPart.Head
            || !TryComp<HandsComponent>(ent, out var hands)
            || !_tag.HasTag(_hands.GetActiveItem((ent, hands)).GetValueOrDefault(), _combatKnife))
            return;

        var (partType, symmetry) = _body.ConvertTargetBodyPart(targeting.Target);
        var targetLimb = _body.GetBodyChildrenOfType(target, partType, symmetry: symmetry).FirstOrDefault();

        var targetEntity = targetLimb.Id != default ? targetLimb.Id : target;

        if (!TryComp<WoundableComponent>(targetLimb.Id, out var woundable)
            || woundable.WoundableIntegrity <= 0)
            return;

        var damage = new DamageSpecifier();
        damage.DamageDict.Add("Piercing", proto.ExtraDamage);
        _damageable.TryChangeDamage(targetEntity, damage, ignoreResistances: false, origin: ent, canMiss: false);

        if (_wound.TryInduceWound(targetLimb.Id, "Piercing", proto.ExtraDamage, out var woundInduced))
        {
            var bone = woundable.Bone.ContainedEntities.FirstOrDefault();
            if (bone != default)
            {
                _trauma.ApplyBoneTrauma(
                    bone,
                    (targetLimb.Id, woundable),
                    (woundInduced.Value.Owner, EnsureComp<TraumaInflicterComponent>(woundInduced.Value.Owner)),
                    proto.ExtraDamage
                );
            }

            // Прекращаем захват после выполнения приема
            _pulling.TryStopPull(target, pullable, ent, true);

            // Звук режущего удара / всплеска крови
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/_Shitmed/Medical/Surgery/scalpel1.ogg"), target);
            ComboPopup(ent, target, proto.ID);
            ent.Comp.LastAttacks.Clear();
        }
    }

    private void OnCombativesKnockdown(Entity<CanPerformComboComponent> ent, ref CombativesKnockdownPerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out _))
            return;

        var restrained =
               HasComp<ArmbarredComponent>(ent)
            || HasComp<CombativesRestrainComponent>(ent);

        bool performerStanding = true;
        bool targetStanding = HasComp<StandingStateComponent>(target);
        if (TryComp<StandingStateComponent>(ent, out var standingState))
        {
            performerStanding = standingState.Standing;
        }

        float mult =
            Math.Max((restrained ? 4 : 1),
            (!targetStanding ? 2 : 1));

        if (TryComp<PullableComponent>(ent, out var pullable))
            _pulling.TryStopPull(ent, pullable, target, true);

        DoDamage(ent, target, proto.DamageType, proto.ExtraDamage * mult, out _);
        _stamina.TakeStaminaDamage(target, proto.StaminaDamage * mult * 2, applyResistances: true);

        if (!performerStanding)
        {
            if (HasComp<KnockedDownComponent>(ent.Owner))
                RemComp<KnockedDownComponent>(ent.Owner);
            _standingState.Stand(ent.Owner, standingState);
        }

        if (restrained)
        {
            _grabThrowing.Throw(target,
                ent,
                _transform.GetMapCoordinates(ent).Position - _transform.GetMapCoordinates(target).Position,
                5,
                behavior: proto.DropItems);
        }

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Weapons/genhit2.ogg"), target);
        ComboPopup(ent, target, proto.ID);
        ent.Comp.LastAttacks.Clear();
    }

    private void OnCombativesWeakening(Entity<CanPerformComboComponent> ent, ref CombativesWeakeningPerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out _)
            || !TryComp<StandingStateComponent>(target, out var standingState)
            || !standingState.Standing
            || HasComp<CombativesRestrainComponent>(ent))
            return;

        _movementMod.TryUpdateMovementSpeedModDuration(target, MartsGenericSlow, TimeSpan.FromSeconds(5), 0.5f, 0.5f);

        _stamina.TakeStaminaDamage(target, proto.StaminaDamage, applyResistances: true);

        if (TryComp<HandsComponent>(target, out var hands)
            && _hands.TryGetActiveItem((target, hands), out var activeItem)
            && activeItem.HasValue
            && TryComp<ChamberMagazineAmmoProviderComponent>(activeItem.Value, out var chambered)
            && chambered.BoltClosed == true)
        {
            _gun.ToggleBolt(activeItem.Value, chambered, ent);
        }

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Weapons/genhit2.ogg"), target);
        ComboPopup(ent, target, proto.ID);
    }

    private void OnCombativesPummel(Entity<CanPerformComboComponent> ent, ref CombativesPummelPerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out _)
            || !TryComp<StandingStateComponent>(target, out var standingState)
            || !standingState.Standing
            || HasComp<CombativesRestrainComponent>(ent))
            return;

        _stamina.TakeStaminaDamage(target, proto.StaminaDamage, applyResistances: true);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Weapons/genhit2.ogg"), target);
        ComboPopup(ent, target, proto.ID);
        ent.Comp.LastAttacks.Clear();
    }

    private void OnCombativesDisarm(Entity<CanPerformComboComponent> ent, ref CombativesDisarmPerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out _)
            || HasComp<CombativesRestrainComponent>(ent))
            return;

        if (!_hands.TryGetActiveItem(target, out var activeItem))
            return;
        if (HasComp<VirtualItemComponent>(activeItem))
            return;
        if (!_hands.TryDrop(target, activeItem.Value))
            return;
        if (!_hands.TryGetEmptyHand(ent.Owner, out var emptyHand))
            return;
        if (!_hands.TryPickup(ent, activeItem.Value, emptyHand))
            return;

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Weapons/genhit2.ogg"), target);
        ComboPopup(ent, target, proto.ID);
        ent.Comp.LastAttacks.Clear();

        _hands.SetActiveHand(ent.Owner, emptyHand);
    }

    #endregion

    #region Restrain

    private void OnRestrainCancelInteraction(Entity<CombativesRestrainComponent> ent, ref InteractionAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnRestrainCancelUse(Entity<CombativesRestrainComponent> ent, ref UseAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnRestrainCancelPickup(Entity<CombativesRestrainComponent> ent, ref PickupAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnRestrainReleaseAttempt(Entity<CombativesRestrainComponent> ent, ref BeforeReleaseEvent args)
    {
        args.Canceled = true;
    }

    private void OnRestrainAttackAttempt(Entity<CombativesRestrainComponent> ent, ref AttackAttemptEvent args)
    {
        if (!TryComp<CanPerformComboComponent>(ent, out var performer)
            || !performer.ArtsForms.Contains(MartialArtsForms.Combatives))
            args.Cancel();
    }

    private void OnRestrainSpeakAttempt(Entity<CombativesRestrainComponent> ent, ref SpeakAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnKnockDownAttempt(Entity<CombativesRestrainComponent> ent, ref KnockDownAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnKnockedDown(Entity<CombativesRestrainComponent> ent, ref KnockedDownEvent args)
    {
        if (!TryComp<PullableComponent>(ent, out var pullable))
            return;

        _pulling.TryStopPull(ent, pullable, ent, true);
        Unrestrain(ent);
    }

    private void OnRestrainPreventCollide(Entity<CombativesRestrainComponent> ent, ref PreventCollideEvent args)
    {
        // Проверяем, является ли другой объект коллизии снарядом
        if (!HasComp<AmmoComponent>(args.OtherEntity) && !HasComp<ProjectileComponent>(args.OtherEntity))
            return;

        // Достаем компонент снаряда, чтобы узнать, кто выстрелил
        if (!TryComp<ProjectileComponent>(args.OtherEntity, out var projectile))
            return;

        // Если выстрелил тот, кто держит в захвате (Puller) — пуля не задевает удерживаемую жертву
        if (projectile.Shooter == ent.Comp.Puller || projectile.Weapon == ent.Comp.Puller)
        {
            args.Cancelled = true;
        }
    }

    private void OnRestrainStood(Entity<CombativesRestrainComponent> ent, ref StoodEvent args)
    {
        if (!TryComp<PullableComponent>(ent, out var pullable) || ent.Comp.HandledForcedStand)
        {
            ent.Comp.HandledForcedStand = false;
            return;
        }

        _pulling.TryStopPull(ent, pullable, ent.Comp.Puller, true);
        Unrestrain(ent);
    }

    private void OnRestrainStopped(Entity<CombativesRestrainComponent> ent, ref PullStoppedMessage args)
    {
        if (args.PullerUid != ent.Comp.Puller)
            return;

        Unrestrain(ent);
    }

    private void Unrestrain(Entity<CombativesRestrainComponent> ent)
    {
        _virtualItem.DeleteInHandsMatching(ent, ent.Comp.Puller);
        CheckAndDropIfUnconscious(ent);
        RemComp<CombativesRestrainComponent>(ent);
    }

    private bool CheckAndDropIfUnconscious(EntityUid uid)
    {
        var isSleeping = HasComp<SleepingComponent>(uid);
        var isStaminaCrit = TryComp<StaminaComponent>(uid, out var stamina) && stamina.Critical;

        if (isSleeping || isStaminaCrit)
        {
            // Укладываем на пол без автоматического подъема (autoStand: false)
            _stun.TryKnockdown(uid, TimeSpan.FromSeconds(5), autoStand: false, force: true);
            _standingState.Down(uid);
            return true;
        }

        return false;
    }


    #endregion
}
