using System;

namespace TerminalEmulator
{
    public class OnCharacterChangedEventArgs : EventArgs
    {
        public CharacterInformation Changes { get; set; }
    }
}