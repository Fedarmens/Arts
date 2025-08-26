using System.Collections.Generic;
using System.IO;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using static Vintagestory.GameContent.BlockLiquidContainerBase;
using Vintagestory.API.Common.Entities;
using Vintagestory.Client.NoObf;

namespace CoreOfArts.Systems
{
    /// <summary>
    /// Creates a recipe for use inside a barrel. Primarily used to craft with liquids. 
    /// </summary>
    /// <example>
    /// <code language="json">
    ///{
    ///  "code": "compost",
    ///  "sealHours": 480,
    ///  "ingredients": [
    ///    {
    ///      "type": "item",
    ///      "code": "rot",
    ///      "litres": 64
    ///    }
    ///  ],
    ///  "output": {
    ///    "type": "item",
    ///    "code": "compost",
    ///    "stackSize": 16
    ///  }
    ///}</code></example>
    [DocumentAsJson]
    public class COALiquidMixingRecipe : IByteSerializable, IRecipeBase<COALiquidMixingRecipe>
    {
        /// <summary>
        /// <!--<jsonoptional>Obsolete</jsonoptional>-->
        /// Unused. Defines an ID for the recipe.
        /// </summary>
        [DocumentAsJson] public int RecipeId;

        /// <summary>
        /// <!--<jsonoptional>Required</jsonoptional>-->
        /// Defines the set of ingredients used inside the barrel. Barrels can have a maximum of one item and one liquid ingredient.
        /// </summary>
        [DocumentAsJson] public BarrelRecipeIngredient[] Ingredients;

        /// <summary>
        /// <!--<jsonoptional>Required</jsonoptional>-->
        /// The final output of this recipe.
        /// </summary>
        [DocumentAsJson] public BarrelOutputStack Output;

        /// <summary>
        /// <!--<jsonoptional>Obsolete</jsonoptional>-->
        /// Unused. Defines a name for the recipe.
        /// </summary>
        [DocumentAsJson] public AssetLocation Name { get; set; }

        /// <summary>
        /// <!--<jsonoptional>Optional</jsonoptional><jsondefault>True</jsondefault>-->
        /// Should this recipe be loaded by the recipe loader?
        /// </summary>
        [DocumentAsJson] public bool Enabled { get; set; } = true;

        /// <summary>
        /// <!--<jsonoptional>Required</jsonoptional>-->
        /// A code for this recipe, used to create an entry in the handbook.
        /// </summary>
        [DocumentAsJson] public string Code;


        IRecipeIngredient[] IRecipeBase<COALiquidMixingRecipe>.Ingredients => Ingredients;
        IRecipeOutput IRecipeBase<COALiquidMixingRecipe>.Output => Output;

