using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Renci.SshNet;
using Renci.SshNet.Common;
using System;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using TerminalEmulator;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;


// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Win2DTerm
{
    public sealed partial class TerminalControl : UserControl
    {
        public Terminal Model { get; set; } = new Terminal();
        public int ViewTop { get; set; } = 0;

        public TerminalControl()
        {
            InitializeComponent();

            Model.SendData += OnSendData;
        }

        private void OnSendData(object sender, SendDataEventArgs e)
        {
            Task.Run(() =>
            {
                _stream.Write(e.Data, 0, e.Data.Length);
                _stream.Flush();
            });
        }

        private void CoreWindow_CharacterReceived(CoreWindow sender, CharacterReceivedEventArgs args)
        {
            if (!Connected)
                return;

            var ch = char.ConvertFromUtf32((int)args.KeyCode);

            var toSend = System.Text.Encoding.UTF8.GetBytes(ch.ToString());
            _stream.Write(toSend, 0, toSend.Length);
            _stream.Flush();

            //System.Diagnostics.Debug.WriteLine(ch.ToString());
            args.Handled = true;
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            canvas.RemoveFromVisualTree();
            canvas = null;
        }

        public double CharacterWidth = -1;
        public double CharacterHeight = -1;
        public int Columns = -1;
        public int Rows = -1;

        AuthenticationMethod _authenticationMethod;
        ConnectionInfo _connectionInfo;
        SshClient _client;
        ShellStream _stream;

        public bool Connected
        {
            get
            {
                return _stream != null && _client.IsConnected && _stream.CanWrite;
            }
        }

        string InputBuffer { get; set; } = "";

        public bool ConnectToSsh(string hostname, int port, string username, string password)
        {
            _authenticationMethod = new PasswordAuthenticationMethod(username, password);

            _connectionInfo = new ConnectionInfo(hostname, 22, username, _authenticationMethod);
            _client = new SshClient(_connectionInfo);
            try
            {
                _client.Connect();
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.ToString());
                return false;
            }
            _stream = _client.CreateShellStream("xterm", (uint)Columns, (uint)Rows, 800, 600, 16384);
            _stream.DataReceived += _OnDataReceived;

            return true;
        }

        private void _OnDataReceived(object sender, ShellDataEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine(e.Data.Length.ToString() + " received");

            lock (Model)
            {
                int oldTopRow = Model.TopRow;

                if (Model.Push(e.Data))
                {
                    if (oldTopRow != Model.TopRow && oldTopRow >= ViewTop)
                        ViewTop = Model.TopRow;

                    canvas.Invalidate();
                }
            }
        }

        bool ViewDebugging = false;
        private void OnCanvasDraw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            CanvasDrawingSession drawingSession = args.DrawingSession;

            CanvasTextFormat format =
                new CanvasTextFormat
                {
                    FontSize = Convert.ToSingle(canvas.FontSize),
                    FontFamily = canvas.FontFamily.Source,
                    FontWeight = canvas.FontWeight,
                    WordWrapping = CanvasWordWrapping.NoWrap
                };

            ProcessTextFormat(drawingSession, format);

            drawingSession.FillRectangle(new Rect(0, 0, canvas.RenderSize.Width, canvas.RenderSize.Height), GetBackgroundColor(Model.CursorState.Attribute, false));

            lock (Model)
            {
                int row = ViewTop;
                float verticalOffset = -row * (float)CharacterHeight;

                var defaultTransform = drawingSession.Transform;
                while (row < Model.Buffer.Count && (row < ViewTop + Rows))
                {
                    var line = Model.Buffer[row];
                    int column = 0;

                    drawingSession.Transform = Matrix3x2.CreateScale(
                        (float)(line.DoubleWidth ? 2.0 : 1.0),
                        (float)(line.DoubleHeightBottom | line.DoubleHeightTop ? 2.0 : 1.0)
                    );

                    foreach (var character in line)
                    {
                        bool selected = IsSelected(column, row);

                        var rect = new Rect(
                            column * CharacterWidth,
                            ((row - (line.DoubleHeightBottom ? 1 : 0)) * CharacterHeight + verticalOffset) * (line.DoubleHeightBottom | line.DoubleHeightTop ? 0.5 : 1.0),
                            CharacterWidth + 0.9, 
                            CharacterHeight + 0.9
                        );

                        var textLayout = new CanvasTextLayout(drawingSession, character.Char.ToString(), format, 0.0f, 0.0f);
                        var backgroundColor = GetBackgroundColor(character.Attribute, selected);
                        var foregroundColor = GetForegroundColor(character.Attribute, selected);
                        drawingSession.FillRectangle(rect, backgroundColor);

                        drawingSession.DrawTextLayout(
                            textLayout,
                            (float)rect.Left,
                            (float)rect.Top,
                            foregroundColor
                        );

                        if (character.Attribute.Underscore)
                        {
                            drawingSession.DrawLine(
                                new Vector2(
                                    (float)rect.Left,
                                    (float)rect.Bottom
                                ),
                                new Vector2(
                                    (float)rect.Right,
                                    (float)rect.Bottom
                                ),
                                foregroundColor
                            );
                        }

                        column++;
                    }
                    row++;
                }
                drawingSession.Transform = defaultTransform;

                if (Model.CursorState.ShowCursor)
                {
                    var cursorY = Model.TopRow - ViewTop + Model.CursorState.CurrentRow;
                    var cursorRect = new Rect(
                        Model.CursorState.CurrentColumn * CharacterWidth,
                        cursorY * CharacterHeight,
                        CharacterWidth + 0.9,
                        CharacterHeight + 0.9
                    );

                    drawingSession.DrawRectangle(cursorRect, GetForegroundColor(Model.CursorState.Attribute, false));
                }
            }

            if (ViewDebugging)
                AnnotateView(drawingSession);
        }

        private void AnnotateView(CanvasDrawingSession drawingSession)
        {
            CanvasTextFormat lineNumberFormat =
                                new CanvasTextFormat
                                {
                                    FontSize = Convert.ToSingle(canvas.FontSize / 2),
                                    FontFamily = canvas.FontFamily.Source,
                                    FontWeight = canvas.FontWeight,
                                    WordWrapping = CanvasWordWrapping.NoWrap
                                };

            for (var i = 0; i < Rows; i++)
            {
                string s = i.ToString();
                var textLayout = new CanvasTextLayout(drawingSession, s.ToString(), lineNumberFormat, 0.0f, 0.0f);
                float y = i * (float)CharacterHeight;
                drawingSession.DrawLine(0, y, (float)canvas.RenderSize.Width, y, Colors.Beige);
                drawingSession.DrawTextLayout(textLayout, (float)(canvas.RenderSize.Width - (CharacterWidth / 2 * s.Length)), y, Colors.Yellow);

                s = (i + 1).ToString();
                textLayout = new CanvasTextLayout(drawingSession, s.ToString(), lineNumberFormat, 0.0f, 0.0f);
                drawingSession.DrawTextLayout(textLayout, (float)(canvas.RenderSize.Width - (CharacterWidth / 2 * (s.Length + 3))), y, Colors.Green);
            }
        }

        private static Color[] AttributeColors =
        {
            Color.FromArgb(255,0,0,0),        // Black
            Color.FromArgb(255,187,0,0),      // Red
            Color.FromArgb(255,0,187,0),      // Green
            Color.FromArgb(255,187,187,0),    // Yellow
            Color.FromArgb(255,0,0,187),      // Blue
            Color.FromArgb(255,187,0,187),    // Magenta
            Color.FromArgb(255,0,187,187),    // Cyan
            Color.FromArgb(255,187,187,187),  // White
            Color.FromArgb(255,85,85,85),     // Bright black
            Color.FromArgb(255,255,85,85),    // Bright red
            Color.FromArgb(255,85,255,85),    // Bright green
            Color.FromArgb(255,255,255,85),   // Bright yellow
            Color.FromArgb(255,85,85,255),    // Bright blue
            Color.FromArgb(255,255,85,255),   // Bright Magenta
            Color.FromArgb(255,85,255,255),   // Bright cyan
            Color.FromArgb(255,255,255,255),  // Bright white
        };

        private static SolidColorBrush[] AttributeBrushes =
        {
            new SolidColorBrush(AttributeColors[0]),
            new SolidColorBrush(AttributeColors[1]),
            new SolidColorBrush(AttributeColors[2]),
            new SolidColorBrush(AttributeColors[3]),
            new SolidColorBrush(AttributeColors[4]),
            new SolidColorBrush(AttributeColors[5]),
            new SolidColorBrush(AttributeColors[6]),
            new SolidColorBrush(AttributeColors[7]),
            new SolidColorBrush(AttributeColors[8]),
            new SolidColorBrush(AttributeColors[9]),
            new SolidColorBrush(AttributeColors[10]),
            new SolidColorBrush(AttributeColors[11]),
            new SolidColorBrush(AttributeColors[12]),
            new SolidColorBrush(AttributeColors[13]),
            new SolidColorBrush(AttributeColors[14]),
            new SolidColorBrush(AttributeColors[15]),
        };

        private Color GetBackgroundColor(TerminalAttribute attribute, bool invert)
        {
            var flip = Model.CursorState.ReverseVideoMode ^ attribute.Reverse ^ invert;

            if (flip)
            {
                if (attribute.Bright)
                    return AttributeColors[(int)attribute.ForegroundColor + 8];

                return AttributeColors[(int)attribute.ForegroundColor];
            }

            return AttributeColors[(int)attribute.BackgroundColor];
        }

        private Color GetForegroundColor(TerminalAttribute attribute, bool invert)
        {
            var flip = Model.CursorState.ReverseVideoMode ^ attribute.Reverse ^ invert;

            if (flip)
                return AttributeColors[(int)attribute.BackgroundColor];

            if (attribute.Bright)
                return AttributeColors[(int)attribute.ForegroundColor + 8];

            return AttributeColors[(int)attribute.ForegroundColor];
        }

        private void ProcessTextFormat(CanvasDrawingSession drawingSession, CanvasTextFormat format)
        {
            CanvasTextLayout textLayout = new CanvasTextLayout(drawingSession, "Q", format, 0.0f, 0.0f);
            if (CharacterWidth != textLayout.DrawBounds.Width || CharacterHeight != textLayout.DrawBounds.Height)
            {
                CharacterWidth = textLayout.DrawBounds.Right;
                CharacterHeight = textLayout.DrawBounds.Bottom;
            }

            int columns = Convert.ToInt32(Math.Floor(canvas.RenderSize.Width / CharacterWidth));
            int rows = Convert.ToInt32(Math.Floor(canvas.RenderSize.Height / CharacterHeight));
            if (Columns != columns || Rows != rows)
            {
                Columns = columns;
                Rows = rows;
                ResizeTerminal();

                if(_stream != null && _stream.CanWrite)
                    _stream.SendWindowChangeRequest((uint)columns, (uint)rows, 800, 600);
            }
        }

        private void ResizeTerminal()
        {
            //System.Diagnostics.Debug.WriteLine("ResizeTerminal()");
            //System.Diagnostics.Debug.WriteLine("  Character size " + CharacterWidth.ToString() + "," + CharacterHeight.ToString());
            //System.Diagnostics.Debug.WriteLine("  Terminal size " + Columns.ToString() + "," + Rows.ToString());

            Model.ResizeView(Columns, Rows);
        }

        private void TerminalKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (!Connected)
                return;

            var controlPressed = (Window.Current.CoreWindow.GetKeyState(Windows.System.VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down));
            var shiftPressed = (Window.Current.CoreWindow.GetKeyState(Windows.System.VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down));

            switch(e.Key)
            {
                case Windows.System.VirtualKey.Shift:
                case Windows.System.VirtualKey.Control:
                    return;

                default:
                    break;
            }

            if (controlPressed && e.Key == Windows.System.VirtualKey.F12)
                Model.Debugging = !Model.Debugging;

            if (controlPressed && e.Key == Windows.System.VirtualKey.F10)
                Model.SequenceDebugging = !Model.SequenceDebugging;

            if (controlPressed && e.Key == Windows.System.VirtualKey.F11)
            {
                ViewDebugging = !ViewDebugging;
                canvas.Invalidate();
            }

            var code = Model.GetKeySequence((controlPressed ? "Ctrl+" : "") + (shiftPressed ? "Shift+" : "") + e.Key.ToString());
            if(code != null)
            {
                Task.Run(() =>
                {
                    _stream.Write(code, 0, code.Length);
                    _stream.Flush();
                });

                e.Handled = true;

                if(ViewTop != Model.TopRow)
                {
                    Model.TopRow = ViewTop;
                    canvas.Invalidate();
                }
            }

            //System.Diagnostics.Debug.WriteLine(e.Key.ToString() + ",S" + (shiftPressed ? "1" : "0") + ",C" + (controlPressed ? "1" : "0"));
        }

        private void TerminalTapped(object sender, TappedRoutedEventArgs e)
        {
            this.Focus(FocusState.Pointer);
        }

        private void TerminalGotFocus(object sender, RoutedEventArgs e)
        {
            Window.Current.CoreWindow.CharacterReceived += CoreWindow_CharacterReceived;
        }

        private void TerminalLostFocus(object sender, RoutedEventArgs e)
        {
            Window.Current.CoreWindow.CharacterReceived -= CoreWindow_CharacterReceived;
        }

        private void TerminalWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var pointer = e.GetCurrentPoint(canvas);

            int oldViewTop = ViewTop;

            ViewTop -= pointer.Properties.MouseWheelDelta / 40;
            if (ViewTop < 0)
                ViewTop = 0;
            else if (ViewTop > Model.TopRow)
                ViewTop = Model.TopRow;

            if (oldViewTop != ViewTop)
                canvas.Invalidate();
        }

        int MouseOverColumn = -1;
        int MouseOverRow = -1;
        int SelectStartColumn = -1;
        int SelectStartRow = -1;
        int SelectEndColumn = -1;
        int SelectEndRow = -1;
        bool Selecting = false;

        private bool IsSelected(int column, int row)
        {
            var startColumn = SelectStartColumn;
            var startRow = SelectStartRow;
            var endColumn = SelectEndColumn;
            var endRow = SelectEndRow;

            if(startRow > endRow || (startRow == endRow && startColumn > endColumn))
            {
                startColumn = SelectEndColumn;
                startRow = SelectEndRow;
                endColumn = SelectStartColumn;
                endRow = SelectStartRow;
            }

            return
                (
                    row == startRow &&
                    startRow == endRow &&
                    column >= startColumn &&
                    column <= endColumn
                ) ||
                (
                    startRow != endRow &&
                    (
                        (row == startRow && column >= startColumn) ||
                        (row > startRow && row < endRow) ||
                        (row == endRow && column <= endColumn)
                    )
                );
        }

        private void TerminalPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var pointer = e.GetCurrentPoint(canvas);

            int overColumn = (int)Math.Floor(pointer.Position.X / CharacterWidth);
            if (overColumn >= Columns)
                overColumn = Columns - 1;

            int overRow = (int)Math.Floor(pointer.Position.Y / CharacterHeight);
            if (overRow >= Rows)
                overRow = Rows - 1;

            if (overColumn == MouseOverColumn && overRow == MouseOverRow)
                return;

            MouseOverColumn = overColumn;
            MouseOverRow = overRow;

            if (pointer.Properties.IsLeftButtonPressed)
            {
                int newSelectStartColumn = -1;
                int newSelectStartRow = -1;
                int newSelectEndColumn = -1;
                int newSelectEndRow = -1;
                if (MousePressedAtColumn != MouseOverColumn || MousePressedAtRow != (ViewTop + MouseOverRow))
                {
                    if (MousePressedAtRow < (ViewTop + MouseOverRow) || (MousePressedAtRow == (ViewTop + MouseOverRow) && MousePressedAtColumn  <= MouseOverColumn))
                    {
                        newSelectStartColumn = MousePressedAtColumn;
                        newSelectStartRow = MousePressedAtRow;
                        newSelectEndColumn = MouseOverColumn - 1;
                        newSelectEndRow = (ViewTop + MouseOverRow);
                    }
                    else
                    {
                        newSelectStartColumn = MousePressedAtColumn;
                        newSelectStartRow = MousePressedAtRow;
                        newSelectEndColumn = MouseOverColumn;
                        newSelectEndRow = (ViewTop + MouseOverRow);
                    }

                    Selecting = true;
                }

                if(
                    !(
                        SelectStartColumn == newSelectStartColumn &&
                        SelectEndColumn == newSelectEndColumn &&
                        SelectStartRow == newSelectStartRow &&
                        SelectEndRow == newSelectEndRow
                    )
                )
                {
                    SelectStartColumn = newSelectStartColumn;
                    SelectEndColumn = newSelectEndColumn;
                    SelectStartRow = newSelectStartRow;
                    SelectEndRow = newSelectEndRow;

                    //System.Diagnostics.Debug.WriteLine(
                    //    "Select from (c=" +
                    //    SelectStartColumn.ToString() + ",r=" + SelectStartRow.ToString() +
                    //    ") to (c=" +
                    //    SelectEndColumn.ToString() + ",r=" + SelectEndRow.ToString() +
                    //    ")"
                    //);
                }
            }

            //System.Diagnostics.Debug.WriteLine("Pointer Moved (c" + MouseOverColumn.ToString() + ",r=" + MouseOverRow.ToString() + ")");
            canvas.Invalidate();
        }

        private void TerminalPointerExited(object sender, PointerRoutedEventArgs e)
        {
            MouseOverColumn = -1;
            MouseOverRow = -1;

            //System.Diagnostics.Debug.WriteLine("Pointer Left (c" + MouseOverColumn.ToString() + ",r=" + MouseOverRow.ToString() + ")");
            canvas.Invalidate();
        }

        public int MousePressedAtColumn = -1;
        public int MousePressedAtRow = -1;

        private void TerminalPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var pointer = e.GetCurrentPoint(canvas);
            if (pointer.Properties.IsLeftButtonPressed)
            {
                MousePressedAtColumn = MouseOverColumn;
                MousePressedAtRow = ViewTop + MouseOverRow;
            }
            else if(pointer.Properties.IsRightButtonPressed)
            {
                PasteClipboard();
            }
        }

        private void TerminalPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            var pointer = e.GetCurrentPoint(canvas);
            if (!pointer.Properties.IsLeftButtonPressed)
            {
                if (Selecting)
                {
                    MousePressedAtColumn = -1;
                    MousePressedAtRow = -1;

                    Selecting = false;

                    //System.Diagnostics.Debug.WriteLine("Captured : " + Model.GetText(SelectStartColumn, SelectStartRow, SelectEndColumn, SelectEndRow));

                    var captured = Model.GetText(SelectStartColumn, SelectStartRow, SelectEndColumn, SelectEndRow);

                    var dataPackage = new DataPackage();
                    dataPackage.SetText(captured);
                    dataPackage.Properties.EnterpriseId = "Terminal";
                    Clipboard.SetContent(dataPackage);
                }
                else
                {
                    SelectStartColumn = -1;
                    SelectStartRow = -1;
                    SelectEndColumn = -1;
                    SelectEndRow = -1;
                    canvas.Invalidate();
                }
            }
        }

        private void PasteText(string text)
        {
            Task.Run(() =>
            {
                var buffer = Encoding.UTF8.GetBytes(text);
                _stream.Write(buffer, 0, buffer.Length);
                _stream.Flush();
            });
        }

        private void PasteClipboard()
        {
            var package = Clipboard.GetContent();

            Task.Run(async () =>
            {
                string text = await package.GetTextAsync();
                if (!string.IsNullOrEmpty(text))
                    PasteText(text);
            });
        }
    }
}
