using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TerminalEmulator.StreamParser;

// https://github.com/joejulian/xterm/blob/defc6dd5684a12dc8e56cb6973ef973e7a32caa3/ctlseqs.txt

namespace TerminalEmulator
{
    public class Terminal : TerminalController
    {
        private TerminalBuffer alternativeBuffer = new TerminalBuffer();
        private int alternativeBufferTopRow = 0;

        private TerminalBuffer normalBuffer = new TerminalBuffer();
        private int normalBufferTopRow = 0;

        public EActiveBuffer ActiveBuffer { get; set; } = EActiveBuffer.Normal;

        public bool Debugging = false;

        public TerminalBuffer Buffer
        {
            get
            {
                return ActiveBuffer == EActiveBuffer.Normal ? normalBuffer : alternativeBuffer;
            }
        }

        public int TopRow
        {
            get
            {
                return ActiveBuffer == EActiveBuffer.Normal ? normalBufferTopRow : alternativeBufferTopRow;
            }
            set
            {
                if (ActiveBuffer == EActiveBuffer.Normal)
                    normalBufferTopRow = value;
                else
                    alternativeBufferTopRow = value;
            }
        }

        public int Columns { get; set; } = 80;
        public int Rows { get; set; } = 24;

        public int VisibleColumns { get; set; } = 0;
        public int VisibleRows { get; set; } = 0;

        TerminalCursorState SavedCursorState { get; set; } = null;
        public TerminalCursorState CursorState { get; set; } = new TerminalCursorState();

        public bool InvalidateView = false;
        public TerminalStream stream = new TerminalStream();

        public EventHandler<SendDataEventArgs> SendData;

        public char GetVisibleChar(int x, int y)
        {
            if ((TopRow + y) >= Buffer.Count)
                return ' ';

            var line = Buffer[TopRow + y];
            if (line.Count <= x)
                return ' ';

            return line[x].Char;
        }

        public string GetVisibleChars(int x, int y, int count)
        {
            string result = "";
            for(var i=0; i<count; i++)
                result += GetVisibleChar(x + i, y);

            return result;
        }

        public string GetScreenText()
        {
            string result = "";
            for (var y = 0; y < Rows; y++)
            {
                for (var x = 0; x < Columns; x++)
                    result += GetVisibleChar(x, y);
                if (y < (Rows - 1))
                    result += '\n';
            }
            return result;
        }

        public override void FullReset()
        {
            alternativeBuffer = new TerminalBuffer();
            alternativeBufferTopRow = 0;

            normalBuffer = new TerminalBuffer();
            normalBufferTopRow = 0;

            ActiveBuffer = EActiveBuffer.Normal;

            SavedCursorState = null;
            CursorState = new TerminalCursorState();

            Columns = VisibleColumns;
            Rows = VisibleRows;

            InvalidateView = true;
        }

        private void Log(string message)
        {
            if(Debugging)
                System.Diagnostics.Debug.WriteLine("Terminal: " + message);
        }

        private void LogController(string message)
        {
            if (Debugging)
                System.Diagnostics.Debug.WriteLine("Controller: " + message);
        }

        private void LogExtreme(string message)
        {
            if (Debugging)
                System.Diagnostics.Debug.WriteLine("Terminal: (c=" + CursorState.CurrentColumn.ToString() + ",r=" + CursorState.CurrentRow.ToString() + ")" + message);
        }

        public override void SetCharacterSet(ECharacterSet characterSet)
        {
            LogController("Unimplemented: SetCharacterSet(characterSet:" + characterSet.ToString() + ")");
        }

        public override void TabSet()
        {
            var stop = CursorState.CurrentColumn + 1;
            LogController("TabSet() [cursorX=" + stop.ToString() + "]");

            var tabStops = CursorState.TabStops;
            int index = 0;
            while (index < tabStops.Count && tabStops[index] < stop)
                index++;

            if (index >= tabStops.Count)
                tabStops.Add(stop);
            else if (tabStops[index] != stop)
                tabStops.Insert(index, stop);
        }

