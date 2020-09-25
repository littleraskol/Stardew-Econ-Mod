using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StardewEconMod
{
    class KeyList
    {
        /// <summary>Persistent data is to be stored in the save file. Each such saved data structure creates its own unique key, which it adds to this list. This can then be written to and read from a json file.</summary>
        public List<string> keys { get; }

        /// <summary>There will only ever be one key list with one consistent file name.</summary>
        /// <returns>The file name "keylist.json"</returns>
        public static string getFileName()
        {
            return "keylist.json";
        }

        /// <summary>Controls adding keys.</summary>
        public void addNewKey(string key)
        {
            keys.Add(key);
        }
    }
}
