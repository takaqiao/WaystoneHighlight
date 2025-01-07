﻿using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;

using System.Linq;

using System.Numerics;

using System.Drawing;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;


namespace WaystoneHighlight;

public class WaystoneHighlight : BaseSettingsPlugin<WaystoneHighlightSettings>
{
    private IngameState InGameState => GameController.IngameState;
    private List<string> BannedModifiers;

    private void ParseBannedModifiers()
    {
        BannedModifiers = Settings.Score.BannedModifiers.Value
            .Split(',')
            .Select(x => x.Trim().ToLower())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }

    public override bool Initialise()
    {
        //BannedModifiers = ParseBannedModifiers();
        Settings.Score.ReloadBannedModifiers.OnPressed = ParseBannedModifiers;
        ParseBannedModifiers();
        return base.Initialise();
    }
    public override void Render()
    {
        IList<WaystoneItem> waystones = [];


        var stashPanel = InGameState.IngameUi.StashElement;
        var stashPanelGuild = InGameState.IngameUi.GuildStashElement;
        var inventoryPanel = InGameState.IngameUi.InventoryPanel;

        bool isQuadTab = false;


        // Run if inventory panel is opened
        if (inventoryPanel.IsVisible)
        {
            // Add stash items
            if (stashPanel.IsVisible && stashPanel.VisibleStash != null)
            {
                if (stashPanel.VisibleStash.TotalBoxesInInventoryRow == 24)
                {
                    isQuadTab = true;
                }
                foreach (var item in stashPanel.VisibleStash.VisibleInventoryItems)
                {
                    waystones.Add(new WaystoneItem(item.Item.GetComponent<Base>(), item.Item.GetComponent<Map>(), item.Item.GetComponent<Mods>(), item.GetClientRectCache, ItemLocation.Stash));
                }
            }
            else if (stashPanelGuild.IsVisible && stashPanelGuild != null)
            {
                if (stashPanelGuild.VisibleStash.TotalBoxesInInventoryRow == 24)
                {
                    isQuadTab = true;
                }
                foreach (var item in stashPanelGuild.VisibleStash.VisibleInventoryItems)
                {
                    waystones.Add(new WaystoneItem(item.Item.GetComponent<Base>(), item.Item.GetComponent<Map>(), item.Item.GetComponent<Mods>(), item.GetClientRectCache, ItemLocation.Stash));
                }
            } 
            // Add inventory items
            var inventoryItems = GameController.IngameState.ServerData.PlayerInventories[0].Inventory.InventorySlotItems;
            foreach (var item in inventoryItems)
            {
                waystones.Add(new(item.Item.GetComponent<Base>(), item.Item.GetComponent<Map>(), item.Item.GetComponent<Mods>(), item.GetClientRect(), ItemLocation.Inventory));
            }

            foreach (var waystone in waystones)
            {

                var item = waystone.map;

                if (item == null)
                    continue;

                // Check for map tier
                if (item.Tier < Settings.Score.MinimumTier)
                {
                    continue;
                }

                var itemMods = waystone.mods;
                var bbox = waystone.rect;

                int prefixCount = 0;
                int suffixCount = 0;

                int score = 0;

                int iiq = 0;
                int iir = 0;
                bool extraRareMod = false;
                int packSize = 0;
                int magicPackSize = 0;
                int extraPacks = 0;
                int extraMagicPack = 0;
                int extraRarePack = 0;
                int additionalPacks = 0;

                var drawColor = Color.White;
                bool hasBannedMod = false;
                bool isCorrupted = waystone.baseComponent.isCorrupted;

                // Iterate through the mods
                foreach (var mod in itemMods.ItemMods)
                {
                    // Check for banned modifiers
                    if (BannedModifiers.Count > 0)
                    {
                        foreach (var bannedMod in BannedModifiers)
                        {

                            if (mod.DisplayName.Contains(bannedMod, StringComparison.OrdinalIgnoreCase))
                            {
                                hasBannedMod = true;
                                break;
                            }
                        }
                    }

                    // Count prefixes and suffixes
                    if (mod.DisplayName.StartsWith("of", StringComparison.OrdinalIgnoreCase))
                    {
                        suffixCount++;
                    }
                    else
                    {
                        if (mod.Group != "AfflictionMapDeliriumStacks")
                        {
                            prefixCount++;
                        }
                    }

                    // Find good mods
                    switch (mod.Name)
                    {
                        case "MapDroppedItemRarityIncrease":
                            iir += mod.Values[0];
                            break;
                        case "MapDroppedItemQuantityIncrease":
                            iiq += mod.Values[0];
                            if (mod.Values.Count != 1)
                            {
                                iir += mod.Values[1];
                            }
                            break;
                        case "MapRareMonstersAdditionalModifier":
                            extraRareMod = true;
                            break;
                        case "MapPackSizeIncrease":
                            packSize += mod.Values[0];
                            break;
                        case "MapMagicPackSizeIncrease":
                            magicPackSize += mod.Values[0];
                            break;
                        case "MapTotalEffectivenessIncrease":
                            extraPacks += mod.Values[0];
                            break;
                        case "MapMagicPackIncrease":
                            extraMagicPack += mod.Values[0];
                            break;
                        case "MapMagicRarePackIncrease":
                            extraRarePack += mod.Values[0];
                            if (mod.Values.Count != 1)
                            {
                                extraMagicPack += mod.Values[1];
                            }
                            break;
                        case "MapRarePackIncrease":
                            extraRarePack += mod.Values[0];
                            break;
                        case string s when s.StartsWith("MapMonsterAdditionalPacks"):
                            additionalPacks += mod.Values[0];
                            break;
                    }
                }

                // Sum the score
                score += iiq * Settings.Score.ScorePerQuantity;
                score += iir * Settings.Score.ScorePerRarity;
                score += packSize * Settings.Score.ScorePerPackSize;
                score += magicPackSize * Settings.Score.ScorePerMagicPackSize;
                score += extraPacks * Settings.Score.ScorePerExtraPacksPercent;
                score += extraMagicPack * Settings.Score.ScorePerExtraMagicPack;
                score += extraRarePack * Settings.Score.ScorePerExtraRarePack;
                score += additionalPacks * Settings.Score.ScorePerAdditionalPack;
                if (extraRareMod)
                {
                    score += Settings.Score.ScoreForExtraRareMonsterModifier;
                }


                // Drawing

                // Frame
                if (hasBannedMod)
                {
                    Graphics.DrawFrame(bbox, Settings.Graphics.BannedBorderColor, Settings.Graphics.BannedBorderThickness.Value);
                }
                else
                {
                    if (score >= Settings.Score.MinimumCraftHighlightScore)
                    {
                        if (prefixCount < 3 && !isCorrupted)
                        {
                            Graphics.DrawFrame(bbox, Settings.Graphics.CraftBorderColor, Settings.Graphics.CraftBorderThickness.Value);

                        }
                        else if (score >= Settings.Score.MinimumRunHighlightScore)
                        {
                            Graphics.DrawFrame(bbox, Settings.Graphics.RunBorderColor, Settings.Graphics.RunBorderThickness.Value);
                        }
                    }
                }

                if (waystone.location == ItemLocation.Inventory || (waystone.location == ItemLocation.Stash && !isQuadTab))
                {

                    // Stats
                    // SetTextScale doesn't scale well we need to change origin point or add x:y placement modifications depending on scale
                    using (Graphics.SetTextScale(Settings.Graphics.QRFontSizeMultiplier))
                    {
                        Graphics.DrawText(iir.ToString(), new Vector2(bbox.Left + 5, bbox.Top), ExileCore2.Shared.Enums.FontAlign.Left);
                        Graphics.DrawText(iiq.ToString(), new Vector2(bbox.Left + 5, bbox.Top + 2 + (10 * Settings.Graphics.QRFontSizeMultiplier)), ExileCore2.Shared.Enums.FontAlign.Left);
                        if (extraRareMod)
                        {
                            Graphics.DrawText("+1", new Vector2(bbox.Left + 5, bbox.Top + 4 + (20 * Settings.Graphics.QRFontSizeMultiplier)), ExileCore2.Shared.Enums.FontAlign.Left);
                        }
                    }

                    // Affixes count
                    // SetTextScale doesn't scale well we need to change origin point or add x:y placement modifications depending on scale
                    using (Graphics.SetTextScale(Settings.Graphics.PrefSuffFontSizeMultiplier))
                    {
                        Graphics.DrawText(prefixCount.ToString(), new Vector2(bbox.Right - 5, bbox.Top), ExileCore2.Shared.Enums.FontAlign.Right);
                        Graphics.DrawText(suffixCount.ToString(), new Vector2(bbox.Right - 5, bbox.Top + 2 + (10 * Settings.Graphics.PrefSuffFontSizeMultiplier)), ExileCore2.Shared.Enums.FontAlign.Right);
                    }

                    // Score
                    // SetTextScale doesn't scale well we need to change origin point or add x:y placement modifications depending on scale
                    using (Graphics.SetTextScale(Settings.Graphics.ScoreFontSizeMultiplier))
                    {
                        Graphics.DrawText(score.ToString(), new Vector2(bbox.Left + 5, bbox.Bottom - 5 - (15 * Settings.Graphics.ScoreFontSizeMultiplier)), ExileCore2.Shared.Enums.FontAlign.Left);
                    }
                }

            }
        }
    }


}
