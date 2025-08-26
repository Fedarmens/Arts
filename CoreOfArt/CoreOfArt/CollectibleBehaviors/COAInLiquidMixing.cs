using System;
using System.Collections.Generic;
using System.Linq;
using CoreOfArts.Systems;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using static Vintagestory.GameContent.BlockLiquidContainerBase;

namespace CoreOfArts.CollectibleBehaviors
{
    public class COAInLiquidMixing : CollectibleBehavior
    {

        COAInLiquidMixingProperties[] Recipes;
        COAInLiquidMixingProperties ActiveRecipe;
        string MixingType = "basemixing";
        public COAInLiquidMixing(CollectibleObject collObj) : base(collObj)
        {
            this.collObj = collObj;
        }

        WorldInteraction[] interactions;

        public COAInLiquidMixingProperties[] GetRecipes ()
        {
            return Recipes;
        }
        public override void Initialize(JsonObject properties)
        {
            Recipes = properties["inLiquidMixingList"]?.AsObject<COAInLiquidMixingProperties[]>();
            MixingType = properties["mixingType"].AsString();

            base.Initialize(properties);
        }
        public override void OnLoaded(ICoreAPI api)
        {
            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;

            interactions = ObjectCacheUtil.GetOrCreate(api, MixingType + "Interactions", () =>
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
                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = "coreofart:heldhelp-" + MixingType,
                        HotKeyCode = "ctrl",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = stacks.ToArray()
                    }
                };
            });
        }

        public bool CanMixIn(ItemSlot slot, EntityAgent byEntity, Block block, BlockSelection blockSel, COAInLiquidMixingProperties recipe)
        {
            var pos = blockSel?.Position;
            int sourceStackSize = slot.StackSize / recipe.SourceSize;
            int coef = 1;
            Item inputItem = byEntity.World.GetItem(new AssetLocation(recipe.InputStack.Code));
            if (inputItem == null) return false;
            ItemStack inputStack = new ItemStack(inputItem, 99999);

            ItemStack outputLiquid = null;
            bool isLiquid = false;

            if (recipe.OutputLiquid != null)
            {
                Item liquidItem = byEntity.World.GetItem(new AssetLocation(recipe.OutputLiquid?.Code));
                if (liquidItem != null)
                {
                    outputLiquid = new ItemStack(liquidItem, 99999);
                    isLiquid = outputLiquid.Collectible.Attributes?["waterTightContainerProps"].Exists == true;
                }
            }

            if (recipe.OutputStacks == null && outputLiquid == null || inputStack == null) return false;

            var props = BlockLiquidContainerBase.GetContainableProps(inputStack);
            int inputStackSize = props != null ? (int)(props?.ItemsPerLitre * recipe.InputLitres) : 0;
             

            if (block is ILiquidSink blcto)
            {
                bool trueContain = blcto?.GetContent(pos)?.Id == inputStack?.Id;
                if (!trueContain) return false;

                float litres = blcto.GetCurrentLitres(pos);
                if (litres < recipe.InputLitres) return false;

                if (recipe.CanBulk && recipe.NeedExactLitres || isLiquid)
                {
                    if (!recipe.CanBulk) return litres == recipe.InputLitres;

                    int blctoStackSize = (int)(props.ItemsPerLitre * litres);
                    bool canMix = blctoStackSize % inputStackSize == 0;
                    
                    if (canMix) coef = blctoStackSize / inputStackSize;
                    return canMix && sourceStackSize >= coef;
                }

                return recipe.NeedExactLitres ? litres == recipe.InputLitres : litres >= recipe.InputLitres;
            }

            if (pos != null)
            {
                var beg = byEntity.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityGroundStorage;
                if (beg != null)
                {
                    ItemSlot mixInSlot = beg.GetSlotAt(blockSel);

                    if (mixInSlot?.Itemstack?.Block is BlockLiquidContainerBase begblcto)
                    {
                        bool trueContain = begblcto?.GetContent(mixInSlot?.Itemstack)?.Id == inputStack?.Id;
                        if (!trueContain) return false;

                        float litres = begblcto.GetCurrentLitres(mixInSlot?.Itemstack);
                        if (litres < recipe.InputLitres) return false;

                        if (recipe.CanBulk && recipe.NeedExactLitres || isLiquid)
                        {
                            if (!recipe.CanBulk) return litres == recipe.InputLitres;

                            int blctoStackSize = (int)(props.ItemsPerLitre * litres);
                            bool canMix = blctoStackSize % inputStackSize == 0;

                            if (canMix) coef = blctoStackSize / inputStackSize;
                            return canMix && sourceStackSize >= coef;
                        }
                        return recipe.NeedExactLitres ? litres == recipe.InputLitres : litres >= recipe.InputLitres;
                    }
                }
            }

            return false;
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            Block block = byEntity.World.BlockAccessor.GetBlock(blockSel?.Position);
            if (ActiveRecipe != null) ActiveRecipe = null;
            if (Recipes != null && block != null && !byEntity.Controls.ShiftKey && byEntity.Controls.CtrlKey)
            {
                foreach (var rec in Recipes)
                {
                    if (rec.OutputStacks == null && rec.OutputLiquid == null || rec.InputStack == null) continue;

                    bool canMixIn = CanMixIn(slot, byEntity, block, blockSel, rec);

                    if (canMixIn)
                    {                        
                        handHandling = EnumHandHandling.PreventDefault;
                        if (byEntity.World.Side == EnumAppSide.Client && rec.Sound != null)
                        {
                            byEntity.World.PlaySoundAt(new AssetLocation(rec.Sound), byEntity, null, true, 16, 0.5f);
                        }
                        ActiveRecipe = rec;
                        break;
                    }
                    else
                    {
                        continue;
                    }
                }
                if (ActiveRecipe == null)
                {
                    base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling, ref handling);
                }
            }
            else
            { 
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling, ref handling); 
            }
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
        {
            if (ActiveRecipe != null && !byEntity.Controls.ShiftKey && byEntity.Controls.CtrlKey)
            {
                if (byEntity.World is IClientWorldAccessor && ActiveRecipe.Animation != null)
                {
                    byEntity.StartAnimation(ActiveRecipe.Animation);
                }
                handling = EnumHandling.PreventSubsequent;

                return ActiveRecipe.MixingTime == 0 ? true : secondsUsed < ActiveRecipe.MixingTime;
            }
            
            return base.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel, ref handling);
            
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
        {
            if (ActiveRecipe?.Animation != null) byEntity.StopAnimation(ActiveRecipe?.Animation);
            
            if (ActiveRecipe != null)
            {
                if (ActiveRecipe.MixingTime != 0 && secondsUsed < ActiveRecipe.MixingTime - 0.1f) return;

                Block block = byEntity.World.BlockAccessor.GetBlock(blockSel?.Position);
                Item inputItem = byEntity.World.GetItem(new AssetLocation(ActiveRecipe.InputStack.Code));
                if (inputItem == null) return;
                ItemStack inputStack = new ItemStack(inputItem, 99999);

                ItemStack outputLiquid = null;
                bool isLiquid = false;

                if (ActiveRecipe.OutputLiquid != null)
                {
                    Item liquidItem = byEntity.World.GetItem(new AssetLocation(ActiveRecipe.OutputLiquid?.Code));
                    if (liquidItem != null)
                    {
                        outputLiquid = new ItemStack(liquidItem, 99999);
                        isLiquid = outputLiquid.Collectible.Attributes?["waterTightContainerProps"].Exists == true;
                    }
                }

                IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
                int coef = 1;

                bool canMixIn = canMixIn = CanMixIn(slot, byEntity, block, blockSel, ActiveRecipe);

                if (!canMixIn) return;


                if (block is BlockLiquidContainerBase blcto)
                {
                    if (isLiquid)
                    {
                        outputLiquid.StackSize = 99999;
                        coef = (int)(blcto.GetCurrentLitres(blockSel.Position) / ActiveRecipe.InputLitres);
                        float outputLitres = ActiveRecipe.OutputLitres != null ? (float)ActiveRecipe.OutputLitres * coef : blcto.GetCurrentLitres(blockSel.Position);
                        
                        blcto.TryTakeContent(blockSel.Position, 99999);
                        int moved = blcto.TryPutLiquid(blockSel.Position, outputLiquid, outputLitres);
                        blcto.DoLiquidMovedEffects(byPlayer, outputLiquid, moved, EnumLiquidDirection.Fill);
                    }
                    else
                    {
                        var props = BlockLiquidContainerBase.GetContainableProps(blcto?.GetContent(blockSel.Position));
                        float inputLitres = ActiveRecipe.InputLitres;
                        if (ActiveRecipe.CanBulk)
                        {
                            coef = (int)(blcto.GetCurrentLitres(blockSel.Position) / inputLitres);
                        }
                        coef = Math.Min(coef, slot.StackSize / ActiveRecipe.SourceSize);
                        int moved = (int)(props.ItemsPerLitre * (ActiveRecipe.ConsumeInputLitres != null ? ActiveRecipe.ConsumeInputLitres : inputLitres * coef));

                        if (moved != 0)
                        {
                            blcto.TryTakeContent(blockSel.Position, moved);
                            blcto.DoLiquidMovedEffects(byPlayer, inputStack, moved, EnumLiquidDirection.Pour);
                        }
                    }
                }

                if (blockSel?.Position != null)
                {
                    var beg = byEntity.World.BlockAccessor.GetBlockEntity(blockSel?.Position) as BlockEntityGroundStorage;
                    if (beg != null)
                    {
                        ItemSlot mixInSlot = beg.GetSlotAt(blockSel);

                        if (mixInSlot?.Itemstack?.Block is BlockLiquidContainerBase begblcto)
                        {

                            if (isLiquid)
                            {
                                outputLiquid.StackSize = 99999;
                                coef = (int)(begblcto.GetCurrentLitres(mixInSlot?.Itemstack) / ActiveRecipe.InputLitres);
                                float outputLitres = ActiveRecipe.OutputLitres != null ? (float)ActiveRecipe.OutputLitres * coef : begblcto.GetCurrentLitres(mixInSlot?.Itemstack);

                                begblcto.SetContent(mixInSlot?.Itemstack, null);
                                int moved = begblcto.TryPutLiquid(mixInSlot?.Itemstack, outputLiquid, outputLitres);
                                begblcto.DoLiquidMovedEffects(byPlayer, outputLiquid, moved, EnumLiquidDirection.Fill);
                            }
                            else
                            {
                                var props = BlockLiquidContainerBase.GetContainableProps(begblcto?.GetContent(mixInSlot?.Itemstack));
                                float inputLitres = ActiveRecipe.InputLitres;
                                if (ActiveRecipe.CanBulk)
                                {
                                    coef = (int)(begblcto.GetCurrentLitres(mixInSlot?.Itemstack) / inputLitres);
                                }
                                coef = Math.Min(coef, slot.StackSize / ActiveRecipe.SourceSize);
                                int moved = (int)(props.ItemsPerLitre * (ActiveRecipe.ConsumeInputLitres != null ? ActiveRecipe.ConsumeInputLitres : inputLitres * coef));

                                if (moved != 0)
                                {
                                    begblcto.TryTakeLiquid(mixInSlot?.Itemstack, moved / props.ItemsPerLitre);
                                    begblcto.DoLiquidMovedEffects(byPlayer, inputStack, moved, EnumLiquidDirection.Pour);
                                }

                            }
                            beg.MarkDirty(true);
                        }
                    }
                }

                slot.TakeOut(coef * ActiveRecipe.SourceSize);
                slot.MarkDirty();

                if (ActiveRecipe.OutputStacks != null)
                {
                    foreach (var stack in ActiveRecipe.OutputStacks)
                    {
                        CollectibleObject collobj = byEntity.World.GetItem(new AssetLocation(stack.Code));
                        if (collobj == null) collobj = byEntity.World.GetBlock(new AssetLocation(stack.Code));

                        if (collobj != null)
                        {
                            ItemStack outputStack = new ItemStack(collobj, stack.StackSize);
                            outputStack.StackSize *= coef;
                            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
                            if (byPlayer?.InventoryManager.TryGiveItemstack(outputStack) == false)
                            {
                                byEntity.World.SpawnItemEntity(outputStack, byEntity.SidedPos.XYZ);
                            }
                        }
                    }
                }

                ActiveRecipe = null;
                return;
            }
            
            base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel, ref handling);
        }

        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason, ref EnumHandling handled)
        {
            if (ActiveRecipe?.Animation != null) byEntity.StopAnimation(ActiveRecipe?.Animation);
            ActiveRecipe = null;
            return base.OnHeldInteractCancel(secondsUsed, slot, byEntity, blockSel, entitySel, cancelReason, ref handled);
        }


        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot, ref EnumHandling handling)
        {
            return interactions.Append(base.GetHeldInteractionHelp(inSlot, ref handling));
        }
    }
}
