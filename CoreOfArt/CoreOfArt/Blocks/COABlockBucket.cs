using CoreOfArts.Systems;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace CoreOfArts.Blocks
{
    public class COABlockBucket : BlockBucket
    {
        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        { 
            if (!byEntity.Controls.ShiftKey && byEntity.Controls.CtrlKey)
            {
                foreach (var recipe in api.GetLiquidMixingRecipes())
                {
                    foreach (var ingredient in recipe.Ingredients)
                    {
                        if (ingredient.ResolvedItemstack.Id == GetContent(itemslot.Itemstack)?.Id)
                        {
                            recipe.TryCraftNow(api, itemslot, byEntity, blockSel, entitySel, recipe);
                        }
                    }
                } 
            }

            base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
        }
        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            List<ItemStack> stacks = new List<ItemStack>();

            foreach (Block block in api.World.Blocks)
            {
                if (block.Code == null) continue;

                if (block is BlockLiquidContainerBase)
                {
                    stacks.Add(new ItemStack(block));
                }
            }

            return base.GetHeldInteractionHelp(inSlot).Append(
                [
                    new WorldInteraction
                    {
                        ActionLangCode = "coreofart:heldhelp-mixing",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "ctrl",
                        Itemstacks = stacks.ToArray(),
                        GetMatchingStacks = (wi, _, _) =>
                        {
                            bool canMixing = false;
                            foreach (var recipe in api.GetLiquidMixingRecipes())
                            {
                                foreach (var ingredient in recipe.Ingredients)
                                {
                                    if (ingredient.ResolvedItemstack.Id == GetContent(inSlot.Itemstack)?.Id)
                                    {
                                        canMixing = true;
                                    }
                                }
                            }

                            return canMixing ? wi.Itemstacks : null;
                        }
                    }
                ]
            );
        }
    }
}