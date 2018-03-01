using System;

namespace TerminalEmulator
{
    public class SendDataEventArgs : EventArgs
    {
        public byte [] Data { get; set; }
    }
}
