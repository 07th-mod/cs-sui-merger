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
        public static StreamWriter CreateDirectoriesAndOpen(string filePath, FileMode fm)
        {
            string enclosingDirectory = Path.GetDirectoryName(filePath);
            if (enclosingDirectory != null && enclosingDirectory.CompareTo("") != 0)
            {
                Directory.CreateDirectory(enclosingDirectory);
            }
            StreamWriter sw = new StreamWriter(new FileStream(filePath, fm))
            {
                NewLine = Config.newline.ToString()
            };
            return sw;
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
