using System.Drawing;
using System.Windows.Input;
using Wox.Plugin;

namespace Wox.ViewModel
{
    public class ContextMenuItemViewModel : BaseModel
    {
        public string Title { get; set; }
        public string Glyph { get; set; }
        public string FontFamily { get; set; }
        public ICommand Command { get; set; }
        public Key AcceleratorKey { get; set; }
        public ModifierKeys AcceleratorModifiers { get; set; }
        public bool IsAcceleratorKeyEnabled { get; set; }
    }
}