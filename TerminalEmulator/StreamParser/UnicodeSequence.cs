namespace TerminalEmulator.StreamParser
{
    public class UnicodeSequence : EscapeSequence
    {
        public override string ToString()
        {
            return "Unicode - " + base.ToString();
        }
    }
}
