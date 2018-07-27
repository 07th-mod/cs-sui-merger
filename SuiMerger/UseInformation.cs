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

        bool lastLineWasXML = false;
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
                    lastLineWasXML = true;
                }
                else //MG type line
                {
                    lastLineWasXML = false;
                }
            }

            return null;
        }

        public bool LastLineWasXML()
        {
            return lastLineWasXML;
        }

    }

    class UseInformation
    {
        //Regexes used to parse the hybrid script
        static Regex playBGMRegex = new Regex(@"PlayBGM\(", RegexOptions.IgnoreCase);
        static Regex fadeOutBGMRegex = new Regex(@"FadeOutBGM\(", RegexOptions.IgnoreCase);

        public static void InsertMGLinesUsingPS3XML(string mergedMGScriptPath, string outputPath)
        {
            using (StreamWriter outputFile = FileUtils.CreateDirectoriesAndOpen(outputPath, FileMode.Create))
            using (StreamReader mgScript = new StreamReader(mergedMGScriptPath, Encoding.UTF8))
            {
                PS3XMLChunkFinder chunkFinder = new PS3XMLChunkFinder();
                string mgScriptLine;
                while ((mgScriptLine = mgScript.ReadLine()) != null)
                {
                    //TODO: handle commented lines here

                    //Handle XML data if there is any
                    string ps3Chunk = chunkFinder.Update(mgScriptLine);
                    if (ps3Chunk != null)
                    {
                        List<string> outputInstructions = new List<string>();

                        PS3InstructionReader ps3Reader = new PS3InstructionReader(new StringReader(ps3Chunk));
                        while (ps3Reader.AdvanceToNextInstruction())
                        {
                            if (ps3Reader.reader.GetAttribute("type") == "BGM_PLAY")
                            {
                                string bgmFileName = ps3Reader.reader.GetAttribute("bgm_file");
                                outputInstructions.Add($"PlayBGM( 0, \"{bgmFileName}\", 128, 0 );");
                            }

                            Console.WriteLine("Got data:" + ps3Reader.reader.ReadOuterXml());
                        }

                        //When writing out instructions, need to add a \t otherwise game won't recognize it
                        foreach(string s in outputInstructions)
                        {
                            outputFile.WriteLine($"\t{s}");
                        }
                    }

                    //Handle original mg lines here
                    if(!chunkFinder.LastLineWasXML())
                    {
                        outputFile.WriteLine(mgScriptLine);
                    }
                }
            }
        }
    }
}
