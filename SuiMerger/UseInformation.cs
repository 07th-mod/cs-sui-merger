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
        //note: double quote is transformed into single quote in below @ string
        static Regex playBGMGetFileName = new Regex(@"PlayBGM\(\s*(\d)\s*,\s*""([^""]*)""");

        static Regex playBGMMusicRegex = new Regex(@"\tPlayBGM\(\s*(\d)", RegexOptions.IgnoreCase);
        static Regex fadeOutBGMMusicRegex = new Regex(@"\tFadeOutBGM\(\s*(\d)", RegexOptions.IgnoreCase);
        static Regex dialogueRegex = new Regex(@"\tOutputLine\(", RegexOptions.IgnoreCase);

        //Returns the audio length of a given file in seconds (includes fractional part)
        public static double GetAudioLength(string path) => TagLib.File.Create(path).Properties.Duration.TotalSeconds;

        //This function tries to determine the BGM channel, given a single script line.
        //If the channel couldn't be determined or is not a music file, will return null. Otherwise returns the channel.
        // Reasons for channel determination failure are below:
        //  - doesn't contain a BGMPlay command or has an invalid BGMPlay command
        //  - audio file in the BGMPlay command couldn't be found
        //  - audio file in the BGMPlay command is < 30 seconds indicating it's not a music file
        // List<string> searchFolders - folders to search for the music file
        // double bgmLengthThresholdSeconds - length in seconds above which an audio file is considered to be BGM
        public static int? TryGetBGMMusicChannel(string line, List<string> searchFolders, double bgmLengthThresholdSeconds)
        {
            //if can't parse line, assume not a playbgm line
            Match match = playBGMGetFileName.Match(line);
            if (!match.Success)
                return null;

            int channel = int.Parse(match.Groups[1].Value);
            string audioFileName = match.Groups[2].Value;

            //Try to get the audio length, scanning each given folder. If file not found, assume not a playbgm line
            //Note that in the script, the file extension is not specified - therfore add '.ogg' to filename
            double? audioLength = null;
            foreach (string searchFolder in searchFolders)
            {
                try
                {
                    audioLength = GetAudioLength(Path.Combine(searchFolder, audioFileName + ".ogg"));
                    break;
                }
                catch (Exception e)
                {

                }
            }

            if (audioLength == null)
                return null;

            bool isMusic = audioLength >= bgmLengthThresholdSeconds;

            Console.WriteLine($"Audio file {audioFileName} on channel {channel} is {audioLength} seconds long. Type: {(isMusic ? "Music" : "Not Music")}");

            if(isMusic)
            {
                return channel;
            }

            return null;
        }

        public static bool LineHasPlayBGMOnChannel(string line, int channel)
        {
            Match match = playBGMMusicRegex.Match(line);
            if (!match.Success)
                return false;

            return int.Parse(match.Groups[1].Value) == channel;
        }

        public static bool LineHasFadeOutBGMOnChannel(string line, int channel)
        {
            Match match = fadeOutBGMMusicRegex.Match(line);
            if (!match.Success)
                return false;

            return int.Parse(match.Groups[1].Value) == channel;
        }


        //Some files use different BGM channels for music (as opposed to background sounds). Another
        //function should scan the file to determine the BGM channel, and set bgmChannelNumber appropriately
        public static void HandlePS3Chunk(string ps3Chunk, List<MangaGamerInstruction> linesToOutput, int bgmChannelNumber)
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
                        DebugUtils.Print($"Found BGM play: {playBGM.GetInstructionForScript()}");
                        lastBGMPlay = playBGM;
                        break;

                    case MGFadeOutBGM fadeBGM:
                        DebugUtils.Print($"Found BGM fade: {fadeBGM.GetInstructionForScript()}");
                        lastFade = fadeBGM;
                        break;
                }
            }

            //remember what the last instruction (fade or play bgm) - this is the instruction to be inserted
            MangaGamerInstruction lastFadeBGMOrPlayBGM = lastBGMPlay != null ? lastBGMPlay : lastFade;


            if (lastFadeBGMOrPlayBGM != null)
            {
                //When writing out instructions, need to add a \t otherwise game won't recognize it
                DebugUtils.Print($"In this chunk, selected: {lastFadeBGMOrPlayBGM.GetInstructionForScript()}");

                //find a good spot to insert the instruction, depending on the type (either playBGM or FadeBGM)
                bool shouldFindPlayBGM = lastFadeBGMOrPlayBGM is MGPlayBGM;

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
                    else if( ( shouldFindPlayBGM && LineHasPlayBGMOnChannel   (currentLine.GetInstructionForScript(), bgmChannelNumber)) ||
                             (!shouldFindPlayBGM && LineHasFadeOutBGMOnChannel(currentLine.GetInstructionForScript(), bgmChannelNumber))    )
                    {
                        //replace similar instruction with this instruction
                        linesToOutput[i] = lastFadeBGMOrPlayBGM;
                        break;
                    }
                }

            }
        }

        public static int? DetectBGMChannel(string mgScriptPath, MergerConfiguration configuration)
        {
            Dictionary<int, int> channelCounter = new Dictionary<int, int>();

            using (StreamReader mgScript = new StreamReader(mgScriptPath, Encoding.UTF8))
            {
                //Count how many times each channel plays a BGM
                //channel is considered to play a BGM if the file being played is longer than the configured length
                string line;
                while ((line = mgScript.ReadLine()) != null)
                {
                    int? maybeBGMChannel = TryGetBGMMusicChannel(line, searchFolders : configuration.bgm_folders, bgmLengthThresholdSeconds : 30);
                    if (maybeBGMChannel != null)
                    {
                        int BGMChannel = (int)maybeBGMChannel;
                        if (channelCounter.ContainsKey(BGMChannel))
                        {
                            channelCounter[BGMChannel] += 1;
                        }
                        else
                        {
                            channelCounter[BGMChannel] = 0;
                        }
                    }
                }

                //TODO: Debug - remove later
                foreach (KeyValuePair<int, int> item in channelCounter)
                {
                    Console.WriteLine($"channel: {item.Key} count: {item.Value}");
                }

                //return the channel which has the max number of BGM plays
                foreach (KeyValuePair<int, int> item in channelCounter.OrderByDescending(key => key.Value))
                {
                    return item.Key;
                }
            }

            //if no playBGMs were found, return null
            return null;
        }

        public static void InsertMGLinesUsingPS3XML(string mergedMGScriptPath, string outputPath, MergerConfiguration configuration)
        {
            Console.WriteLine("--------- Begin inserting BGM into original script ------");
            int ? maybeBGMChannel = DetectBGMChannel(mergedMGScriptPath, configuration);

            int bgmChannelNumber = 2;
            if(maybeBGMChannel != null)
            {
                bgmChannelNumber = (int) maybeBGMChannel;
                Console.WriteLine($"Detected channel [{bgmChannelNumber}] as BGM Channel number");
            }
            else
            {
                Console.WriteLine($"WARNING: Could not detect bgmChannel for [{mergedMGScriptPath}]. Will use channel 2 when inserting PS3 music");
            }

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
                        HandlePS3Chunk(ps3Chunk, linesToOutput, bgmChannelNumber);
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
                        LineHasPlayBGMOnChannel(inst.GetInstructionForScript(), bgmChannelNumber) ||
                        LineHasFadeOutBGMOnChannel(inst.GetInstructionForScript(), bgmChannelNumber);

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
