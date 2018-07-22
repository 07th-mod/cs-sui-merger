﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuiMerger
{
    public class PS3DialogueTools
    {
        //same as SplitPS3String, but removes the name, if present
        //will fail if 0 length string passed in
        public static List<string> SplitPS3StringNoNames(string s)
        {
            List<string> retArray = new List<string>(SplitPS3String(s));

            //if line does not start with 'r' then it contains the name as the first element - remove the name
            if (s[0] != 'r')
                retArray.RemoveAt(0);

            return retArray;
        }

        //splits a ps3 string containg multiple phrases. This includes the character name, if present
        public static string[] SplitPS3String(string s)
        {
            StringBuilder sb = new StringBuilder();

            foreach (char c in s)
            {
                if(StringUtils.CharIsASCII(c) || c == '�')
                {
                    sb.Append('X');
                }
                else 
                {
                    sb.Append(c);
                }
            }

            return sb.ToString().Split(new char[] { 'X' }, StringSplitOptions.RemoveEmptyEntries);
        }

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
