﻿using System.Text;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Common;
using Vintagestory.GameContent;
using XLib.XLeveling;
using XSkills;

namespace ArtsXSlills
{
    public class ArtsXSkillsItemPlantableSeed : ItemPlantableSeed
    {
        SkillItem[] toolModes;
        public string Type => Variant["type"];
        public string Size => Variant["size"];

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api is ICoreClientAPI capi)
            {
                toolModes = ObjectCacheUtil.GetOrCreate(api, "hoeToolModes", () =>
                {
                    SkillItem[] modes = new SkillItem[3];

                    modes[0] = new SkillItem() { Code = new AssetLocation("1size"), Name = Lang.Get("1x1") }.WithIcon(capi, ItemClay.Drawcreate1_svg);
                    modes[1] = new SkillItem() { Code = new AssetLocation("2size"), Name = Lang.Get("2x2") }.WithIcon(capi, ItemClay.Drawcreate4_svg);
                    modes[2] = new SkillItem() { Code = new AssetLocation("3size"), Name = Lang.Get("3x3") }.WithIcon(capi, (new ItemClay()).Drawcreate9_svg);

                    return modes;
                });
            }
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            if (toolModes == null) return;
            for (int ii = 0; ii < toolModes.Length; ii++)
            {
                toolModes[ii]?.Dispose();
            }
        }

        public override SkillItem[] GetToolModes(ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel)
        {
            Farming farming = XLeveling.Instance(api)?.GetSkill("farming") as Farming;
            if (farming == null) return null;
            PlayerAbility playerAbility = forPlayer?.Entity?.GetBehavior<PlayerSkillSet>()?[farming.Id][farming.ExtensiveFarmingId];
            if (playerAbility != null ? playerAbility.Tier <= 0 : true) return null;
            return toolModes.Copy(0, playerAbility.Tier + 1);
        }

        public override int GetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            Farming farming = XLeveling.Instance(api)?.GetSkill("farming") as Farming;
            if (farming == null) return 0;
            PlayerAbility playerAbility = byPlayer.Entity?.GetBehavior<PlayerSkillSet>()[farming.Id][farming.ExtensiveFarmingId];
            if (playerAbility == null) return 0;

            return GameMath.Clamp(slot.Itemstack.Attributes.GetInt("toolMode"), 0, playerAbility.Tier);
        }

        public override void SetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel, int toolMode)
        {
            slot.Itemstack.Attributes.SetInt("toolMode", toolMode);
        }

        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (blockSel == null) return;
            IPlayer byPlayer = (byEntity as EntityPlayer).Player;

            string lastCodePart = itemslot.Itemstack.Collectible.LastCodePart();
            Block cropBlock = byEntity.World.GetBlock(new AssetLocation("game:crop-" + Size + "-" + lastCodePart + "-1"));
            if (lastCodePart == "pumpkin") cropBlock = byEntity.World.GetBlock(new AssetLocation("game:crop-" + lastCodePart + "-" + Size + "-1"));
            if (cropBlock == null) return;

            Farming farming = XLeveling.Instance(api)?.GetSkill("farming") as Farming;
            if (farming == null)
            {
                BaseOnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
                return;
            }
            PlayerSkill playerSkill = byEntity.GetBehavior<PlayerSkillSet>()?[farming.Id];
            if (playerSkill == null)
            {
                BaseOnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
                return;
            }
            PlayerAbility playerAbility = playerSkill[farming.ExtensiveFarmingId];
            int toolMode = GetToolMode(itemslot, byPlayer, blockSel);

            int range = 1;
            if (playerAbility != null && toolMode > 0) range = playerAbility.Ability.Value(toolMode, 0);

            int x = blockSel.Position.X;
            int y = blockSel.Position.Y;
            int z = blockSel.Position.Z;

            int directionX = 0;
            int directionZ = 0;

            if (range % 2 == 0)
            {
                if (x - byEntity.Pos.X >= 0.0f) directionX = 1;
                if (z - byEntity.Pos.Z >= 0.0f) directionZ = 1;
            }

            x = x - range / 2 + directionX;
            z = z - range / 2 + directionZ;
            AssetLocation sound = null;

            for (int xx = x; xx < x + range && itemslot.StackSize > 0; ++xx)
            {
                for (int zz = z; zz < z + range && itemslot.StackSize > 0; ++zz)
                {
                    BlockPos pos = new BlockPos(xx, y, zz, blockSel.Position.dimension);
                    BlockEntityFarmland farmland = byEntity.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityFarmland;
                    if (farmland != null)
                    {
                        bool planted = farmland.TryPlant(cropBlock);
                        if (planted)
                        {
                            handHandling = EnumHandHandling.PreventDefault;
                            if (sound == null) sound = new AssetLocation("sounds/block/plant");

                            (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

                            if (byPlayer?.WorldData?.CurrentGameMode != EnumGameMode.Creative)
                            {
                                itemslot.TakeOut(1);
                                itemslot.MarkDirty();
                            }

                            // cultivated seeds
                            playerAbility = playerSkill[farming.CultivatedSeedsId];
                            if (playerAbility?.Tier > 0 )
                            {
                                if (farmland.roomness > 0) farmland.TryGrowCrop(api.World.Calendar.TotalHours);
                                if (byEntity.World.Rand.NextDouble() < playerAbility.SkillDependentFValue()) farmland.TryGrowCrop(api.World.Calendar.TotalHours);
                            }
                        }
                    }
                }
            }
            if (sound != null) byEntity.World.PlaySoundAt(sound, blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, null);
        }
        
        public void BaseOnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (blockSel == null) return;

            BlockPos pos = blockSel.Position;

            string lastCodePart = itemslot.Itemstack.Collectible.LastCodePart();

            if (lastCodePart == "bellpepper") return;

            BlockEntity be = byEntity.World.BlockAccessor.GetBlockEntity(pos);
            if (be is BlockEntityFarmland)
            {
                Block cropBlock = byEntity.World.GetBlock(new AssetLocation("game:crop-" + Size + "-" + lastCodePart + "-1"));
                if (lastCodePart == "pumpkin") cropBlock = byEntity.World.GetBlock(new AssetLocation("game:crop-" + lastCodePart + "-" + Size + "-1"));
                if (cropBlock == null) return;

                IPlayer byPlayer = null;
                if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);

                bool planted = ((BlockEntityFarmland)be).TryPlant(cropBlock);
                if (planted)
                {
                    byEntity.World.PlaySoundAt(new AssetLocation("sounds/block/plant"), pos, 0.4375, byPlayer);

                    ((byEntity as EntityPlayer)?.Player as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

                    if (byPlayer?.WorldData?.CurrentGameMode != EnumGameMode.Creative)
                    {
                        itemslot.TakeOut(1);
                        itemslot.MarkDirty();
                    }
                }

                if (planted) handHandling = EnumHandHandling.PreventDefault;
            }
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            WorldInteraction[] temp = base.GetHeldInteractionHelp(inSlot);
            IPlayer byPlayer = (inSlot.Inventory as InventoryBasePlayer)?.Player;

            Farming farming = XLeveling.Instance(api)?.GetSkill("farming") as Farming;
            if (farming == null) return temp;
            PlayerAbility playerAbility = byPlayer?.Entity?.GetBehavior<PlayerSkillSet>()?[farming.Id][farming.ExtensiveFarmingId];
            if (!(playerAbility?.Tier > 0)) return temp;

            return new WorldInteraction[] {
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-selecttoolmode",
                    HotKeyCode = "toolmodeselect",
                    MouseButton = EnumMouseButton.None
                }
            }.Append(temp);
        }
        
        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {   
            ItemStack itemstack = inSlot.Itemstack;
            EntityPlayer entityPlayer = ((world.Side == EnumAppSide.Client) ? (world as IClientWorldAccessor).Player.Entity : null);
            float spoilState = AppendPerishableInfoText(inSlot, dsc, world);
            FoodNutritionProperties nutritionProperties = GetNutritionProperties(world, itemstack, entityPlayer);
            if (nutritionProperties != null)
            {
            float num2 = GlobalConstants.FoodSpoilageSatLossMul(spoilState, itemstack, entityPlayer);
            float num3 = GlobalConstants.FoodSpoilageHealthLossMul(spoilState, itemstack, entityPlayer);
            if (Math.Abs(nutritionProperties.Health * num3) > 0.001f)
            {
                dsc.AppendLine(Lang.Get((MatterState == EnumMatterState.Liquid) ? "liquid-when-drunk-saturation-hp" : "When eaten: {0} sat, {1} hp", Math.Round(nutritionProperties.Satiety * num2), Math.Round(nutritionProperties.Health * num3, 2)));
            }
            else
            {
                dsc.AppendLine(Lang.Get((MatterState == EnumMatterState.Liquid) ? "liquid-when-drunk-saturation" : "When eaten: {0} sat", Math.Round(nutritionProperties.Satiety * num2)));
            }

            dsc.AppendLine(Lang.Get("Food Category: {0}", Lang.Get("foodcategory-" + nutritionProperties.FoodCategory.ToString().ToLowerInvariant())));
            }
            
            dsc.AppendLine(Lang.Get("artofgrowing:size-food: {0}", Lang.Get("artofgrowing:food-" + Size)));

            dsc.Append("\n");
        
            Block cropBlock = world.GetBlock(new AssetLocation("game:crop-" + Size + "-" + inSlot.Itemstack.Collectible.LastCodePart() + "-1"));
            if (inSlot.Itemstack.Collectible.LastCodePart() == "pumpkin") cropBlock = world.GetBlock(new AssetLocation("game:crop-" + inSlot.Itemstack.Collectible.LastCodePart() + "-" + Size + "-1"));
            if (cropBlock == null || cropBlock.CropProps == null) return;
            
            dsc.AppendLine(Lang.Get("soil-nutrition-requirement") + cropBlock.CropProps.RequiredNutrient);
            dsc.AppendLine(Lang.Get("soil-nutrition-consumption") + cropBlock.CropProps.NutrientConsumption);

            double totalDays = cropBlock.CropProps.TotalGrowthDays;
            if (totalDays > 0)
            {
                var defaultTimeInMonths = totalDays / 12;
                totalDays = defaultTimeInMonths * world.Calendar.DaysPerMonth;
            } else
            {
                totalDays = cropBlock.CropProps.TotalGrowthMonths * world.Calendar.DaysPerMonth;
            }

            totalDays /= api.World.Config.GetDecimal("cropGrowthRateMul", 1);

            dsc.AppendLine(Lang.Get("soil-growth-time") + " " + Lang.Get("count-days", Math.Round(totalDays, 1)));
            dsc.AppendLine(Lang.Get("crop-coldresistance", Math.Round(cropBlock.CropProps.ColdDamageBelow, 1)));
            dsc.AppendLine(Lang.Get("crop-heatresistance", Math.Round(cropBlock.CropProps.HeatDamageAbove, 1)));
        }

    }//!public class ArtsXskillsItemPlantableSeed
}//!namespace XSkills