        public override void Tab()
        {
            var current = CursorState.CurrentColumn + 1;
            LogController("Tab() [cursorX=" + current.ToString() + "]");

            var tabStops = CursorState.TabStops;
            int index = 0;
            while (index < tabStops.Count && tabStops[index] <= current)
                index++;

            if (index < tabStops.Count)
                SetCursorPosition(tabStops[index], CursorState.CurrentRow + 1);

            // TODO : If past the end of the list, should it tab to the next line's first tab or first char if no tab is present?
        }

        public override void ClearTabs()
        {
            LogController("ClearTabs()");

            CursorState.TabStops.Clear();
        }

        public override void ClearTab()
        {
            var stop = CursorState.CurrentColumn + 1;

            LogController("ClearTab() [cursorX=" + stop.ToString() + "]");

            var tabStops = CursorState.TabStops;
            int index = 0;
            while (index < tabStops.Count && tabStops[index] < stop)
                index++;

            if (index < tabStops.Count && tabStops[index] == stop)
                tabStops.RemoveAt(index);
        }

        public override void CarriageReturn()
        {
            LogExtreme("Carriage return");

            CursorState.CurrentColumn = 0;
            InvalidateView = true;
        }

        public override void NewLine()
        {
            LogExtreme("NewLine()");

            CursorState.CurrentRow++;

            if (CursorState.ScrollBottom == -1 && CursorState.CurrentRow >= VisibleRows)
            {
                LogController("Scroll all (before:" + TopRow.ToString() + ",after:" + (TopRow+1).ToString() + ")");
                TopRow++;
                CursorState.CurrentRow--;
            }
            else if (CursorState.ScrollBottom >= 0 && CursorState.CurrentRow > CursorState.ScrollBottom)
            {
                LogController("Scroll region");

                if (Buffer.Count > (CursorState.ScrollBottom + TopRow))
                    Buffer.Insert(CursorState.ScrollBottom + TopRow + 1, new TerminalLine());

                Buffer.RemoveAt(CursorState.ScrollTop + TopRow);

                CursorState.CurrentRow--;
            }

            InvalidateView = true;
        }

        public override void ReverseIndex()
        {
            LogController("ReverseIndex()");

            CursorState.CurrentRow--;
            if(CursorState.CurrentRow < CursorState.ScrollTop)
            {
                var scrollBottom = 0;
                if (CursorState.ScrollBottom == -1)
                    scrollBottom = TopRow + VisibleRows - 1;
                else
                    scrollBottom = TopRow + CursorState.ScrollBottom;

                if (Buffer.Count > scrollBottom)
                    Buffer.RemoveAt(scrollBottom);

                Buffer.Insert(TopRow + CursorState.ScrollTop, new TerminalLine());

                CursorState.CurrentRow++;
            }
        }

        public override void Backspace()
        {
            LogExtreme("Backspace");

            if (CursorState.CurrentColumn > 0)
            {
                CursorState.CurrentColumn--;

                //if (Buffer.Count > TopRow + CursorState.CurrentRow)
                //{
                //    var line = Buffer[TopRow + CursorState.CurrentRow];
                //    if (line != null && line.Count > CursorState.CurrentColumn)
                //        line.RemoveAt(CursorState.CurrentColumn);
                //}

                InvalidateView = true;
            }
        }

        public override void Bell()
        {
            LogExtreme("Unimplemented: Bell()");
        }

        public override void MoveCursorRelative(int x, int y)
        {
            LogController("MoveCursorRelative(x:" + x.ToString() + ",y:" + y.ToString() + ",vis:[" + VisibleColumns.ToString() + "," + VisibleRows.ToString() + "]" + ")");

            CursorState.CurrentColumn += x;
            if (CursorState.CurrentColumn < 0)
                CursorState.CurrentColumn = 0;
            if (CursorState.CurrentColumn >= Columns)
                CursorState.CurrentColumn = Columns - 1;

            CursorState.CurrentRow += y;
            if (CursorState.CurrentRow < CursorState.ScrollTop)
                CursorState.CurrentRow = CursorState.ScrollTop;

            var scrollBottom = (CursorState.ScrollBottom == -1) ? Rows - 1 : CursorState.ScrollBottom;
            if (CursorState.CurrentRow > scrollBottom)
                CursorState.CurrentRow = scrollBottom;

            InvalidateView = true;
        }

