﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;

namespace StardewEconMod
{
    public class EconomyManager : Mod
    {
        /// <summary>Stable helper reference.</summary>
        private IModHelper myHelper;

        /// <summary>Stable player reference.</summary>
        private Farmer myPlayer;

        /// <summary>Stable configuration reference.</summary>
        private EconConfig myConfig;

        /// <summary>Controls ultra crunchy diagnostic output.</summary>
        private bool verbose;

        /// <summary>Whether to do the player commerce based supply and demand stuff.</summary>
        private bool doingSupplyDemand;

        /// <summary>Whether to do random market forces changing prices.</summary>
        private bool doingMarketFlux;

        /// <summary>Control flag used to tell if the player is in a shop menu.</summary>
        private bool isShopping;

        /// <summary>List of non-shop "shops"</summary>
        private string[] nonShopShops;

        /// <summary>Stores the last known value of the player's money.</summary>
        private int lastPlayerMoney;

        /// <summary>Daily register of all sales made, to be processed at EOD.</summary>
        private List<TransactionTicket> ticketsToday;

        /// <summary>A record of the original prices of objects in the player's inventory.</summary>
        private Dictionary<Item, int> salePriceChangeRecord;

        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            myHelper = helper;
            verbose = false;

            isShopping = false;
            nonShopShops = new string[3] { "Furniture Catalogue", "Catalogue", "Dresser" };
            ticketsToday = new List<TransactionTicket>();
            salePriceChangeRecord = new Dictionary<Item, int>();

            myHelper.Events.GameLoop.GameLaunched += StartupTasks;
            myHelper.Events.GameLoop.SaveLoaded += LoadTasks;
            myHelper.Events.GameLoop.DayStarted += DailyStartTasks;
            myHelper.Events.GameLoop.DayEnding += HandleShippingBin;
            myHelper.Events.Display.MenuChanged += HandleShopMenu;
            myHelper.Events.Player.InventoryChanged += CheckForSaleOrPurchase;

            Monitor.Log("Stardew Economy Mod => Initialized", LogLevel.Info);
        }

        // *** INTERNAL METHODS ***

        /// <summary>Controlled logging.</summary>
        /// <param name="msg">The message to log.</param>
        /// <param name="lvl">The desired log level.</param>
        void LogIt(string msg, LogLevel lvl = LogLevel.Trace)
        {
            if (verbose) Monitor.Log(msg, lvl);
        }

        /// <summary>Reloads values from config file.</summary>
        void RefreshConfig()
        {
            verbose = myConfig.VerboseMode;
            doingSupplyDemand = myConfig.DoSupplyAndDemand;
            doingMarketFlux = myConfig.DoMarketFluxuations;
        }

        /// <summary>Sets up the in-game mod config menu.</summary>
        private void TryLoadingGMCM()
        {
            //See if we can find GMCM, quit if not.
            var api = Helper.ModRegistry.GetApi<GenericModConfigMenu.GenericModConfigMenuAPI>("spacechase0.GenericModConfigMenu");

            if (api == null)
            {
                Monitor.Log("Unable to load GMCM API.", LogLevel.Info);
                return;
            }

            api.RegisterModConfig(ModManifest, () => myConfig = new EconConfig(), () => Helper.WriteConfig(myConfig));
            api.RegisterSimpleOption(ModManifest, "Supply and Demand", "Whether to monitor and adapt to the player's impact on supply and demand via sales and purchases.", () => myConfig.DoSupplyAndDemand, (bool val) => myConfig.DoSupplyAndDemand = val);
            api.RegisterSimpleOption(ModManifest, "Verbose Mode", "This turns on finely detailed debug messages. Don't set if you don't need it (you probably do not need it).", () => myConfig.VerboseMode, (bool val) => myConfig.VerboseMode = val);
        }

        /// <summary>Applies the modifier for a given item.</summary>
        /// <param name="keyItem">The item to get the modifier for.</param>
        /// <param name="playerBuying">(Optional) Logic involved is different for buying vs. selling, we assume buying.</param>
        private double getModifierFor(Item keyItem, bool playerBuying = true)
        {
            LogIt($"TBD: Actually calculate modifier for {keyItem.Name}");
            if (playerBuying) return 1.5;
            else return 0.5;
            //return 1.0;
        }

        /// <summary>Modifies player inventory prices as needed.</summary>
        private void modifyPlayerInventoryPrices()
        {
            int p;
            foreach (Item i in myPlayer.Items)
            {
                if (i == null) continue;

                LogIt($"Checking player inventory item '{i.DisplayName}' for price modification if possible.");
                if (i is StardewValley.Object)
                {
                    p = (i as StardewValley.Object).Price;
                    salePriceChangeRecord.Add(i, p);
                    LogIt($"'{i.DisplayName}' is an Object costing ${p}");

                    (i as StardewValley.Object).Price = (int)(p * getModifierFor(i, false));
                    LogIt($"'{i.DisplayName}' now costs ${(i as StardewValley.Object).Price}");
                }
            }
        }

