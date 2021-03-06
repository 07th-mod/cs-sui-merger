﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace SuiMerger.MergedScriptPostProcessing
{
    class PostProcessingMain
    {
        static List<string> GetArgs(string s)
        {
            return s.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();
        }

        static MGFadeOutBGM TryParseFadeOutBGM(string s, bool isPS3)
        {
            Match fadeOutBGMMatch = fadeOutBGMMusicRegex.Match(s);
            if (!fadeOutBGMMatch.Success)
                return null;

            List<string> args = GetArgs(fadeOutBGMMatch.Groups[1].ToString());
            int channel = int.Parse(args[0]);
            int fadeTime = int.Parse(args[1]);
            bool unkBool = bool.Parse(args[2]);
            return new MGFadeOutBGM(channel, fadeTime, unkBool, isPS3: isPS3);
        }

        static MGPlayBGM TryParsePlayBGM(string s, bool isPS3)
        {
            Match match = playBGMMusicRegex.Match(s);
            if (!match.Success)
                return null;

            List<string> args = GetArgs(match.Groups[1].ToString());

            string bgmFileName = args[1];
            if(bgmFileName[0] != '"')
            {
                throw new Exception("filename not quoted!");
            }

            return new MGPlayBGM(
                channel: int.Parse(args[0]),
                bgmFileName: bgmFileName.Trim(new char[] {'"'}),
                pan: int.Parse(args[2]),
                unk: int.Parse(args[3]),
                isPS3: isPS3
            );
        }

        static MGPlaySE TryParsePlaySE(string s, bool isPS3)
        {
            Match match = playSERegex.Match(s);
            if (!match.Success)
                return null;

            List<string> args = GetArgs(match.Groups[1].ToString());

            string filename = args[1];
            if (filename[0] != '"')
            {
                throw new Exception("filename not quoted!");
            }

            return new MGPlaySE(
                channel: int.Parse(args[0]),
                filename: filename.Trim(new char[] { '"' }),
                volume: int.Parse(args[2]),
                panning: int.Parse(args[3]),
                isPS3: isPS3
            );
        }

        static MangaGamerInstruction ParseMangaGamerInstruction(string s, bool isPS3)
        {
            MGFadeOutBGM fadeOutBGM = TryParseFadeOutBGM(s, isPS3: isPS3);
            if (fadeOutBGM != null)
            {
                return fadeOutBGM;
            }

            MGPlayBGM playBGM = TryParsePlayBGM(s, isPS3: isPS3);
            if (playBGM != null)
            {
                return playBGM;
            }

            MGPlaySE playSE = TryParsePlaySE(s, isPS3: isPS3);
            if (playSE != null)
            {
                return playSE;
            }

            return new GenericInstruction(s, false);
        }

        //Regexes used to parse the hybrid script
        //note: double quote is transformed into single quote in below @ string
        static Regex dialogueRegex = new Regex(@"\s*OutputLine\(", RegexOptions.IgnoreCase);
        static Regex fadeOutBGMMusicRegex = new Regex(@"\s*FadeOutBGM\(([^\)]+)\)", RegexOptions.IgnoreCase);
        static Regex playBGMMusicRegex = new Regex(@"\s*PlayBGM\(([^\)]+)\)", RegexOptions.IgnoreCase);
        static Regex playSERegex = new Regex(@"\s*PlaySE\(([^\)]+)\)", RegexOptions.IgnoreCase);

        public static void PrintChunk(List<MangaGamerInstruction> instructions)
        {
            Console.WriteLine("\n-------------- Begin new section --------------");
            foreach (MangaGamerInstruction mgInstruction in instructions)
            {
                Console.Write(mgInstruction.IsPS3() ? "PS3 - " : "MG  - ");
                switch (mgInstruction)
                {
                    case MGPlayBGM playBGM:
                        Console.Write($"Found BGM play: {playBGM.GetInstruction()}");
                        break;

                    case MGFadeOutBGM fadeBGM:
                        Console.Write($"Found BGM fade: {fadeBGM.GetInstruction()}");
                        break;

                    case MGPlaySE playSE:
                        Console.Write($"Found SE play: {playSE.GetInstruction()}");
                        break;

                    default:
                        Console.Write($"Found Generic instruction: {mgInstruction.GetInstruction()}");
                        break;
                }
                Console.WriteLine();
            }
        }

        public class InstructionAssociation
        {
            public MangaGamerInstruction mgOriginalInstruction;
            public List<MangaGamerInstruction> associatedPS3Instructions;

            public InstructionAssociation(MangaGamerInstruction mgOriginalInstruction)
            {
                this.mgOriginalInstruction = mgOriginalInstruction;
                this.associatedPS3Instructions = new List<MangaGamerInstruction>();
            }
        }

        public static void HandleChunk(List<InstructionAssociation> workingInstructionList, List<MangaGamerInstruction> newInstructions)
        {
            const bool includeOutputInLookBack = true;

            List<MGPlayBGM> ps3PlayBGMInstructions = new List<MGPlayBGM>();
            int numMGBGM = 0;

            List<MGPlaySE> ps3PlaySEInstructions = new List<MGPlaySE>();
            int numMGSE = 0;

            foreach (MangaGamerInstruction inst in newInstructions)
            {
                switch (inst)
                {
                    case MGPlayBGM playBGM:
                        if (inst.IsPS3())
                        {
                            ps3PlayBGMInstructions.Add(playBGM);
                        }
                        else
                        {
                            numMGBGM += 1;
                        }
                        break;

                    case MGPlaySE playSE:
                        if (inst.IsPS3())
                        {
                            ps3PlaySEInstructions.Add(playSE);
                        }
                        else
                        {
                            numMGSE += 1;
                        }
                        break;

                    default:
                        break;
                }
            }

            //determine whether OK by counting number of each
            bool bgmOK = ps3PlayBGMInstructions.Count == numMGBGM;
            bool seOK = ps3PlaySEInstructions.Count == numMGSE;

            Console.WriteLine("BGM: " + (bgmOK ? "match" : "NO MATCH"));
            Console.WriteLine("SE: " + (seOK ? "match" : "NO MATCH"));

            List<InstructionAssociation> output = new List<InstructionAssociation>();
            foreach (MangaGamerInstruction inst in newInstructions.Where(x => !x.IsPS3()))
            {
                InstructionAssociation current_inst = new InstructionAssociation(inst);

                switch (inst)
                {
                    case MGPlayBGM playBGM:
                        if (bgmOK)
                        {
                            current_inst.associatedPS3Instructions.Add(ps3PlayBGMInstructions[0]);
                            ps3PlayBGMInstructions.RemoveAt(0);
                        }
                        break;

                    case MGPlaySE playSE:
                        if (seOK)
                        {
                            current_inst.associatedPS3Instructions.Add(ps3PlaySEInstructions[0]);
                            ps3PlaySEInstructions.RemoveAt(0);
                        }
                        break;

                    default:
                        break;
                }

                output.Add(current_inst);
            }

            //Add working instructions to output. Do not use 'output' variable past this point
            if (includeOutputInLookBack)
            {
                workingInstructionList.AddRange(output);
            }

            //look back in the working buffer for unmatched instructions - try to fill with ps3 instructions if possible
            bool lookBackBGMOK = true;
            bool lookBackSEOK = true;

            int lookBackAmount = 100;
            for (int i = workingInstructionList.Count - 1; (i >= 0) && (i >= workingInstructionList.Count - lookBackAmount); i--)
            {
                InstructionAssociation inst = workingInstructionList[i]; 

                switch (inst.mgOriginalInstruction)
                {
                    case MGPlayBGM playBGM:
                        if (lookBackBGMOK)
                        {
                            if (inst.associatedPS3Instructions.Count > 0)
                            {
                                if (lookBackBGMOK)
                                {
                                    Console.WriteLine($"Instruction {inst.mgOriginalInstruction} caused search termination");
                                }
                                lookBackBGMOK = false;
                            }
                            else
                            {
                                //try to insert BGM if any. since iterating backwards, take from end of ps3PlayBGMInstructions list
                                if (ps3PlayBGMInstructions.Count > 0)
                                {
                                    int lastIndex = ps3PlayBGMInstructions.Count - 1;
                                    inst.associatedPS3Instructions.Add(ps3PlayBGMInstructions[lastIndex]);
                                    ps3PlayBGMInstructions.RemoveAt(lastIndex);
                                }
                            }
                        }
                        break;

                    case MGPlaySE playSE:
                        if (lookBackSEOK)
                        {
                            if (inst.associatedPS3Instructions.Count > 0)
                            {
                                lookBackSEOK = false;
                            }
                            else
                            {
                                if (ps3PlaySEInstructions.Count > 0)
                                {
                                    int lastIndex = ps3PlaySEInstructions.Count - 1;
                                    inst.associatedPS3Instructions.Add(ps3PlaySEInstructions[lastIndex]);
                                    ps3PlaySEInstructions.RemoveAt(lastIndex);
                                }
                            }
                        }
                        break;
                }
                
            }

            if (!includeOutputInLookBack)
            {
                workingInstructionList.AddRange(output);
            }

            //if bgm not OK, output a comment error in instructions
            if (ps3PlayBGMInstructions.Count != 0 || ps3PlaySEInstructions.Count != 0)// || ps3FadeInstructions.Count != 0)
            {
                var last_inst = workingInstructionList[workingInstructionList.Count - 1];
                last_inst.associatedPS3Instructions.Add(new FailInstruction("//Failed to insert some PS3 instruction!", false));
                last_inst.associatedPS3Instructions.AddRange(ps3PlayBGMInstructions);
                last_inst.associatedPS3Instructions.AddRange(ps3PlaySEInstructions);
                last_inst.associatedPS3Instructions.Add(new FailInstruction("//End failed instructions", false));
            }
        }

        public static void ExtractBGMAssociations(List<InstructionAssociation> instructionAssociations, Counter counter)
        {
            foreach(var instructionAssociation in instructionAssociations)
            {
                switch(instructionAssociation.mgOriginalInstruction)
                {
                    case MGPlayBGM mgPlayBGM:
                        foreach(var ps3Inst in instructionAssociation.associatedPS3Instructions)
                        {
                            switch(ps3Inst)
                            {
                                case MGPlayBGM ps3PlayBGM:
                                    counter.Add(mgPlayBGM.bgmFileName, ps3PlayBGM.bgmFileName);
                                    break;
                            }
                        }
                        break;
                }
            }
        }

        public static void InsertMGLinesUsingPS3XML(string mergedMGScriptPath, string outputPath, MergerConfiguration configuration, Counter counter)
        {
            const bool USE_OLD_METHOD_FOR_INSERT_BGM = false; 

            Console.WriteLine($"--------- Begin Applying Postprocessing Stage 1 to {mergedMGScriptPath} ------");

            //Detect the BGM channel of the original Manga Gamer script. This is used for BGM insertion and FadeOut insertion
            int bgmChannelNumber = MGScriptBGMChannelDetector.DetectBGMChannelOrDefault(mergedMGScriptPath, configuration, defaultChannel: 2, PrintOnFoundChannelAndWarnings: true);


            // --------- Perform stage 1 - this converts the raw merged script into a list of MangaGamerInstructions --------- 
            //Iterate over the the Manga Gamer chunks and the PS3 XML Instructions Chunks of the merged script
            int debug_i = 0;
            List<InstructionAssociation> workingInstructionAssociations = new List<InstructionAssociation>();

            List<MangaGamerInstruction> newChunk = new List<MangaGamerInstruction>();
            foreach (var chunk in PS3XMLChunkFinder.GetAllChunksFromMergedScript(mergedMGScriptPath))
            {
                if(chunk.isPS3Chunk)
                {
                    //PS3 XML reader doesn't care about newlines, so just join each line directly to next line
                    HandlePS3Chunk(String.Join("", chunk.lines), newChunk, bgmChannelNumber);

                    HandleChunk(workingInstructionAssociations, newChunk);
                    newChunk = new List<MangaGamerInstruction>();
                }
                else
                {
                    //handle a mangagamer chunk
                    foreach (string mgScriptLine in chunk.lines)
                    {
                        newChunk.Add(ParseMangaGamerInstruction(mgScriptLine, false));
                    }
                }
            }

            //If there are any leftovers (probably just mangagamer original instructions), just add them to the output.
            HandleChunk(workingInstructionAssociations, newChunk);

            // Extract BGM associations of mgBGM -> ps3BGM
            ExtractBGMAssociations(workingInstructionAssociations, counter);

            // Convert the instruction associations to regular mangagamer instructions for stage 2
            List<MangaGamerInstruction> outputStage1 = new List<MangaGamerInstruction>();
            foreach(var instructionAssociation in workingInstructionAssociations)
            {
                outputStage1.Add(instructionAssociation.mgOriginalInstruction);

                //check for a "failed" section. If failed section, just insert the whole section.
                bool failed = false;
                foreach (var inst in instructionAssociation.associatedPS3Instructions)
                {
                    if(inst is FailInstruction)
                    {
                        failed = true;
                    }
                }

                if(failed)
                {
                    outputStage1.AddRange(instructionAssociation.associatedPS3Instructions);
                    continue;
                }


                foreach (var ps3Inst in instructionAssociation.associatedPS3Instructions)
                {
                    MangaGamerInstruction outputInstruction = ps3Inst;
                    
                    switch(ps3Inst)
                    {
                        case MGPlayBGM ps3MGPlayBGM:
                            if (instructionAssociation.mgOriginalInstruction is MGPlayBGM)
                            {
                                MGPlayBGM mgPlayBGM = (MGPlayBGM)instructionAssociation.mgOriginalInstruction;
                                outputInstruction = mgPlayBGM.CloneWithFilename(ps3MGPlayBGM.bgmFileName, ps3MGPlayBGM.IsPS3()); 
                            }
                            break;

                        case MGPlaySE ps3MGPlaySE:
                            if (instructionAssociation.mgOriginalInstruction is MGPlaySE)
                            {
                                MGPlaySE mgPlaySE = (MGPlaySE)instructionAssociation.mgOriginalInstruction;
                                outputInstruction = mgPlaySE.CloneWithFilename(ps3MGPlaySE.filename, ps3MGPlaySE.IsPS3());
                            }
                            break;
                    }

                    outputStage1.Add(outputInstruction);
                }
            }

            // --------- Perform stage 2 filtering to 'tidy up' the script ---------
            //This section runs filters after all the other filters have run.
            //This stage converts the list of MangaGamerInstructions to a list of strings 
            List<string> outputStage2 = new List<string>();
            foreach (MangaGamerInstruction inst in outputStage1)
            {
                //TODO: correctly interpret mangagamer instructions instead of using regexes to determine the line typ
                if (inst.IsPS3())
                {
                    //Disable PS3 fade instructions for now
                    if (!(inst is MGFadeOutBGM))
                    {
                        //wrap all ps3-origin instructions in GAltBGMflow
                        outputStage2.Add($"\tif (GetGlobalFlag(GAltBGMflow) == 1) {{ {inst.GetInstruction()} }}");
                    }
                }
                else if(//fadeOutBGMMusicRegex.IsMatch(inst.GetInstruction()) || 
                        playBGMMusicRegex.IsMatch(inst.GetInstruction()) || 
                        playSERegex.IsMatch(inst.GetInstruction())) 
                {
                    //wrap only the above types of MG-origin instructions in GAltBGMflow
                    outputStage2.Add($"\tif (GetGlobalFlag(GAltBGMflow) == 0) {{ {inst.GetInstruction()} }}");
                }
                else
                {
                    //all other MG-origin instructions are output as-is
                    outputStage2.Add(inst.GetInstructionStandalone());
                }
            }

            // --------- Finally, write the output to file. ---------
            File.WriteAllLines(outputPath, outputStage2);
        }

        private static int PS3TimeConversion(int ps3Time)
        {
            return (int)Math.Round(ps3Time / 60.0 * 1000.0);
        }

        /// <summary>
        /// As per drdiablo's comments " I mapped channel 4 in the xml to channel 1 in the scripts, and 7 in xml to 0 in the scripts."
        /// 
        /// 0-1 looping sfx
        /// 2 background music
        /// 3 regular nonlooping sfx
        /// 4+ voices
        /// 
        /// </summary>
        /// <param name="ps3Chunk"></param>
        /// <returns></returns>
        private static List<MangaGamerInstruction> convertPS3InstructionsToMGInstructions(string ps3Chunk)
        {
            List<MangaGamerInstruction> instructionsList = new List<MangaGamerInstruction>();

            //Read through the ps3 chunk of xml and generate instruction objects for the targeted instructions
            PS3InstructionReader ps3Reader = new PS3InstructionReader(new StringReader(ps3Chunk));
            while (ps3Reader.AdvanceToNextInstruction())
            {
                switch (ps3Reader.reader.GetAttribute("type"))
                {
                    case "BGM_PLAY":
                        string bgmFileName = ps3Reader.reader.GetAttribute("bgm_file");
                        instructionsList.Add(new MGPlayBGM(2, bgmFileName, true));  //all PS3 BGM shall paly on channel 2
                        break;

                    case "BGM_FADE":
                        int durationBGMFade = PS3TimeConversion(Convert.ToInt32(ps3Reader.reader.GetAttribute("duration")));
                        instructionsList.Add(new MGFadeOutBGM(2, durationBGMFade, false, true));
                        break;

                    case "SFX_PLAY":
                        Debug.Assert(ps3Reader.reader.GetAttribute("single_play") == "1" || ps3Reader.reader.GetAttribute("single_play") == "0");

                        string sfx_file = ps3Reader.reader.GetAttribute("sfx_file");
                        int ps3_sfx_channel = Int32.Parse(ps3Reader.reader.GetAttribute("sfx_channel"));
                        bool ps3_single_play = ps3Reader.reader.GetAttribute("single_play") == "1";

                        if (ps3_single_play)
                        {
                            //a 'normal', non looping sound effect shall be played on channel 3
                            instructionsList.Add(new MGPlaySE(3, sfx_file, true));
                        }
                        else
                        {
                            if (ps3_sfx_channel == 4)
                            {
                                instructionsList.Add(new MGPlayBGM(1, sfx_file, true));
                            }
                            else if (ps3_sfx_channel == 7)
                            {
                                instructionsList.Add(new MGPlayBGM(0, sfx_file, true));
                            }
                            else if (ps3_sfx_channel == 5)
                            {
                                //TODO: figure out if this is correct behavior. Channel 5 only occurs a handful of times in the ps3 script
                                instructionsList.Add(new MGPlayBGM(4, sfx_file, true));
                            }
                            else
                            {
                                throw new Exception($"A ps3 sfx was found which is not on channel 4 or 7 (sorry, no debug output for this, please add it)");
                            }
                        }
                        
                        break;

                    case "CHANNEL_FADE":
                        //these 'bgm fades' actually fade looping sound effects
                        int durationFade = PS3TimeConversion(Convert.ToInt32(ps3Reader.reader.GetAttribute("duration")));
                        int fadeChannel = Int32.Parse(ps3Reader.reader.GetAttribute("channel"));

                        if (fadeChannel == 4)
                        {
                            instructionsList.Add(new MGFadeOutBGM(1, durationFade, false, true));
                        }
                        else if (fadeChannel == 7)
                        {
                            instructionsList.Add(new MGFadeOutBGM(0, durationFade, false, true));
                        }
                        else if (fadeChannel == 5)
                        {
                            instructionsList.Add(new MGFadeOutBGM(4, durationFade, false, true));
                        }
                        else if (fadeChannel == 0)
                        {
                            Console.WriteLine($"Ignoring channel 0 fade - these are non-looping SFX playback which don't need a fade.");
                        }
                        else
                        {
                            throw new Exception($"WARNING: unknown PS3 channel fade, chan {fadeChannel}");
                        }

                        break;

                }
            }

            return instructionsList;
        }

        /// <summary>
        /// This handles inserting BGMPlay and BGMFade instructions into the output.
        /// It assumes that 'partialLinesToOutput' is a partial list of instructions currently being generated, 
        /// where the 'end' of the list represents where the ps3 chunk is. 
        /// </summary>
        /// <param name="PS3InstructionsAsMGInstructions"></param>
        /// <param name="partialLinesToOutput"></param>
        /// <param name="bgmChannelNumber"></param>
        /*private static List<MangaGamerInstruction> ConsumeInsertBGMPlayAndFadeIntoOutput(List<MangaGamerInstruction> PS3InstructionsAsMGInstructions, List<MangaGamerInstruction> partialLinesToOutput, int bgmChannelNumber)
        {
            //Only insert only the last play/fadebgm instruction in the list
            MangaGamerInstruction lastFade = null;
            MangaGamerInstruction lastBGMPlay = null;

            List<MangaGamerInstruction> newPS3InstructionsAsMGInstructions = new List<MangaGamerInstruction>();

            foreach (MangaGamerInstruction mgInstruction in PS3InstructionsAsMGInstructions)
            {
                switch (mgInstruction)
                {
                    case MGPlayBGM playBGM:
                        DebugUtils.Print($"Found BGM play: {playBGM.GetInstruction()}");
                        lastBGMPlay = playBGM;
                        break;

                    case MGFadeOutBGM fadeBGM:
                        DebugUtils.Print($"Found BGM fade: {fadeBGM.GetInstruction()}");
                        lastFade = fadeBGM;
                        break;

                    default:
                        newPS3InstructionsAsMGInstructions.Add(mgInstruction);
                        break;
                }
            }

            //set PS3InstructionsAsMGInstructions empty so it can't be used anymore
            PS3InstructionsAsMGInstructions.Clear();

            //remember what the last instruction (fade or play bgm) - this is the instruction to be inserted
            MangaGamerInstruction lastFadeBGMOrPlayBGM = lastBGMPlay != null ? lastBGMPlay : lastFade;


            if (lastFadeBGMOrPlayBGM != null)
            {
                //When writing out instructions, need to add a \t otherwise game won't recognize it
                DebugUtils.Print($"In this chunk, selected: {lastFadeBGMOrPlayBGM.GetInstruction()}");

                //find a good spot to insert the instruction, depending on the type (either playBGM or FadeBGM)
                bool shouldFindPlayBGM = lastFadeBGMOrPlayBGM is MGPlayBGM;

                //search backwards in the current output until finding the insertion point regex (PlayBGM( or FadeOutBGM()
                //however if find a dialogue line, give up and just insert at the end of the list (where the ps3 xml is)
                for (int i = partialLinesToOutput.Count - 1; i > 0; i--)
                {
                    MangaGamerInstruction currentLine = partialLinesToOutput[i];
                    if (dialogueRegex.IsMatch(currentLine.GetInstruction()))
                    {
                        //insert at end of list
                        partialLinesToOutput.Add(lastFadeBGMOrPlayBGM);
                        break;
                    }
                    else if ((shouldFindPlayBGM && LineHasPlayBGMOnChannel(currentLine.GetInstruction(), bgmChannelNumber)) ||
                             (!shouldFindPlayBGM && LineHasFadeOutBGMOnChannel(currentLine.GetInstruction(), bgmChannelNumber)))
                    {
                        //replace similar instruction with this instruction
                        //partialLinesToOutput[i] = lastFadeBGMOrPlayBGM;

                        //for now, insert the playbgm just below the last similar playBGM instruction. Don't replace it.
                        partialLinesToOutput.Insert(i, lastFadeBGMOrPlayBGM);
                        break;
                    }
                }

            }

            return newPS3InstructionsAsMGInstructions;
        }*/

        //Some files use different BGM channels for music (as opposed to background sounds). Another
        //function should scan the file to determine the BGM channel, and set bgmChannelNumber appropriately
        private static void HandlePS3Chunk(string ps3Chunk, List<MangaGamerInstruction> out_partialLinesToOutput, int bgmChannelNumber)
        {
            //handle just the BGMPlay and BGMFade instructions.
            //List<MangaGamerInstruction> instructionsWithoutPlayOrFade =  ConsumeInsertBGMPlayAndFadeIntoOutput(convertPS3InstructionsToMGInstructions(ps3Chunk), out_partialLinesToOutput, bgmChannelNumber);

            List<MangaGamerInstruction> allInstructions = convertPS3InstructionsToMGInstructions(ps3Chunk);

            //other types of instructions are just inserted directly
            out_partialLinesToOutput.AddRange(allInstructions);
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
