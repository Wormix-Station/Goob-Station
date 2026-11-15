using Content.Shared.Humanoid;

namespace Content.Shared._Wormix;

/// <summary>
/// Reorders sprite layers depending on whether the entity is facing South: slot bookmark layers (clothing is rendered
/// right after them) plus the face and tail blocks (base layers plus every marking attached to them) are moved in
/// front of the configured anchor layers. Handled client-side by DirectionalClothingLayerSystem.
/// </summary>
[RegisterComponent]
public sealed partial class DirectionalClothingLayerComponent : Component
{
    /// <summary>
    /// Slot bookmark layer key -> layer key it is drawn before while facing South.
    /// </summary>
    [DataField]
    public Dictionary<string, string> SouthSlots = new();

    /// <summary>
    /// Slot bookmark layer key -> layer key it is drawn before while facing any other direction.
    /// </summary>
    [DataField]
    public Dictionary<string, string> OtherSlots = new();

    /// <summary>
    /// Base layers that make up the face. Markings attached to these layers are moved together with them.
    /// </summary>
    [DataField]
    public List<HumanoidVisualLayers> FaceLayers = new();

    /// <summary>
    /// Layer key the face block is drawn before while facing South.
    /// </summary>
    [DataField]
    public string SouthFaceBefore = "eyes";

    /// <summary>
    /// Layer key the face block is drawn before while facing any other direction. Base layer names
    /// (e.g. RArm) are resolved before plain map keys (e.g. jumpsuit).
    /// </summary>
    [DataField]
    public string? OtherFaceBefore;

    /// <summary>
    /// Base layers that make up the tail. Markings attached to these layers are moved together with them.
    /// </summary>
    [DataField]
    public List<HumanoidVisualLayers> TailLayers = new();

    /// <summary>
    /// Layer key the tail block is drawn before while facing South.
    /// </summary>
    [DataField]
    public string? SouthTailBefore;

    /// <summary>
    /// Layer key the tail block is drawn before while facing any other direction. Base layer names
    /// (e.g. SnoutCover) are resolved before plain map keys (e.g. jumpsuit).
    /// </summary>
    [DataField]
    public string? OtherTailBefore;

    /// <summary>
    /// Direction state the layers were last arranged for, null until the first arrangement.
    /// </summary>
    [ViewVariables]
    public bool? FacingSouth;
}
