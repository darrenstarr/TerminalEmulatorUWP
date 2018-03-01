using System;
using System.Collections.Generic;
using System.Text;

namespace TerminalEmulator
{
    public class TerminalLine : List<TerminalCharacter>
    {
        public bool DoubleWidth { get; set; } = false;
        public bool DoubleHeightTop { get; set; } = false;
        public bool DoubleHeightBottom { get; set; } = false;
    }
}
