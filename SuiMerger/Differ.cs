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
        /// This function keeps only japanese characters (and sometimes punctuation) in a string according to the following criteria:
        /// - keep only japanese characters
        /// - remove spaces
        ///
        ///   TYPE                                                      KEEP
        /// - is_japanese_punct = in_range(code_point, 0x3000, 0x303f)  ONLY IF NO JAPANESE CHARACTERS ON LINE
        /// - is_hiragana = in_range(code_point, 0x3040, 0x309f)        YES
        /// - is_katakana = in_range(code_point, 0x30a0, 0x30ff)        YES
        /// - is_ideograph = in_range(code_point, 0x4e00, 0x9faf)       YES
        /// 
        /// - this function should also remove newlines etc.
        /// 
        /// Punctuation is ignored to improve matching, unless the line consists entirely of puncuation, in which case punctuation is preserved
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        /// TODO: if mangagamer script uses one method of filtering, and PS3 uses another method...that may result in a wrong match when it really should match.
        public static string PrepareStringForDiff(string s)
        {
            {
                StringBuilder sb = new StringBuilder(s.Length);
                foreach (char c in s)
                {
                    if (StringUtils.CharIsJapaneseCharacter(c))
                    {
                        sb.Append(c);
                    }
                }

                string japaneseCharactersOnlyString = sb.ToString();

                if(Config.DIFF_IGNORE_JAPANESE_PUNCTUATION_ONLY_LINES)
                {
                    //return the line even if it is of length zero - line is probably
                    //just japanese punctuation, or \n\n or some other formatting stuff
                    return japaneseCharactersOnlyString;
                }

                if (japaneseCharactersOnlyString.Length > 0)
                {
                    return japaneseCharactersOnlyString;
                }
            }

            //This section only executes if there are no japanese characters in the string
            {
                StringBuilder sb = new StringBuilder(s.Length);
                foreach (char c in s)
                {
                    if (StringUtils.CharIsJapaneseCharacterOrPunctuationOrFullHalfWidth(c))
                    {
                        sb.Append(c);
                    }
                }
                return sb.ToString();
            }
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

        public static List<string> RunDiffToolNoHeader(string inputPathA, string inputPathB)
        {
            string resultString = RunDiffTool(inputPathA, inputPathB);

            List<string> retval = new List<string>();
            using (StringReader reader = new StringReader(resultString))
            {
                string line;
                //skip the header information
                while ((line = reader.ReadLine()) != null)
                {
                    if (line[0] == '@')
                        break;
                }

                while ((line = reader.ReadLine()) != null)
                {
                    retval.Add(line);
                }
            }

            return retval;
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
        public static List<AlignmentPoint> DoDiff(string tempFolderPath, List<MangaGamerDialogue> mangaGamerDialogueList, List<PS3DialogueFragment> ps3DialogueFragments, string debugFilenamePrefix = "")
        {
            //Convert PS3 Dialogue list into list of subsections before performing diff - this can be re-assembled later!
            string mangaGamerDiffInputPath = Path.Combine(tempFolderPath, debugFilenamePrefix + "_diffInputA.txt");
            string PS3DiffInputPath = Path.Combine(tempFolderPath, debugFilenamePrefix + "_diffInputB.txt");
            string diffOutputPath = Path.Combine(tempFolderPath, debugFilenamePrefix + "_diffOutput.txt");

            //write the diff-prepared manga gamer dialogue to a file
            WriteListOfDialogueToFile(mangaGamerDialogueList, mangaGamerDiffInputPath);

            //write the diff-prepared ps3 dialogue to a file
            WriteListOfDialogueToFile(ps3DialogueFragments, PS3DiffInputPath);

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
                        PS3DialogueFragment dummyPS3Instruction = ps3DialogueFragments[ps3Index];
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
                        PS3DialogueFragment currentDialog = ps3DialogueFragments[ps3Index];
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