        public override void SetCursorPosition(int column, int row)
        {
            LogController("SetCursorPosition(column:" + column.ToString() + ",row:" + row.ToString() + ")");

            CursorState.CurrentColumn = column - 1;
            CursorState.CurrentRow = row - 1 + (CursorState.OriginMode ? CursorState.ScrollTop : 0);

            InvalidateView = true;
        }

        public override void PutChar(char character)
        {
            LogExtreme("PutChar(ch:'" + character + "'=" + ((int)character).ToString() + ")");

            if(CursorState.InsertMode == EInsertReplaceMode.Insert)
            {
                while (Buffer.Count <= (TopRow + CursorState.CurrentRow))
                    Buffer.Add(new TerminalLine());

                var line = Buffer[TopRow + CursorState.CurrentRow];
                while (line.Count < CursorState.CurrentColumn)
                    line.Add(new TerminalCharacter());

                line.Insert(CursorState.CurrentColumn, new TerminalCharacter());
            }

            SetCharacter(CursorState.CurrentColumn, CursorState.CurrentRow, character, CursorState.Attribute);
            CursorState.CurrentColumn++;

            if (CursorState.WordWrap)
            {
                var line = Buffer[TopRow + CursorState.CurrentRow];
                while (line.Count > Columns)
                    line.RemoveAt(line.Count - 1);
                //if(line.Count > Columns)
                //{
                //    while (Buffer.Count <= (TopRow + CursorState.CurrentRow + 1))
                //        Buffer.Add(new TerminalLine());

                //    var nextLine = Buffer[TopRow + CursorState.CurrentRow + 1];
                //    while(line.Count > Columns)
                //    {
                //        nextLine.Insert(0, line.Last());
                //        line.RemoveAt(line.Count - 1);
                //    }
                //}

                if (CursorState.CurrentColumn >= Columns)
                {
                    CursorState.CurrentColumn = 0;
                    NewLine();
                }
            }

            InvalidateView = true;
        }

        public override void SetWindowTitle(string title)
        {
            LogController("SetWindowTitle(t:'" + title + "')");
        }

        public override void ShiftIn()
        {
            LogController("Unimplemented: ShiftIn()");
        }

        public override void ShiftOut()
        {
            LogController("Unimplemented: ShiftOut()");
        }

