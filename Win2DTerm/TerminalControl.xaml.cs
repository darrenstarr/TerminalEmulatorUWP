using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Renci.SshNet;
using Renci.SshNet.Common;
using System;
using System.Threading.Tasks;
using TerminalEmulator;
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

        public TerminalControl()
        {
            InitializeComponent();
        }

        private void CoreWindow_CharacterReceived(CoreWindow sender, CharacterReceivedEventArgs args)
        {
            if (!Connected)
                return;

            var ch = char.ConvertFromUtf32((int)args.KeyCode);

            var toSend = System.Text.Encoding.UTF8.GetBytes(ch.ToString());
            _stream.Write(toSend, 0, toSend.Length);
            _stream.Flush();

            System.Diagnostics.Debug.WriteLine(ch.ToString());
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
            byte[] toSend;

            System.Diagnostics.Debug.WriteLine(e.Data.Length.ToString() + " received");

            lock (Model)
            {
                if (Model.Push(e.Data))
                    canvas.Invalidate();
                else
                    System.Diagnostics.Debug.WriteLine("Nothing processed");
                toSend = Model.GetSendQueue();
            }

            if(toSend != null)
            {
                Task.Run(() =>
                {
                    _stream.Write(toSend, 0, toSend.Length);
                    _stream.Flush();
                });
            }
        }

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
            CanvasTextFormat boldFormat =
                new CanvasTextFormat
                {
                    FontSize = Convert.ToSingle(canvas.FontSize),
                    FontFamily = canvas.FontFamily.Source,
                    FontWeight = Windows.UI.Text.FontWeights.Bold,
                    WordWrapping = CanvasWordWrapping.NoWrap
                };

            ProcessTextFormat(drawingSession, format);

            drawingSession.FillRectangle(new Rect(0, 0, canvas.RenderSize.Width, canvas.RenderSize.Height), Colors.Black);

            lock (Model)
            {
                float verticalOffset = 0;
                int row = 0;
                if (Model.Buffer.Count > Rows)
                {
                    row = Model.Buffer.Count - Rows;
                    verticalOffset = (Rows - Model.Buffer.Count) * (float)CharacterHeight;
                }

                while(row < Model.Buffer.Count)
                {
                    var line = Model.Buffer[row];
                    int column = 0;
                    foreach (var character in line)
                    {
                        var rect = new Rect((double)column * CharacterWidth, (double)row * CharacterHeight + verticalOffset, CharacterWidth + 0.9, CharacterHeight + 0.9);

                        var textLayout = new CanvasTextLayout(drawingSession, character.Char.ToString(), (character.Attribute.Bright ? boldFormat : format), 0.0f, 0.0f);
                        var backgroundColor = GetBackgroundColor(character.Attribute);
                        var foregroundColor = GetForegroundColor(character.Attribute);
                        drawingSession.FillRectangle(rect, backgroundColor);
                        //drawingSession.DrawRectangle(rect, backgroundColor);
                        drawingSession.DrawTextLayout(textLayout, (float)column * (float)CharacterWidth, (float)row * (float)CharacterHeight + verticalOffset, foregroundColor);
                        column++;
                    }
                    row++;
                }
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

        private Color GetBackgroundColor(TerminalAttribute attribute)
        {
            if (attribute.Standout)
                return AttributeColors[(int)attribute.BackgroundColor + 8];
            return AttributeColors[(int)attribute.BackgroundColor];
        }

        private Color GetForegroundColor(TerminalAttribute attribute)
        {
            if (attribute.Standout)
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
            }
        }

        private void ResizeTerminal()
        {
            System.Diagnostics.Debug.WriteLine("ResizeTerminal()");
            System.Diagnostics.Debug.WriteLine("  Character size " + CharacterWidth.ToString() + "," + CharacterHeight.ToString());
            System.Diagnostics.Debug.WriteLine("  Terminal size " + Columns.ToString() + "," + Rows.ToString());

            Model.ResizeView(Columns, Rows);
        }

        private void canvas_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (!Connected)
                return;

            var controlPressed = (Window.Current.CoreWindow.GetKeyState(Windows.System.VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down));
            var shiftPressed = (Window.Current.CoreWindow.GetKeyState(Windows.System.VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down));

            switch(e.Key)
            {
                case Windows.System.VirtualKey.Shift:
                case Windows.System.VirtualKey.Control:
                    return;

                default:
                    break;
            }

            var code = Model.GetKeySequence((controlPressed ? "Ctrl+" : "") + (shiftPressed ? "Shift+" : "") + e.Key.ToString());
            if(code != null)
            {
                Task.Run(() =>
                {
                    _stream.Write(code, 0, code.Length);
                    _stream.Flush();
                });
            }

            System.Diagnostics.Debug.WriteLine(e.Key.ToString() + ",S" + (shiftPressed ? "1" : "0") + ",C" + (controlPressed ? "1" : "0"));
        }

        private void Grid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            this.Focus(FocusState.Pointer);
        }

        private void UserControl_GotFocus(object sender, RoutedEventArgs e)
        {
            Window.Current.CoreWindow.CharacterReceived += CoreWindow_CharacterReceived;
        }

        private void UserControl_LostFocus(object sender, RoutedEventArgs e)
        {
            Window.Current.CoreWindow.CharacterReceived -= CoreWindow_CharacterReceived;
        }
    }
}
