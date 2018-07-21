using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuiMerger
{
    // Holds the data for a single Dialogue instruction (from the XML file).
    // Automatically translates the string portion in the constructor
    public class PS3DialogueInstruction
    {
        int num;
        int dlgtype;
        string data; //raw string data from the translated XML file

        public PS3DialogueInstruction(int num, int dlgtype, string data, bool autoTranslate = true)
        {
            this.num = num;
            this.dlgtype = dlgtype;
            this.data = autoTranslate ? FileTranslator.TranslateString(data) : data;
        }
    }
}