        public override void SetCharacterAttribute(int parameter)
        {
            switch (parameter)
            {
                case 0:
                    LogController("SetCharacterAttribute(reset)");
                    CursorState.Attribute.ForegroundColor = TerminalColor.White;
                    CursorState.Attribute.BackgroundColor = TerminalColor.Black;
                    CursorState.Attribute.Bright = false;
                    CursorState.Attribute.Standout = false;
                    CursorState.Attribute.Underscore = false;
                    CursorState.Attribute.Blink = false;
                    CursorState.Attribute.Reverse = false;
                    CursorState.Attribute.Hidden = false;
                    break;

                case 1:
                    LogController("SetCharacterAttribute(bright)");
                    CursorState.Attribute.Bright = true;
                    break;

                case 2:
                    LogController("SetCharacterAttribute(dim)");
                    CursorState.Attribute.Bright = false;
                    break;

                case 3:
                    LogController("SetCharacterAttribute(standout)");
                    CursorState.Attribute.Standout = true;
                    break;

                case 4:
                    LogController("SetCharacterAttribute(underscore)");
                    CursorState.Attribute.Underscore = true;
                    break;

                case 5:
                    LogController("SetCharacterAttribute(blink)");
                    CursorState.Attribute.Blink = true;
                    break;

                case 7:
                    LogController("SetCharacterAttribute(reverse)");
                    CursorState.Attribute.Reverse = true;
                    break;

                case 8:
                    LogController("SetCharacterAttribute(hidden)");
                    CursorState.Attribute.Hidden = true;
                    break;

                case 22:
                    LogController("SetCharacterAttribute(not bright)");
                    CursorState.Attribute.Bright = false;
                    break;

                case 24:
                    LogController("SetCharacterAttribute(not underlined)");
                    CursorState.Attribute.Underscore = false;
                    break;

                case 25:
                    LogController("SetCharacterAttribute(steady)");
                    CursorState.Attribute.Blink = false;
                    break;

                case 27:
                    LogController("SetCharacterAttribute(not reverse)");
                    CursorState.Attribute.Reverse = false;
                    break;

                case 28:
                    LogController("SetCharacterAttribute(not hidden)");
                    CursorState.Attribute.Hidden = false;
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
                    CursorState.Attribute.ForegroundColor = (TerminalColor)(parameter - 30);
                    LogController("SetCharacterAttribute(foreground:" + CursorState.Attribute.ForegroundColor.ToString() + ")");
                    break;
                case 39:
                    CursorState.Attribute.ForegroundColor = TerminalColor.White;
                    LogController("SetCharacterAttribute(foreground:default)");
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
                    CursorState.Attribute.BackgroundColor = (TerminalColor)(parameter - 40);
                    LogController("SetCharacterAttribute(background:" + CursorState.Attribute.BackgroundColor.ToString() + ")");
                    break;
                case 49:
                    CursorState.Attribute.BackgroundColor = TerminalColor.Black;
                    LogController("SetCharacterAttribute(background:default)");
                    break;

                default:
                    LogController("SetCharacterAttribute(parameter:" + parameter + ")");
                    break;
            }
        }

        public override void SetCharacterSize(ECharacterSize size)
        {
            LogController("SetCharacterSize(size:" + size.ToString() + ")");

            while ((CursorState.CurrentRow + TopRow) >= Buffer.Count)
                Buffer.Add(new TerminalLine());
            var currentLine = Buffer[CursorState.CurrentRow + TopRow];

            switch(size)
            {
                default:
                case ECharacterSize.SingleWidthLine:
                    currentLine.DoubleWidth = false;
                    currentLine.DoubleHeightTop = false;
                    currentLine.DoubleHeightBottom = false;
                    break;
                case ECharacterSize.DoubleHeightLineTop:
                    currentLine.DoubleWidth = true;
                    currentLine.DoubleHeightBottom = false;
                    currentLine.DoubleHeightTop = true;
                    break;
                case ECharacterSize.DoubleHeightLineBottom:
                    currentLine.DoubleWidth = true;
                    currentLine.DoubleHeightTop = false;
                    currentLine.DoubleHeightBottom = true;
                    break;
                case ECharacterSize.DoubleWidthLine:
                    currentLine.DoubleHeightTop = false;
                    currentLine.DoubleHeightBottom = false;
                    currentLine.DoubleWidth = true;
                    break;
                case ECharacterSize.ScreenAlignmentTest:
                    ScreenAlignmentTest();
                    break;
            }
        }

        public void ScreenAlignmentTest()
        {
            for (var y = 0; y < VisibleRows; y++)
                for (var x = 0; x < VisibleColumns; x++)
                    SetCharacter(x, y, 'E', CursorState.Attribute);
        }

        public override void SaveCursor()
        {
            LogController("SaveCursor()");

            SavedCursorState = CursorState.Clone();

            LogController("     C=" + CursorState.CurrentColumn.ToString() + ",R=" + CursorState.CurrentRow.ToString());
        }
        public override void RestoreCursor()
        {
            LogController("RestoreCursor()");

            if (SavedCursorState != null)
                CursorState = SavedCursorState.Clone();

            LogController("     C=" + CursorState.CurrentColumn.ToString() + ",R=" + CursorState.CurrentRow.ToString());
        }

        public override void EnableNormalBuffer()
        {
            LogController("EnableNormalBuffer()");
            ActiveBuffer = EActiveBuffer.Normal;
            InvalidateView = true;
        }

