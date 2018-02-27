using System;
using System.Collections.Generic;
using System.Text;

namespace TerminalEmulator
{
    public class CharacterInformation
    {
        public long Column { get; set; }
        public long Row { get; set; }
        public TerminalCharacter Character { get; set; }
    }
}
