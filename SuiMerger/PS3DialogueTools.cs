using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuiMerger
{
	// NOTE: If multiple voices are to be played at the same time, they are separated with a | , for example:
	// 全員ro45.vS20/03/440300314|S20/06/440600075|S20/04/440400178|S20/05/440500111|S20/08/440800105|S20/11/440700376|S20/09/440900102.「「お疲れさまでした〜〜〜ッ！！！！」」

    public class PS3DialogueTools
    {
        //returns the japanese characters in the string without the name at the front (for voiced lines)
        //for unvoiced lines it just returns all the japanese characters
        public static string GetPS3StringNoNames(string s)
        {
            StringBuilder sb = new StringBuilder();
            foreach(string splitJapaneseCharacters in SplitPS3StringNoNames(s))
            {
                sb.Append(splitJapaneseCharacters);
            }
            return sb.ToString();
        }

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
        //NOTE: the ascii '!' character MIGHT be used to escape normally ascii characters - for example, to have a
        //literal 'u' in the japanese text, you would do '!u' ? or not?. This has not been added to below because
        //I'm not sure and it doesn't happen too often (only saw it in !T!I!P!S
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
