using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;

namespace StardewEconMod
{
    /// <summary>Stores the item, quantity, and (approx.) price of a buy/sell transaction.</summary>
    struct TransactionTicket
    {
        /// <summary>Which item was bought/sold, critical for tracking S/D shifts.</summary>
        public Item myItem { get; }

        /// <summary>How many of the item were bought/sold, critical for tracking S/D shifts.</summary>
        public int quant { get; }

        /// <summary>Approx. price of the item; not really that useful except for reporting out.</summary>
        public int price { get; }

        /// <summary>Creates a new record of transaction.</summary>
        /// <param name="i">Item bought/sold.</param>
        /// <param name="q">(Optional) Quantity of item bought/sold. Use only to override deriving quantity from item's 'Stack' property.</param>
        /// <param name="q">(Optional) Price per item of item bought/sold. Use only to override deriving price from item's 'Price' (if SV Object) or 'salePrice()' (if not SV Object) properties.</param>
        public TransactionTicket(Item i, int q = 0, int p = 0)
        {
            myItem = i;

            if (q != 0) quant = q;
            else quant = i.Stack;

            if (p != 0) price = p;
            else price = EconomyManager.getPriceForItemOrObject(i);
        }

        public override string ToString()
        {
            if (quant < 0) return $"Sold {-1*quant} {myItem.DisplayName}(s) for ${price*-1*quant}";
            else return $"Bought {quant} {myItem.DisplayName}(s) for ${price*quant}";
        }
    }
}