        public bool TryCraftNow(ICoreAPI api, ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, COALiquidMixingRecipe recipe)
        {
            BlockLiquidContainerBase baseBlock = itemslot.Itemstack.Block as BlockLiquidContainerBase;
            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            
            ItemStack sourceStack = baseBlock.GetContent(itemslot.Itemstack);
            if (blockSel == null) return false;
            Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
            
            if (recipe != null)
            {
                if (block == null) return false;
                bool sourceIsFirst = sourceStack == recipe.Ingredients[0].ResolvedItemstack;
                ItemStack inputStack = recipe.Ingredients[sourceIsFirst ? 1 : 0].ResolvedItemstack;
                ItemStack outputStack = new ItemStack(byEntity.World.GetItem(new AssetLocation(recipe.Output.Code)), 99999);
                
                float sourceLitres = recipe.Ingredients[sourceIsFirst ? 0 : 1].Litres;
                float inputLitres = recipe.Ingredients[sourceIsFirst ? 1 : 0].Litres;
                float outputLitres = sourceLitres + inputLitres;
                
                if (sourceLitres == 0 || inputLitres == 0 || outputLitres == 0) return false;
                
                if (block is BlockLiquidContainerBase blcto)
                {
                    if (blcto.GetContent(blockSel.Position)?.Id == inputStack?.Id)
                    {
                        float coef = blcto.GetCurrentLitres(blockSel.Position) / inputLitres;
                        if (coef >= 1)
                        { 
                            if (coef != 1)
                            {
                                sourceLitres *= coef;
                                outputLitres = sourceLitres + blcto.GetCurrentLitres(blockSel.Position);
                            }
                            if (baseBlock.GetCurrentLitres(itemslot.Itemstack) < sourceLitres || outputLitres > blcto.CapacityLitres) return false;
                            blcto.TryTakeContent(blockSel.Position, 99999);
                            int moved = blcto.TryPutLiquid(blockSel.Position, outputStack, outputLitres);
                            baseBlock.TryTakeLiquid(itemslot.Itemstack, sourceLitres);
                            blcto.DoLiquidMovedEffects(byPlayer, outputStack, moved, EnumLiquidDirection.Fill);
                            return true;
                        }
                    }
                }
                else
                {
                    var beg = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityGroundStorage;
                    if (beg != null)
                    {
                        ItemSlot crackIntoSlot = beg.GetSlotAt(blockSel);
                        
                        if (crackIntoSlot?.Itemstack?.Block is BlockLiquidContainerBase bowl)
                        {
                            if (bowl.GetContent(crackIntoSlot?.Itemstack)?.Id == inputStack?.Id)
                            {
                                float coef = bowl.GetCurrentLitres(crackIntoSlot?.Itemstack) / inputLitres;
                                if (coef >= 1)
                                {
                                    if (coef != 1)
                                    {
                                        sourceLitres *= coef;
                                        outputLitres = sourceLitres + bowl.GetCurrentLitres(crackIntoSlot?.Itemstack);
                                    }
                                    if (baseBlock.GetCurrentLitres(itemslot.Itemstack) < sourceLitres || outputLitres > bowl.CapacityLitres) return false;
                                    bowl.SetContent(crackIntoSlot?.Itemstack, null);
                                    int moved = bowl.TryPutLiquid(crackIntoSlot?.Itemstack, outputStack, outputLitres);
                                    bowl.DoLiquidMovedEffects(byPlayer, outputStack, moved, EnumLiquidDirection.Fill);
                                    baseBlock.TryTakeLiquid(itemslot.Itemstack, sourceLitres);
                                    beg.MarkDirty(true);
                                    return true;
                                }
                            }

                        }
                    }
                } 
            }

            return false;
        }


        /// <summary>
        /// Serialized the alloy
        /// </summary>
        /// <param name="writer"></param>
        public void ToBytes(BinaryWriter writer)
        {
            writer.Write(Code);
            writer.Write(Ingredients.Length);
            for (int i = 0; i < Ingredients.Length; i++)
            {
                Ingredients[i].ToBytes(writer);
            }

            Output.ToBytes(writer);
        }

        /// <summary>
        /// Deserializes the alloy
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="resolver"></param>
        public void FromBytes(BinaryReader reader, IWorldAccessor resolver)
        {
            Code = reader.ReadString();
            Ingredients = new BarrelRecipeIngredient[reader.ReadInt32()];

            for (int i = 0; i < Ingredients.Length; i++)
            {
                Ingredients[i] = new BarrelRecipeIngredient();
                Ingredients[i].FromBytes(reader, resolver);
                Ingredients[i].Resolve(resolver, "Liquid Mixed Recipe (FromBytes)");
            }

            Output = new BarrelOutputStack();
            Output.FromBytes(reader, resolver.ClassRegistry);
            Output.Resolve(resolver, "Liquid Mixed Recipe (FromBytes)");
        }



