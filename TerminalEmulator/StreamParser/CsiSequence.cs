namespace TerminalEmulator.StreamParser
{
    public class CsiSequence : TerminalSequence
    {
        public override string ToString()
        {
            return "CSI - " + base.ToString();
        }
    }
}
