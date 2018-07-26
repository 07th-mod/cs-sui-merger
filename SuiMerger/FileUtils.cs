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
        public static FileStream CreateDirectoriesAndOpen(string filePath, FileMode fm)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            return new FileStream(filePath, fm);
        }
    }
}
