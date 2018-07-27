using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuiMerger
{
    public class StringUtils
    {
        public static bool CharIsJapanese(char c)
        {
            int c_codepoint = (int)c; //NOTE: doesn't work for >16 bit codepoints (> 0xFFFF)
            return c_codepoint >= 0x3040 && c_codepoint <= 0x9faf;
        }

        public static bool CharIsASCII(char c)
        {
            int c_codepoint = (int)c; //NOTE: doesn't work for >16 bit codepoints (> 0xFFFF)
            return c_codepoint <= 0xFF;
        }

        public static void WriteStringList(StreamWriter sw, IEnumerable<string> strings)
        {
            foreach (string s in strings)
            {
                sw.WriteLine(s);
            }
        }

        public static void WriteStringListRegion(StreamWriter sw, IEnumerable<string> strings, int startIndex, int nonInclusiveLastIndex)
        {
            int i = 0;
            foreach (string s in strings)
            {
                if (i >= startIndex && i < nonInclusiveLastIndex)
                {
                    sw.WriteLine(s);
                }
                i++;
            }
        }

    }
}
