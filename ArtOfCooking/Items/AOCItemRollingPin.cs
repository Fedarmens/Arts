using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace ArtOfCooking.Items
{
    public class AOCItemRollingPin : Item
    {
        public bool CanRolling(Block block, BlockSelection blockSel)
        {
            var pos = blockSel?.Position;

            if (pos != null)
            {
                var beg = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityGroundStorage;
                if (beg != null)
                {
                    ItemSlot rollingSlot = beg.GetSlotAt(blockSel);
                    var canRoll = rollingSlot?.Itemstack?.Collectible?.Attributes?.KeyExists("canRollingInto") == true;
                    return canRoll;
                }
            }

            return false;
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel != null && CanRolling(blockSel.Block, blockSel) && !byEntity.Controls.ShiftKey)
            {                
                Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);

                if (block != null && byEntity.World.Side == EnumAppSide.Client)
                {
                    byEntity.World.PlaySoundAt(new AssetLocation("sounds/effect/squish2"), byEntity, null, true, 16, 0.5f);
                }
                handling = EnumHandHandling.PreventDefault;
            }
            else
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            }
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel?.Block != null && CanRolling(blockSel.Block, blockSel))
            {
                if (byEntity.Controls.ShiftKey)
                    return false;
                if (byEntity.World is IClientWorldAccessor)
                {
                    byEntity.StartAnimation("squeezehoneycomb");
                }

                return secondsUsed < 2f;
            }

            return base.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel);
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity,
            BlockSelection blockSel, EntitySelection entitySel)
        {
            byEntity.StopAnimation("squeezehoneycomb");
                
            if (byEntity.Controls.ShiftKey) return;

            if (blockSel != null)
            {
                Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
                if (CanRolling(block, blockSel))
                {
                    if (secondsUsed < 1.9f) return;

                    IWorldAccessor world = byEntity.World;
                    var pos = blockSel?.Position;

                    if (!CanRolling(block, blockSel) || pos == null) return;

                    var beg = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityGroundStorage;
                    if (beg == null) return;

                    ItemSlot rollingSlot = beg.GetSlotAt(blockSel);
                    var rollingProps = rollingSlot?.Itemstack?.Collectible?.Attributes["canRollingInto"]?.AsObject<JsonItemStack>();

                    if (rollingProps != null)
                    {
                        ItemStack outputStack = null;
                        switch (rollingProps.Type)
                        {
                            case EnumItemClass.Item:                                
                                var outputItem = api.World.GetItem(new AssetLocation(rollingProps.Code));
                                if (outputItem != null) outputStack = new ItemStack(outputItem, 1);
                                break;
                            case EnumItemClass.Block:
                                var outputBlock = api.World.GetBlock(new AssetLocation(rollingProps.Code));
                                if (outputBlock != null) outputStack = new ItemStack(outputBlock, 1);
                                break;
                        }

                        rollingSlot.TakeOutWhole();
                        rollingSlot.Itemstack = outputStack;
                        rollingSlot.MarkDirty();
                        beg.MarkDirty(true);

                        if (world.Side == EnumAppSide.Client)
                        {
                            world.PlaySoundAt(new AssetLocation("sounds/effect/squish2"), byEntity, null, true, 16, 0.5f);
                        }
                        slot.Itemstack.Collectible.DamageItem(api.World, byEntity, slot, 1);
                        
                        return;
                    }
                }
            }
            base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
        }

        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            byEntity.StopAnimation("squeezehoneycomb");
            return base.OnHeldInteractCancel(secondsUsed, slot, byEntity, blockSel, entitySel, cancelReason);
        }
    }
}
