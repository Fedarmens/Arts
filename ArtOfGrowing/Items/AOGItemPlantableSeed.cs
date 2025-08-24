using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.API.Common.Entities;
using System.Drawing;

namespace ArtOfGrowing.Items
{
    public class AOGItemPlantableSeed : ItemPlantableSeed
    {
        ICoreClientAPI capi;
        public string Type => Variant["type"];
        public string Size => Variant["size"];
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            capi = api as ICoreClientAPI; 
            
        }  
        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
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

                bool planted = ((BlockEntityFarmland)be).TryPlant(cropBlock, itemslot, byEntity, blockSel);
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
    }
}
