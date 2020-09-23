using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StardewEconMod
{
    class EconConfig
    {
        /// <summary>Whether to do the player commerce based supply and demand stuff.</summary>
        public bool DoSupplyAndDemand { get; set; } = true;

        /// <summary>Whether to do random market forces changing prices.</summary>
        public bool DoMarketFluxuations { get; set; } = true;

        /// <summary>Controls ultra crunchy diagnostic output.</summary>
        public bool VerboseMode { get; set; } = false;
    }
}
