using System;
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
    }
}
