using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TerminalEmulator
{
    public class Terminal
    {
        static bool _initialized = false;
        public Terminal()
        {
            if(!_initialized)
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                _initialized = true;
            }
        }

        public TerminalBuffer Buffer { get; set; } = new TerminalBuffer();
        public int Columns { get; set; } = 80;
        public int Rows { get; set; } = 24;
        public int CurrentColumn { get; set; } = 0;
        public int CurrentRow { get; set; } = 0;
        public int VisibleColumns { get; set; } = 0;
        public int VisibleRows { get; set; } = 0;

        public System.Text.Encoding TextCodec { get; set; } = Encoding.UTF8;

        public byte[] Queue { get; set; }
        public string StringQueue { get { return System.Text.Encoding.UTF8.GetString(Queue); } }

        public EventHandler<OnCharacterChangedEventArgs> OnCharacterChanged;

        public TerminalAttribute Attribute { get; set; } = new TerminalAttribute();

        public void ResizeView(int columns, int rows)
        {
            VisibleColumns = columns;
            VisibleRows = rows;

            // TODO : Send to server resize
        }

        private void AppendQueue(byte[] data)
        {
            if (Queue == null)
                Queue = data;
            else
                Queue = Queue.Concat(data).ToArray();
        }

        private bool ProcessQueueChar()
        {
            try
            {
                int charLength = 1;
                if ((Queue[0] & 0xF8) == 0xF0)
                    charLength = 4;
                else if ((Queue[0] & 0xF0) == 0xE0)
                    charLength = 3;
                else if ((Queue[0] & 0xE0) == 0xC0)
                    charLength = 2;

                if (Queue.Length < charLength)
                    return false;

                var ch = TextCodec.GetString(Queue, 0, charLength).SingleOrDefault();
                if (char.IsLetterOrDigit(ch) || char.IsPunctuation(ch) || char.IsSymbol(ch) || ch == ' ')
                {
                    SetCharacter(CurrentColumn, CurrentRow, ch, Attribute);
                    CurrentColumn++;

                    Queue = Queue.Skip(charLength).ToArray();

                    return true;
                }
                else if (ch == '\r')
                {
                    CurrentColumn = 0;
                    Queue = Queue.Skip(charLength).ToArray();

                    return true;
                }
                else if (ch == '\n')
                {
                    CurrentRow++;
                    Queue = Queue.Skip(charLength).ToArray();

                    return true;
                }
                else if (ch == 0x07)
                {
                    System.Diagnostics.Debug.WriteLine("Bel");
                    Queue = Queue.Skip(charLength).ToArray();

                    return true;
                }
                else if (ch == '\t')
                {
                    System.Diagnostics.Debug.WriteLine("Bel");
                    Tab();
                    Queue = Queue.Skip(charLength).ToArray();

                    return true;
                }
                else if (ch == 0x0E)
                {
                    System.Diagnostics.Debug.WriteLine("Shift-out");
                    ShiftOut();
                    Queue = Queue.Skip(charLength).ToArray();

                    return true;
                }
                else if (ch == 0x0F)
                {
                    System.Diagnostics.Debug.WriteLine("Shift-in");
                    ShiftIn();
                    Queue = Queue.Skip(charLength).ToArray();

                    return true;
                }
                else if (ch == 0x08)
                {
                    if (CurrentColumn > 0)
                        CurrentColumn--;

                    Queue = Queue.Skip(charLength).ToArray();

                    return true;
                }
                else if(ch == 0x8F)
                {
                    var prefix = new byte[] { (byte)ch };
                    Queue = prefix.Concat(Queue.Skip(charLength)).ToArray();
                    return false;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private void ShiftIn()
        {
        }

        private void ShiftOut()
        {
        }

        private bool ProcessSS3()
        {
            if (Queue.Length < 2)
                return false;

            if (Queue[0] != 0x8F)
                return false;

            System.Diagnostics.Debug.WriteLine("Unhandled SS3 code " + ((int)Queue[1]).ToString("X2"));

            Queue = Queue.Skip(2).ToArray();
            return true;
        }

        private bool ProcessEscapeSequence()
        {
            if (Queue.Length < 2)
                return false;

            if (Queue[0] != 0x1B)
                return false;

            if (Queue[1] == (byte)'[')  // CSI
            {
                if (Queue.Length < 3)
                    return false;

                List<int> EscParams = new List<int>();
                int CurrentParam = -1;

                int index = 2;
                bool query = false;
                while (Queue.Length > index)
                {
                    switch (Queue[index])
                    {
                        case (byte)'0':
                        case (byte)'1':
                        case (byte)'2':
                        case (byte)'3':
                        case (byte)'4':
                        case (byte)'5':
                        case (byte)'6':
                        case (byte)'7':
                        case (byte)'8':
                        case (byte)'9':
                            if (CurrentParam == -1)
                                CurrentParam = Queue[index] - 0x30;
                            else
                                CurrentParam = CurrentParam * 10 + Queue[index] - 0x30;
                            break;

                        case (byte)'?':
                            if (CurrentParam != -1 || EscParams.Count > 0)
                            {
                                Queue = new byte[] { };
                                return false;
                            }

                            query = true;
                            break;

                        case (byte)';':
                            if (CurrentParam == -1)
                            {
                                Queue = new byte[] { };
                                return false;
                            }

                            EscParams.Add(CurrentParam);
                            CurrentParam = -1;

                            break;

                        default:
                            if (CurrentParam != -1)
                            {
                                EscParams.Add(CurrentParam);
                                CurrentParam = -1;
                            }

                            switch (Queue[index])
                            {
                                case (byte)'A':
                                    if (EscParams.Count == 0)
                                        MoveCursor(0, -1);
                                    else if (EscParams.Count == 1)
                                        MoveCursor(0, -(EscParams[0]));
                                    else
                                        throw new Exception("Invalid number of parameters on ESC sequence CSI A");

                                    break;

                                case (byte)'B':
                                    if (EscParams.Count == 0)
                                        MoveCursor(0, 1);
                                    else if (EscParams.Count == 1)
                                        MoveCursor(0, EscParams[0]);
                                    else
                                        throw new Exception("Invalid number of parameters on ESC sequence CSI B");

                                    break;

                                case (byte)'C':
                                    if (EscParams.Count == 0)
                                        MoveCursor(1, 0);
                                    else if (EscParams.Count == 1)
                                        MoveCursor(EscParams[0], 0);
                                    else
                                        throw new Exception("Invalid number of parameters on ESC sequence CSI C");

                                    break;

                                case (byte)'D':
                                    if (EscParams.Count == 0)
                                        MoveCursor(-1, 0);
                                    else if (EscParams.Count == 1)
                                        MoveCursor(-(EscParams[0]), 0);
                                    else
                                        throw new Exception("Invalid number of parameters on ESC sequence CSI D");

                                    break;

                                case (byte)'H':
                                    if (EscParams.Count == 0)
                                        SetCursorPosition(0, 0);
                                    else if (EscParams.Count == 2)
                                        SetCursorPosition(EscParams[1] - 1, EscParams[0] - 1);
                                    else
                                        return false;

                                    break;

                                case (byte)'J':
                                    if (EscParams.Count == 0)
                                        EraseDown();
                                    else if (EscParams.Count == 1)
                                    {
                                        if (EscParams[0] == 0)
                                            EraseDown();
                                        else if (EscParams[0] == 1)
                                            EraseUp();
                                        else if (EscParams[0] == 2)
                                            EraseScreen();
                                        else
                                            return false;
                                    }
                                    else
                                        return false;

                                    break;

                                case (byte)'K':
                                    if (EscParams.Count == 0 || (EscParams.Count == 1 && EscParams[0] == 0))
                                        EraseEndOfLine();
                                    else if (EscParams.Count == 1 && EscParams[0] == 1)
                                        EraseToStartOfLine();
                                    else if (EscParams.Count == 1 && EscParams[0] == 2)
                                        EraseLine();
                                    else
                                        return false;
                                    break;

                                case (byte)'P':
                                    if (EscParams.Count == 0 || (EscParams.Count == 1 && EscParams[0] == 0))
                                        DeleteChars(0);
                                    else if (EscParams.Count == 1)
                                        DeleteChars(EscParams[0]);
                                    else
                                        return false;

                                    break;

                                case (byte)'c':
                                    Send(Encoding.ASCII.GetBytes("\u001b[>0;136;0c"));
                                    break;

                                case (byte)'g':
                                    if (EscParams.Count == 0 || (EscParams.Count == 1 && EscParams[0] == 0))
                                        ClearTab();
                                    else if (EscParams.Count == 1 && EscParams[0] == 3)
                                        ClearAllTabs();
                                    else if (EscParams.Count == 1 && EscParams[0] == 1)
                                        System.Diagnostics.Debug.WriteLine("Unhandled sequence CSI 1 g");
                                    else if (EscParams.Count == 1 && EscParams[0] == 2)
                                        System.Diagnostics.Debug.WriteLine("Unhandled sequence CSI 2 g");
                                    else
                                        return false;
                                    break;

                                case (byte)'h':
                                    foreach (var mode in EscParams)
                                        ToggleMode(mode, query, true);
                                    break;

                                case (byte)'l':
                                    foreach (var mode in EscParams)
                                        ToggleMode(mode, query, false);
                                    break;

                                case (byte)'m':
                                    SetDisplayAttribute(EscParams);
                                    break;

                                case (byte)'r':
                                    EnableScrolling(0, Rows - 1);
                                    break;

                                case (byte)'s':
                                    if (query && EscParams.Count == 1)
                                    {
                                        SaveDECPrivateModeValue(EscParams[0]);
                                        break;
                                    }

                                    return false;

                                default:
                                    return false;
                            }

                            Queue = Queue.Skip(index + 1).ToArray();
                            return true;
                    }
                    index++;
                }
            }
            else if (Queue[1] == (byte)']')     // OSC
            {
                if (Queue.Length < 3)
                    return false;

                List<int> EscParams = new List<int>();
                int CurrentParam = -1;

                int index = 2;
                while (Queue.Length > index)
                {
                    switch (Queue[index])
                    {
                        case (byte)'0':
                        case (byte)'1':
                        case (byte)'2':
                        case (byte)'3':
                        case (byte)'4':
                        case (byte)'5':
                        case (byte)'6':
                        case (byte)'7':
                        case (byte)'8':
                        case (byte)'9':
                            if (CurrentParam == -1)
                                CurrentParam = Queue[index] - 0x30;
                            else
                                CurrentParam = CurrentParam * 10 + Queue[index] - 0x30;
                            break;

                        case (byte)';':
                            if (CurrentParam == -1)
                            {
                                Queue = new byte[] { };
                                return false;
                            }

                            EscParams.Add(CurrentParam);
                            CurrentParam = -1;

                            System.Diagnostics.Debug.WriteLine("OSC " + EscParams[0]);

                            Queue = Queue.Skip(index + 1).ToArray();

                            return true;

                        default:
                            return false;
                    }
                    index++;
                }
            }
            else
            {
                switch (Queue[1])
                {
                    case (byte)'7':
                        SaveCursor();
                        break;

                    case (byte)'8':
                        RestoreCursor();
                        break;

                    case (byte)'A':
                        MoveCursor(0, -1);
                        break;

                    case (byte)'B':
                        MoveCursor(0, 1);
                        break;

                    case (byte)'C':
                        MoveCursor(1, 0);
                        break;

                    case (byte)'D':
                        MoveCursor(-1, 0);
                        break;

                    case (byte)'H':
                        SetTab();
                        break;

                    case (byte)'I':
                        ReverseLineFeed();
                        break;

                    case (byte)'J':
                        EraseToEndOfScreen();
                        break;

                    case (byte)'K':         // Atari VT-52 Clear to end of line
                        EraseEndOfLine();
                        break;

                    case (byte)'L':         // Atari VT-52 insert line
                        InsertLine();
                        break;

                    case (byte)'M':         // Atari VT 52 Delete line
                        DeleteLine();
                        break;

                    case (byte)'Y':
                        if (Queue.Length < 4)
                            return false;

                        int directLine = Queue[2] - 38;
                        int directColumn = Queue[3] - 38;

                        Queue = Queue.Skip(4).ToArray();
                        return true;

                    case (byte)'=':
                        SetAlternateKeypadMode(true);
                        break;

                    case (byte)'>':
                        SetAlternateKeypadMode(false);
                        break;

                    case (byte)'<':
                        SetVT52Mode(false);
                        break;

                    case (byte)'(':
                    case (byte)')':
                        if (Queue.Length < 3)
                            return false;

                        var gset = Queue[1] == '(' ? "G0" : "G1";

                        switch(Queue[2])
                        {
                            case (byte)'A':
                                System.Diagnostics.Debug.WriteLine("Set UK character set (" + gset + ")");
                                break;
                            case (byte)'B':
                                //TextCodec = Encoding.GetEncoding(437);
                                System.Diagnostics.Debug.WriteLine("Set ASCII character set (" + gset + ")");
                                break;
                            case (byte)'0':
                                System.Diagnostics.Debug.WriteLine("Set special graphics character set (" + gset + ")");
                                break;
                            case (byte)'1':
                                System.Diagnostics.Debug.WriteLine("Set alternate character ROM standard character set (" + gset + ")");
                                break;
                            case (byte)'2':
                                System.Diagnostics.Debug.WriteLine("Set alternate character ROM special character set (" + gset + ")");
                                break;
                            default:
                                return false;
                        }

                        Queue = Queue.Skip(3).ToArray();

                        return true;

                    default:
                        return false;
                }

                Queue = Queue.Skip(2).ToArray();
                return true;
            }

            return false;
        }

        private void DeleteChars(int count)
        {
            System.Diagnostics.Debug.WriteLine("Delete chars: " + count.ToString());
            if (CurrentRow >= Buffer.Count)
                return;

            var line = Buffer[CurrentRow];
            while ((line.Count > CurrentColumn) && (count-- > 0))
                line.RemoveAt(CurrentColumn);
        }

        private void SetVT52Mode(bool on)
        {
            System.Diagnostics.Debug.WriteLine("Set VT52 mode: " + on.ToString());
        }

        private void SetAlternateKeypadMode(bool on)
        {
            System.Diagnostics.Debug.WriteLine("Set alternate keypad mode: " + on.ToString());
        }

        private void SaveDECPrivateModeValue(int id)
        {
            System.Diagnostics.Debug.WriteLine("Save DEC Private Mode value " + id.ToString());
        }

        private void SaveCursor()
        {
            System.Diagnostics.Debug.WriteLine("Save cursor");
        }

        private void RestoreCursor()
        {
            System.Diagnostics.Debug.WriteLine("Restore cursor");
        }

        private void EraseToStartOfLine()
        {
            System.Diagnostics.Debug.WriteLine("Erase to start of line");
            if (CurrentRow >= Buffer.Count)
                return;

            var line = Buffer[CurrentRow];
            var toDelete = Math.Min(CurrentColumn, line.Count);

            for (var i = 0; i < toDelete; i++)
                line.RemoveAt(0);

            CurrentColumn = 0;
        }

        private void DeleteLine()
        {
            System.Diagnostics.Debug.WriteLine("Atari VT-52 Delete line");
        }

        private void MoveCursor(int columns, int rows)
        {
            System.Diagnostics.Debug.WriteLine("Move cursor (" + columns.ToString() + ", " + rows.ToString() + ")");

            var topRow = Buffer.Count - VisibleRows;
            if (topRow < 0)
                topRow = 0;
            var bottomRow = topRow + VisibleRows - 1;

            var newRow = CurrentRow + rows;
            CurrentRow = Math.Max(newRow, topRow);
            CurrentRow = Math.Min(CurrentRow, bottomRow);

            CurrentColumn = Math.Max(0, CurrentColumn + columns);
            CurrentColumn = Math.Min(CurrentColumn, VisibleColumns - 1);
        }

        private void Tab()
        {
            System.Diagnostics.Debug.WriteLine("Tab");
        }

        private void ReverseLineFeed()
        {
            System.Diagnostics.Debug.WriteLine("Atari VT-52 Reverse line feed");
        }

        private void InsertLine()
        {
            System.Diagnostics.Debug.WriteLine("Atari VT-52 Insert Line");
        }

        private void EraseLine()
        {
            System.Diagnostics.Debug.WriteLine("Erase line");
            Buffer[CurrentRow].Clear();
        }

        private void EraseToEndOfScreen()
        {
            System.Diagnostics.Debug.WriteLine("Erase to end of screen");
            EraseDown();
        }

        private void SetTab()
        {
            System.Diagnostics.Debug.WriteLine("Set tab " + CurrentColumn.ToString());
        }

        private void ClearAllTabs()
        {
            System.Diagnostics.Debug.WriteLine("Clear all tabs");
        }

        private void ClearTab()
        {
            System.Diagnostics.Debug.WriteLine("Clear tab");
        }

        private void SetCursorPosition(int column, int row)
        {
            System.Diagnostics.Debug.WriteLine("Set cursor position " + column.ToString() + "," + row.ToString());

            CurrentColumn = column;
            CurrentRow = row;
        }

        private void EraseScreen()
        {
            System.Diagnostics.Debug.WriteLine("Erase screen");
            // TODO : Implement something which doesn't delete everything
            Buffer.Clear();
        }

        private void EraseUp()
        {
            System.Diagnostics.Debug.WriteLine("Erase up");
        }

        private void EraseDown()
        {
            System.Diagnostics.Debug.WriteLine("Erase down");
        }

        private void EnableScrolling(int startLine, int endLine)
        {
            System.Diagnostics.Debug.WriteLine("Enable scrolling from " + startLine.ToString() + " to " + endLine.ToString());
        }

        public bool ApplicationCursorKeysMode { get; set; } = false;

        private void ToggleMode(int mode, bool query, bool value)
        {
            System.Diagnostics.Debug.WriteLine("Toggle mode : " + mode.ToString() + ", q=" + query.ToString() + ", v=" + value.ToString());
            switch(mode)
            {
                case 1:     // Application cursor keys
                    ApplicationCursorKeysMode = true;
                    break;
            }
        }

        public byte[] QueueToSend { get; set; }

        public byte[] GetSendQueue()
        {
            var result = QueueToSend;
            QueueToSend = null;
            return result;
        }

        private void Send(byte[] v)
        {
            if (QueueToSend == null)
                QueueToSend = v;
            else
                QueueToSend = QueueToSend.Concat(v).ToArray();
        }

        private void EraseEndOfLine()
        {
            if (Buffer.Count <= CurrentRow)
                return;

            var line = Buffer[CurrentRow];
            while (line.Count > CurrentColumn)
                line.RemoveAt(CurrentColumn);
        }

        private void SetDisplayAttribute(List<int> escParams)
        {
            System.Diagnostics.Debug.WriteLine("Set display attributes [" + string.Join(",", escParams.Select(x => x.ToString()).ToList()) + "]");
            foreach(var param in escParams)
            {
                switch(param)
                {
                    case 0:
                        Attribute.ForegroundColor = TerminalColor.White;
                        Attribute.BackgroundColor = TerminalColor.Black;
                        Attribute.Bright = false;
                        Attribute.Dim = false;
                        Attribute.Standout = false;
                        Attribute.Underscore = false;
                        Attribute.Blink = false;
                        Attribute.Reverse = false;
                        Attribute.Hidden = false;
                        break;

                    case 1:
                        Attribute.Bright = true;
                        break;

                    case 2:
                        Attribute.Dim = true;
                        break;

                    case 3:
                        Attribute.Standout = true;
                        break;

                    case 4:
                        Attribute.Underscore = true;
                        break;

                    case 5:
                        Attribute.Blink = true;
                        break;

                    case 7:
                        Attribute.Reverse = true;
                        break;

                    case 8:
                        Attribute.Hidden = true;
                        break;

                    case 30:
                    case 31:
                    case 32:
                    case 33:
                    case 34:
                    case 35:
                    case 36:
                    case 37:
                    case 38:
                        Attribute.ForegroundColor = (TerminalColor)(param - 30);
                        break;
                    case 39:
                        Attribute.ForegroundColor = TerminalColor.Green;
                        break;
                    case 40:
                    case 41:
                    case 42:
                    case 43:
                    case 44:
                    case 45:
                    case 46:
                    case 47:
                    case 48:
                        Attribute.BackgroundColor = (TerminalColor)(param - 40);
                        break;
                    case 49:
                        Attribute.BackgroundColor = TerminalColor.Black;
                        break;
                }
            }
        }

        private void SetCharacter(int currentColumn, int currentRow, char ch, TerminalAttribute attribute)
        {
            while (Buffer.Count < (currentRow + 1))
                Buffer.Add(new TerminalLine());

            var line = Buffer[currentRow];
            while (line.Count < (currentColumn + 1))
                line.Add(new TerminalCharacter { Char = ' ', Attribute = Attribute });

            var character = line[currentColumn];
            character.Char = ch;
            character.Attribute = attribute.Clone();
        }

        private bool ProcessQueue()
        {
            bool refreshNeeded = false;

            var processed = true;
            while ((Queue.Length > 0) && processed)
            {
                if (ProcessQueueChar())
                {
                    refreshNeeded = true;
                    continue;
                }

                if (ProcessEscapeSequence())
                {
                    refreshNeeded = true;
                    continue;
                }

                if(ProcessSS3())
                {
                    continue;
                }

                processed = false;
            }

            return refreshNeeded;
        }

        public bool Push(byte[] data)
        {
            AppendQueue(data);
            return ProcessQueue();
        }

        private static readonly Dictionary<string, string> KeySequences = new Dictionary<string, string>
        {
            { "F1", "\u008FP" },
            { "F2", "\u008FQ" },
            { "F3", "\u008FR" },
            { "F4", "\u008FS" },
            { "F5", "\u001B[15~" },
            { "F6", "\u001B[17~" },
            { "F7", "\u001B[18~" },
            { "F8", "\u001B[19~" },
            { "F9", "\u001B[20~" },
            { "F10", "\u001B[21~" },
            { "F11", "\u001B[23~" },
            { "F12", "\u001B[24~" },
            { "Up", "\u001B[A" },
            { "Down", "\u001B[B" },
            { "Right", "\u001B[C" },
            { "Left", "\u001B[D" },
            { "Escape", "\u001B" },
        };

        private static readonly Dictionary<string, string> ApplicationModeKeySequences = new Dictionary<string, string>
        {
            { "F1", "\u008FP" },
            { "F2", "\u008FQ" },
            { "F3", "\u008FR" },
            { "F4", "\u008FS" },
            { "F5", "\u001B[15~" },
            { "F6", "\u001B[17~" },
            { "F7", "\u001B[18~" },
            { "F8", "\u001B[19~" },
            { "F9", "\u001B[20~" },
            { "F10", "\u001B[21~" },
            { "F11", "\u001B[23~" },
            { "F12", "\u001B[24~" },
            { "Up", "\u001BOA" },
            { "Down", "\u001BOB" },
            { "Right", "\u001BOC" },
            { "Left", "\u001BOD" },
            { "Escape", "\u001B" },
        };

        public byte [] GetKeySequence(string key)
        {
            if (ApplicationCursorKeysMode)
            {
                if (ApplicationModeKeySequences.TryGetValue(key, out string code))
                    return code.Select(x => (byte)x).ToArray();
            }
            else
            {
                if (KeySequences.TryGetValue(key, out string code))
                    return Encoding.ASCII.GetBytes(code);
            }

            return null;
        }
    }
}
