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
    /// <summary>Stores the item and quantity of a buy/sell transaction.</summary>
    struct TransactionTicket
    {
        public Item myItem { get; }
        public int quant { get; }

        public TransactionTicket(Item i, int q)
        {
            myItem = i;
            quant = q;
        }

        public override string ToString()
        {
            if (quant < 0) return $"Sold {-1*quant} {myItem.DisplayName}(s) for ${(myItem.salePrice()*-1*quant)/2}";
            else return $"Bought {quant} {myItem.DisplayName}(s) for ${myItem.salePrice()*quant}";
        }
    }
}
