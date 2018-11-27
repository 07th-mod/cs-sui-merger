﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace SuiMerger.MergedScriptPostProcessing
{
    class PostProcessingMain
    {
        //Regexes used to parse the hybrid script
        //note: double quote is transformed into single quote in below @ string
        static Regex dialogueRegex = new Regex(@"\tOutputLine\(", RegexOptions.IgnoreCase);
        static Regex fadeOutBGMMusicRegex = new Regex(@"\tFadeOutBGM\(\s*(\d)", RegexOptions.IgnoreCase);
        static Regex playBGMMusicRegex = new Regex(@"\tPlayBGM\(\s*(\d)", RegexOptions.IgnoreCase);

        public static void InsertMGLinesUsingPS3XML(string mergedMGScriptPath, string outputPath, MergerConfiguration configuration)
        {
            Console.WriteLine($"--------- Begin Applying Postprocessing Stage 1 to {mergedMGScriptPath} ------");

            //Detect the BGM channel of the original Manga Gamer script. This is used for BGM insertion and FadeOut insertion
            int bgmChannelNumber = MGScriptBGMChannelDetector.DetectBGMChannelOrDefault(mergedMGScriptPath, configuration, defaultChannel: 2, PrintOnFoundChannelAndWarnings: true);


            // --------- Perform stage 1 - this converts the raw merged script into a list of MangaGamerInstructions --------- 
            //Iterate over the the Manga Gamer chunks and the PS3 XML Instructions Chunks of the merged script
            List<MangaGamerInstruction> outputStage1 = new List<MangaGamerInstruction>();
            foreach (var chunk in PS3XMLChunkFinder.GetAllChunksFromMergedScript(mergedMGScriptPath))
            {
                if(chunk.isPS3Chunk)
                {
                    //PS3 XML reader doesn't care about newlines, so just join each line directly to next line
                    HandlePS3Chunk(String.Join("", chunk.lines), outputStage1, bgmChannelNumber);
                }
                else
                {
                    //handle a mangagamer chunk
                    foreach (string mgScriptLine in chunk.lines)
                    {
                        //add a fadebgm before last line of the script
                        if (mgScriptLine.Trim() == "}")
                        {
                            outputStage1.Add(new GenericInstruction("\tFadeOutBGM(0,1000,FALSE);", false));
                        }

                        outputStage1.Add(new GenericInstruction(mgScriptLine, false));
                    }
                }
            }

            // --------- Perform stage 2 filtering to 'tidy up' the script ---------
            //This section runs filters after all the other filters have run.
            //This stage converts the list of MangaGamerInstructions to a list of strings 
            List<string> outputStage2 = new List<string>();
            foreach (MangaGamerInstruction inst in outputStage1)
            {
                //clear out any Music (channel 2) BGM or Fade lines from the original manga gamer script
                bool lineIsPlayBGMOrFadeBGM =
                    LineHasPlayBGMOnChannel(inst.GetInstructionForScript(), bgmChannelNumber) ||
                    LineHasFadeOutBGMOnChannel(inst.GetInstructionForScript(), bgmChannelNumber);

                if (lineIsPlayBGMOrFadeBGM && inst.IsPS3() == false)
                {
                    continue;
                }

                outputStage2.Add(inst.GetInstructionForScript());
            }

            // --------- Finally, write the output to file. ---------
            FileUtils.WriteAllLinesCustomNewline(outputPath, outputStage2);
        }

        //Some files use different BGM channels for music (as opposed to background sounds). Another
        //function should scan the file to determine the BGM channel, and set bgmChannelNumber appropriately
        private static void HandlePS3Chunk(string ps3Chunk, List<MangaGamerInstruction> linesToOutput, int bgmChannelNumber)
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
                    else if ((shouldFindPlayBGM && LineHasPlayBGMOnChannel(currentLine.GetInstructionForScript(), bgmChannelNumber)) ||
                             (!shouldFindPlayBGM && LineHasFadeOutBGMOnChannel(currentLine.GetInstructionForScript(), bgmChannelNumber)))
                    {
                        //replace similar instruction with this instruction
                        linesToOutput[i] = lastFadeBGMOrPlayBGM;
                        break;
                    }
                }

            }
        }

        /// <summary>
        /// Checks if a line of the MangaGamer script has a PlayBGM command on a given channel 
        /// like PlayBGM(5, "audio");
        /// </summary>
        /// <param name="line"></param>
        /// <param name="channel"></param>
        /// <returns></returns>
        private static bool LineHasPlayBGMOnChannel(string line, int channel)
        {
            Match match = playBGMMusicRegex.Match(line);
            if (!match.Success)
                return false;

            return int.Parse(match.Groups[1].Value) == channel;
        }

        /// <summary>
        /// Checks if a line of the MangaGamer script has a FadeOutBGM command on a given channel 
        /// like FadeBGM(5, 3000);
        /// The regex doesn't check for a full match, just "FadeBGM([number]"
        /// </summary>
        /// <param name="line"></param>
        /// <param name="channel"></param>
        /// <returns></returns>
        private static bool LineHasFadeOutBGMOnChannel(string line, int channel)
        {
            Match match = fadeOutBGMMusicRegex.Match(line);
            if (!match.Success)
                return false;

            return int.Parse(match.Groups[1].Value) == channel;
        }

    }
}
