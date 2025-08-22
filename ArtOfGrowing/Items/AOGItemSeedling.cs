using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace ArtOfGrowing.Items
{
    public class AOGItemSeedling : Item
    {
        ICoreClientAPI capi;
        WorldInteraction[] interactions;
        public string Type => Variant["type"];
        public string Size => Variant["size"];
        public string Name => Code.FirstCodePart();
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            capi = api as ICoreClientAPI;

            interactions = ObjectCacheUtil.GetOrCreate(api, "seedInteractions", () =>
            {
                List<ItemStack> stacks = new List<ItemStack>();

                foreach (Block block in api.World.Blocks)
                {
                    if (block.Code == null || block.EntityClass == null) continue;

                    Type type = api.World.ClassRegistry.GetBlockEntity(block.EntityClass);
                    if (type == typeof(BlockEntityFarmland))
                    {
                        stacks.Add(new ItemStack(block));
                    }
                }

                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = "heldhelp-plant",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = stacks.ToArray()
                    }
                };
            });
        }      
        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (blockSel != null) 
            { 
                    BlockPos pos = blockSel.Position;

                    string lastCodePart = itemslot.Itemstack.Collectible.LastCodePart();

                    BlockEntity be = byEntity.World.BlockAccessor.GetBlockEntity(pos);
                    if (be is BlockEntityFarmland)
                    {
                        Block cropBlock = byEntity.World.GetBlock(new AssetLocation("game:crop-seed-" + lastCodePart + "-1"));
                        if (Size != null) cropBlock = byEntity.World.GetBlock(new AssetLocation("game:crop-seed-" + Size + "-" + lastCodePart + "-1"));
                        if (cropBlock != null)
                        { 

                            IPlayer byPlayer = null;
                            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);

                            bool planted = ((BlockEntityFarmland)be).TryPlant(cropBlock, itemslot, byEntity, blockSel);
                            if (planted)
                            {
                                byEntity.World.PlaySoundAt(new AssetLocation("sounds/block/plant"), pos.X, pos.Y, pos.Z, byPlayer);

                                ((byEntity as EntityPlayer)?.Player as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

                                if (byPlayer?.WorldData?.CurrentGameMode != EnumGameMode.Creative)
                                {
                                    itemslot.TakeOut(1);
                                    itemslot.MarkDirty();
                                }
                                handHandling = EnumHandHandling.PreventDefault;
                                return;
                            }
                        }
                    }
            }
            EnumHandHandling bhHandHandling = EnumHandHandling.NotHandled;            
            WalkBehaviors(
                (CollectibleBehavior bh, ref EnumHandling hd) => bh.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref bhHandHandling, ref hd),
                () => tryEatBegin(itemslot, byEntity, ref bhHandHandling)
            );
            handHandling = bhHandHandling;
        }
        
        void WalkBehaviors(CollectibleBehaviorDelegate onBehavior, Action defaultAction)
        {
            bool executeDefault = true;
            foreach (CollectibleBehavior behavior in CollectibleBehaviors)
            {
                EnumHandling handling = EnumHandling.PassThrough;
                onBehavior(behavior, ref handling);

                if (handling == EnumHandling.PreventSubsequent) return;
                if (handling == EnumHandling.PreventDefault) executeDefault = false;
            }

            if (executeDefault) defaultAction();
        }
        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {            
            return interactions.Append(base.GetHeldInteractionHelp(inSlot));
        }

    }
}
