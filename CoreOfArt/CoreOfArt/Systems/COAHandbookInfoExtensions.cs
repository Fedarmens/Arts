using Cairo;
using CoreOfArts.CollectibleBehaviors;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;
using static System.Net.Mime.MediaTypeNames;
using static Vintagestory.Server.Timer;

namespace CoreOfArts.Systems
{
    public static class COAHandbookInfoExtensions
    {
        public static void COAcreatedByMixingInfo(this List<RichTextComponentBase> components, ItemSlot inSlot, ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor)
        {
            List<COALiquidMixingRecipe> mixed = new List<COALiquidMixingRecipe>();
            List<COAInLiquidMixingProperties> mixedIn = new List<COAInLiquidMixingProperties>();

            foreach (var recipe in capi.GetLiquidMixingRecipes())
            {
                if (recipe != null)
                {
                    ItemStack sourceStack = recipe.Ingredients[0].ResolvedItemstack;
                    ItemStack inputStack = recipe.Ingredients[1].ResolvedItemstack;
                    ItemStack outputStack = recipe.Output.ResolvedItemstack;
                    if (sourceStack != null && inputStack != null && outputStack != null
                        && outputStack.Equals(capi.World, inSlot.Itemstack, GlobalConstants.IgnoredStackAttributes))
                        mixed.Add(recipe);
                }
            }

            foreach (var item in allStacks)
            {
                if (item.Collectible.HasBehavior<COAInLiquidMixing>())
                {
                    COAInLiquidMixing beh = item.Collectible.GetCollectibleBehavior<COAInLiquidMixing>(false);
                    foreach (var rec in beh.GetRecipes())
                    {
                        var inputStack = capi.World.GetItem(new AssetLocation(rec.InputStack.Code));


                        if (inputStack != null)
                        {
                            if (rec.OutputStacks != null)
                            {
                                foreach (var output in rec.OutputStacks)
                                {
                                    ItemStack outputStack = null;
                                    var outputItem = capi.World.GetItem(new AssetLocation(output.Code));
                                    if (outputItem != null) outputStack = new ItemStack(outputItem);
                                    else
                                    {
                                        var outputBlock = capi.World.GetBlock(new AssetLocation(output.Code));
                                        if (outputBlock != null) outputStack = new ItemStack(outputBlock);
                                    }

                                    if (outputStack != null && outputStack.Equals(capi.World, inSlot.Itemstack, GlobalConstants.IgnoredStackAttributes))
                                    {
                                        mixedIn.Add(rec);
                                    }
                                }
                            }
                            if (rec.OutputLiquid != null)
                            {
                                ItemStack outputLiquid = null;
                                var liquid = capi.World.GetItem(new AssetLocation(rec.OutputLiquid?.Code));
                                if (liquid != null) outputLiquid = new ItemStack(liquid);
                                if (outputLiquid != null && outputLiquid.Equals(capi.World, inSlot.Itemstack, GlobalConstants.IgnoredStackAttributes))
                                    mixedIn.Add(rec);
                            }
                        }
                    }
                }
            }

            if (mixed.Count > 0 || mixedIn.Count > 0)
            {
                components.Add(new ClearFloatTextComponent(capi, 14));

                var bullet = new RichTextComponent(capi, Lang.Get("Mixing (in world)") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold));
                bullet.PaddingLeft = 2;
                components.Add(bullet);

                if (mixed.Count > 0)
                    foreach (var rec in mixed)
                    {
                        if (rec != null)
                        {
                            ItemStack sourceStack = rec.Ingredients[0].ResolvedItemstack;
                            if (sourceStack.Collectible.Attributes?["waterTightContainerProps"].Exists == true)
                            {
                                var props = BlockLiquidContainerBase.GetContainableProps(sourceStack);
                                sourceStack.StackSize = (int)(props.ItemsPerLitre * rec.Ingredients[0].Litres);
                            }
                            else
                            {
                                sourceStack.StackSize = 1;
                            }

                            ItemStack inputStack = rec.Ingredients[1].ResolvedItemstack;
                            if (inputStack.Collectible.Attributes?["waterTightContainerProps"].Exists == true)
                            {
                                var props = BlockLiquidContainerBase.GetContainableProps(inputStack);
                                inputStack.StackSize = (int)(props.ItemsPerLitre * rec.Ingredients[1].Litres);
                            }
                            else
                            {
                                inputStack.StackSize = 1;
                            }

                            ItemStack outputStack = rec.Output.ResolvedItemstack;
                            if (outputStack.Collectible.Attributes?["waterTightContainerProps"].Exists == true)
                            {
                                var props = BlockLiquidContainerBase.GetContainableProps(outputStack);
                                outputStack.StackSize = (int)(props.ItemsPerLitre * (rec.Ingredients[0].Litres + rec.Ingredients[1].Litres));
                            }
                            else
                            {
                                outputStack.StackSize = 1;
                            }

                            var scomp = new ItemstackTextComponent(capi, sourceStack, 40, 0, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                            scomp.ShowStacksize = true;
                            scomp.PaddingLeft = 2;
                            components.Add(scomp);

                            RichTextComponent cmp = new RichTextComponent(capi, " + ", CairoFont.WhiteMediumText());
                            cmp.VerticalAlign = EnumVerticalAlign.Middle;
                            components.Add(cmp);

                            var icomp = new ItemstackTextComponent(capi, inputStack, 40, 0, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                            icomp.ShowStacksize = true;
                            components.Add(icomp);

                            var eqcomp = new RichTextComponent(capi, " = ", CairoFont.WhiteMediumText());
                            eqcomp.VerticalAlign = EnumVerticalAlign.Middle;
                            components.Add(eqcomp);

                            var ocomp = new ItemstackTextComponent(capi, outputStack, 40, 0, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                            ocomp.ShowStacksize = true;
                            components.Add(ocomp);
                            components.Add(new ClearFloatTextComponent(capi, 10));
                        }
                    }

                if (mixedIn.Count > 0)
                    foreach (var rec in mixedIn)
                    {
                        if (rec != null)
                        {
                            CollectibleObject sourceColl = capi.World.GetItem(new AssetLocation(rec.InitialCode));
                            if (sourceColl == null) sourceColl = capi.World.GetBlock(new AssetLocation(rec.InitialCode));
                            if (sourceColl == null) continue;
                            ItemStack sourceStack = new ItemStack(sourceColl)
                            {
                                StackSize = rec.SourceSize
                            };

                            Item inputItem = capi.World.GetItem(new AssetLocation(rec.InputStack.Code));
                            if (inputItem == null) continue;
                            ItemStack inputStack = new ItemStack(inputItem);

                            if (inputStack.Collectible.Attributes?["waterTightContainerProps"].Exists == true)
                            {
                                var props = BlockLiquidContainerBase.GetContainableProps(inputStack);
                                inputStack.StackSize = (int)(props.ItemsPerLitre * rec.InputLitres);
                            }
                            else
                            {
                                inputStack.StackSize = 1;
                            }


                            var icomp = new ItemstackTextComponent(capi, inputStack, 40, 0, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                            icomp.ShowStacksize = true;
                            icomp.PaddingLeft = 2;
                            components.Add(icomp);

                            RichTextComponent cmp = new RichTextComponent(capi, " + ", CairoFont.WhiteMediumText());
                            cmp.VerticalAlign = EnumVerticalAlign.Middle;
                            components.Add(cmp);

                            var scomp = new ItemstackTextComponent(capi, sourceStack, 40, 0, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                            scomp.ShowStacksize = true;
                            scomp.PaddingLeft = 0;
                            components.Add(scomp);


                            var eqcomp = new RichTextComponent(capi, " = ", CairoFont.WhiteMediumText());
                            eqcomp.VerticalAlign = EnumVerticalAlign.Middle;
                            components.Add(eqcomp);

                            if (rec.OutputLiquid != null)
                            {
                                Item liquidItem = capi.World.GetItem(new AssetLocation(rec.OutputLiquid.Code));
                                if (liquidItem != null)
                                {
                                    ItemStack outputLiquid = new ItemStack(liquidItem);
                                    if (outputLiquid.Collectible.Attributes?["waterTightContainerProps"].Exists == true)
                                    {
                                        var props = BlockLiquidContainerBase.GetContainableProps(outputLiquid);
                                        outputLiquid.StackSize = (int)(props.ItemsPerLitre * (rec.OutputLitres != null ? rec.OutputLitres : rec.InputLitres));
                                    }
                                    else
                                    {
                                        outputLiquid.StackSize = 1;
                                    }

                                    var olcomp = new ItemstackTextComponent(capi, outputLiquid, 40, 0, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                                    olcomp.PaddingLeft = 0;
                                    olcomp.ShowStacksize = true;
                                    components.Add(olcomp);

                                }
                            }

                            if (rec.OutputStacks != null)
                            {
                                foreach (var output in rec.OutputStacks)
                                {
                                    Item outputItem = capi.World.GetItem(new AssetLocation(output.Code));
                                    if (outputItem == null) continue;
                                    ItemStack outputStack = new ItemStack(outputItem)
                                    {
                                        StackSize = output.StackSize
                                    };

                                    var ocomp = new ItemstackTextComponent(capi, outputStack, 40, 0, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                                    ocomp.PaddingLeft = 0;
                                    ocomp.ShowStacksize = true;
                                    components.Add(ocomp);
                                }
                            }
                            components.Add(new ClearFloatTextComponent(capi, 10));
                        }
                    }
            }

        }

        public static void COAaddMixingIngredientForInfo(this List<RichTextComponentBase> components, ItemSlot inSlot, ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor)
        {
            List<ItemStack> recipestacks = new List<ItemStack>();

            foreach (var recipe in capi.GetLiquidMixingRecipes())
            {
                if (recipe != null)
                {
                    ItemStack sourceStack = recipe.Ingredients[0].ResolvedItemstack;
                    ItemStack inputStack = recipe.Ingredients[1].ResolvedItemstack;
                    ItemStack outputStack = recipe.Output.ResolvedItemstack;

                    if (sourceStack.Equals(capi.World, inSlot.Itemstack, GlobalConstants.IgnoredStackAttributes)
                        && !recipestacks.Any(s => s.Equals(capi.World, outputStack, GlobalConstants.IgnoredStackAttributes)))
                        recipestacks.Add(outputStack);
                    if (inputStack.Equals(capi.World, inSlot.Itemstack, GlobalConstants.IgnoredStackAttributes)
                        && !recipestacks.Any(s => s.Equals(capi.World, outputStack, GlobalConstants.IgnoredStackAttributes)))
                        recipestacks.Add(outputStack);
                }
            }

            foreach (var val in capi.GetDoughformingRecipes())
            {
                if (val.Ingredient.SatisfiesAsIngredient(inSlot.Itemstack)
                    && !recipestacks.Any(s => s.Equals(capi.World, val.Output.ResolvedItemstack, GlobalConstants.IgnoredStackAttributes)))
                {
                    recipestacks.Add(val.Output.ResolvedItemstack);
                }
            }

            foreach (var item in allStacks)
            {
                if (item.Collectible.HasBehavior<COAInLiquidMixing>())
                {
                    COAInLiquidMixing beh = item.Collectible.GetCollectibleBehavior<COAInLiquidMixing>(false);
                    foreach (var rec in beh.GetRecipes())
                    {
                        ItemStack inputStack = null;

                        var inputItem = capi.World.GetItem(new AssetLocation(rec.InputStack.Code));
                        if (inputItem != null) inputStack = new ItemStack(inputItem);

                        if (inputStack != null)
                        {
                            if (item.Equals(capi.World, inSlot.Itemstack, GlobalConstants.IgnoredStackAttributes)
                                || inputStack.Equals(capi.World, inSlot.Itemstack, GlobalConstants.IgnoredStackAttributes))
                            {
                                if (rec.OutputStacks != null)
                                {
                                    foreach (var output in rec.OutputStacks)
                                    {
                                        ItemStack outputStack = null;
                                        var outputItem = capi.World.GetItem(new AssetLocation(output.Code));
                                        if (outputItem != null) outputStack = new ItemStack(outputItem);
                                        else
                                        {
                                            var outputBlock = capi.World.GetBlock(new AssetLocation(output.Code));
                                            if (outputBlock != null) outputStack = new ItemStack(outputBlock);
                                        }


                                        if (outputStack != null && !recipestacks.Any(s => s.Equals(capi.World, outputStack, GlobalConstants.IgnoredStackAttributes)))
                                        {
                                            recipestacks.Add(outputStack);
                                        }
                                    }
                                }
                                if (rec.OutputLiquid != null)
                                {
                                    ItemStack outputLiquid = null;
                                    var liquid = capi.World.GetItem(new AssetLocation(rec.OutputLiquid?.Code));
                                    if (liquid != null) outputLiquid = new ItemStack(liquid);
                                    if (outputLiquid != null && !recipestacks.Any(s => s.Equals(capi.World, outputLiquid, GlobalConstants.IgnoredStackAttributes)))
                                        recipestacks.Add(outputLiquid);
                                }
                            }
                        }
                    }
                }
            }

            if (recipestacks.Count > 0)
            {
                int firstIndent = 2;
                components.Add(new ClearFloatTextComponent(capi, 14));

                var bullet = new RichTextComponent(capi, Lang.Get("Ingredient for") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold));
                bullet.PaddingLeft = 2;
                components.Add(bullet);
                
                components.Add(new ClearFloatTextComponent(capi, 2));

                while (recipestacks.Count > 0)
                {
                    ItemStack dstack = recipestacks[0];
                    recipestacks.RemoveAt(0);
                    if (dstack == null) continue;

                    SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, recipestacks, 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                    components.Add(comp);
                }

                components.Add(new ClearFloatTextComponent(capi, 3));
            }
        }
        public static void COAaddCreatedByInfo(this List<RichTextComponentBase> components, ItemSlot inSlot, ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor)
        {
            bool doughformable = false;

            foreach (var val in capi.GetDoughformingRecipes())
            {
                if (val.Output.ResolvedItemstack.Equals(capi.World, inSlot.Itemstack, GlobalConstants.IgnoredStackAttributes))
                {
                    doughformable = true;
                    break;
                }
            }
            if (doughformable)
            {
                components.Add(new ClearFloatTextComponent(capi, 14));
                var bullet = new RichTextComponent(capi, Lang.Get("Created by") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold));
                bullet.PaddingLeft = 2;
                components.Add(bullet);

                var verticalSpaceSmall = new ClearFloatTextComponent(capi, 7);
                var verticalSpace = new ClearFloatTextComponent(capi, 3);

                components.Add(verticalSpace);
                verticalSpace = verticalSpaceSmall;

                var bullet2 = new RichTextComponent(capi, "• ", CairoFont.WhiteSmallText());
                bullet2.PaddingLeft = 2;
                components.Add(bullet2);
                components.Add(new LinkTextComponent(capi, Lang.Get("Dough forming") + "\n", CairoFont.WhiteSmallText(), (cs) => { openDetailPageFor("artsguides-doughforming"); }));
            }
        }
        public static void COACookingRecipes(this List<RichTextComponentBase> components, ItemSlot inSlot, ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor)
        {
            List<CookingRecipe> primecookrecipes = new List<CookingRecipe>();
            List<CookingRecipe> secondcookrecipes = new List<CookingRecipe>();
            foreach (var val in capi.GetCookingRecipes())
            {
                if (val.CooksInto?.ResolvedItemstack == null)
                {
                    foreach (var ingred in val.Ingredients)
                    {
                        if (ingred.GetMatchingStack(inSlot.Itemstack) != null)
                        {
                            if (ingred.MinQuantity > 0) primecookrecipes.Add(val);
                            if (ingred.MinQuantity <= 0) secondcookrecipes.Add(val);
                            break;
                        }
                    }
                }
            }

            if (primecookrecipes.Count > 0)
            {
                components.Add(new ClearFloatTextComponent(capi, 14));                
                var bullet = new RichTextComponent(capi, Lang.Get("Main cooking ingredient for") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold));
                bullet.PaddingLeft = 2;
                components.Add(bullet);

                foreach (var rec in primecookrecipes)
                {
                    components.Add(new ClearFloatTextComponent(capi, 2));
                    var spacer = new RichTextComponent(capi, "", CairoFont.WhiteSmallText());
                    spacer.PaddingLeft = 2;
                    components.Add(spacer);
                    components.AddRange(VtmlUtil.Richtextify(capi, Lang.Get("meal-recipe-" + rec.Code) + "\n", CairoFont.WhiteSmallText()));
                }
                components.Add(new ClearFloatTextComponent(capi, 10));

            }
            
            if (secondcookrecipes.Count > 0)
            {
                components.Add(new ClearFloatTextComponent(capi, 14));

                var bullet = new RichTextComponent(capi, Lang.Get("Additional cooking ingredient for") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold));
                bullet.PaddingLeft = 2;
                components.Add(bullet);

                foreach (var rec in secondcookrecipes)
                {
                    components.Add(new ClearFloatTextComponent(capi, 2));
                    var spacer = new RichTextComponent(capi, "", CairoFont.WhiteSmallText());
                    spacer.PaddingLeft = 2;
                    components.Add(spacer);
                    components.AddRange(VtmlUtil.Richtextify(capi, Lang.Get("meal-recipe-" + rec.Code) + "\n", CairoFont.WhiteSmallText()));
                }
                components.Add(new ClearFloatTextComponent(capi, 10));

            }
        }
    }
}