        /// <summary>
        /// Resolves Wildcards in the ingredients
        /// </summary>
        /// <param name="world"></param>
        /// <returns></returns>
        public Dictionary<string, string[]> GetNameToCodeMapping(IWorldAccessor world)
        {
            Dictionary<string, string[]> mappings = new Dictionary<string, string[]>();

            if (Ingredients == null || Ingredients.Length == 0) return mappings;

            foreach (var ingred in Ingredients)
            {
                if (!ingred.Code.Path.Contains('*')) continue;

                int wildcardStartLen = ingred.Code.Path.IndexOf('*');
                int wildcardEndLen = ingred.Code.Path.Length - wildcardStartLen - 1;

                List<string> codes = new List<string>();

                if (ingred.Type == EnumItemClass.Block)
                {
                    foreach (Block block in world.Blocks)
                    {
                        if (block.IsMissing) continue;   // BlockList already performs the null check for us, in its enumerator

                        if (WildcardUtil.Match(ingred.Code, block.Code))
                        {
                            string code = block.Code.Path.Substring(wildcardStartLen);
                            string codepart = code.Substring(0, code.Length - wildcardEndLen);
                            if (ingred.AllowedVariants != null && !ingred.AllowedVariants.Contains(codepart)) continue;

                            codes.Add(codepart);

                        }
                    }
                }
                else
                {
                    foreach (Item item in world.Items)
                    {
                        if (item.Code == null || item.IsMissing) continue;

                        if (WildcardUtil.Match(ingred.Code, item.Code))
                        {
                            string code = item.Code.Path.Substring(wildcardStartLen);
                            string codepart = code.Substring(0, code.Length - wildcardEndLen);
                            if (ingred.AllowedVariants != null && !ingred.AllowedVariants.Contains(codepart)) continue;

                            codes.Add(codepart);
                        }
                    }
                }

                mappings[ingred.Name ?? "wildcard"+mappings.Count] = codes.ToArray();
            }

            return mappings;
        }



        public bool Resolve(IWorldAccessor world, string sourceForErrorLogging)
        {
            bool ok = true;

            for (int i = 0; i < Ingredients.Length; i++)
            {
                var ingred = Ingredients[i];
                bool iOk = ingred.Resolve(world, sourceForErrorLogging);
                ok &= iOk;

                if (iOk)
                {
                    var lprops = BlockLiquidContainerBase.GetContainableProps(ingred.ResolvedItemstack);
                    if (lprops != null)
                    {
                        if (ingred.Litres < 0)
                        {
                            if (ingred.Quantity > 0)
                            {
                                world.Logger.Warning("Liquid Mixed recipe {0}, ingredient {1} does not define a litres attribute but a quantity, will assume quantity=litres for backwards compatibility.", sourceForErrorLogging, ingred.Code);
                                ingred.Litres = ingred.Quantity;
                                ingred.ConsumeLitres = ingred.ConsumeQuantity;
                            } else ingred.Litres = 1;
                            
                        }

                        ingred.Quantity = (int)(lprops.ItemsPerLitre * ingred.Litres);
                        if (ingred.ConsumeLitres != null)
                        {
                            ingred.ConsumeQuantity = (int)(lprops.ItemsPerLitre * ingred.ConsumeLitres);
                        }
                    }
                }
            }

            ok &= Output.Resolve(world, sourceForErrorLogging);

            if (ok)
            {
                var lprops = BlockLiquidContainerBase.GetContainableProps(Output.ResolvedItemstack);
                if (lprops != null)
                {
                    if (Output.Litres < 0)
                    {
                        if (Output.Quantity > 0)
                        {
                            world.Logger.Warning("Liquid Mixed recipe {0}, output {1} does not define a litres attribute but a stacksize, will assume stacksize=litres for backwards compatibility.", sourceForErrorLogging, Output.Code);
                            Output.Litres = Output.Quantity;
                        }
                        else Output.Litres = 1;

                    }

                    Output.Quantity = (int)(lprops.ItemsPerLitre * Output.Litres);
                }
            }

            return ok;
        }


        public COALiquidMixingRecipe Clone()
        {
            BarrelRecipeIngredient[] ingredients = new BarrelRecipeIngredient[Ingredients.Length];
            for (int i = 0; i < Ingredients.Length; i++)
            {
                ingredients[i] = Ingredients[i].Clone();
            }

            return new COALiquidMixingRecipe()
            {
                Output = Output.Clone(),
                Code = Code,
                Enabled = Enabled,
                Name = Name,
                RecipeId = RecipeId,
                Ingredients = ingredients
            };
        }

    }
    
}
