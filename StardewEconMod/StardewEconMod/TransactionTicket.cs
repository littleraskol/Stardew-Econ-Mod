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
        public Item myItem { get; }
        public int quant { get; }
        public int price { get; }

        public TransactionTicket(Item i, int q, int p)
        {
            myItem = i;
            quant = q;
            price = p;
        }

        public override string ToString()
        {
            if (quant < 0) return $"Sold {-1*quant} {myItem.DisplayName}(s) for ${price*-1*quant}";
            else return $"Bought {quant} {myItem.DisplayName}(s) for ${price*quant}";
        }
    }
}
