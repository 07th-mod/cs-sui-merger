using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuiMerger.MergedScriptPostProcessing
{
    //base class for all MangaGamer Instructions
    abstract class MangaGamerInstruction
    {
        private readonly bool isPS3;   //use to flag which classes are ps3 
        private readonly bool noTab;

        protected MangaGamerInstruction(bool isPS3, bool noTab)
        {
            this.isPS3 = isPS3;
            this.noTab = noTab;
        }

        //gets the instruction, without the tab character or newline
        protected abstract string GetInstruction();

        //returns the instruction string with tab character
        public string GetInstructionForScript()
        {
            if (noTab)
            {
                return GetInstruction();
            }
            else
            {
                return $"\t{GetInstruction()}";
            }
        }

        //returns true if instruction originated from PS3 xml
        public bool IsPS3() => isPS3;

    }

    class MGFadeOutBGM : MangaGamerInstruction
    {
        readonly int channel;
        readonly int fadeTime;

        public MGFadeOutBGM(int channel, int ps3Duration, bool isPS3) : base(isPS3, false)
        {
            this.channel = channel;
            this.fadeTime = (int)Math.Round(ps3Duration / 60.0 * 1000.0);
        }

        protected override string GetInstruction()
        {
            return $"FadeOutBGM( {channel}, {fadeTime}, FALSE );";
        }
    }

    class MGPlayBGM : MangaGamerInstruction
    {
        readonly int channel;
        readonly string bgmFileName;

        public MGPlayBGM(int channel, string bgmFileName, bool isPS3) : base(isPS3, false)
        {
            this.channel = channel;
            this.bgmFileName = bgmFileName;
        }

        protected override string GetInstruction()
        {
            return $"PlayBGM( {channel}, \"{bgmFileName}\", 128, 0 );";
        }
    }

    class GenericInstruction : MangaGamerInstruction
    {
        readonly string data;

        public GenericInstruction(string data, bool isPS3) : base(isPS3, true)
        {
            this.data = data;
        }

        protected override string GetInstruction()
        {
            return data;
        }
    }
}
