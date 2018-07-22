﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuiMerger
{
    public class StringUtils
    {
        public static bool CharIsJapanese(char c)
        {
            int c_codepoint = (int)c; //NOTE: doesn't work for >16 bit codepoints (> 0xFFFF)
            return c_codepoint >= 0x3040 && c_codepoint <= 0x9faf;
        }

        public static bool CharIsASCII(char c)
        {
            int c_codepoint = (int)c; //NOTE: doesn't work for >16 bit codepoints (> 0xFFFF)
            return c_codepoint <= 0xFF;
        }

        public static void WriteString(FileStream fs, string s)
        {
            byte[] stringAsBytes = new UTF8Encoding(true).GetBytes(s);
            fs.Write(stringAsBytes, 0, stringAsBytes.Length);
        }

        public static void WriteStringList(FileStream fs, IEnumerable<string> strings, bool forceNewline)
        {
            if (forceNewline)
            {
                foreach (string s in strings)
                {
                    WriteString(fs, s.TrimEnd() + '\n');
                }
            }
            else
            {
                foreach (string s in strings)
                {
                    WriteString(fs, s);
                }
            }
        }
    }
}
