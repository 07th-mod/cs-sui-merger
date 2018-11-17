using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuiMerger
{
    class DebugUtils
    {
        static bool debug;

        /// <summary>
        /// Allow toggling printing depending on the above 'debug' variable
        /// </summary>
        /// <param name="s"></param>
        public static void Print(string s)
        {
            if (debug)
            {
                Console.WriteLine(s);
            }
        }
    }
}
