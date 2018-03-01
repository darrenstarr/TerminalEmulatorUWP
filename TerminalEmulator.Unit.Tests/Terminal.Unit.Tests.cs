using System;
using System.Linq;
using Xunit;

namespace TerminalEmulator.Unit.Tests
{
    public class TerminalUnitTests
    {
        private void PushToTerminal(Terminal t, string s)
        {
            t.Push(s.Select(x => (byte)x).ToArray());
        }

        [Fact]
        public void Backspace()
        {
            var t = new Terminal();
            t.ResizeView(200, 100);
            PushToTerminal(t, "12345\u001b[D\u001b[D\b0");

            Assert.Equal("12045 ", t.GetVisibleChars(0, 0, 6));
        }

        [Fact]
        public void EraseToStartOfLine()
        {
            var t = new Terminal();
            t.ResizeView(200, 100);
            PushToTerminal(t, "12345\u001b[D\u001b[D\u001b[1K");

            Assert.Equal("    5 ", t.GetVisibleChars(0, 0, 6));
        }

        public readonly string ExpectedScreenAlignment =
            "EEEEE \n" +
            "EEEEE \n" +
            "EEEEE \n" +
            "EEEEE \n" +
            "EEEEE \n" +
            "      ";

        [Fact]
        public void ScreenAlignmentTest()
        {
            var t = new Terminal();
            t.ResizeView(5, 5);
            t.ScreenAlignmentTest();
            t.ResizeView(6, 6);

            Assert.Equal(ExpectedScreenAlignment, t.GetScreenText());
        }


    }
}