        public override void EnableAlternateBuffer()
        {
            LogController("EnableAlternateBuffer()");
            ActiveBuffer = EActiveBuffer.Alternative;
            InvalidateView = true;
        }

        public override void UseHighlightMouseTracking(bool enable)
        {
            LogController("Unimplemented: UseHighlightMouseTracking(enable:" + enable.ToString() + ")");
        }

        public override void UseCellMotionMouseTracking(bool enable)
        {
            LogController("Unimplemented: UseCellMotionMouseTracking(enable:" + enable.ToString() + ")");
        }

        public override void EnableSgrMouseMode(bool enable)
        {
            LogController("Unimplemented: EnableSgrMouseMode(enable:" + enable.ToString() + ")");
        }

        public override void SaveEnableNormalBuffer()
        {
            LogController("Unimplemented: SaveEnableNormalBuffer()");
        }

        public override void RestoreEnableNormalBuffer()
        {
            LogController("Unimplemented: RestoreEnableNormalBuffer()");
        }

        public override void SaveUseHighlightMouseTracking()
        {
            LogController("Unimplemented: SaveUseHighlightMouseTracking()");
        }

        public override void RestoreUseHighlightMouseTracking()
        {
            LogController("Unimplemented: RestoreUseHighlightMouseTracking()");
        }

        public override void SaveUseCellMotionMouseTracking()
        {
            LogController("Unimplemented: SaveUseCellMotionMouseTracking()");
        }

        public override void RestoreUseCellMotionMouseTracking()
        {
            LogController("Unimplemented: RestoreUseCellMotionMouseTracking()");
        }

        public override void SaveEnableSgrMouseMode()
        {
            LogController("Unimplemented: SaveEnableSgrMouseMode()");
        }

        public override void RestoreEnableSgrMouseMode()
        {
            LogController("Unimplemented: RestoreEnableSgrMouseMode()");
        }

        public override void SetBracketedPasteMode(bool enable)
        {
            LogController("Unimplemented: SetBracketedPasteMode(enable:" + enable.ToString() + ")");
        }

        public override void SaveBracketedPasteMode()
        {
            LogController("Unimplemented: SaveBracketedPasteMode()");
        }

        public override void RestoreBracketedPasteMode()
        {
            LogController("Unimplemented: RestoreBracketedPasteMode()");
        }

        public override void SetInsertReplaceMode(EInsertReplaceMode mode)
        {
            LogController("Unimplemented: SetInsertReplaceMode(mode:" + mode.ToString() + ")");
            CursorState.InsertMode = mode;
        }

        public override void ClearScrollingRegion()
        {
            LogController("ClearScrollingRegion()");
            CursorState.ScrollTop = 0;
            CursorState.ScrollBottom = -1;
        }

        public override void SetAutomaticNewLine(bool enable)
        {
            LogController("Unimplemented: SetAutomaticNewLine(enable:" + enable.ToString() + ")");
        }

        public override void EnableApplicationCursorKeys(bool enable)
        {
            LogController("EnableApplicationCursorKeys(enable:" + enable.ToString() + ")");
            CursorState.ApplicationCursorKeysMode = enable;
        }

        public override void SaveCursorKeys()
        {
            LogController("Unimplemented: SaveCursorKeys()");
        }

        public override void RestoreCursorKeys()
        {
            LogController("Unimplemented: RestoreCursorKeys()");
        }

        public override void SetKeypadType(EKeypadType type)
        {
            LogController("Unimplemented: SetKeypadType(type:" + type.ToString() + ")");
        }

        public override void SetScrollingRegion(int top, int bottom)
        {
            LogController("SetScrollingRegion(top:" + top.ToString() + ",bottom:" + bottom.ToString() + ")");

            if (top == 1 && bottom == VisibleRows)
                ClearScrollingRegion();
            else
            {
                CursorState.ScrollTop = top - 1;
                CursorState.ScrollBottom = bottom - 1;

                if (CursorState.OriginMode)
                    CursorState.CurrentRow = CursorState.ScrollTop;
            }
        }