        /// <summary>Resets prices of items in player inventory to original values.</summary>
        private void resetPlayerInventoryPrices()
        {
            foreach (Item i in myPlayer.Items)
            {
                if (i == null) continue;

                LogIt($"Checking player inventory item '{i}' for price reset if possible.");
                if (i is StardewValley.Object && salePriceChangeRecord.ContainsKey(i))
                {
                    LogIt($"'{i.DisplayName}' is in the record of changed prices, currently costs ${(i as StardewValley.Object).Price}, and has a stored original price of ${salePriceChangeRecord[i]}.");
                    (i as StardewValley.Object).Price = salePriceChangeRecord[i];
                    LogIt($"'{i.DisplayName}' now costs ${(i as StardewValley.Object).Price}");
                }
            }
            salePriceChangeRecord.Clear();
        }

        /// <summary>Modifies price of single item as added.</summary>
        /// <param name="i">Item to modify.</param>
        /// <returns>New item price.</returns>
        private int modifyNewItemPrice(Item i)
        {
            if (i == null) return 0;

            LogIt($"Checking newly added inventory item '{i.DisplayName}' for price modification if possible.");
            if (i is StardewValley.Object)
            {
                int p = (i as StardewValley.Object).Price;
                salePriceChangeRecord.Add(i, p);
                LogIt($"'{i.DisplayName}' is an Object costing ${p}");

                p = (int)(p * getModifierFor(i, false));
                (i as StardewValley.Object).Price = p;
                LogIt($"'{i.DisplayName}' now costs ${p}");
                return p;
            }

            return 0;
        }

        // *** EVENT HANDLING METHODS ***

        /// <summary>Initial configuration and etc.</summary>
        private void StartupTasks(object sender, GameLaunchedEventArgs e)
        {
            myConfig = myHelper.ReadConfig<EconConfig>();

            TryLoadingGMCM();
        }

        /// <summary>Does everything necessary for the mod once the save loads.</summary>
        private void LoadTasks(object sender, SaveLoadedEventArgs e)
        {
            myPlayer = Game1.player;
            lastPlayerMoney = myPlayer.Money;

            RefreshConfig();
        }

        /// <summary>Every day, handle the previous day's transactions.</summary>
        private void DailyStartTasks(object sender, DayStartedEventArgs e)
        {
            foreach (TransactionTicket t in ticketsToday)
            {
                LogIt($"TBD: Handle new transaction '{t}'");
            }

            //Clear transactions for new day.
            ticketsToday.Clear();

            //Just in case
            salePriceChangeRecord.Clear();
        }

        /// <summary>Creates transaction tickets for every item in shipping bin.</summary>
        private void HandleShippingBin(object sender, DayEndingEventArgs e)
        {
            int totalShipmentsValue = 0;
            int curPrice;
            foreach (Item i in Game1.getFarm().getShippingBin(Game1.player))
            {
                curPrice = modifyNewItemPrice(i);
                ticketsToday.Add(new TransactionTicket(i, -1*i.Stack, curPrice));
                curPrice *= i.Stack;
                totalShipmentsValue += curPrice;

                LogIt($"Shipping: {i.DisplayName} (Quantity: {i.Stack}, Category: [{i.Category}] {i.getCategoryName()}, Price: ${curPrice/i.Stack} each, ${curPrice} total)");
            }
            LogIt($"Total value of shipped items should be: ${totalShipmentsValue}");
        }

