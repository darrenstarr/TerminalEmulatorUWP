namespace TerminalEmulator
{
    public abstract class TerminalController
    {
        public abstract void MoveCursorRelative(int x, int y);
        public abstract void SetCursorPosition(int column, int row);
        public abstract void CarriageReturn();
        public abstract void NewLine();
        public abstract void PutChar(char character);
        public abstract void SetWindowTitle(string title);
        public abstract void SetCharacterAttribute(int parameter);

        // https://www.vt100.net/docs/vt510-rm/DECSC.html
        public abstract void SaveCursor();
        public abstract void RestoreCursor();
        public abstract void EnableAlternateBuffer();
        public abstract void EnableNormalBuffer();
        public abstract void ClearScrollingRegion();
        public abstract void SetScrollingRegion(int top, int bottom);
        public abstract void EraseToEndOfLine();
        public abstract void EraseToStartOfLine();
        public abstract void EraseLine();
        public abstract void Backspace();
        public abstract void Bell();
        public abstract void DeleteCharacter(int count);
        public abstract void EraseBelow();
        public abstract void EraseAbove();
        public abstract void EraseAll();
        public abstract void ShiftIn();
        public abstract void ShiftOut();
        public abstract void UseHighlightMouseTracking(bool enable);
        public abstract void UseCellMotionMouseTracking(bool enable);
        public abstract void SetCharacterSet(ECharacterSet characterSet);
        public abstract void VerticalTab();
        public abstract void EnableSgrMouseMode(bool enable);
        public abstract void FormFeed();
        public abstract void SaveEnableNormalBuffer();
        public abstract void SaveUseHighlightMouseTracking();
        public abstract void SaveUseCellMotionMouseTracking();
        public abstract void SaveEnableSgrMouseMode();
        public abstract void RestoreEnableNormalBuffer();
        public abstract void RestoreUseHighlightMouseTracking();
        public abstract void RestoreUseCellMotionMouseTracking();
        public abstract void RestoreEnableSgrMouseMode();
        public abstract void SetBracketedPasteMode(bool enable);
        public abstract void SaveBracketedPasteMode();
        public abstract void RestoreBracketedPasteMode();
        public abstract void SetInsertReplaceMode(EInsertReplaceMode mode);
        public abstract void SetAutomaticNewLine(bool enable);
        public abstract void EnableApplicationCursorKeys(bool enable);
        public abstract void SaveCursorKeys();
        public abstract void RestoreCursorKeys();
        public abstract void SetKeypadType(EKeypadType type);
        public abstract void DeleteLines(int count);
        public abstract void FullReset();
        public abstract void SendDeviceAttributes();
        public abstract void SendDeviceAttributesSecondary();
        public abstract void TabSet();
        public abstract void Tab();
        public abstract void ClearTabs();
        public abstract void ClearTab();
        public abstract void Enable132ColumnMode(bool enable);
        public abstract void EnableSmoothScrollMode(bool enable);
        public abstract void EnableReverseVideoMode(bool enable);
        public abstract void EnableOriginMode(bool enable);
        public abstract void EnableWrapAroundMode(bool enable);
        public abstract void EnableAutoRepeatKeys(bool enable);
        public abstract void Enable80132Mode(bool enable);
        public abstract void EnableReverseWrapAroundMode(bool enable);
        public abstract void ReverseIndex();
        public abstract void SetCharacterSize(ECharacterSize size);
        public abstract void SetLatin1();
        public abstract void SetUTF8();
        public abstract void InsertBlanks(int count);
        public abstract void EnableBlinkingCursor(bool enable);
        public abstract void ShowCursor(bool show);
        public abstract void DeviceStatusReport();
        public abstract void ReportCursorPosition();
        public abstract void InsertLines(int count);
    }
}
