using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuiMerger
{
    public class PS3DialogueTools
    {
        //PS3 lines need to strip the names of the characters etc. from the string.
        //should probably make this into its own class
        public static string StripPS3NamesFromString(string s)
        {
            string retString = s;

            //If line starts with 'r' then it's a text-only line
            bool textOnlyLine = s[0] == 'r';
            if (textOnlyLine)
            {
                //for now, don't modify the string if it's a text-only line
            }
            else
            {
                int nonJapaneseCharIndex = 0; //if no non-japanese characters are found, just assume entire string.
                for (int i = 0; i < s.Length; i++)
                {
                    if (!StringUtils.CharIsJapanese(s[i]))
                    {
                        nonJapaneseCharIndex = i;
                        break;
                    }
                }

                retString = s.Substring(nonJapaneseCharIndex);
            }

            return retString;
        }

    }
}
