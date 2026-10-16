// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.Grab;
using Content.Goobstation.Common.MartialArts;
using Content.Goobstation.Maths.FixedPoint; // Wormix EDIT
using Content.Goobstation.Shared.GrabIntent;
using Content.Goobstation.Shared.MartialArts.Components;
using Content.Goobstation.Shared.MartialArts.Events;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Targeting; // Wormix EDIT
using Content.Shared.Clothing;
using Content.Shared.Damage;
using Content.Shared.Damage.Components; // Wormix EDIT
using Content.Shared.Damage.Events;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Hands.Components; // Wormix EDIT
using Content.Shared.Interaction.Events; // Wormix EDIT
using Content.Shared.Item; // Wormix EDIT
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Standing;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Audio;
using System.Linq; // Wormix EDIT

namespace Content.Goobstation.Shared.MartialArts;

public partial class SharedMartialArtsSystem
{
    private void InitializeCorporateJudo()
    {
        SubscribeLocalEvent<CanPerformComboComponent, JudoDiscombobulatePerformedEvent>(OnJudoDiscombobulate);
        SubscribeLocalEvent<CanPerformComboComponent, JudoEyePokePerformedEvent>(OnJudoEyePoke);
        SubscribeLocalEvent<CanPerformComboComponent, JudoThrowPerformedEvent>(OnJudoThrow);
        SubscribeLocalEvent<CanPerformComboComponent, JudoArmbarPerformedEvent>(OnJudoArmbar);
        SubscribeLocalEvent<CanPerformComboComponent, JudoWheelThrowPerformedEvent>(OnJudoWheelThrow);
        SubscribeLocalEvent<CanPerformComboComponent, JudoGoldenBlastPerformedEvent>(OnJudoGoldenBlast);
        SubscribeLocalEvent<CanPerformComboComponent, JudoDisarmingPerformedEvent>(OnJudoDisarming); // Wormix EDIT

        SubscribeLocalEvent<GrantCorporateJudoComponent, ClothingGotEquippedEvent>(OnGrantCorporateJudo);
        SubscribeLocalEvent<GrantCorporateJudoComponent, ClothingGotUnequippedEvent>(OnRemoveCorporateJudo);

        SubscribeLocalEvent<ArmbarredComponent, StoodEvent>(OnArmbarredStood);
        SubscribeLocalEvent<ArmbarredComponent, PullStoppedMessage>(OnArmbarStopped);
        // Wormix EDIT Start
        SubscribeLocalEvent<ArmbarredComponent, InteractionAttemptEvent>(OnArmbarCancelInteraction);
        SubscribeLocalEvent<ArmbarredComponent, UseAttemptEvent>(OnArmbarCancelUse);
        SubscribeLocalEvent<ArmbarredComponent, PickupAttemptEvent>(OnArmbarCancelPickup);
        // Wormix EDIT End
    }

    #region Generic Methods

    private void OnGrantCorporateJudo(Entity<GrantCorporateJudoComponent> ent, ref ClothingGotEquippedEvent args)
    {
        if (!_netManager.IsServer)
            return;

        var user = args.Wearer;
        TryGrantMartialArt(user, ent.Comp);
    }

    private void OnRemoveCorporateJudo(Entity<GrantCorporateJudoComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        var user = args.Wearer;
        if (!TryComp<MartialArtsKnowledgeComponent>(user, out var martialArtsKnowledge))
            return;

        if (martialArtsKnowledge.MartialArtsForm != MartialArtsForms.CorporateJudo)
            return;

        if (!TryComp<MeleeWeaponComponent>(args.Wearer, out var meleeWeaponComponent))
            return;

        var originalDamage = new DamageSpecifier();
        originalDamage.DamageDict[martialArtsKnowledge.OriginalFistDamageType]
            = FixedPoint2.New(martialArtsKnowledge.OriginalFistDamage);
        meleeWeaponComponent.Damage = originalDamage;

        RemComp<MartialArtsKnowledgeComponent>(user);
        RemComp<CanPerformComboComponent>(user);
    }

