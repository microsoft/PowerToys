namespace Wox.Plugin
{
    public class ActionContext
    {
        public SpecialKeyState SpecialKeyState { get; set; }
    }

    public class SpecialKeyState
    {
        public bool CtrlPressed { get; set; }
        public bool ShiftPressed { get; set; }
        public bool AltPressed { get; set; }
        public bool WinPressed { get; set; }
    }
}