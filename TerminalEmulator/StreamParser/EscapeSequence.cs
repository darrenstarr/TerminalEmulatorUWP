namespace TerminalEmulator.StreamParser
{
    public class EscapeSequence : TerminalSequence
    {
        public override string ToString()
        {
            return "ESC - " + base.ToString();
        }
    }
}
