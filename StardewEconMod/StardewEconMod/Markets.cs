using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StardewEconMod
{
    /// <summary>
    /// This is a data model that captures the CSV-to-JSON description of the different markets, for conversion into actual Market objects.
    /// </summary>
    public class MarketModel
    {
        /// <summary> A unique identifier and indexing string. </summary>
        public string InternalToken { get; set; }

        /// <summary> Contains item category codes of items governed by the market, delimited by | characters, to be split later. </summary>
        public string CategoryCodeString { get; set; }

        /// <summary> Contains names of items governed by the market, delimited by | characters, to be split later. </summary>
        public string ItemNameString { get; set; }

        /// <summary> On what "level" (global, national, or local) the market operates. </summary>
        public string Context { get; set; }

        /// <summary> How the market's name should appear to the player. </summary>
        public string DisplayName { get; set; }

        /// <summary> The term for the kind of item traded in this market. </summary>
        public string CommodityDesc { get; set; }

        /// <summary> List of unique identifiers for markets that provide inputs to this market, delimited by | characters, to be split later. </summary>
        public string ConsumesString { get; set; }

        /// <summary> List of unique identifiers of markets on the level "below" this market deal in the same goods, delimited by | characters, to be split later. </summary>
        public string VerticalIntegration { get; set; }

        /// <summary> The likelihood of this market having a random flux event. </summary>
        public int RndWght { get; set; }

        /// <summary> The operative tags describing what this market can do, delimited by | characters, to be split later. </summary>
        public string TagsString { get; set; }
    }

    /// <summary>
    /// Provides the basic framework for a market.
    /// </summary>
    public abstract class AbstractMarket
    {
        /// <summary> A unique identifier and indexing string. </summary>
        public string InternalToken { get; set; }

        /// <summary> Contains item category codes of items governed by the market. </summary>
        public List<int> CategoryCodes { get; set; }

        /// <summary> Contains names of items governed by the market. </summary>
        public List<string> ItemNames { get; set; }

        /// <summary> On what "level" (global, national, or local) the market operates. </summary>
        public string Context { get; set; }

        /// <summary> The term for a market at its level (index, market, or vendor). </summary>
        public string Type { get; set; }

        /// <summary> How the market's name should appear to the player. </summary>
        public string DisplayName { get; set; }

        /// <summary> The term for the kind of item traded in this market. </summary>
        public string CommodityDesc { get; set; }

        /// <summary> List of unique identifiers for markets that provide inputs to this market. </summary>
        public List<string> MarketsConsumed { get; set; }

        /// <summary> List of unique identifiers of markets on the level "below" this market deal in the same goods. </summary>
        public List<string> VerticallyIntegratedMarkets { get; set; }

        /// <summary> The likelihood of this market having a random flux event. </summary>
        public int RandomWeight { get; set; }

        /// <summary> The operative tags describing what this market can do. </summary>
        public List<string> TagsString { get; set; }
    }
}
