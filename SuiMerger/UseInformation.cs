using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace SuiMerger
{
    class PS3XMLChunkFinder
    {
        static Regex ps3Start = new Regex(@"<?xml", RegexOptions.IgnoreCase);
        static Regex ps3End = new Regex(@"</PS3_SECTION", RegexOptions.IgnoreCase);

        bool insidePS3XML = false;
        StringBuilder sb = new StringBuilder();

        public string Update(string line)
        {
            if (insidePS3XML)
            {
                sb.Append(line);

                if (ps3End.IsMatch(line))
                {
                    Console.WriteLine($"saw ps3  end: {line}");
                    insidePS3XML = false;
                    
                    string retString = sb.ToString();
                    sb.Clear();
                    return retString;
                }
            }
            else
            {
                if (ps3Start.IsMatch(line))
                {
                    Console.WriteLine($"saw ps3 start: {line}");
                    sb.Append(line);

                    insidePS3XML = true;
                }
            }

            return null;
        }
    }

    class UseInformation
    {
        //Regexes used to parse the hybrid script
        static Regex playBGMRegex = new Regex(@"PlayBGM\(", RegexOptions.IgnoreCase);
        static Regex fadeOutBGMRegex = new Regex(@"FadeOutBGM\(", RegexOptions.IgnoreCase);

        public static void InsertMGLinesUsingPS3XML(string mergedMGScriptPath)
        {
            using (StreamReader mgScript = new StreamReader(mergedMGScriptPath, Encoding.UTF8))
            {
                PS3XMLChunkFinder chunkFinder = new PS3XMLChunkFinder();
                string line;
                while ((line = mgScript.ReadLine()) != null)
                {
                    //TODO: handle commented lines here
                    string ps3Chunk = chunkFinder.Update(line);
                    if(ps3Chunk != null)
                    {
                        PS3InstructionReader instructionReader = new PS3InstructionReader(new StringReader(ps3Chunk));
                        while (instructionReader.AdvanceToNextInstruction())
                        {
                            Console.WriteLine("Got data:" + instructionReader.reader.ReadOuterXml());
                        }
                    }
                }
            }
        }
    }
}
