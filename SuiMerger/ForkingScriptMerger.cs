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
        Regex modCallScriptSectionRegex = new Regex(@"ModCallScriptSection\(\s*""([^""]+)""\s*,\s*""([^""]+)""", RegexOptions.IgnoreCase);

        public List<string> MergeForkedScript(string scriptFolder, string fileName, bool useUncencoredVersion)
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

                bool isUncensoredVersion = line.Contains(">=");
                if(useUncencoredVersion != isUncensoredVersion)
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
        public string[] GetScriptFunctionContent(string subScriptPath, string functionName)
        {
            if (!File.Exists(subScriptPath))
            {
                throw new Exception($"Couldn't find subscript at path {subScriptPath}");
            }

            string[] subScriptLines = File.ReadAllLines(subScriptPath);

            Regex functionDefinitionStartRegex = new Regex(functionName + @"\s*\(\s*\)", RegexOptions.IgnoreCase);

            // Try to find the subscript function in the subscript file
            int line_i = 0;
            int? functionDefinitionStart = null;
            for(; line_i < subScriptLines.Count(); line_i++)
            {
                if(functionDefinitionStartRegex.Match(subScriptLines[line_i]).Success)
                {
                    functionDefinitionStart = line_i;
                    break;
                }
            }
            line_i++;

            if(functionDefinitionStart == null)
            {
                throw new Exception($"Function {functionName}() couldn't be found in {subScriptPath}");
            }

            //the next line after the function definition start should be just a "{" - throw exception if it isn't
            if(subScriptLines[line_i].Trim() != "{")
            {
                throw new Exception($"Expected '{{' after {functionName}() in {subScriptPath} but got {subScriptLines[line_i]}");
            }
            line_i++;

            int firstContentLine = line_i;
            int? lastContentLine = null;

            //get all the lines until reaching a closing '}'. If another '{' on a line by itself is seen, then throw an exception as we don't handle parsing scopes properly
            for (; line_i < subScriptLines.Count(); line_i++)
            {
                string currentLine = subScriptLines[line_i];
                if(currentLine.Trim() == "{")
                {
                    throw new Exception($"Got a second lone '{{' in {functionName}() in {subScriptPath} - scope handling not implemented");
                }
                else if(currentLine.Trim() == "}")
                {
                    lastContentLine = line_i - 1;
                    break;
                }
            }

            // Check for missing closing brace
            if (lastContentLine == null)
            {
                throw new Exception($"Did not find closing '}}' in {functionName}() in {subScriptPath}");
            }

            int contentLength = lastContentLine.Value - firstContentLine;
            string[] content = new string[contentLength];

            Array.Copy(subScriptLines, firstContentLine, content, 0, contentLength);

            return content;
        }

    }
}
