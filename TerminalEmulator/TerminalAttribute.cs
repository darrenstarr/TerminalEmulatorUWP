namespace TerminalEmulator
{
    public class TerminalAttribute
    {
        public TerminalColor ForegroundColor { get; set; } = TerminalColor.White;
        public TerminalColor BackgroundColor { get; set; } = TerminalColor.Black;
        public bool Bright { get; set; } = false;
        public bool Dim { get; set; } = false;
        public bool Standout { get; set; } = false;
        public bool Underscore { get; set; } = false;
        public bool Blink { get; set; } = false;
        public bool Reverse { get; set; } = false;
        public bool Hidden { get; set; } = false;

        public TerminalAttribute Clone()
        {
            return new TerminalAttribute
            {
                ForegroundColor = ForegroundColor,
                BackgroundColor = BackgroundColor,
                Bright = Bright,
                Dim = Dim,
                Standout = Standout,
                Underscore = Underscore,
                Blink = Blink,
                Reverse = Reverse,
                Hidden = Hidden
            };
        }
    }
}
