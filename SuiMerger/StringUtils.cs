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
        //see https://www.unicode.org/charts/PDF/UFF00.pdf for full/half width ranges
        //see http://www.unicode.org/charts/PDF/U2000.pdf for punctuation ranges
        public static bool CharIsJapaneseCharacterOrPunctuationOrFullHalfWidth(char c)
        {
            int c_codepoint = c; //NOTE: doesn't work for >16 bit codepoints (> 0xFFFF)
            bool is_japanese_character_or_punctuation = c_codepoint >= 0x3000 && c_codepoint <= 0x9faf;
            bool is_full_or_half_width = c_codepoint >= 0xFF01 && c_codepoint <= 0xFFEE;
            bool is_general_punctuation = c_codepoint >= 0x2000 && c_codepoint <= 0x206F;
            return is_japanese_character_or_punctuation || is_full_or_half_width || is_general_punctuation;
        }

        public static bool CharIsJapaneseCharacter(char c)
        {
            int c_codepoint = c; //NOTE: doesn't work for >16 bit codepoints (> 0xFFFF)
            return c_codepoint >= 0x3040 && c_codepoint <= 0x9faf;
        }

        public static bool CharIsASCII(char c)
        {
            int c_codepoint = (int)c; //NOTE: doesn't work for >16 bit codepoints (> 0xFFFF)
            return c_codepoint <= 0xFF;
        }

        public static void WriteStringList(TextWriter sw, IEnumerable<string> strings)
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

        //make this generic!
        public static string PrettyPrintListToString(List<int> list)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("[");
            for(int i = 0; i < list.Count; i++)
            {
                if (i != list.Count - 1)
                {
                    sb.Append($"{list[i]}, ");
                }
                else
                {
                    sb.Append(list[i].ToString());
                }
            }
            sb.Append("]");

            return sb.ToString();
        }

        public static string PrettyPrintListOfListToString(List<List<int>> list)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("[");
            for (int i = 0; i < list.Count; i++)
            {
                if (i != list.Count - 1)
                {
                    sb.Append($"{PrettyPrintListToString(list[i])}, ");
                }
                else
                {
                    sb.Append(PrettyPrintListToString(list[i]));
                }
            }
            sb.Append("]");

            return sb.ToString();
        }

    }
}
