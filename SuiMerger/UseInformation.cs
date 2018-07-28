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
                sb.Append(line + Config.newline);

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
                        List<string> instructionsToInsert = new List<string>();
                        //foreach(string inst in outputInstructions)
                        /*{
                            Console.WriteLine(ps3Chunk);
                        }*/

                        PS3InstructionReader ps3Reader = new PS3InstructionReader(new StringReader(ps3Chunk));
                        while (ps3Reader.AdvanceToNextInstruction())
                        {
                            switch(ps3Reader.reader.GetAttribute("type"))
                            {
                                case "BGM_PLAY":
                                    string bgmFileName = ps3Reader.reader.GetAttribute("bgm_file");
                                    string mgPlayBGMString = $"PlayBGM( 0, \"{bgmFileName}\", 128, 0 );";
                                    instructionsToInsert.Add(mgPlayBGMString);
                                    Console.WriteLine($"Found BGM play string, will add: {mgPlayBGMString}");
                                    break;

                                case "BGM_FADE":
                                    int duration = Convert.ToInt32(ps3Reader.reader.GetAttribute("duration"));
                                    int channel = 0;
                                    int fadeTime = (int)Math.Round(duration / 60.0 * 1000.0);
                                    string mgFadeOutBGM = $"FadeOutBGM( {channel}, {fadeTime}, FALSE );";
                                    instructionsToInsert.Add(mgFadeOutBGM);
                                    Console.WriteLine($"Found BGM fade string, will add: {mgFadeOutBGM}");
                                    break;
                            }

                            //Console.WriteLine("Got data:" + ps3Reader.reader.ReadOuterXml());
                        }

                        //When writing out instructions, need to add a \t otherwise game won't recognize it
                        foreach(string s in instructionsToInsert)
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
