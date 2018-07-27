using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace SuiMerger
{
    class UseInformation
    {
        //Regexes used to parse the hybrid script
        static Regex playBGMRegex = new Regex(@"PlayBGM\(", RegexOptions.IgnoreCase);
        static Regex fadeOutBGMRegex = new Regex(@"FadeOutBGM\(", RegexOptions.IgnoreCase);
        //static Regex ps3Start = new Regex(@"<PS3_SECTION", RegexOptions.IgnoreCase);
        static Regex ps3Start = new Regex(@"<?xml", RegexOptions.IgnoreCase);

        static Regex ps3End = new Regex(@"</PS3_SECTION", RegexOptions.IgnoreCase);

        public static void InsertMGLinesUsingPS3XML(string mergedMGScriptPath)
        {
            bool insidePS3XML = false;
            MemoryStream ps3XML = new MemoryStream();
            StreamWriter ps3XMLWriter = new StreamWriter(ps3XML, Encoding.UTF8);

            using (StreamReader mgScript = new StreamReader(mergedMGScriptPath, Encoding.UTF8))
            {
                int outer_count = 0;
                string line;
                while ((line = mgScript.ReadLine()) != null)
                {
                    //TODO: handle commented lines here
                    
                    if (insidePS3XML)
                    {
                        ps3XMLWriter.WriteLine(line);

                        Console.WriteLine($"wrote {outer_count}: {line} to memory stream");
                        outer_count += 1;

                        if (ps3End.IsMatch(line))
                        {
                            Console.WriteLine($"saw ps3  end: {line}");
                            insidePS3XML = false;

                            //MUST flush before use, otherwise some lines might not be seen?
                            ps3XMLWriter.Flush();

                            //debug
                            ps3XML.Seek(0, SeekOrigin.Begin);
                            StreamReader testReader = new StreamReader(ps3XML, Encoding.UTF8);
                            int line_no = 0;
                            string line2;
                            while ((line2 = testReader.ReadLine()) != null)
                            {
                                Console.WriteLine($"{line_no}: {line2}");
                                line_no += 1;
                            }

                            //Now process the xml chunk
                            ps3XML.Seek(0, SeekOrigin.Begin);
                            PS3InstructionReader instructionReader = new PS3InstructionReader(ps3XML);
                            while(instructionReader.AdvanceToNextInstruction())
                            {
                                Console.WriteLine("Got data:" + instructionReader.reader.ReadOuterXml());
                            }

                            //Clear the stream after finished
                            ps3XML.SetLength(0);
                        }
                    }
                    else
                    {
                        if (ps3Start.IsMatch(line))
                        {
                            Console.WriteLine($"saw ps3 start: {line}");
                            ps3XMLWriter.WriteLine(line);
                            outer_count += 1;
                            insidePS3XML = true;
                        }
                    }             

                }
            }
        }
    }
}
