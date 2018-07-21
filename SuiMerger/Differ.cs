using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuiMerger
{
    public class Differ
    {
        //keep only japanese characters
        //remove spaces
        //is_japanese_punct = in_range(code_point, 0x3000, 0x303f) NO
        //is_hiragana = in_range(code_point, 0x3040, 0x309f) YES
        //is_katakana = in_range(code_point, 0x30a0, 0x30ff) YES
        //is_ideograph = in_range(code_point, 0x4e00, 0x9faf) YES
        //this function should also remove newlines etc.
        public static string PrepareStringForDiff(string s)
        {
            StringBuilder sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                int c_codepoint = (int)c; //NOTE: doesn't work for >16 bit codepoints (> 0xFFFF)
                if (c_codepoint >= 0x3040 && c_codepoint <= 0x9faf)
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }
    }
}
