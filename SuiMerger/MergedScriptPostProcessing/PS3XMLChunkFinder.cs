using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SuiMerger.MergedScriptPostProcessing
{
    /// <summary>
    /// The output from the main SuiMerger produces a text file which is the original
    /// MG script but with the relevant PS3 Instructions merged into it. 
    /// 
    /// This class consumes lines from the merged script file one line at a time. Once it
    /// has consumed enough lines to form a ps3 instructions chunk, it returns the entire chunk
    /// all at once. Otherwise, it returns null.
    /// 
    /// Example
    /// 
    /// asdfasdfasdfsfd
    /// asdfasdasdf
    /// <?xml version="1.0" encoding="UTF-8"?>
    /// <PS3_SECTION>  <!-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~START~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ -->
    /// <ins type="MIX_CHANNEL_FADE" duration="60"></ins>
    /// </PS3_SECTION> <!-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~END~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ -->      //only here will the chunk be returned
    /// 
    /// </summary>
    class PS3XMLChunkFinder
    {
        static Regex ps3Start = new Regex(@"<?xml", RegexOptions.IgnoreCase);
        static Regex ps3End = new Regex(@"</PS3_SECTION", RegexOptions.IgnoreCase);

        bool lastLineWasXML = false;
        bool insidePS3XML = false;
        StringBuilder sb = new StringBuilder();

        public string Update(string line)
        {
            if (insidePS3XML)
            {
                sb.Append(line + Config.newline);

                //found ps3 section terminator - leave ps3 section. 
                //return all the ps3 instructions for this chunk as a string
                if (ps3End.IsMatch(line))
                {
                    insidePS3XML = false;
                    string retString = sb.ToString();
                    sb.Clear();
                    return retString;
                }
            }
            else
            {
                //found a ps3 line - have entered a ps3 instructions section
                lastLineWasXML = ps3Start.IsMatch(line);
                if (lastLineWasXML)
                {
                    sb.Append(line + Config.newline);
                    insidePS3XML = true;
                }
            }

            return null;
        }

        public bool LastLineWasXML() => lastLineWasXML;
    }

}
