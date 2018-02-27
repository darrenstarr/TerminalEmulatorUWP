using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Text;
using TerminalEmulator;

namespace AvaloniaTerm
{
    public class TerminalView : TemplatedControl, IInteractive
    {
        public Terminal TerminalEmulator { get; set; } = new Terminal();

        public Typeface Font { get; set; } = new Typeface("Consolas", 16 * 3 / 4);
        public Typeface BoldFont { get; set; } = new Typeface("Consolas", 16 * 3 / 4, FontStyle.Normal, FontWeight.Heavy);
        public Typeface ItalicFont { get; set; } = new Typeface("Consolas", 16 * 3 / 4, FontStyle.Italic, FontWeight.Normal);
        public Typeface BoldItalicFont { get; set; } = new Typeface("Consolas", 16 * 3 / 4, FontStyle.Italic, FontWeight.Heavy);

        public Size CharSize { get; set; }

        public List<TerminalLine> Lines { get; set; } = new List<TerminalLine>();

        public long TopLine = 0;
        public long LeftColumn = 0;

        public TerminalView()
        {
            TerminalEmulator.OnCharacterChanged += OnCharacterChanged;

            FocusableProperty.OverrideDefaultValue<TerminalView>(true);

            Background = Brushes.Black;

            var oneLetter = new FormattedText
            {
                Text = "W",
                Typeface = Font
            };
            CharSize = oneLetter.Measure();
        }
        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            Focus();
        }

        private void OnCharacterChanged(object sender, OnCharacterChangedEventArgs e)
        {
            while (Lines.Count < e.Changes.Row + 1)
                Lines.Add(new TerminalLine());

            var currentLine = Lines[Convert.ToInt32(e.Changes.Row)];
            while (currentLine.Count < e.Changes.Column + 1)
            {
                currentLine.Add(
                    new TerminalCharacter
                    {
                        Char = ' ',
                        Attribute = new TerminalAttribute()
                    }
                );
            }

            var currentChar = currentLine[Convert.ToInt32(e.Changes.Column)];
            currentLine[Convert.ToInt32(e.Changes.Column)] = e.Changes.Character.Clone();

            InvalidateVisual();
        }

        protected override Size MeasureCore(Size availableSize)
        {
            return base.MeasureCore(availableSize);
        }

        protected override void OnMeasureInvalidated()
        {
            base.OnMeasureInvalidated();
        }

        public override void Render(DrawingContext context)
        {
            //var gridPen = new Pen(Brushes.Red);
            //for(double x = 0; x < Bounds.Width; x += CharSize.Width)
            //    context.DrawLine(gridPen, new Point(x, 0), new Point(x, Bounds.Height - 1));

            //for (double y = 0; y < Bounds.Height; y += CharSize.Height)
            //    context.DrawLine(gridPen, new Point(0, y), new Point(Bounds.Width - 1, y));

            var visibleRows = Convert.ToInt64(Math.Floor(Bounds.Height / CharSize.Height));
            var visibleColumns = Convert.ToInt64(Math.Floor(Bounds.Width / CharSize.Width));
            var lastRow = Math.Min(TopLine + visibleRows - 1, Lines.Count - TopLine);

            for(var row=0; row < lastRow; row++)
            {
                var line = Lines[Convert.ToInt32(row + TopLine)];
                var lastColumn = Math.Min(LeftColumn + visibleColumns - 1, line.Count - LeftColumn);

                for(var col=0; col < Convert.ToInt32(lastColumn); col++)
                {
                    var ch = line[col];

                    var str = ch.Char.ToString();
                    var background = ColorToBrush(ch.Attribute.BackgroundColor, false);
                    var foreground = ColorToBrush(ch.Attribute.ForegroundColor, ch.Attribute.Bright);

                    context.DrawRectangle(new Pen(background), new Rect(col * CharSize.Width, row * CharSize.Height, CharSize.Width, CharSize.Height));
                    context.DrawText(foreground, new Point(col * CharSize.Width, row * CharSize.Height), new FormattedText { Text = str, Typeface = Font });
                }
            }
        }

        public static Brush Black = new SolidColorBrush(Color.FromRgb(0, 0, 0));
        public static Brush Red = new SolidColorBrush(Color.FromRgb(187, 0, 0));
        public static Brush Green = new SolidColorBrush(Color.FromRgb(0, 187, 0));
        public static Brush Yellow = new SolidColorBrush(Color.FromRgb(187, 187, 0));
        public static Brush Blue = new SolidColorBrush(Color.FromRgb(0, 0, 187));
        public static Brush Magenta = new SolidColorBrush(Color.FromRgb(187, 0, 187));
        public static Brush Cyan = new SolidColorBrush(Color.FromRgb(0, 187, 187));
        public static Brush White = new SolidColorBrush(Color.FromRgb(187, 187, 187));
        public static Brush BrightBlack = new SolidColorBrush(Color.FromRgb(85, 85, 85));
        public static Brush BrightRed = new SolidColorBrush(Color.FromRgb(255, 85, 85));
        public static Brush BrightGreen = new SolidColorBrush(Color.FromRgb(85, 255, 85));
        public static Brush BrightYellow = new SolidColorBrush(Color.FromRgb(255, 255, 85));
        public static Brush BrightBlue = new SolidColorBrush(Color.FromRgb(85, 85, 255));
        public static Brush BrightMagenta = new SolidColorBrush(Color.FromRgb(255, 85, 255));
        public static Brush BrightCyan = new SolidColorBrush(Color.FromRgb(85, 255, 255));
        public static Brush BrightWhite = new SolidColorBrush(Color.FromRgb(255, 255, 255));

        private Brush ColorToBrush(TerminalColor color, bool bright)
        {
            if(bright)
            {
                switch (color)
                {
                    case TerminalColor.Black:
                        return BrightBlack;
                    case TerminalColor.Red:
                        return BrightRed;
                    case TerminalColor.Green:
                        return BrightGreen;
                    case TerminalColor.Yellow:
                        return BrightYellow;
                    case TerminalColor.Blue:
                        return BrightBlue;
                    case TerminalColor.Magenta:
                        return BrightMagenta;
                    case TerminalColor.Cyan:
                        return BrightCyan;
                    case TerminalColor.White:
                        return BrightWhite;
                }
            }

            switch (color)
            {
                case TerminalColor.Black:
                    return Black;
                case TerminalColor.Red:
                    return Red;
                case TerminalColor.Green:
                    return Green;
                case TerminalColor.Yellow:
                    return Yellow;
                case TerminalColor.Blue:
                    return Blue;
                case TerminalColor.Magenta:
                    return Magenta;
                case TerminalColor.Cyan:
                    return Cyan;
                case TerminalColor.White:
                    return White;
            }

            throw new ArgumentException("Invalid color specified", "color");
        }
    }
}
