using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace SuiMerger
{
    abstract class MangaGamerInstruction
    {
        private readonly bool isPS3;   //use to flag which classes are ps3 
        private readonly bool noTab;

        protected MangaGamerInstruction(bool isPS3, bool noTab)
        {
            this.isPS3 = isPS3;
            this.noTab = noTab;
        }

        //gets the instruction, without the tab character or newline
        protected abstract string GetInstruction();

        //returns the instruction string with tab character
        public string GetInstructionForScript()
        {
            if(noTab)
            {
                return GetInstruction();
            }
            else
            {
                return $"\t{GetInstruction()}";
            }
        }

        //returns true if instruction originated from PS3 xml
        public bool IsPS3() => isPS3;

    }

    class MGPlayBGM : MangaGamerInstruction
    {
        readonly int channel;
        readonly string bgmFileName;

        public MGPlayBGM(int channel, string bgmFileName, bool isPS3) : base(isPS3, false)
        {
            this.channel = channel;
            this.bgmFileName = bgmFileName;
        }

        protected override string GetInstruction()
        {
            return $"PlayBGM( {channel}, \"{bgmFileName}\", 128, 0 );";
        }
    }

    class MGFadeOutBGM : MangaGamerInstruction
    {
        readonly int channel;
        readonly int fadeTime;

        public MGFadeOutBGM(int channel, int ps3Duration, bool isPS3) : base(isPS3, false)
        {
            this.channel = channel;
            this.fadeTime = (int)Math.Round(ps3Duration / 60.0 * 1000.0);
        }

        protected override string GetInstruction()
        {
            return $"FadeOutBGM( {channel}, {fadeTime}, FALSE );";
        }
    }

    class GenericInstruction : MangaGamerInstruction
    {
        readonly string data;

        public GenericInstruction(string data, bool isPS3) : base(isPS3, true)
        {
            this.data = data;
        }

        protected override string GetInstruction()
        {
            return data;
        }
    }

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
        static Regex playBGMMusicCH2Regex = new Regex(@"\tPlayBGM\(\s*2", RegexOptions.IgnoreCase);
        static Regex fadeOutBGMMusicCH2Regex = new Regex(@"\tFadeOutBGM\(\s*2", RegexOptions.IgnoreCase);
        static Regex dialogueRegex = new Regex(@"\tOutputLine\(", RegexOptions.IgnoreCase);

        public static void HandlePS3Chunk(string ps3Chunk, List<MangaGamerInstruction> linesToOutput)
        {
            List<MangaGamerInstruction> instructionsToInsert = new List<MangaGamerInstruction>();

            //Read through the ps3 chunk of xml and generate instruction objects for the targeted instructions
            PS3InstructionReader ps3Reader = new PS3InstructionReader(new StringReader(ps3Chunk));
            while (ps3Reader.AdvanceToNextInstruction())
            {
                switch (ps3Reader.reader.GetAttribute("type"))
                {
                    case "BGM_PLAY":
                        string bgmFileName = ps3Reader.reader.GetAttribute("bgm_file");
                        instructionsToInsert.Add(new MGPlayBGM(2, bgmFileName, true));
                        break;

                    case "BGM_FADE":
                        int duration = Convert.ToInt32(ps3Reader.reader.GetAttribute("duration"));
                        instructionsToInsert.Add(new MGFadeOutBGM(2, duration, true));
                        break;
                }
            }

            //Only insert only the last play/fadebgm instruction in the list
            MangaGamerInstruction lastFade = null;
            MangaGamerInstruction lastBGMPlay = null;
            foreach (MangaGamerInstruction mgInstruction in instructionsToInsert)
            {
                switch (mgInstruction)
                {
                    case MGPlayBGM playBGM:
                        Console.WriteLine($"Found BGM play: {playBGM.GetInstructionForScript()}");
                        lastBGMPlay = playBGM;
                        break;

                    case MGFadeOutBGM fadeBGM:
                        Console.WriteLine($"Found BGM fade: {fadeBGM.GetInstructionForScript()}");
                        lastFade = fadeBGM;
                        break;
                }
            }

            MangaGamerInstruction lastFadeBGMOrPlayBGM = lastBGMPlay != null ? lastBGMPlay : lastFade;
            if (lastFadeBGMOrPlayBGM != null)
            {
                //When writing out instructions, need to add a \t otherwise game won't recognize it
                Console.WriteLine($"In this chunk, selected: {lastFadeBGMOrPlayBGM.GetInstructionForScript()}");

                //find a good spot to insert the instruction, depending on the type
                Regex insertionPointRegex = lastFadeBGMOrPlayBGM is MGPlayBGM ? playBGMMusicCH2Regex : fadeOutBGMMusicCH2Regex;

                //search backwards in the current output until finding the insertion point regex (PlayBGM( or FadeOutBGM()
                //however if find a dialogue line, give up and just insert at the end of the list (where the ps3 xml is)
                for (int i = linesToOutput.Count - 1; i > 0; i--)
                {
                    MangaGamerInstruction currentLine = linesToOutput[i];
                    if (dialogueRegex.IsMatch(currentLine.GetInstructionForScript()))
                    {
                        //insert at end of list
                        linesToOutput.Add(lastFadeBGMOrPlayBGM);
                        break;
                    }
                    else if (insertionPointRegex.IsMatch(currentLine.GetInstructionForScript()))
                    {
                        //replace similar instruction with this instruction
                        linesToOutput[i] = lastFadeBGMOrPlayBGM;
                        break;
                    }
                }

            }
        }

        public static void InsertMGLinesUsingPS3XML(string mergedMGScriptPath, string outputPath)
        {
            List<MangaGamerInstruction> linesToOutput = new List<MangaGamerInstruction>();

            using (StreamReader mgScript = new StreamReader(mergedMGScriptPath, Encoding.UTF8))
            {
                PS3XMLChunkFinder chunkFinder = new PS3XMLChunkFinder();
                string mgScriptLine;
                while ((mgScriptLine = mgScript.ReadLine()) != null)
                {
                    //TODO: handle commented lines here?

                    //Handle XML data if there is any by inserting instructions into output script
                    string ps3Chunk = chunkFinder.Update(mgScriptLine);
                    if (ps3Chunk != null)
                    {
                        HandlePS3Chunk(ps3Chunk, linesToOutput);
                    }

                    //Handle original mg lines here
                    if(!chunkFinder.LastLineWasXML())
                    {
                        //add a fadebgm before last line of the script
                        if (mgScriptLine.Trim() == "}")
                        {
                            linesToOutput.Add(new GenericInstruction("\tFadeOutBGM(0,1000,FALSE);", false));
                        }

                        linesToOutput.Add(new GenericInstruction(mgScriptLine, false));
                    }
                }
            }

            //filter, then write lines to output to file
            using (StreamWriter outputFile = FileUtils.CreateDirectoriesAndOpen(outputPath, FileMode.Create))
            {
                foreach(MangaGamerInstruction inst in linesToOutput)
                {
                    //clear out any Music (channel 2) BGM or Fade lines from the original manga gamer script
                    bool lineIsPlayBGMOrFadeBGM = 
                        playBGMMusicCH2Regex.IsMatch(inst.GetInstructionForScript()) || 
                        fadeOutBGMMusicCH2Regex.IsMatch(inst.GetInstructionForScript());

                    if (lineIsPlayBGMOrFadeBGM && inst.IsPS3() == false)
                    {
                        continue;
                    }

                    outputFile.WriteLine(inst.GetInstructionForScript());
                }
            }
        }
    }
}
