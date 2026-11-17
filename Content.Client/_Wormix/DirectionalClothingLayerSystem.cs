using Content.Shared._Wormix;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client._Wormix;

public sealed class DirectionalClothingLayerSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly MarkingManager _markings = default!;

    public override void FrameUpdate(float frameTime)
    {
        var eyeRot = _eye.CurrentEye.Rotation;

        var query = EntityQueryEnumerator<DirectionalClothingLayerComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var sprite, out var xform))
        {
            // Same angle-to-direction logic the sprite uses, so the swap lines up with the body's own direction.
            var angle = (_transform.GetWorldRotation(xform) + eyeRot).Reduced().FlipPositive();
            var south = SpriteComponent.Layer.GetDirection(RsiDirectionType.Dir4, angle) == RsiDirection.South;

            if (comp.FacingSouth == south)
                continue;

            comp.FacingSouth = south;
            Arrange((uid, comp), (uid, sprite), south);
        }
    }

    private void Arrange(Entity<DirectionalClothingLayerComponent> ent, Entity<SpriteComponent> sprite, bool south)
    {
        if (south)
        {
            // Slots first, so that the face block ends up in front of them when both share an anchor.
            foreach (var (slot, anchor) in ent.Comp.SouthSlots)
            {
                MoveBookmark(ent, sprite, slot, anchor);
            }

            MoveBlock(ent, sprite, ent.Comp.FaceLayers, ent.Comp.SouthFaceBefore);
            MoveBlock(ent, sprite, ent.Comp.TailLayers, ent.Comp.SouthTailBefore);
        }
        else
        {
            // Tail first: the slot bookmarks (e.g. the cloak at pocket1) are inserted afterwards,
            // so the tail ends up above the helmet but below the cloak.
            MoveBlock(ent, sprite, ent.Comp.TailLayers, ent.Comp.OtherTailBefore);

            foreach (var (slot, anchor) in ent.Comp.OtherSlots)
            {
                MoveBookmark(ent, sprite, slot, anchor);
            }

            MoveBlock(ent, sprite, ent.Comp.FaceLayers, ent.Comp.OtherFaceBefore);
        }
    }

    /// <summary>
    /// Resolves a configured anchor key to a layer index. Base layer names (e.g. SnoutCover) are tried
    /// before plain map keys (e.g. jumpsuit), so one config value works for both key kinds.
    /// </summary>
    private bool TryGetLayerIndex(Entity<SpriteComponent> sprite, string? key, out int index)
    {
        index = default;
        if (string.IsNullOrEmpty(key))
            return false;

        if (Enum.TryParse<HumanoidVisualLayers>(key, out var enumKey) &&
            _sprite.LayerMapTryGet(sprite.AsNullable(), enumKey, out index, false))
        {
            return true;
        }

        return _sprite.LayerMapTryGet(sprite.AsNullable(), key, out index, false);
    }

    private void MoveBookmark(
        Entity<DirectionalClothingLayerComponent> ent,
        Entity<SpriteComponent> sprite,
        string slot,
        string anchor)
    {
        if (!_sprite.LayerMapTryGet(sprite.AsNullable(), anchor, out _, false) ||
            !_sprite.RemoveLayer(sprite.AsNullable(), slot, false))
        {
            return;
        }

        // Anchor index is looked up after the removal because it may have shifted.
        if (!_sprite.LayerMapTryGet(sprite.AsNullable(), anchor, out var index, false))
            return;

        _sprite.AddBlankLayer(sprite, index);
        _sprite.LayerMapSet(sprite.AsNullable(), slot, index);

        // Clothing layers are inserted right after the bookmark, so they have to be rendered again.
        if (_inventory.TryGetSlotEntity(ent, slot, out var item))
            RaiseLocalEvent(ent, new VisualsChangedEvent(GetNetEntity(item.Value), slot));
    }

    private void MoveBlock(
        Entity<DirectionalClothingLayerComponent> ent,
        Entity<SpriteComponent> sprite,
        List<HumanoidVisualLayers> layers,
        string? anchor)
    {
        if (layers.Count == 0)
            return;

        if (!TryGetLayerIndex(sprite, anchor, out _))
            return;

        var entries = new List<(int Index, Enum? EnumKey, string? StringKey)>();
        var seen = new HashSet<int>();

        foreach (var baseLayer in layers)
        {
            if (_sprite.LayerMapTryGet(sprite.AsNullable(), baseLayer, out var index, false) && seen.Add(index))
                entries.Add((index, baseLayer, null));
        }

        if (TryComp(ent, out HumanoidAppearanceComponent? humanoid))
        {
            foreach (var list in humanoid.MarkingSet.Markings.Values)
            {
                foreach (var marking in list)
                {
                    if (!_markings.TryGetMarking(marking, out var proto))
                        continue;

                    foreach (var markingSprite in proto.Sprites)
                    {
                        if (markingSprite is not SpriteSpecifier.Rsi rsi)
                            continue;

                        // Mirrors the layer selection in HumanoidAppearanceSystem.ApplyMarking.
                        var slot = proto.BodyPart;
                        if (proto.Layering != null &&
                            proto.Layering.TryGetValue(rsi.RsiState, out var layerName) &&
                            Enum.TryParse<HumanoidVisualLayers>(layerName, out var parsed))
                        {
                            slot = parsed;
                        }

                        if (!layers.Contains(slot))
                            continue;

                        var key = $"{proto.ID}-{rsi.RsiState}";
                        if (_sprite.LayerMapTryGet(sprite.AsNullable(), key, out var index, false) && seen.Add(index))
                            entries.Add((index, null, key));
                    }
                }
            }
        }

        if (entries.Count == 0)
            return;

        entries.Sort((a, b) => a.Index.CompareTo(b.Index));

        // Remove back to front so the remaining indices stay valid, then re-insert the very same layer objects
        // (keeping rsi, state, color and shader) in their original relative order.
        var removed = new List<(SpriteComponent.Layer Layer, Enum? EnumKey, string? StringKey)>();
        for (var i = entries.Count - 1; i >= 0; i--)
        {
            var entry = entries[i];
            if (!_sprite.RemoveLayer(sprite.AsNullable(), entry.Index, out var layer, false))
                continue;

            removed.Insert(0, (layer, entry.EnumKey, entry.StringKey));
        }

        // Null index appends at the end, which is only reached if the anchor vanished during removal.
        int? anchorIndex = TryGetLayerIndex(sprite, anchor, out var anchorIndexValue)
            ? anchorIndexValue
            : null;

        foreach (var (layer, enumKey, stringKey) in removed)
        {
            var newIndex = _sprite.AddLayer(sprite.AsNullable(), layer, anchorIndex);
            if (anchorIndex != null)
                anchorIndex++;

            if (enumKey != null)
                _sprite.LayerMapSet(sprite.AsNullable(), enumKey, newIndex);
            else if (stringKey != null)
                _sprite.LayerMapSet(sprite.AsNullable(), stringKey, newIndex);
        }
    }
}
