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
            int contentLength = lastContentLine - firstContentLine;
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


            return content;
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