        /// <summary>Attempts to detect player use of a shop to keep track of changes in player money and apply modified prices.</summary>
        private void HandleShopMenu(object sender, MenuChangedEventArgs e)
        {
            LogIt($"Entered/Exited Menu: '{e.NewMenu}', Previous Menu: '{e.OldMenu}'");

            if (e.NewMenu != null)
            {
                //NOTE: Not really a tragedy if we do stuff here in something other than a shop, just a waste of time down the line.

                //Store the menu as a shop so we can act on it as needed.
                ShopMenu shop = e.NewMenu as ShopMenu;

                //If this cannot be treated as a shop (not a shop?) return.
                if (shop == null)
                {
                    isShopping = false;

                    LogIt($"Couldn't cast '{e.NewMenu}' as ShopMenu, Player shopping = {isShopping}.");

                    return;
                }

                try
                {
                    //Bail out if this is a dresser or something.
                    if (myHelper.Reflection.GetField<bool>(shop, "_isStorageShop").GetValue()) return;

                    //Final sanity check
                    if (nonShopShops.Contains(shop.storeContext))
                    {
                        isShopping = false;

                        LogIt($"Shop's store context '{shop.storeContext}' was invalid, Player shopping = {isShopping}.");

                        return;
                    }

                    isShopping = true;
                    lastPlayerMoney = myPlayer.Money;
                    LogIt($"Result of menu check: Player shopping = {isShopping}, Player money recorded = {lastPlayerMoney}");

                    //Need to change player inventory prices.
                    modifyPlayerInventoryPrices();

                    //The first number of the int[] is the price.
                    Dictionary<ISalable, int[]> inventory = myHelper.Reflection.GetField<Dictionary<ISalable, int[]>>(shop, "itemPriceAndStock").GetValue();

                    // Change inventory prices
                    foreach (KeyValuePair<ISalable, int[]> kvp in inventory)
                    {
                        LogIt($"Checking shop inventory item '{kvp.Key.DisplayName}' for price modification, currently costs {kvp.Value[0]}");
                        if (kvp.Key is Item)
                        {
                            kvp.Value.SetValue((int)(kvp.Value[0] * getModifierFor(kvp.Key as Item)), 0);
                            LogIt($"'{kvp.Key.DisplayName}' now costs {kvp.Value[0]}");
                        }
                    }
                }
                //Thrown when the object lacks the field we asked reflection for.
                //This shouldn't happen because we should only reach this point if dealing with a ShopMenu, but...
                catch (InvalidOperationException ex)
                {
                    isShopping = false;

                    LogIt($"Detected possible 'object lacks field' exception: {ex.Message}", LogLevel.Warn);

                    return;
                }
            }
            else
            {
                isShopping = false;    //Cannot possibly be shopping if there's no menu (i.e., the menu closed)
                LogIt($"No menu loaded, Player shopping = {isShopping}.");

                if (salePriceChangeRecord.Count > 0) resetPlayerInventoryPrices();
            }
        }

        /// <summary>Attempts to detect whether a change in player inventory means a sale has occurred, and handle it.</summary>
        private void CheckForSaleOrPurchase(object sender, InventoryChangedEventArgs e)
        {
            /*Everything here requires:
             * 1. that the supply/demand system be on, and
             * 2. that the player be shopping, and
             * 3. that the player gained or lost money.
             */
            if (doingSupplyDemand && isShopping && myPlayer.Money != lastPlayerMoney)
            {
                LogIt("Inventory change detected while shopping; money changed indicates a sale or purchase.");

                Item[] addedItems = e.Added as Item[];
                Item[] remedItems = e.Removed as Item[];
                ItemStackSizeChange[] changedStacks = e.QuantityChanged as ItemStackSizeChange[];

                if (addedItems != null && addedItems.Length > 0)
                {
                    int curPrice;
                    foreach (Item i in addedItems)
                    {
                        curPrice = modifyNewItemPrice(i);
                        ticketsToday.Add(new TransactionTicket(i, i.Stack, curPrice));

                        LogIt($"Added: {i.DisplayName} (Quantity: {i.Stack}, Category: [{i.Category}] {i.getCategoryName()}, Price: ${curPrice} each, ${curPrice*i.Stack} total)");
                    }
                }
                
                if (changedStacks != null && changedStacks.Length > 0)
                {
                    foreach (ItemStackSizeChange i in changedStacks)
                    {
                        ticketsToday.Add(new TransactionTicket(i.Item, i.NewSize - i.OldSize, i.Item.salePrice()));

                        LogIt($"Changed: {i.Item.DisplayName} (Quantity: {i.Item.Stack}, Category: [{i.Item.Category}] {i.Item.getCategoryName()}, Price: Approx. ${i.Item.salePrice()} each, ${i.Item.salePrice()*i.Item.Stack} total) from {i.OldSize} to {i.NewSize} (by {i.NewSize - i.OldSize})");
                    }
                }

                if (remedItems != null && remedItems.Length > 0)
                {
                    foreach (Item i in remedItems)
                    {
                        ticketsToday.Add(new TransactionTicket(i, -1*i.Stack, i.salePrice()));

                        LogIt($"Removed: {i.DisplayName} (Quantity: {i.Stack}, Category: [{i.Category}] {i.getCategoryName()}, Price: Approx. ${i.salePrice()} each, ${i.salePrice()*i.Stack} total).");
                    }
                }

                LogIt($"Player money changed from {lastPlayerMoney} to {myPlayer.Money} (a difference of {myPlayer.Money - lastPlayerMoney}).");

                lastPlayerMoney = myPlayer.Money;
            }
            else
            {
                LogIt($"Inventory change detected. Doing S&D: {doingSupplyDemand}, Shopping: {isShopping}, Money changed: {(myPlayer.Money - lastPlayerMoney) != 0}");
            }
        }
    }
}