        public override void EraseLine()
        {
            LogController("EraseLine()");

            for (var i = 0; i < Columns; i++)
                SetCharacter(i, CursorState.CurrentRow, ' ', CursorState.Attribute);

            var line = Buffer[TopRow + CursorState.CurrentRow];
            while (line.Count > Columns)
                line.RemoveAt(line.Count - 1);

            InvalidateView = true;
        }

        public override void EraseToEndOfLine()
        {
            LogController("EraseToEndOfLine()");

            for (var i = CursorState.CurrentColumn; i < Columns; i++)
                SetCharacter(i, CursorState.CurrentRow, ' ', CursorState.Attribute);

            var line = Buffer[TopRow + CursorState.CurrentRow];
            while (line.Count > Columns)
                line.RemoveAt(line.Count - 1);

            InvalidateView = true;
        }

        public override void EraseToStartOfLine()
        {
            LogController("EraseToStartOfLine()");

            for (var i = 0; i < Columns && i <= CursorState.CurrentColumn; i++)
                SetCharacter(i, CursorState.CurrentRow, ' ', CursorState.Attribute);

            var line = Buffer[TopRow + CursorState.CurrentRow];
            while (line.Count > Columns)
                line.RemoveAt(line.Count - 1);

            InvalidateView = true;
        }

        public override void EraseBelow()
        {
            // TODO : Optimize
            LogController("EraseBelow()");

            for (var y = CursorState.CurrentRow + 1; y < VisibleRows; y++)
            {
                for (var x = 0; x < VisibleColumns; x++)
                    SetCharacter(x, y, ' ', CursorState.Attribute);

                var line = Buffer[TopRow + y];
                while (line.Count > Columns)
                    line.RemoveAt(line.Count - 1);
            }


            for (var x = CursorState.CurrentColumn; x < VisibleColumns; x++)
                SetCharacter(x, CursorState.CurrentRow, ' ', CursorState.Attribute);
        }

        public override void EraseAbove()
        {
            // TODO : Optimize
            LogController("EraseAbove()");

            for (var y = CursorState.CurrentRow - 1; y >= 0; y--)
            {
                for (var x = 0; x < VisibleColumns; x++)
                    SetCharacter(x, y, ' ', CursorState.Attribute);

                var line = Buffer[TopRow + y];
                while (line.Count > Columns)
                    line.RemoveAt(line.Count - 1);
            }

            for (var x = 0; x <= CursorState.CurrentColumn; x++)
                SetCharacter(x, CursorState.CurrentRow, ' ', CursorState.Attribute);
        }

        public override void DeleteLines(int count)
        {
            // TODO : Verify it works with scroll range
            LogController("Unimplemented: DeleteLines(count:" + count.ToString() + ")");

            if ((CursorState.CurrentRow + TopRow) >= Buffer.Count)
                return;

            while ((count > 0) && (CursorState.CurrentRow + TopRow) < Buffer.Count)
                Buffer.RemoveAt(CursorState.CurrentRow);

            InvalidateView = true;
        }

        public override void EraseAll()
        {
            // TODO : Verify it works with scroll range
            LogController("Unimplemented: EraseAll()");
            Buffer.Clear();
            SetCursorPosition(1, 1);
            InvalidateView = true;
        }

        public override void DeleteCharacter(int count)
        {
            LogController("DeleteCharacter(count:" + count.ToString() + ")");

            if (CursorState.CurrentRow >= Buffer.Count)
                return;

            var line = Buffer[CursorState.CurrentRow];

            while (count > 0 && CursorState.CurrentColumn < line.Count)
            {
                line.RemoveAt(CursorState.CurrentColumn);
                count--;
            }

            InvalidateView = true;
        }

        public override void Enable132ColumnMode(bool enable)
        {
            // TODO : Consider removeing column setting and instead make host resize the terminal
            LogController("Enable132ColumnMode(enable:" + enable.ToString() + ")");
            Columns = enable ? 132 : 80;
        }

        public override void EnableSmoothScrollMode(bool enable)
        {
            LogController("Unimplemented: EnableSmoothScrollMode(enable:" + enable.ToString() + ")");
        }

