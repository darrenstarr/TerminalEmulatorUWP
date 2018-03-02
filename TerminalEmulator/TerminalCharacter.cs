namespace TerminalEmulator
{
    public class TerminalCharacter
    {
        public char Char { get; set; } = ' ';
        public TerminalAttribute Attribute { get; set; } = new TerminalAttribute();

        public TerminalCharacter Clone()
        {
            return new TerminalCharacter
            {
                Char = Char,
                Attribute = Attribute.Clone()
            };
        }
    }
}
