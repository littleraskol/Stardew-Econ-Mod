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

        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            myHelper = helper;
            verbose = false;

            isShopping = false;
            nonShopShops = new string[3] { "Furniture Catalogue", "Catalogue", "Dresser" };

            myHelper.Events.GameLoop.GameLaunched += StartupTasks;
            myHelper.Events.GameLoop.SaveLoaded += LoadTasks;
            myHelper.Events.Display.MenuChanged += HandleShopMenu;

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
            api.RegisterSimpleOption(ModManifest, "Verbose Mode", "This turns on finely detailed debug messages. Don't set if you don't need it (you probably do not need it).", () => myConfig.VerboseMode, (bool val) => myConfig.VerboseMode = val);
        }

        // *** EVENT HANDLING METHODS ***

        /// <summary>Initial configuration and etc.</summary>
        private void StartupTasks(object sender, GameLaunchedEventArgs e)
        {
            myConfig = myHelper.ReadConfig<EconConfig>();

            RefreshConfig();

            TryLoadingGMCM();
        }

        /// <summary>Does everything necessary for the mod once the save loads.</summary>
        private void LoadTasks(object sender, SaveLoadedEventArgs e)
        {
            myPlayer = Game1.player;

            RefreshConfig();
        }

        /// <summary>Attempts to detect player use of a shop to keep track of changes in player money.</summary>
        private void HandleShopMenu(object sender, MenuChangedEventArgs e)
        {
            LogIt($"Entered/Exited Menu: {e.NewMenu}, Previous Menu: {e.OldMenu}");

            if (e.NewMenu != null)
            {
                //NOTE: Not really a tragedy if we do stuff here in something other than a shop, just a waste of time down the line.

                //Store the menu as a shop so we can act on it as needed.                
                ShopMenu shop = e.NewMenu as ShopMenu;

                //If this cannot be treated as a shop (not a shop?) return
                if (shop == null)
                {
                    isShopping = false;

                    LogIt($"Couldn't cast {e.NewMenu} as ShopMenu, Player shopping = {isShopping}.");

                    return;
                }

                //Bail out if this is a dresser or something.
                try
                {
                    if (myHelper.Reflection.GetField<bool>(shop, "_isStorageShop").GetValue()) return;
                }
                //This gets thrown when the object lacks the field we asked for.
                //This shouldn't happen because we should only reach this point if dealing with a ShopMenu, but...
                catch (InvalidOperationException ex)
                {
                    isShopping = false;

                    LogIt($"Detected possible 'object lacks field' exception: {ex.Message}", LogLevel.Warn);

                    return;
                }

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
            }
            else
            {
                isShopping = false;    //Cannot possibly be shopping if there's no menu (i.e., the menu closed)
                LogIt($"No menu loaded, Player shopping = {isShopping}.");
            }
        }
    }
}
