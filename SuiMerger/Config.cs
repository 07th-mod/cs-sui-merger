using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuiMerger
{
    class Config
    {
        public static string newline = "\r\n";

        //enable to ignore any lines which consist only of japanese punctuation when performing diff operations
        public static bool DIFF_IGNORE_JAPANESE_PUNCTUATION_ONLY_LINES = false;
    }
}