        public override void EnableReverseVideoMode(bool enable)
        {
            LogController("EnableReverseVideoMode(enable:" + enable.ToString() + ")");
            CursorState.ReverseVideoMode = enable;

            InvalidateView = true;
        }

        public override void EnableOriginMode(bool enable)
        {
            LogController("EnableOriginMode(enable:" + enable.ToString() + ")");
            CursorState.OriginMode = enable;
        }

        public override void EnableWrapAroundMode(bool enable)
        {
            LogController("EnableWrapAroundMode(enable:" + enable.ToString() + ")");
            CursorState.WordWrap = enable;
        }

        public override void EnableAutoRepeatKeys(bool enable)
        {
            LogController("Unimplemented: EnableAutoRepeatKeys(enable:" + enable.ToString() + ")");
        }

        public override void Enable80132Mode(bool enable)
        {
            LogController("Unimplemented: Enable80132Mode(enable:" + enable.ToString() + ")");
            if (!enable)
                Columns = VisibleColumns;
        }

        public override void EnableReverseWrapAroundMode(bool enable)
        {
            LogController("Unimplemented: EnableReverseWrapAroundMode(enable:" + enable.ToString() + ")");
        }
        
        public static readonly byte[] VT102DeviceAttributes = { 0x1B, (byte)'[', (byte)'?', (byte)'6', (byte)'C' };

        public override void SendDeviceAttributes()
        {
            LogController("SendDeviceAttributes()");
            SendData.Invoke(this, new SendDataEventArgs { Data = VT102DeviceAttributes });
        }

        public void ResizeView(int columns, int rows)
        {
            VisibleColumns = columns;
            VisibleRows = rows;
            Columns = columns;
            Rows = rows;

            if (CursorState.CurrentRow >= Rows)
            {
                var offset = CursorState.CurrentRow - Rows + 1;
                TopRow += offset;
                CursorState.CurrentRow -= offset;
            }
        }

        private void Send(byte[] v)
        {
            SendData.Invoke(this, new SendDataEventArgs { Data = v });
        }

        private void SetCharacter(int currentColumn, int currentRow, char ch, TerminalAttribute attribute)
        {
            while (Buffer.Count < (currentRow + TopRow + 1))
                Buffer.Add(new TerminalLine());

            var line = Buffer[currentRow + TopRow];
            while (line.Count < (currentColumn + 1))
                line.Add(new TerminalCharacter { Char = ' ', Attribute = CursorState.Attribute });

            var character = line[currentColumn];
            character.Char = ch;
            character.Attribute = attribute.Clone();
        }

        public bool Consume(byte[] data)
        {
            stream.Add(data);

            InvalidateView = false;
            while (!stream.AtEnd)
            {
                try
                {
                    var sequence = TerminalSequenceReader.ConsumeNextSequence(stream);
                    XTermSequenceHandlers.ProcessSequence(sequence, this);
                }
                catch (IndexOutOfRangeException)
                {
                    stream.PopAllStates();
                    break;
                }
                catch (ArgumentException)
                {
                    // We've reached an invalid state of the stream.
                    stream.ReadRaw();
                    stream.Commit();
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine("Unknown exception " + e.Message);
                }
            }

            stream.Flush();
            return InvalidateView;
        }

        public bool Push(byte[] data)
        {
            return Consume(data);
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
            { "Home", "\u001B[1~" },
            { "End", "\u001B[4~" },
            { "Insert", "\u001B[2~" },
            { "Delete", "\u001B[3~" },
            { "PageUp", "\u001B[5~" },
            { "PageDown", "\u001B[6~" },
            { "Ctrl+Tab", "\t" },
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
            { "Home", "\u001B[1~" },
            { "End", "\u001B[4~" },
            { "Insert", "\u001B[2~" },
            { "Delete", "\u001B[3~" },
            { "PageUp", "\u001B[5~" },
            { "PageDown", "\u001B[6~" },
            { "Ctrl+Tab", "\t" },
        };

        public byte [] GetKeySequence(string key)
        {
            if (CursorState.ApplicationCursorKeysMode)
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
