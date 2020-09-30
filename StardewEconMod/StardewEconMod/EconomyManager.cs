using System;
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

        /// <summary>A record of the prices of objects as sold in a particular store.</summary>
        private Dictionary<string, int> storePriceRecord;

        /// <summary>Stores unique keys of saveable data, keys written to json.</summary>
        public KeyList keyList;

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
            storePriceRecord = new Dictionary<string, int>();

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
        /// <param name="playerBuying">(Optional) Whether we are 'simulating' a purchase to get purcahse price, assume not.</param>
        /// <returns>New item price.</returns>
        private int modifyNewItemPrice(Item i, bool playerBuying = false)
        {
            if (i == null) return 0;

            LogIt($"Checking newly added inventory item '{i.DisplayName}' for price modification if possible.");
            if (i is StardewValley.Object)
            {
                int p = (i as StardewValley.Object).Price;
                salePriceChangeRecord.Add(i, p);
                LogIt($"'{i.DisplayName}' is an Object costing ${p}");

                p = (int)(p * getModifierFor(i, playerBuying));
                (i as StardewValley.Object).Price = p;
                LogIt($"'{i.DisplayName}' now costs ${p}");
                return p;
            }

            return 0;
        }

        /// <summary>Determines if an Item is a SV Object or not, and returns its price as appropriate.</summary>
        /// <param name="i">Item to get price for.</param>
        /// <returns>Item price.</returns>
        public static int getPriceForItemOrObject(Item i)
        {
            if (i is StardewValley.Object) return (i as StardewValley.Object).Price;
            else return i.salePrice();
        }

        /// <summary>Prevents divide-by-0 error.</summary>
        /// <param name="i">Integer to proof against DIV0 error.</param>
        /// <returns>If i == 0, 1, otherwise i.</returns>
        public static int preventDIV0(int i)
        {
            if (i == 0) return 1;
            else return i;
        }

        // *** EVENT HANDLING METHODS ***

        /// <summary>Initial configuration and etc.</summary>
        private void StartupTasks(object sender, GameLaunchedEventArgs e)
        {
            myConfig = myHelper.ReadConfig<EconConfig>();

            TryLoadingGMCM();

            List<MarketModel> loadedMarketsData = myHelper.Data.ReadJsonFile<List<MarketModel>>("markets.json");

            if (loadedMarketsData != null)
            {
                Monitor.Log("Got list of market data.");

                foreach (MarketModel mm in loadedMarketsData)
                {
                    Monitor.Log($"Found {mm.Context} market '{mm.DisplayName}'");
                }
            }
            else Monitor.Log($"Unable to load market data.", LogLevel.Error);
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
                ticketsToday.Add(new TransactionTicket(i, -1*i.Stack));
                if (curPrice == 0) curPrice = i.Stack * i.salePrice();
                else curPrice *= i.Stack;
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
                            storePriceRecord.Add(kvp.Key.DisplayName, kvp.Value[0]);
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
                if (storePriceRecord.Count > 0)
                {
                    foreach (KeyValuePair<string, int> kvp in storePriceRecord)
                    {
                        LogIt($"In store price record: Item '{kvp.Key}' with price ${kvp.Value}");
                    }
                    storePriceRecord.Clear();
                }
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

                int moneyChange = myPlayer.Money - lastPlayerMoney;
                LogIt($"Player money changed from {lastPlayerMoney} to {myPlayer.Money} (a difference of {moneyChange}).");

                lastPlayerMoney = myPlayer.Money;

                Item[] addedItems = e.Added as Item[];
                Item[] remedItems = e.Removed as Item[];
                ItemStackSizeChange[] changedStacks = e.QuantityChanged as ItemStackSizeChange[];

                int curPrice;
                int addedItemsCost = 0;

                if (addedItems != null && addedItems.Length > 0)
                {
                    Item saleCopy;
                    foreach (Item i in addedItems)
                    {
                        //For ticketing purposes, we record a "store price" copy of the item.
                        saleCopy = i.getOne();
                        saleCopy.Stack = i.Stack;
                        LogIt($"Item '{i.DisplayName}' is in store price record: {storePriceRecord.ContainsKey(i.DisplayName)}, is SV Object: {i is StardewValley.Object}");
                        if (storePriceRecord.ContainsKey(i.DisplayName) && i is StardewValley.Object)
                        {
                            LogIt($"Found {i.DisplayName} in store price record with price ${storePriceRecord[i.DisplayName]}");
                            (saleCopy as StardewValley.Object).Price = storePriceRecord[i.DisplayName];
                            LogIt($"Sale copy of {saleCopy.DisplayName} now has price ${(saleCopy as StardewValley.Object).Price}");
                        }

                        ticketsToday.Add(new TransactionTicket(saleCopy));
                        LogIt($"Created ticket '{ticketsToday.Last()}'");

                        curPrice = getPriceForItemOrObject(saleCopy);
                        addedItemsCost = curPrice * saleCopy.Stack;

                        LogIt($"Added: {i.DisplayName} (Quantity: {i.Stack}, Category: [{i.Category}] {i.getCategoryName()}, Price: ${curPrice} each, ${addedItemsCost} total)");

                        //This is the item actually added to player inventory, and needs conversion to player price.
                        modifyNewItemPrice(i);

                    }
                }
                
                if (changedStacks != null && changedStacks.Length > 0)
                {
                    int q;
                    int qDiv;
                    int changedStackCost;
                    foreach (ItemStackSizeChange i in changedStacks)
                    {
                        q = i.NewSize - i.OldSize;
                        changedStackCost = Math.Abs(moneyChange) - Math.Abs(addedItemsCost);
                        qDiv = preventDIV0(Math.Abs(q));

                        ticketsToday.Add(new TransactionTicket(i.Item, q, changedStackCost / qDiv));
                        LogIt($"Created ticket '{ticketsToday.Last()}'");

                        LogIt($"Changed: {i.Item.DisplayName} (Quantity: {i.Item.Stack}, Category: [{i.Item.Category}] {i.Item.getCategoryName()}, Price: Approx. ${changedStackCost / qDiv} each, ${changedStackCost} total) from {i.OldSize} to {i.NewSize} (by {q})");
                    }
                }

                if (remedItems != null && remedItems.Length > 0)
                {
                    foreach (Item i in remedItems)
                    {
                        ticketsToday.Add(new TransactionTicket(i, -1*i.Stack));
                        LogIt($"Created ticket '{ticketsToday.Last()}'");

                        curPrice = getPriceForItemOrObject(i);

                        LogIt($"Removed: {i.DisplayName} (Quantity: {i.Stack}, Category: [{i.Category}] {i.getCategoryName()}, Price: Approx. ${curPrice} each, ${curPrice * i.Stack} total).");
                    }
                }
            }
            else
            {
                LogIt($"Inventory change detected. Doing S&D: {doingSupplyDemand}, Shopping: {isShopping}, Money changed: {(myPlayer.Money - lastPlayerMoney) != 0}");
            }
        }
    }
}
