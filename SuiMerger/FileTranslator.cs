using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuiMerger
{
    public class FileTranslator
    {
        //static string dialogue_line_marker = "<ins type=\"DIALOGUE\"";
        static string hwk = "｢｣ｧｨｩｪｫｬｭｮｱｲｳｴｵｶｷｸｹｺｻｼｽｾｿﾀﾁﾂﾃﾄﾅﾆﾇﾈﾉﾊﾋﾌﾍﾎﾏﾐﾑﾒﾓﾔﾕﾖﾗﾘﾙﾚﾛﾜｦﾝｰｯ､ﾟﾞ･?｡";
        static string hira = "「」ぁぃぅぇぉゃゅょあいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをんーっ、？！…　。";

        /// <summary>
        /// Convert a ps3 mangled string into proper hiragana
        /// </summary>
        public static string TranslateString(string inputString)
        {
            string newline = inputString;

            //apply translation to string (this is very slow, but it works.)
            for (int i = 0; i < hwk.Length; i++)
            {
                char original_char = hwk[i];
                char replacement_char = hira[i];
                newline = newline.Replace(original_char, replacement_char);
                // Console.WriteLine($"{i}: Replacing {original_char.ToString()} with {replacement_char.ToString()}");
            }

            return newline;
        }

        /*public static void Translate(string inputFilePath, string outputFilePath)
        {
            Console.WriteLine("Begin Translation...");
            //read in whole file as array of lines
            string[] allText = File.ReadAllLines(inputFilePath);

            List<string> translatedFile = new List<string>();

            foreach (string line in allText)
            {
                //Only process dialogue type lines
                translatedFile.Add(line.Contains(dialogue_line_marker) ? TranslateString(line) : line);
            }

            //save file
            File.WriteAllLines(outputFilePath, translatedFile);
            Console.WriteLine("Translation Finished");
        }*/
    }
}
