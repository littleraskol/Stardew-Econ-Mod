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

        /// <summary>Controls ultra crunchy diagnostic output.</summary>
        private bool verbose;

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

            myHelper.Events.GameLoop.SaveLoaded += LoadTasks;
            myHelper.Events.Display.MenuChanged += HandleShopMenu;
        }

        /// <summary>Controlled logging.</summary>
        /// <param name="msg">The message to log.</param>
        /// <param name="lvl">The desired log level.</param>
        void LogIt(string msg, LogLevel lvl = LogLevel.Trace)
        {
            if (verbose) Monitor.Log(msg, lvl);
        }

        /// <summary>Does everything necessary for the mod once the save loads.</summary>
        private void LoadTasks(object sender, SaveLoadedEventArgs e)
        {
            myPlayer = Game1.player;
        }

        /// <summary>Attempts to detect player use of a shop to keep track of changes in player money.</summary>
        private void HandleShopMenu(object sender, MenuChangedEventArgs e)
        {
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

                    LogIt($"Detected possible 'object lacks field' exception:{ex.Message}", LogLevel.Warn);

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
                LogIt($"{e.NewMenu} is not a ShopMenu, Player shopping = {isShopping}.");
            }
        }
    }
}