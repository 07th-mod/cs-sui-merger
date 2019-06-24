using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SuiMerger
{
    class ForkingScriptMerger
    {
        static Regex modCallScriptSectionRegex = new Regex(@"ModCallScriptSection\(\s*""([^""]+)""\s*,\s*""([^""]+)""", RegexOptions.IgnoreCase);
        static Regex mergedSectionHeaderRegex = new Regex(@"//BEGIN_MERGED_SUBSCRIPT\|([^\|]*)\|(.*)", RegexOptions.IgnoreCase);

        public static List<string> MergeForkedScript(string scriptFolder, string fileName, bool useCensoredVersion)
        {
            List<string> mergedScriptLines = new List<string>();

            string scriptPath = Path.Combine(scriptFolder, fileName);
            string[] allLines = File.ReadAllLines(scriptPath);

            foreach (string line in allLines)
            {
                // Add the line from the original script
                mergedScriptLines.Add(line);

                //TODO: only want to insert the highest level censor script section!!!
                bool gotBasicMatch = line.Contains("ModCallScriptSection");
                Match match = modCallScriptSectionRegex.Match(line);
                bool gotRegexMatch = match.Success && match.Groups.Count == 3;
                if(gotBasicMatch && !gotRegexMatch)
                {
                    throw new Exception("Line contained ModCallScriptSection but regex was unable to match it!");
                }

                if(!gotRegexMatch)
                {
                    continue;
                }

                if(!line.Contains(">=") && !line.Contains("<="))
                {
                    throw new Exception("Line does not have a comparison operator - can't determine censoredness");
                }

                bool isCensoredVersion = line.Contains("<=");
                if(useCensoredVersion != isCensoredVersion)
                {
                    continue;
                }

                // We want to take the first ModCallSCriptSection of each chunk, whic is delimited by
                //  //VoiceMatching and //VoiceMAtchingEnd
                // To do this, only allow voice matching once per each "//VoiceMatching" delimiter
                string subScriptFileNameWithExt = match.Groups[1].Value + ".txt";
                string subScriptFunctionName = match.Groups[2].Value;
                Console.WriteLine($"{subScriptFileNameWithExt} -> {subScriptFunctionName}");

                // Try to load the subscript file
                string subScriptPath = Path.Combine(scriptFolder, subScriptFileNameWithExt);

                // Add the lines from the sub-script, along with some markers so we can unmerge it later
                mergedScriptLines.Add($"//BEGIN_MERGED_SUBSCRIPT|{subScriptFileNameWithExt}|{subScriptFunctionName}");
                mergedScriptLines.AddRange(GetScriptFunctionContent(subScriptPath, subScriptFunctionName));
                mergedScriptLines.Add($"//END_MERGED_SUBSCRIPT|{subScriptFileNameWithExt}|{subScriptFunctionName}");
            }

            return mergedScriptLines;
        }

        /// <summary>
        /// Retrieves the script content of a function ("functionName") in a script file ("fileName")
        /// Throws an exception indicating if the file or function couldn't be found
        /// </summary>
        /// <returns></returns>
        public static string[] GetScriptFunctionContent(string subScriptPath, string functionName)
        {
            if (!File.Exists(subScriptPath))
            {
                throw new Exception($"Couldn't find subscript at path {subScriptPath}");
            }

            string[] subScriptLines = File.ReadAllLines(subScriptPath);

            // Try to find the subscript function in the subscript file
            int firstContentLine = getFirstFunctionContentLine(subScriptLines, functionName, debug_subScriptPath: subScriptPath);
            int lastContentLine = getLastFunctionContentLine(subScriptLines, firstContentLine, debug_functionName: functionName, debug_subScriptPath: subScriptPath);

            // Copy the content lines in the file
            // Since this is an inclusive range, need to add 1 to get correct size.
            int contentLength = lastContentLine - firstContentLine + 1;
            string[] content = new string[contentLength];
            Array.Copy(subScriptLines, firstContentLine, content, 0, contentLength);

            return content;
        }

        /// <summary>
        /// Searches the input `scriptLines` for the function `functionToReplace`, then replaces the function content with `replacementContent`
        /// The input is not modified - a new string array is returned.
        /// </summary>
        /// <param name="scriptLines"></param>
        /// <param name="functionToReplace"></param>
        /// <param name="replacementContent"></param>
        /// <returns></returns>
        private static List<string> ReplaceScriptFunctionContent(IList<string> subScriptLines, string functionToReplace, IList<string> replacementContent)
        {
            // Try to find the subscript function in the subscript file
            int firstContentLine = getFirstFunctionContentLine(subScriptLines, functionToReplace);
            int lastContentLine = getLastFunctionContentLine(subScriptLines, firstContentLine, debug_functionName: functionToReplace);

            List<string> returnedValues = new List<string>();

            //copy everything before the function (NOT including the first content line)
            for(int i = 0; i < firstContentLine; i++)
            {
                returnedValues.Add(subScriptLines[i]);
            }

            //copy in the replacement content
            returnedValues.AddRange(replacementContent);

            //copy everything after the function (NOT including the last content line)
            for(int i = lastContentLine + 1; i < subScriptLines.Count(); i++)
            {
                returnedValues.Add(subScriptLines[i]);
            }

            return returnedValues;
        }

        private static int getFirstFunctionContentLine(IList<string> subScriptLines, string functionName, string debug_subScriptPath="Unknown path")
        {
            Regex functionDefinitionStartRegex = new Regex(functionName + @"\s*\(\s*\)", RegexOptions.IgnoreCase);

            bool gotFunctionDefinitionStart = false;
            int line_i = 0;
            for (; line_i < subScriptLines.Count(); line_i++)
            {
                if (functionDefinitionStartRegex.Match(subScriptLines[line_i]).Success)
                {
                    gotFunctionDefinitionStart = true;
                    break;
                }
            }

            if (!gotFunctionDefinitionStart)
            {
                throw new Exception($"Function {functionName}() couldn't be found in {debug_subScriptPath}");
            }

            //advance to to the next line, which should be a "{"
            line_i += 1;

            //the next line after the function definition start should be just a "{" - throw exception if it isn't
            if (subScriptLines[line_i].Trim() != "{")
            {
                throw new Exception($"Expected '{{' after {functionName}() in {debug_subScriptPath} but got {subScriptLines[line_i]}");
            }

            //advance to the actual content of the function
            line_i += 1;

            return line_i;
        }

        private static int getLastFunctionContentLine(IList<string> subScriptLines, int firstContentLine, string debug_functionName, string debug_subScriptPath="Unknown Path")
        {
            int line_i = firstContentLine;
            int? lastContentLine = null;

            //get all the lines until reaching a closing '}'. If another '{' on a line by itself is seen, then throw an exception as we don't handle parsing scopes properly
            for (; line_i < subScriptLines.Count(); line_i++)
            {
                string currentLine = subScriptLines[line_i];
                if (currentLine.Trim() == "{")
                {
                    throw new Exception($"Got a second lone '{{' in {debug_functionName}() in {debug_subScriptPath} - scope handling not implemented");
                }
                else if (currentLine.Trim() == "}")
                {
                    lastContentLine = line_i - 1;
                    break;
                }
            }

            // Check for missing closing brace
            if (lastContentLine == null)
            {
                throw new Exception($"Did not find closing '}}' in {debug_functionName}() in {debug_subScriptPath}");
            }

            return lastContentLine.Value;
        }

        /// <summary>
        /// Looks through the `mergedScript` for forked scripts, then unmerges them into their
        /// appropriate files. Needs to know where the original forked scripts are located
        /// </summary>
        /// <param name="forkedScriptSearchFolder"></param>
        /// <param name="mergedScript"></param>
        public static List<PartialSubScriptToMerge> GetForkedScriptContentFromMergedScript(string forkedScriptSearchFolder, string mergedScript)
        {
            List<PartialSubScriptToMerge> forkedScriptContentToMergeList = new List<PartialSubScriptToMerge>();

            string[] mergedScriptLines = File.ReadAllLines(mergedScript);

            bool inFunction = false;
            string scriptToUnmerge = null;
            string functionToUnmerge = null;
            List<string> functionContent = new List<string>();

            foreach (string line in mergedScriptLines)
            {
                Match m = mergedSectionHeaderRegex.Match(line);
                if(m.Success)
                {
                    scriptToUnmerge = m.Groups[1].ToString();
                    functionToUnmerge = m.Groups[2].ToString();
                    functionContent = new List<string>();
                    inFunction = true;
                }
                else if(line.Contains("//END_MERGED_SUBSCRIPT"))
                {
                    if(!inFunction)
                    {
                        throw new Exception("Got END_MERGED_SUBSCRIPT but no BEGIN_MERGED_SUBSCRIPT");
                    }

                    inFunction = false;
                    forkedScriptContentToMergeList.Add(
                        new PartialSubScriptToMerge(scriptToUnmerge, functionToUnmerge, functionContent)
                    );

                }
                else if(inFunction)
                {
                    functionContent.Add(line);
                }
            }

            return forkedScriptContentToMergeList;
        }

        public static void UnMergeForkedScripts(string inputSubScriptFolder, string outputFolder, List<PartialSubScriptToMerge> partialSubScriptToMergeList)
        {
            Dictionary<string, List<string>> scriptToScriptContentDict = new Dictionary<string, List<string>>();

            //Load all the forked scripts into memory, accessible by scriptName (force lowercase)
            foreach(var partialSubScript in partialSubScriptToMergeList)
            {
                string scriptPath = Path.Combine(inputSubScriptFolder, partialSubScript.scriptName);
                string lowercaseScriptFileName = partialSubScript.scriptName.ToLower();
                
                // Check if already loaded - if so skip this item
                if(scriptToScriptContentDict.ContainsKey(lowercaseScriptFileName))
                {
                    continue;
                }

                if(!File.Exists(scriptPath))
                {
                    throw new Exception($"Couldn't find sub-script {scriptPath}");
                }

                scriptToScriptContentDict.Add(lowercaseScriptFileName, File.ReadAllLines(scriptPath).ToList());
            }

            //For each forked script to merege:
            //  newLines = do ReplaceScriptFunctionContent(IList<string> subScriptLines (from script name), string functionToReplace, IList<string> replacementContent)
            //  replace that map entry with the newLines
            foreach(var partialSubScript in partialSubScriptToMergeList)
            {
                scriptToScriptContentDict[partialSubScript.scriptName.ToLower()] = 
                    ReplaceScriptFunctionContent(
                        scriptToScriptContentDict[partialSubScript.scriptName.ToLower()], 
                        partialSubScript.functionName, 
                        partialSubScript.functionContent
                    );
            }

            //Write out all the files to disk.
            foreach (KeyValuePair<string, List<string>> kvp in scriptToScriptContentDict)
            {
                string scriptPath = Path.Combine(outputFolder, kvp.Key);
                File.WriteAllLines(scriptPath, kvp.Value);
            }

            // Port changes made to the uncensored scripts
            foreach (KeyValuePair<string, List<string>> kvp in scriptToScriptContentDict)
            {
                string moreCensoredFileName = kvp.Key;
                string lessCensoredFileName = moreCensoredFileName.Replace("_vm00", "_vm0x");
                if(moreCensoredFileName == lessCensoredFileName)
                {
                    throw new Exception("Couldn't determine name of less censored sub script");
                }

                string originalScriptPath = Path.Combine(inputSubScriptFolder, moreCensoredFileName);
                string modifiedScriptPath = Path.Combine(outputFolder, moreCensoredFileName);

                string subScriptToPortPath = Path.Combine(inputSubScriptFolder, lessCensoredFileName);
                string outputScriptPath = Path.Combine(outputFolder, lessCensoredFileName);

                CopyChangesFromOneSubscriptToAnother(originalScriptPath, modifiedScriptPath, subScriptToPortPath, outputScriptPath);
            }
        }

        public static void CopyChangesFromOneSubscriptToAnother(string originalSubScriptPath, string modifiedSubScriptPath, string subScriptToPortToPath, string outputScriptPath)
        {
            const string marker = "|||ADDED_LINE";

            //Do a diff between "originalSubScriptPath" and "modifedSubScriptPath". Lines with a "+" are added lines.
            //Convert lines with a "+" to "|||ADDED_LINE" (if there are any '-' lines it is an error). Trim the first character of every line of the diff
            List<string> origToModifiedDiff = Differ.RunDiffToolNoHeader(originalSubScriptPath, modifiedSubScriptPath);

            List<string> fixedLines = new List<string>();
            foreach (string line in origToModifiedDiff)
            {
                char c = line[0];
                string restOfLine = line.Substring(1);
                if(c == '+')
                {
                    fixedLines.Add(marker + "+" + restOfLine);
                }
                else if(c == '-')
                {
                    fixedLines.Add(marker + "-" + restOfLine);
                }
                else if(c == ' ')
                {
                    fixedLines.Add(restOfLine);
                }
                else
                {
                    throw new Exception("Diff resulted in a removed or other type of line!");
                }
            }

            //Write out the diff to a temp file so it can be consumed by the differ
            string tempFilePath = Path.GetTempFileName();
            try
            {
                File.WriteAllLines(tempFilePath, fixedLines);

                //Do a diff that takes you FROM the less censored script TO the more censored script.
                List<string> modifiedToSubScriptToPortDiff = Differ.RunDiffToolNoHeader(subScriptToPortToPath, tempFilePath);
                List<string> outputScript = new List<string>();

                //Keep all " "  and "-" lines. Throw away any "+" lines, unless they start with "|||ADDED_LINE". Save to outputScriptPath
                foreach (string line in modifiedToSubScriptToPortDiff)
                {
                    char c = line[0];
                    string restOfLine = line.Substring(1);

                    if(restOfLine.StartsWith(marker))
                    {
                        // If a line is marked, apply the changes in the payload (don't obey the outer level change type)
                        char payloadType = restOfLine[marker.Length];
                        string payload = restOfLine.Substring(marker.Length + 1);

                        if (payloadType == '+')
                        {
                            //new line to be added
                            outputScript.Add(payload); //"SHOULD ADD:" + payload);
                        }
                        else if(payloadType == '-')
                        {
                            if (outputScript[outputScript.Count - 1] == payload)
                            {
                                outputScript.RemoveAt(outputScript.Count - 1);
                            }
                            else
                            {
                                throw new Exception("Couldn't port change from censored to uncensored script?");
                            }

                            //previous line to be removed if identical
                            //outputScript.Add("REMOVED:" + payload);
                        }
                        else
                        {
                            throw new Exception($"Unexpected payload diff type for line {line}");
                        }
                    }
                    else
                    {
                        // If a line is not marked, "undo" the changes by keeping original
                        if(c == '+')
                        {
                            //Console.WriteLine($"Throwing away {line}");
                        }
                        else if(c == '-' || c == ' ')
                        {
                            outputScript.Add(restOfLine);
                        }
                        else
                        {
                            throw new Exception($"Unexpected diff type for line {line}");
                        }
                    }
                }

                File.WriteAllLines(outputScriptPath, outputScript);
            }
            finally
            {
                File.Delete(tempFilePath);
            }
        }

    }

    class PartialSubScriptToMerge
    {
        public readonly string scriptName;
        public readonly string functionName;
        public readonly List<string> functionContent;

        public PartialSubScriptToMerge(string scriptName, string functionName, List<string> functionContent)
        {
            this.scriptName = scriptName;
            this.functionName = functionName;
            this.functionContent = functionContent;
        }
    }


}
