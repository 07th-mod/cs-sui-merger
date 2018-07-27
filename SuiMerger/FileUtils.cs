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
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            StreamWriter sw = new StreamWriter(new FileStream(filePath, fm))
            {
                NewLine = Config.newline.ToString()
            };
            return sw;
        }
    }
}