    #endregion

    #region Combo Methods

    private void OnJudoDiscombobulate(Entity<CanPerformComboComponent> ent, ref JudoDiscombobulatePerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out _)
            || !TryComp(target, out StatusEffectsComponent? status)
            || !TryComp<GrabbableComponent>(target, out var grabbable)) // Wormix EDIT
            return;

        _movementMod.TryUpdateMovementSpeedModDuration(target, MartsGenericSlow, TimeSpan.FromSeconds(5), 0.5f, 0.5f);

        _stamina.TakeStaminaDamage(target, proto.StaminaDamage);

        // Wormix EDIT Start

        grabbable.NextEscapeAttempt = _timing.CurTime.Add(TimeSpan.FromSeconds(2));
        Dirty(target, grabbable);

        // Wormix EDIT End

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Weapons/genhit3.ogg"), target);
        ComboPopup(ent, target, proto.ID); // CorvaxGoob-Localization // proto.Name -> proto.ID
        ent.Comp.LastAttacks.Clear();
    }

    private void OnJudoEyePoke(Entity<CanPerformComboComponent> ent, ref JudoEyePokePerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out _)
            || !TryComp(target, out StatusEffectsComponent? status))
            return;

        _status.TryAddStatusEffect<TemporaryBlindnessComponent>(target,
            "TemporaryBlindness",
            TimeSpan.FromSeconds(2),
            true,
            status);

        _status.TryAddStatusEffect<BlurryVisionComponent>(target,
            "BlurryVision",
            TimeSpan.FromSeconds(5),
            true,
            status);

        DoDamage(ent, target, proto.DamageType, proto.ExtraDamage, out _);

        float mul = 1;
        // Wormix EDIT Start
        if(TryComp<MartialArtModifiersComponent>(ent, out var modifiers))
        {
            (mul, _) = GetMultiplierModifier(new Entity<MartialArtModifiersComponent>(ent, modifiers), MartialArtModifierType.AttackRate | MartialArtModifierType.Unarmed, false);
        }

        if (mul > 1 / args.AttackSpeedMultiplier)
        {
            ApplyMultiplier(ent, args.AttackSpeedMultiplier, 0f, args.AttackSpeedMultiplierTime, MartialArtModifierType.AttackRate | MartialArtModifierType.Unarmed);
            ApplyMultiplier(ent, args.DamageMultiplier, 0f, args.AttackSpeedMultiplierTime, MartialArtModifierType.Damage | MartialArtModifierType.Unarmed);
        }
        // Wormix EDIT End

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Weapons/genhit3.ogg"), target);
        ComboPopup(ent, target, proto.ID); // CorvaxGoob-Localization // proto.Name -> proto.ID
        ent.Comp.LastAttacks.Clear();
    }

    private void OnJudoThrow(Entity<CanPerformComboComponent> ent, ref JudoThrowPerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out var downed)
            || downed
            || !TryComp<PullableComponent>(target, out var pullable)
            || !TryComp<PullerComponent>(ent, out var puller)
            || !TryComp<GrabIntentComponent>(ent, out var grabIntent))
            return;

        var knockdownTime = proto.ParalyzeTime;

        var ev = new BeforeStaminaDamageEvent(1f);
        RaiseLocalEvent(target, ref ev);

        knockdownTime *= ev.Value;

        // Wormix EDIT Start
        var staminaResistance = new BeforeStaminaDamageEvent(100f);
        RaiseLocalEvent(target, ref staminaResistance);

        var canResist =
               staminaResistance.Value < 98f
            || puller.Pulling != target
            || grabIntent.GrabStage < GrabStage.Hard;

        _stun.TryKnockdown(target, knockdownTime, true, true, !canResist);
        // Wormix EDIT End

        _stamina.TakeStaminaDamage(target, proto.StaminaDamage);

        _pulling.TryStopPull(target, pullable, ent, true);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Weapons/genhit3.ogg"), target);
        ComboPopup(ent, target, proto.ID); // CorvaxGoob-Localization // proto.Name -> proto.ID
        ent.Comp.LastAttacks.Clear();
    }

    private void OnJudoArmbar(Entity<CanPerformComboComponent> ent, ref JudoArmbarPerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out var downed)
            || !downed
            || !TryComp<PullerComponent>(ent, out var puller)
            || !TryComp<GrabIntentComponent>(ent, out var grabIntent)
            || !TryComp<PullableComponent>(target, out var pullable)
            || !TryComp<GrabbableComponent>(target, out var grabbable))
            return;

        var knockdownTime = proto.ParalyzeTime;

        var ev = new BeforeStaminaDamageEvent(1f);
        RaiseLocalEvent(target, ref ev);

        knockdownTime *= ev.Value;

        if (!HasComp<ArmbarredComponent>(target))
        {
            _stamina.TakeStaminaDamage(target, proto.StaminaDamage);
            AddComp<ArmbarredComponent>(target).Puller = ent;
        }

        // Taking someone in an armbar is an equivalent of taking them in a choke grab
        if (grabIntent.GrabStage != GrabStage.Suffocate
            || grabbable.GrabStage != GrabStage.Suffocate)
        {
            _grab.TrySetGrabStages((ent, puller, grabIntent), (target, pullable, grabbable), GrabStage.Suffocate);
            grabbable.NextEscapeAttempt = _timing.CurTime.Add(TimeSpan.FromSeconds(10)); // Wormix EDIT
        }

        _stun.TryKnockdown(target, knockdownTime, true, true, proto.DropItems);

        // Wormix EDIT Start
        if (TryComp<HandsComponent>(target, out var hands))
        {
            foreach (var hand in hands.Hands.Keys)
            {
                _virtualItem.TrySpawnVirtualItemInHand(ent, target);
            }
        }
        // Wormix EDIT End

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Weapons/genhit3.ogg"), target);
        ComboPopup(ent, target, proto.ID); // CorvaxGoob-Localization // proto.Name -> proto.ID
        ent.Comp.LastAttacks.Clear();
    }

    private void OnJudoWheelThrow(Entity<CanPerformComboComponent> ent, ref JudoWheelThrowPerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out var downed)
            || !downed
            || !TryComp<PullableComponent>(target, out var pullable)
            || !TryComp<ArmbarredComponent>(target, out var armbarred)
            || armbarred.Puller != ent.Owner)
            return;

        _stamina.TakeStaminaDamage(target, proto.StaminaDamage);

        _pulling.TryStopPull(target, pullable, ent, true);
        _grabThrowing.Throw(target,
            ent,
            _transform.GetMapCoordinates(ent).Position - _transform.GetMapCoordinates(target).Position,
            5,
            behavior: proto.DropItems);

        // Wormix EDIT Start
        if (TryComp<StandingStateComponent>(ent.Owner, out var standing) && !standing.Standing)
        {
            if (HasComp<KnockedDownComponent>(ent.Owner))
                RemComp<KnockedDownComponent>(ent.Owner);
            _standingState.Stand(ent.Owner, standing);
        }
        // Wormix EDIT End

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Weapons/genhit3.ogg"), target);
        ComboPopup(ent, target, proto.ID); // CorvaxGoob-Localization // proto.Name -> proto.ID
        ent.Comp.LastAttacks.Clear();
    }

    // Not implemented yet, but I'll leave it here
    private void OnJudoGoldenBlast(Entity<CanPerformComboComponent> ent, ref JudoGoldenBlastPerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out var _)
            || !TryComp(target, out StatusEffectsComponent? status)
            || !TryComp<PullableComponent>(target, out var pullable))
            return;

        _stun.TryUpdateParalyzeDuration(target, proto.ParalyzeTime);

        _pulling.TryStopPull(target, pullable, ent, true);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Weapons/genhit3.ogg"), target);
        ComboPopup(ent, target, proto.ID); // CorvaxGoob-Localization // proto.Name -> proto.ID
        ent.Comp.LastAttacks.Clear();
    }

    // Wormix EDIT Start
    private void OnJudoDisarming(Entity<CanPerformComboComponent> ent, ref JudoDisarmingPerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out var downed)
            || !downed
            || !TryComp<PullableComponent>(target, out var pullable)
            || !TryComp<ArmbarredComponent>(target, out var armbarred)
            || armbarred.Puller != ent.Owner
            || !(TryComp(target, out StaminaComponent? stamina)/* && stamina.Critical*/)
            || !TryComp<TargetingComponent>(ent, out var targeting)
            || targeting.Target
                is not TargetBodyPart.LeftHand
                and not TargetBodyPart.RightHand)
            return;

        var (partType, symmetry) = _body.ConvertTargetBodyPart(targeting.Target);
        var targetLimb = _body.GetBodyChildrenOfType(target, partType, symmetry: symmetry).FirstOrDefault();

        var targetEntity = targetLimb.Id != default ? targetLimb.Id : target;

        if (!TryComp<WoundableComponent>(targetLimb.Id, out var woundable)
            || woundable.WoundableIntegrity <= 0)
            return;

        var damage = new DamageSpecifier();
        damage.DamageDict.Add("Blunt", proto.ExtraDamage / 3f);

        _damageable.TryChangeDamage(targetEntity, damage, ignoreResistances: false, origin: ent, canMiss: false);

        if (_wound.TryInduceWound(targetLimb.Id, "Blunt", proto.ExtraDamage, out var woundInduced))
        {
            var bone = woundable.Bone.ContainedEntities.FirstOrDefault();
            if (bone != default)
                _trauma.ApplyBoneTrauma(woundable.Bone.ContainedEntities.FirstOrDefault(), (targetLimb.Id, woundable), (woundInduced.Value.Owner, EnsureComp<TraumaInflicterComponent>(woundInduced.Value.Owner)), proto.ExtraDamage);

            _pulling.TryStopPull(target, pullable, ent, true);

            _audio.PlayPvs(new SoundPathSpecifier("/Audio/_Goobstation/Effects/bone_crack.ogg"), target);
            ComboPopup(ent, target, proto.ID);
            ent.Comp.LastAttacks.Clear();
        }
    }
    // Wormix EDIT End

    #endregion

    #region Armbar
    // Wormix EDIT Start
    private void OnArmbarCancelInteraction(Entity<ArmbarredComponent> ent, ref InteractionAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnArmbarCancelUse(Entity<ArmbarredComponent> ent, ref UseAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnArmbarCancelPickup(Entity<ArmbarredComponent> ent, ref PickupAttemptEvent args)
    {
        args.Cancel();
    }
    // Wormix EDIT End

    private void OnArmbarredStood(Entity<ArmbarredComponent> ent, ref StoodEvent args)
    {
        if (!TryComp<PullableComponent>(ent, out var pullable))
            return;

        _virtualItem.DeleteInHandsMatching(ent, ent.Comp.Puller); // Wormix EDIT
        _pulling.TryStopPull(ent, pullable, ent.Comp.Puller, true);
        RemComp<ArmbarredComponent>(ent);
    }

    private void OnArmbarStopped(Entity<ArmbarredComponent> ent, ref PullStoppedMessage args)
    {
        if (args.PullerUid != ent.Comp.Puller)
            return;

        _virtualItem.DeleteInHandsMatching(ent, ent.Comp.Puller); // Wormix EDIT

        if (!_status.HasStatusEffect(ent, "Stun"))
            _status.TryRemoveStatusEffect(ent, "KnockedDown");

        RemComp<ArmbarredComponent>(ent);
    }

    #endregion
}
