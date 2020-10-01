using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StardewEconMod
{
    /// <summary>
    /// Provides the basic framework for an economic system at the global, national, or local level. 
    /// </summary>
    public abstract class EconomicSystem
    {
        /// <summary> A unique identifier and indexing string. </summary>
        public string InternalToken { get; set; }

        /// <summary> How the system's name should appear to the player. </summary>
        public string DisplayName { get; set; }

        /// <summary> Index of markets active on this level. </summary>
        public Dictionary<string, Market> AssociatedMarkets { get; set; }
    }
}
