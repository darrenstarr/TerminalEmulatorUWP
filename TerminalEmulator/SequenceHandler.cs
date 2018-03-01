using System;
using System.Collections.Generic;
using System.Text;
using TerminalEmulator.StreamParser;

namespace TerminalEmulator
{
    public class SequenceHandler
    {
        public enum ESequenceType
        {
            Character,
            CSI,            // Control Sequence Introducer
            OSC,            // Operating System Command
            SS3,            // Signal Shift Select 3
            VT52mc,         // VT52 Move Cursor
            Compliance,     // Compliance
            CharacterSet,   // Character set
            Escape,
            CharacterSize,
        }

        public string Description { get; set; }
        public ESequenceType SequenceType { get; set; }
        public int ExactParameterCount { get; set; } = -1;
        public int ExactParameterCountOrDefault { get; set; } = -1;
        public int MinimumParameterCount { get; set; } = 0;
        public bool Query { get; set; } = false;
        public bool Send { get; set; } = false;
        public bool Bang { get; set; } = false;
        public int[] Param0 { get; set; } = new int[] {};
        public string CsiCommand { get; set; }
        public Action<TerminalSequence, TerminalController> Handler { get; set; }
    }
}
