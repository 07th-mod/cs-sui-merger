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
        private readonly bool isPS3;   //True if instruction was generated from ps3 script, False otherwise.

        protected MangaGamerInstruction(bool isPS3, bool noTab)
        {
            this.isPS3 = isPS3;
        }

        //gets the instruction, without the tab character or newline
        public abstract string GetInstruction();

        //gets the instruction, appending 
        public abstract string GetInstructionStandalone();

        //returns true if instruction originated from PS3 xml
        public bool IsPS3() => isPS3;

        public GenericInstruction ToGenericInstruction()
        {
            return new GenericInstruction(GetInstruction(), IsPS3());
        }
    }

    /// <summary>
    /// Represents a MangaGamer PlaySE call
    /// For example: PlaySE(3, "jakinimititahaikyo", 256, 64);
    /// </summary>
    class MGPlaySE : MangaGamerInstruction
    {
        public readonly int channel;
        public readonly string filename; //filename does not include file extension
        public readonly int volume;
        public readonly int panning;

        public MGPlaySE(int channel, string filename, int volume, int panning, bool isPS3) : base(isPS3, false)
        {
            this.channel = channel;   //game scripts seem to use channel 3 for playing sound effects
            this.filename = filename;
            this.volume = volume;     //default volume - not sure what ranges it is
            this.panning = panning;   //default panning - not sure what ranges it is from-to?
        }

        public MGPlaySE(int channel, string filename, bool isPS3) : base(isPS3, false)
        {
            this.channel = channel; //game scripts seem to use channel 3 for playing sound effects
            this.filename = filename;
            volume = 256; //default volume - not sure what ranges it is
            panning = 64; //default panning - not sure what ranges it is from-to?
        }

        public override string GetInstruction()
        {
            string ps3Prefix = IsPS3() ? "ps3/" : String.Empty;
            return $"PlaySE( {channel}, \"{ps3Prefix}{filename}\", {volume}, {panning} );";
        }

        public override string GetInstructionStandalone()
        {
            return "\t" + GetInstruction();
        }

        public MGPlaySE CloneWithFilename(string filename, bool isPS3)
        {
            return new MGPlaySE(this.channel, filename, this.volume, this.panning, isPS3);
        }
    }

    class MGFadeOutBGM : MangaGamerInstruction
    {
        public readonly int channel;
        public readonly int fadeTime;
        public readonly bool unkBool;

        public MGFadeOutBGM(int channel, int fadeTime, bool unkBool, bool isPS3) : base(isPS3, false)
        {
            this.channel = channel;
            this.fadeTime = fadeTime;
            this.unkBool = unkBool;
        }

        public override string GetInstruction()
        {
            string boolString = unkBool ? "TRUE" : "FALSE";
            return $"FadeOutBGM( {channel}, {fadeTime}, {boolString} );";
        }

        public override string GetInstructionStandalone()
        {
            return "\t" + GetInstruction();
        }
    }

    class MGPlayBGM : MangaGamerInstruction
    {
        public readonly int channel;
        public readonly string bgmFileName;
        public readonly int pan;
        public readonly int unk;

        public MGPlayBGM(int channel, string bgmFileName, int pan, int unk, bool isPS3) : base(isPS3, false)
        {
            this.channel = channel;
            this.bgmFileName = bgmFileName;
            this.pan = pan;
            this.unk = unk;
        }

        public MGPlayBGM(int channel, string bgmFileName, bool isPS3) : base(isPS3, false)
        {
            this.channel = channel;
            this.bgmFileName = bgmFileName;
            this.pan = 128;
            this.unk = 0;
        }

        public override string GetInstruction()
        {
            string ps3Prefix = IsPS3() ? "ps3/" : String.Empty;
            return $"PlayBGM( {channel}, \"{ps3Prefix}{bgmFileName}\", {pan}, {unk} );";
        }

        public override string GetInstructionStandalone()
        {
            return "\t" + GetInstruction();
        }
        
        public MGPlayBGM CloneWithFilename(string filename, bool isPS3)
        {
            return new MGPlayBGM(this.channel, filename, this.pan, this.unk, isPS3);
        }
    }

    class GenericInstruction : MangaGamerInstruction
    {
        readonly string data;

        public GenericInstruction(string data, bool isPS3) : base(isPS3, true)
        {
            this.data = data;
        }

        public override string GetInstruction()
        {
            return data.TrimStart();
        }

        public override string GetInstructionStandalone()
        {
            return data;
        }
    }

    class FailInstruction : GenericInstruction
    {
        public FailInstruction(string data, bool isPS3) : base(data, isPS3)
        {
        }
    }
}
