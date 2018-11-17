using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuiMerger
{
    public class Differ
    {
        /// <summary>
        /// This function keeps only japanese characters in a string according to the following criteria:
        /// - keep only japanese characters
        /// - remove spaces
        ///
        ///   TYPE                                                      KEEP
        /// - is_japanese_punct = in_range(code_point, 0x3000, 0x303f)  NO
        /// - is_hiragana = in_range(code_point, 0x3040, 0x309f)        YES
        /// - is_katakana = in_range(code_point, 0x30a0, 0x30ff)        YES
        /// - is_ideograph = in_range(code_point, 0x4e00, 0x9faf)       YES
        /// 
        /// - this function should also remove newlines etc.
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string PrepareStringForDiff(string s)
        {
            StringBuilder sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (StringUtils.CharIsJapanese(c))
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// This function calls `git diff` on the two input files - you need git installed
        /// for it to work.
        /// Note: this function may not work on files with greater than 1_000_000 lines
        /// </summary>
        /// <param name="inputPathA"></param>
        /// <param name="inputPathB"></param>
        /// <returns></returns>
        public static string RunDiffTool(string inputPathA, string inputPathB)
        {
            string command = "git";
            string arguments = $"diff --no-index --ignore-blank-lines -w -U1000000 \"{inputPathA}\" \"{inputPathB}\"";
            DebugUtils.Print($"Command: [{command}] Arguments: [{arguments}]");
            ProcessStartInfo psi = new ProcessStartInfo
            {
                Arguments = arguments,
                FileName = command,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                RedirectStandardError = true,
            };
            
            Process p = Process.Start(psi);

            //diff stderr->console, diff stdout->string
            void GitDiffErrorDataReceived(object sender, DataReceivedEventArgs e)
            {
                Console.WriteLine($"Git Diff: {e.Data }\n");
            }
            p.ErrorDataReceived += GitDiffErrorDataReceived;
            p.BeginErrorReadLine();
            string output = p.StandardOutput.ReadToEnd();

            p.WaitForExit();
            if (p.ExitCode != 0 && p.ExitCode != 1) //I think exit code 1 is when there is a warning printed by git
            {
                throw new Exception($"Git returned non-zero or one exit code: {p.ExitCode}");
            }

            return output;
        }

        /// <summary>
        /// This function writes out a list of dialogue, but performs preprocesses all the dialogue so it's nicely formatted for the diff
        /// NOTE: this function adds a newline to the end of each string.
        /// </summary>
        /// <param name="dialogues"></param>
        /// <param name="outputFileName"></param>
        static public void WriteListOfDialogueToFile(IEnumerable<DialogueBase> dialogues, string outputFileName)
        {
            //write the diff-prepared manga gamer dialogue to a file
            using (StreamWriter sw = FileUtils.CreateDirectoriesAndOpen(outputFileName, FileMode.Create))
            {
                foreach (DialogueBase line in dialogues)
                {
                    string preprocessedLine = line.data;

                    preprocessedLine = PrepareStringForDiff(preprocessedLine);

                    sw.WriteLine(preprocessedLine);
                }
            }
        }

        //This function performs the diff given the two lists of dialogue.
        //It then UPDATES the values in the mangaGamerDialogueList and the ps3DialogueList (the DialogueBase.other value is updated on each dialogue object!)
        //If a dialogue cannot be associated, it is set to NULL.
        public static List<AlignmentPoint> DoDiff(string tempFolderPath, List<MangaGamerDialogue> mangaGamerDialogueList, List<PS3DialogueInstruction> ps3DialogueList, out List<PS3DialogueFragment> dummyPS3Instructions, string debugFilenamePrefix = "")
        {
            //Convert PS3 Dialogue list into list of subsections before performing diff - this can be re-assembled later!
            string mangaGamerDiffInputPath = Path.Combine(tempFolderPath, debugFilenamePrefix + "_diffInputA.txt");
            string PS3DiffInputPath = Path.Combine(tempFolderPath, debugFilenamePrefix + "_diffInputB.txt");
            string diffOutputPath = Path.Combine(tempFolderPath, debugFilenamePrefix + "_diffOutput.txt");

            //Generate dummy mangaGamerDialogues here
            dummyPS3Instructions = new List<PS3DialogueFragment>();
            int ps3DialogueIndex = 0;
            foreach (PS3DialogueInstruction ps3Dialogue in ps3DialogueList)
            {
                List<string> splitDialogueStrings = PS3DialogueTools.SplitPS3StringNoNames(ps3Dialogue.data);
                PS3DialogueFragment previousPS3DialogueFragment = null;
                for (int i = 0; i < splitDialogueStrings.Count; i++)
                {
                    //dummy instructions index into the ps3DialogueList (for now...)
                    PS3DialogueFragment ps3DialogueFragment = new PS3DialogueFragment(ps3Dialogue, splitDialogueStrings[i], i, previousPS3DialogueFragment);
                    dummyPS3Instructions.Add(ps3DialogueFragment);
                   previousPS3DialogueFragment = ps3DialogueFragment;
                }
                ps3DialogueIndex++;
            }

            //write the diff-prepared manga gamer dialogue to a file
            WriteListOfDialogueToFile(mangaGamerDialogueList, mangaGamerDiffInputPath);

            //write the diff-prepared ps3 dialogue to a file
            WriteListOfDialogueToFile(dummyPS3Instructions, PS3DiffInputPath);

            //do the diff
            string diffResult = RunDiffTool(mangaGamerDiffInputPath, PS3DiffInputPath);

            //save the diff to file for debugging
            using (StreamWriter sw = FileUtils.CreateDirectoriesAndOpen(diffOutputPath, FileMode.Create))
            {
                sw.Write(diffResult);
            }

            List<AlignmentPoint> alignmentPoints = new List<AlignmentPoint>();
            using (StringReader reader = new StringReader(diffResult))
            {
                string line;
                //skip the header information
                while ((line = reader.ReadLine()) != null)
                {
                    if (line[0] == '@')
                        break;
                }

                //TODO: need to think of the best way to categorize all mangagamer lines...
                //a ' ' means lines are the same
                //a '+' means line is present in PS3 ONLY           (it was 'added' in the ps3 version)
                //a '-' means line is present in mangagamer ONLY    (it was 'removed' from the ps3 version)
                List<AlignmentPoint> unmatchedSequence = new List<AlignmentPoint>();
                int mgIndex = 0;
                int ps3Index = 0;
                while ((line = reader.ReadLine()) != null)
                {
                    //classify the line type:
                    char lineType = line[0];
                    if(lineType == ' ') //lines match
                    {
                        PS3DialogueFragment dummyPS3Instruction = dummyPS3Instructions[ps3Index];
                        MangaGamerDialogue currentMangaGamer = mangaGamerDialogueList[mgIndex];

                        //associate the fragment with the mangagamer dialogue
                        currentMangaGamer.Associate(dummyPS3Instruction);

                        //re-match the unmatched sequence, then clear it for next iteration
                        alignmentPoints.AddRange(DialogueReMatcher.ReMatchUnmatchedDialogue(unmatchedSequence));
                        unmatchedSequence.Clear();

                        //add the just found instruction
                        alignmentPoints.Add(new AlignmentPoint(currentMangaGamer, dummyPS3Instruction));

                        mgIndex++;
                        ps3Index++;
                    }
                    else if(lineType == '+') //only exist in ps3
                    {
                        PS3DialogueFragment currentDialog = dummyPS3Instructions[ps3Index];
                        unmatchedSequence.Add(new AlignmentPoint(null, currentDialog));
                        ps3Index++;
                    }
                    else if(lineType == '-') //only exist in mangagamer
                    {
                        MangaGamerDialogue currentDialog = mangaGamerDialogueList[mgIndex];
                        unmatchedSequence.Add(new AlignmentPoint(currentDialog, null));
                        mgIndex++;
                    }
                }

                //Deal with any leftover unmatched sequences at the end of the file
                alignmentPoints.AddRange(DialogueReMatcher.ReMatchUnmatchedDialogue(unmatchedSequence));
            }

            return alignmentPoints;
        }
    }
}
