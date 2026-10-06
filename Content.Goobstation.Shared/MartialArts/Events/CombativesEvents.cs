using Robust.Shared.Serialization;

namespace Content.Goobstation.Shared.MartialArts.Events;

[Serializable, NetSerializable, DataDefinition]
public sealed partial class CombativesRestrainPerformedEvent : EntityEventArgs;

[Serializable, NetSerializable, DataDefinition]
public sealed partial class CombativesThrowPerformedEvent : EntityEventArgs;

[Serializable, NetSerializable, DataDefinition]
public sealed partial class CombativesChokePerformedEvent : EntityEventArgs;

[Serializable, NetSerializable, DataDefinition]
public sealed partial class CombativesSlitThroatPerformedEvent : EntityEventArgs;

[Serializable, NetSerializable, DataDefinition]
public sealed partial class CombativesKnockdownPerformedEvent : EntityEventArgs;

[Serializable, NetSerializable, DataDefinition]
public sealed partial class CombativesWeakeningPerformedEvent : EntityEventArgs
{
    [DataField]
    public float SpeedMultiplier = 0.5f;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(3);
}

[Serializable, NetSerializable, DataDefinition]
public sealed partial class CombativesPummelPerformedEvent : EntityEventArgs;

[Serializable, NetSerializable, DataDefinition]
public sealed partial class CombativesDisarmPerformedEvent : EntityEventArgs;
