// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using VK = Windows.System.VirtualKey;
using VKM = Windows.System.VirtualKeyModifiers;

namespace Microsoft.CmdPal.UI.ViewModels;

public static class VirtualKeyExtensions
{
    /// <summary>
    /// For any country/region, the Comma and Less Than key (VK_OEM_COMMA)
    /// </summary>
    public const VK OemCommaKey = (VK)188;

    extension(VK key)
    {
        public bool IsAltKey()
        {
            return key is VK.Menu or VK.LeftMenu or VK.RightMenu;
        }

        public bool IsModifierKey()
        {
            return key is VK.Shift or VK.LeftShift or VK.RightShift
                or VK.Control or VK.LeftControl or VK.RightControl
                or VK.Menu or VK.LeftMenu or VK.RightMenu
                or VK.LeftWindows or VK.RightWindows;
        }

        public bool IsTextBoxShortcut(VKM modifiers)
        {
            return (modifiers & VKM.Control) != 0
                   && (modifiers & (VKM.Menu | VKM.Windows)) == 0
                   && key is VK.A or VK.C or VK.V or VK.X or VK.Y or VK.Z;
        }

        public bool IsArrowKey()
        {
            return key is VK.Up or VK.Down or VK.Left or VK.Right;
        }

        public bool IsNavigationKey()
        {
            return key.IsArrowKey()
                   || key is VK.Escape or VK.Tab or VK.Enter or
                       VK.Home or VK.End or VK.PageUp or VK.PageDown;
        }

        public bool IsEditingKey()
        {
            return key is VK.Back or VK.Insert or VK.Delete;
        }

        public bool IsFunctionKey()
        {
            return key is >= VK.F1 and <= VK.F24;
        }

        public bool IsReservedHostShortcut(VKM modifiers)
        {
            return key == VK.F4 // F4: opens dropdown
                   || (modifiers == VKM.Control && key is VK.K or OemCommaKey) // Ctrl+K: opens More menu, Ctrl+,: open settings
                   || (modifiers == VKM.Menu && key is VK.F or VK.Space) // Alt+F: opens filter  menu, Alt+Space: open window menu
                   || (modifiers == VKM.Menu && key is VK.Left) // Alt+Left arrow: navigate back
                   || (modifiers == VKM.Menu && key is VK.Home); // Alt+Home: navigate to home page
        }

        public bool IsContextMenuRequest(VKM modifiers)
        {
            return key == VK.Application
                   || (key == VK.F10 && modifiers == VKM.Shift);
        }

        public bool IsAltNumpadInput(VKM modifiers)
        {
            return (modifiers & VKM.Menu) != 0 &&
                   (modifiers & (VKM.Control | VKM.Windows)) == 0 &&
                   key is >= VK.NumberPad0 and <= VK.NumberPad9;
        }

        // AltGr is reported as Ctrl+Alt; Alt+numpad also contributes to character input.
        public bool MayProduceText(VKM modifiers)
        {
            return key.IsAltNumpadInput(modifiers)
                   || (!key.IsFunctionKey()
                       && ((modifiers & (VKM.Control | VKM.Menu | VKM.Windows)) == 0
                           || (modifiers & (VKM.Control | VKM.Menu)) == (VKM.Control | VKM.Menu)));
        }

        public bool IsPageMenuShortcut(VKM modifiers)
        {
            return !key.IsModifierKey()
                   && !key.IsNavigationKey()
                   && !key.IsEditingKey()
                   && !key.IsReservedHostShortcut(modifiers)
                   && !key.IsContextMenuRequest(modifiers)
                   && !key.IsTextBoxShortcut(modifiers)
                   && !key.MayProduceText(modifiers);
        }
    }
}
