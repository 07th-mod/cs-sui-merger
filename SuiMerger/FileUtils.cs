using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuiMerger
{
    class FileUtils
    {
        /// <summary>
        /// Create and open a file, even if the path to the file does not exist, by
        /// creating all required directories for the file.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="fm"></param>
        /// <returns></returns>
        public static StreamWriter CreateDirectoriesAndOpen(string filePath, FileMode fm)
        {
            CreateDirectoryForFile(filePath);
            StreamWriter sw = new StreamWriter(new FileStream(filePath, fm))
            {
                NewLine = Config.newline.ToString()
            };
            return sw;
        }

        /// <summary>
        /// Make sure the path for a file exists by creating all required directories for it
        /// </summary>
        /// <param name="filePath"></param>
        public static void CreateDirectoryForFile(string filePath)
        {
            string enclosingDirectory = Path.GetDirectoryName(filePath);
            if (enclosingDirectory != null && enclosingDirectory.CompareTo("") != 0)
            {
                Directory.CreateDirectory(enclosingDirectory);
            }
        }

        /// <summary>
        /// Writes a list of strings to file, but uses Config.newline as the newline separator
        /// </summary>
        /// <param name="outputPath"></param>
        /// <param name="lines"></param>
        public static void WriteAllLinesCustomNewline(string outputPath, List<string> lines)
        {
            using (StreamWriter outputFile = FileUtils.CreateDirectoriesAndOpen(outputPath, FileMode.Create))
            {
                foreach (string line in lines)
                {
                    outputFile.WriteLine(line);
                }
            }
        }

        //From https://stackoverflow.com/questions/703281/getting-path-relative-to-the-current-working-directory
        public static string GetRelativePath(string filespec, string folder)
        {
            Uri pathUri = new Uri(filespec);
            // Folders must end in a slash
            if (!folder.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                folder += Path.DirectorySeparatorChar;
            }
            Uri folderUri = new Uri(folder);
            return Uri.UnescapeDataString(folderUri.MakeRelativeUri(pathUri).ToString().Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
