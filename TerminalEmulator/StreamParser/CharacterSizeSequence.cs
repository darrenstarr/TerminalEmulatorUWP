using System;
using System.Collections.Generic;
using System.Text;

namespace TerminalEmulator.StreamParser
{
    public class CharacterSizeSequence : TerminalSequence
    {
        public ECharacterSize Size { get; set; }
        public override string ToString()
        {
            return "Character size - " + Size.ToString();
        }
    }
}
