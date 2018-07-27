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
        MemoryStream ps3XML;
        StreamWriter ps3XMLWriter;

        public PS3XMLChunkFinder()
        {
            ps3XML = new MemoryStream();
            ps3XMLWriter = new StreamWriter(ps3XML, Encoding.UTF8);
        }

        public MemoryStream Update(string line)
        {
            if (insidePS3XML)
            {
                ps3XMLWriter.WriteLine(line);

                if (ps3End.IsMatch(line))
                {
                    Console.WriteLine($"saw ps3  end: {line}");
                    insidePS3XML = false;

                    //MUST flush before use, otherwise some lines might not be seen?
                    ps3XMLWriter.Flush();

                    MemoryStream ms = new MemoryStream();
                    ps3XML.Seek(0, SeekOrigin.Begin);
                    ps3XML.CopyTo(ms);
                    ps3XML.Flush();
                    ms.Seek(0, SeekOrigin.Begin);

                    //Clear the stream after finished
                    ps3XML.SetLength(0);
                    
                    return ms;
                }
            }
            else
            {
                if (ps3Start.IsMatch(line))
                {
                    Console.WriteLine($"saw ps3 start: {line}");
                    ps3XMLWriter.WriteLine(line);

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
                    MemoryStream ms = chunkFinder.Update(line);
                    if(ms != null)
                    {
                        PS3InstructionReader instructionReader = new PS3InstructionReader(ms);
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
