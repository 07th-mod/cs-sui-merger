using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SuiMerger.MergedScriptPostProcessing
{
    /// <summary>
    /// The output from the main SuiMerger produces a text file which is the original
    /// MG script but with the relevant PS3 Instructions merged into it. 
    /// 
    /// This class consumes lines from the merged script file one line at a time. Once it
    /// has consumed enough lines to form a ps3 instructions chunk, it returns the entire chunk
    /// all at once. Otherwise, it returns null.
    /// 
    /// Example
    /// 
    /// asdfasdfasdfsfd
    /// asdfasdasdf
    /// <?xml version="1.0" encoding="UTF-8"?>
    /// <PS3_SECTION>  <!-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~START~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ -->
    /// <ins type="MIX_CHANNEL_FADE" duration="60"></ins>
    /// </PS3_SECTION> <!-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~END~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ -->      //only here will the chunk be returned
    /// 
    /// </summary>
    class PS3XMLChunkFinder
    {
        static Regex ps3Start = new Regex(@"<?xml", RegexOptions.IgnoreCase);
        static Regex ps3End = new Regex(@"</PS3_SECTION", RegexOptions.IgnoreCase);

        /// <summary>
        /// The function takes the path to a merged MG/PS3 Script file
        /// As output it produces the chunks MG / PS3 chunks of the script in the order they appear in the script.
        /// The chunks are returned as a list of Chunk objects which indicate themselves whether they are a MG or a PS3 type chunk.
        /// The output lines do not contain line endings.
        /// </summary>
        /// <param name="mergedMGScriptPath"></param>
        /// <returns></returns>
        public static List<Chunk> GetAllChunksFromMergedScript(string mergedMGScriptPath)
        {
            //initialize the chunk list with one chunk, which is assumed to be MG type chunk
            List<Chunk> chunks = new List<Chunk>
            {
                new Chunk(isPS3Chunk: false)
            };

            //define a local function which returns the last chunk
            Chunk lastChunk() => chunks[chunks.Count - 1];

            using (StreamReader mgScript = new StreamReader(mergedMGScriptPath, Encoding.UTF8))
            {
                string mergedScriptLine;
                while ((mergedScriptLine = mgScript.ReadLine()) != null)
                {
                    //if you're not in a ps3 chunk:
                    //  enter ps3 chunk if you see the ps3 chunk start marker
                    //must do this before current line is added to chunk to include start marker in chunk
                    if (!lastChunk().isPS3Chunk && ps3Start.IsMatch(mergedScriptLine))
                    {
                        chunks.Add(new Chunk(isPS3Chunk: true));
                    }

                    //add the current line to the current chunk (the last chunk on the list)
                    lastChunk().lines.Add(mergedScriptLine);

                    //if you are in a ps3 chunk:
                    //  exit from ps3 chunk if you see the ps3 chunk end marker
                    //must do this after current line is added to chunk to include end marker in chunk
                    if (lastChunk().isPS3Chunk && ps3End.IsMatch(mergedScriptLine))
                    {
                        chunks.Add(new Chunk(isPS3Chunk: false));
                    }
                }

                return chunks;
            }
        }

        /// <summary>
        /// Represents a single MangaGamer or PS3 chunk in the merged script, 
        /// Consists of multiple lines, and a tag indicating the type of chunk (MG or PS3) 
        /// </summary>
        public class Chunk
        {
            public List<string> lines;
            public bool isPS3Chunk;

            public Chunk(bool isPS3Chunk)
            {
                lines = new List<string>();
                this.isPS3Chunk = isPS3Chunk;
            }

            /// <summary>
            /// For debugging - print the contents and type of the chunk
            /// </summary>
            /// <returns></returns>
            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"Begin {(isPS3Chunk ? "PS3" : "MangaGamer")} Chunk:");

                foreach (var line in lines)
                {
                    sb.AppendLine(line);
                }

                return sb.ToString();
            }
        }
    }

}
