// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;

namespace MouseJumpUI.NativeMethods;

internal static partial class User32
{
    /// <remarks>
    /// See https://github.com/dotnet/pinvoke/blob/main/src/User32/User32+WindowMessage.cs
    /// </remarks>
    [SuppressMessage("SA1124", "SA1124:DoNotUseRegions", Justification = "Allow rule here")]
    [SuppressMessage("SA1512", "SA1512:SingleLineCommentsMustNotBeFollowedByBlankLine", Justification = "Allow rule here")]
    [SuppressMessage("SA1515", "SA1515:SingleLineCommentMustBePrecededByBlankLine\r\n", Justification = "Allow rule here")]
    internal enum MESSAGE_TYPE : uint
    {
        #region Learn / Windows / Apps / Win32 / Desktop Technologies / Accessibility / Windows Accessibility features
        // see https://learn.microsoft.com/en-us/windows/win32/winauto/about-windows-accessibility-features

        #region Windows Accessibility API Reference / Microsoft Active Accessibility / C/C++ Reference Active Accessibility User Interfaces Services / WM_GETOBJECT Window Message
        // see https://learn.microsoft.com/en-us/windows/win32/winauto/wm-getobject

        /// <summary>
        /// Sent by both Microsoft Active Accessibility and Microsoft UI Automation
        /// to obtain information about an accessible object contained in a server application.
        /// </summary>
        WM_GETOBJECT = 0x003d,

        #endregion

        #endregion

        #region Learn / Windows / Apps / Win32 / Desktop Technologies / Desktop App User Interface / Dialog Boxes
        // see https://learn.microsoft.com/en-us/windows/win32/dlgbox/dialog-boxes

        #region Dialog Box Reference / Dialog Box Notifications
        // see https://learn.microsoft.com/en-us/windows/win32/dlgbox/dialog-box-notifications

        /// <summary>
        /// Sent to a dialog box before the system draws the dialog box.
        /// By responding to this message, the dialog box can set its text
        /// and background colors using the specified display device context handle.
        /// </summary>
        WM_CTLCOLORDLG = 0x0136,

        /// <summary>
        /// Sent to the owner window of a modal dialog box or menu that is entering an idle state.
        /// A modal dialog box or menu enters an idle state when no messages are waiting in its
        /// queue after it has processed one or more previous messages.
        /// </summary>
        WM_ENTERIDLE = 0x0121,

        /// <summary>
        /// Sent to the window procedure associated with a control. By default, the system handles
        /// all keyboard input to the control; the system interprets certain types of keyboard
        /// input as dialog box navigation keys. To override this default behavior, the control
        /// can respond to the WM_GETDLGCODE message to indicate the types of input it wants to
        /// process itself.
        /// </summary>
        WM_GETDLGCODE = 0x0087,

        /// <summary>
        /// Sent to the dialog box procedure immediately before a dialog box is displayed.
        /// Dialog box procedures typically use this message to initialize controls and carry
        /// out any other initialization tasks that affect the appearance of the dialog box.
        /// </summary>
        WM_INITDIALOG = 0x0110,

        /// <summary>
        /// Sent to a dialog box procedure to set the keyboard focus to a different control
        /// in the dialog box.
        /// </summary>
        WM_NEXTDLGCTL = 0x0028,

        #endregion

        #endregion

        #region Learn / Windows / Apps / Win32 / Desktop Technologies / Desktop App User Interface / User Interaction / Legacy / Keyboard and Mouse Input
        // see https://learn.microsoft.com/en-us/windows/win32/inputdev/user-input

        #region Keyboard Input / Keyboard Input Reference / Keyboard Input Notifications
        // see https://learn.microsoft.com/en-us/windows/win32/inputdev/keyboard-input-notifications

        /// <summary>
        /// Sent to both the window being activated and the window being deactivated. If the windows
        /// use the same input queue, the message is sent synchronously, first to the window procedure
        /// of the top-level window being deactivated, then to the window procedure of the top-level
        /// window being activated. If the windows use different input queues, the message is sent
        /// asynchronously, so the window is activated immediately.
        /// </summary>
        WM_ACTIVATE = 0x0006,

        /// <summary>
        /// Notifies a window that the user generated an application command event, for example, by
        /// clicking an application command button using the mouse or typing an application command
        /// key on the keyboard.
        /// </summary>
        WM_APPCOMMAND = 0x0319,

        /// <summary>
        /// Posted to the window with the keyboard focus when a WM_KEYDOWN message is translated by
        /// the TranslateMessage function. The WM_CHAR message contains the character code of the
        /// key that was pressed.
        /// </summary>
        WM_CHAR = 0x0102,

        /// <summary>
        /// Posted to the window with the keyboard focus when a WM_KEYUP message is translated by
        /// the TranslateMessage function. WM_DEADCHAR specifies a character code generated by a
        /// dead key. A dead key is a key that generates a character, such as the umlaut (double-dot),
        /// that is combined with another character to form a composite character. For example, the
        /// umlaut-O character ( ) is generated by typing the dead key for the umlaut character,
        /// and then typing the O key.
        /// </summary>
        WM_DEADCHAR = 0x0103,

        /// <summary>
        /// Posted when the user presses a hot key registered by the RegisterHotKey function.
        /// The message is placed at the top of the message queue associated with the thread
        /// that registered the hot key.
        /// </summary>
        WM_HOTKEY = 0x0312,

        /// <summary>
        /// Posted to the window with the keyboard focus when a nonsystem key is pressed.
        /// A nonsystem key is a key that is pressed when the ALT key is not pressed.
        /// </summary>
        WM_KEYDOWN = 0x0100,

        /// <summary>
        /// Posted to the window with the keyboard focus when a nonsystem key is released.
        /// A nonsystem key is a key that is pressed when the ALT key is not pressed, or a
        /// keyboard key that is pressed when a window has the keyboard focus.
        /// </summary>
        WM_KEYUP = 0x0101,

        /// <summary>
        /// Sent to a window immediately before it loses the keyboard focus.
        /// </summary>
        WM_KILLFOCUS = 0x0008,

        /// <summary>
        /// Sent to a window after it has gained the keyboard focus.
        /// </summary>
        WM_SETFOCUS = 0x0007,

        /// <summary>
        /// Sent to the window with the keyboard focus when a WM_SYSKEYDOWN message is translated
        /// by the TranslateMessage function. WM_SYSDEADCHAR specifies the character code of a
        /// system dead key that is, a dead key that is pressed while holding down the ALT key.
        /// </summary>
        WM_SYSDEADCHAR = 0x0107,

        /// <summary>
        /// Posted to the window with the keyboard focus when the user presses the F10 key (which
        /// activates the menu bar) or holds down the ALT key and then presses another key. It
        /// also occurs when no window currently has the keyboard focus; in this case, the
        /// WM_SYSKEYDOWN message is sent to the active window. The window that receives the
        /// message can distinguish between these two contexts by checking the context code in the
        /// lParam parameter.
        /// </summary>
        WM_SYSKEYDOWN = 0x104,

        /// <summary>
        /// Posted to the window with the keyboard focus when the user releases a key that was
        /// pressed while the ALT key was held down. It also occurs when no window currently has
        /// the keyboard focus; in this case, the WM_SYSKEYUP message is sent to the active window.
        /// The window that receives the message can distinguish between these two contexts by
        /// checking the context code in the lParam parameter.
        /// </summary>
        WM_SYSKEYUP = 0x0105,

        /// <summary>
        /// The WM_UNICHAR message can be used by an application to post input to other windows.
        /// This message contains the character code of the key that was pressed. (Test whether
        /// a target app can process WM_UNICHAR messages by sending the message with wParam set
        /// to UNICODE_NOCHAR.)
        /// </summary>
        WM_UNICHAR = 0x0109,

        #endregion

        #region Mouse Input / Mouse Input Reference / Mouse Input Notifications
        // see https://learn.microsoft.com/en-us/windows/win32/inputdev/mouse-input-notifications

        /// <summary>
        /// Sent to the window that is losing the mouse capture.
        /// </summary>
        WM_CAPTURECHANGED = 0x0215,

        /// <summary>
        /// Posted when the user double-clicks the left mouse button while the cursor is in the
        /// client area of a window. If the mouse is not captured, the message is posted to the
        /// window beneath the cursor. Otherwise, the message is posted to the window that has
        /// captured the mouse.
        /// </summary>
        WM_LBUTTONDBLCLK = 0x0203,

        /// <summary>
        /// Posted when the user presses the left mouse button while the cursor is in the client
        /// area of a window. If the mouse is not captured, the message is posted to the window
        /// beneath the cursor. Otherwise, the message is posted to the window that has captured
        /// the mouse.
        /// </summary>
        WM_LBUTTONDOWN = 0x0201,

        /// <summary>
        /// Posted when the user releases the left mouse button while the cursor is in the client
        /// area of a window. If the mouse is not captured, the message is posted to the window
        /// beneath the cursor. Otherwise, the message is posted to the window that has captured
        /// the mouse.
        /// </summary>
        WM_LBUTTONUP = 0x0202,

        /// <summary>
        /// Posted when the user double-clicks the middle mouse button while the cursor is in the
        /// client area of a window. If the mouse is not captured, the message is posted to the
        /// window beneath the cursor. Otherwise, the message is posted to the window that has
        /// captured the mouse.
        /// </summary>
        WM_MBUTTONDBLCLK = 0x0209,

        /// <summary>
        /// Posted when the user presses the middle mouse button while the cursor is in the client
        /// area of a window. If the mouse is not captured, the message is posted to the window
        /// beneath the cursor. Otherwise, the message is posted to the window that has captured
        /// the mouse.
        /// </summary>
        WM_MBUTTONDOWN = 0x0207,

        /// <summary>
        /// Posted when the user releases the middle mouse button while the cursor is in the client
        /// area of a window. If the mouse is not captured, the message is posted to the window
        /// beneath the cursor. Otherwise, the message is posted to the window that has captured
        /// the mouse.
        /// </summary>
        WM_MBUTTONUP = 0x0208,

        /// <summary>
        /// Sent when the cursor is in an inactive window and the user presses a mouse button. The
        /// parent window receives this message only if the child window passes it to the
        /// DefWindowProc function.
        /// </summary>
        WM_MOUSEACTIVATE = 0x0021,

        /// <summary>
        /// Posted to a window when the cursor hovers over the client area of the window for the
        /// period of time specified in a prior call to TrackMouseEvent.
        /// </summary>
        WM_MOUSEHOVER = 0x02A1,

        /// <summary>
        /// Sent to the active window when the mouse's horizontal scroll wheel is tilted or rotated.
        /// The DefWindowProc function propagates the message to the window's parent. There should
        /// be no internal forwarding of the message, since DefWindowProc propagates it up the
        /// parent chain until it finds a window that processes it.
        /// </summary>
        WM_MOUSEHWHEEL = 0x020E,

        /// <summary>
        /// Posted to a window when the cursor leaves the client area of the window specified
        /// in a prior call to TrackMouseEvent.
        /// </summary>
        WM_MOUSELEAVE = 0x02A3,

        /// <summary>
        /// Posted to a window when the cursor moves. If the mouse is not captured, the message is
        /// posted to the window that contains the cursor. Otherwise, the message is posted to the
        /// window that has captured the mouse.
        /// </summary>
        WM_MOUSEMOVE = 0x0200,

        /// <summary>
        /// Sent to the focus window when the mouse wheel is rotated. The DefWindowProc function
        /// propagates the message to the window's parent. There should be no internal forwarding
        /// of the message, since DefWindowProc propagates it up the parent chain until it finds
        /// a window that processes it.
        /// </summary>
        WM_MOUSEWHEEL = 0x020A,

        /// <summary>
        /// Sent to a window in order to determine what part of the window corresponds to a particular
        /// screen coordinate. This can happen, for example, when the cursor moves, when a mouse
        /// button is pressed or released, or in response to a call to a function such as WindowFromPoint.
        /// If the mouse is not captured, the message is sent to the window beneath the cursor. Otherwise,
        /// the message is sent to the window that has captured the mouse.
        /// </summary>
        WM_NCHITTEST = 0x0084,

        /// <summary>
        /// Posted when the user double-clicks the left mouse button while the cursor is within the
        /// nonclient area of a window. This message is posted to the window that contains the cursor.
        /// If a window has captured the mouse, this message is not posted.
        /// </summary>
        WM_NCLBUTTONDBLCLK = 0x00A3,

        /// <summary>
        /// Posted when the user presses the left mouse button while the cursor is within the nonclient
        /// area of a window. This message is posted to the window that contains the cursor. If a window
        /// has captured the mouse, this message is not posted.
        /// </summary>
        WM_NCLBUTTONDOWN = 0x00A1,

        /// <summary>
        /// Posted when the user releases the left mouse button while the cursor is within the nonclient
        /// area of a window. This message is posted to the window that contains the cursor. If a window
        /// has captured the mouse, this message is not posted.
        /// </summary>
        WM_NCLBUTTONUP = 0x00A2,

        /// <summary>
        /// Posted when the user double-clicks the middle mouse button while the cursor is within the
        /// nonclient area of a window. This message is posted to the window that contains the cursor.
        /// If a window has captured the mouse, this message is not posted.
        /// </summary>
        WM_NCMBUTTONDBLCLK = 0x00A9,

        /// <summary>
        /// Posted when the user presses the middle mouse button while the cursor is within the nonclient
        /// area of a window. This message is posted to the window that contains the cursor. If a window
        /// has captured the mouse, this message is not posted.
        /// </summary>
        WM_NCMBUTTONDOWN = 0x00A7,

        /// <summary>
        /// Posted when the user releases the middle mouse button while the cursor is within the nonclient
        /// area of a window. This message is posted to the window that contains the cursor. If a window
        /// has captured the mouse, this message is not posted.
        /// </summary>
        WM_NCMBUTTONUP = 0x00A8,

        /// <summary>
        /// Posted to a window when the cursor hovers over the nonclient area of the window for the period
        /// of time specified in a prior call to TrackMouseEvent.
        /// </summary>
        WM_NCMOUSEHOVER = 0x02A0,

        /// <summary>
        /// Posted to a window when the cursor leaves the nonclient area of the window specified in a prior
        /// call to TrackMouseEvent.
        /// </summary>
        WM_NCMOUSELEAVE = 0x2a2,

        /// <summary>
        /// Posted to a window when the cursor is moved within the nonclient area of the window. This message
        /// is posted to the window that contains the cursor. If a window has captured the mouse, this message
        /// is not posted.
        /// </summary>
        WM_NCMOUSEMOVE = 0x00a0,

        /// <summary>
        /// Posted when the user double-clicks the right mouse button while the cursor is within the nonclient
        /// area of a window. This message is posted to the window that contains the cursor. If a window has
        /// captured the mouse, this message is not posted.
        /// </summary>
        WM_NCRBUTTONDBLCLK = 0x00A6,

        /// <summary>
        /// Posted when the user presses the right mouse button while the cursor is within the nonclient area
        /// of a window. This message is posted to the window that contains the cursor. If a window has
        /// captured the mouse, this message is not posted.
        /// </summary>
        WM_NCRBUTTONDOWN = 0x00A4,

        /// <summary>
        /// Posted when the user releases the right mouse button while the cursor is within the nonclient
        /// area of a window. This message is posted to the window that contains the cursor. If a window
        /// has captured the mouse, this message is not posted.
        /// </summary>
        WM_NCRBUTTONUP = 0x00A5,

        /// <summary>
        /// Posted when the user double-clicks the first or second X button while the cursor is in the
        /// nonclient area of a window. This message is posted to the window that contains the cursor.
        /// If a window has captured the mouse, this message is not posted.
        /// </summary>
        WM_NCXBUTTONDBLCLK = 0x00AD,

        /// <summary>
        /// Posted when the user presses the first or second X button while the cursor is in the nonclient
        /// area of a window. This message is posted to the window that contains the cursor. If a window
        /// has captured the mouse, this message is not posted.
        /// </summary>
        WM_NCXBUTTONDOWN = 0x00AB,

        /// <summary>
        /// Posted when the user releases the first or second X button while the cursor is in the nonclient
        /// area of a window. This message is posted to the window that contains the cursor. If a window
        /// has captured the mouse, this message is not posted.
        /// </summary>
        WM_NCXBUTTONUP = 0x00AC,

        /// <summary>
        /// Posted when the user double-clicks the right mouse button while the cursor is in the client
        /// area of a window. If the mouse is not captured, the message is posted to the window beneath
        /// the cursor. Otherwise, the message is posted to the window that has captured the mouse.
        /// </summary>
        WM_RBUTTONDBLCLK = 0x0206,

        /// <summary>
        /// Posted when the user presses the right mouse button while the cursor is in the client area
        /// of a window. If the mouse is not captured, the message is posted to the window beneath the
        /// cursor. Otherwise, the message is posted to the window that has captured the mouse.
        /// </summary>
        WM_RBUTTONDOWN = 0x0204,

        /// <summary>
        /// Posted when the user releases the right mouse button while the cursor is in the client
        /// area of a window. If the mouse is not captured, the message is posted to the window beneath
        /// the cursor. Otherwise, the message is posted to the window that has captured the mouse.
        /// </summary>
        WM_RBUTTONUP = 0x0205,

        /// <summary>
        /// Posted when the user double-clicks the first or second X button while the cursor is in the
        /// client area of a window. If the mouse is not captured, the message is posted to the window
        /// beneath the cursor. Otherwise, the message is posted to the window that has captured the mouse.
        /// </summary>
        WM_XBUTTONDBLCLK = 0x020D,

        /// <summary>
        /// Posted when the user presses the first or second X button while the cursor is in the client
        /// area of a window. If the mouse is not captured, the message is posted to the window beneath
        /// the cursor. Otherwise, the message is posted to the window that has captured the mouse.
        /// </summary>
        WM_XBUTTONDOWN = 0x020B,

        /// <summary>
        /// Posted when the user releases the first or second X button while the cursor is in the client
        /// area of a window. If the mouse is not captured, the message is posted to the window beneath
        /// the cursor. Otherwise, the message is posted to the window that has captured the mouse.
        /// </summary>
        WM_XBUTTONUP = 0x020C,

        #endregion

        #endregion

        #region Learn / Windows / Apps / Win32 / Desktop Technologies / Desktop App User Interface / Internationalization
        // see https://learn.microsoft.com/en-us/windows/win32/intl/international-support

        #region Input Method Manager / Input Method Manager Reference / Input Method Manager Messages
        // see https://learn.microsoft.com/en-us/windows/win32/intl/input-method-manager-messages

        /// <summary>
        /// Sent to an application to notify it of changes to the IME window.
        /// </summary>
        WM_IME_NOTIFY = 0x0282,

        /// <summary>
        /// Sent to an application when a window is activated.
        /// </summary>
        WM_IME_SETCONTEXT = 0x281,

        #endregion

        #endregion

        #region Learn / Windows / Apps / Win32 / Desktop Technologies / Desktop App User Interface / Menus and Other Resources
        // see https://learn.microsoft.com/en-us/windows/win32/menurc/resources

        #region Cursors / Cursor Reference / Cursor Notifications
        // see https://learn.microsoft.com/en-us/windows/win32/menurc/cursor-notifications

        /// <summary>
        /// Sent to a window if the mouse causes the cursor to move within
        /// a window and mouse input is not captured.
        /// </summary>
        WM_SETCURSOR = 0x0020,

        #endregion

        #region Keyboard Accelerators / Keyboard Accelerator Reference / Keyboard Accelerator Notifications
        // see https://learn.microsoft.com/en-us/windows/win32/menurc/keyboard-accelerator-notifications

        /// <summary>
        /// Sent when a drop-down menu or submenu is about to become active.
        /// This allows an application to modify the menu before it is displayed, without changing the entire menu.
        /// </summary>
        WM_INITMENUPOPUP = 0x0117,

        /// <summary>
        /// Sent when a menu is active and the user presses a key that does not correspond to any
        /// mnemonic or accelerator key. This message is sent to the window that owns the menu.
        /// </summary>
        WM_MENUCHAR = 0x0120,

        /// <summary>
        /// Sent to a menu's owner window when the user selects a menu item.
        /// </summary>
        WM_MENUSELECT = 0x011F,

        /// <summary>
        /// Posted to the window with the keyboard focus when a WM_SYSKEYDOWN message is translated by
        /// the TranslateMessage function. It specifies the character code of a system character key
        /// that is, a character key that is pressed while the ALT key is down.
        /// </summary>
        WM_SYSCHAR = 0x0106,

        /// <summary>
        /// A window receives this message when the user chooses a command from the Window menu
        /// (formerly known as the system or control menu) or when the user chooses the maximize
        /// button, minimize button, restore button, or close button.
        /// </summary>
        WM_SYSCOMMAND = 0x0112,

        #endregion

        #endregion

        #region Learn / Windows / Apps / Win32 / Desktop Technologies / Desktop App User Interface / Windows and Messages
        // see https://learn.microsoft.com/en-us/windows/win32/winmsg/windowing

        #region Windows / Window Reference / Window Messages
        // see https://learn.microsoft.com/en-us/windows/win32/winmsg/window-messages

        /// <summary>
        /// Retrieves the menu handle for the current window.
        /// </summary>
        MN_GETHMENU = 0x01E1,

        /// <summary>
        /// Sent when the window background must be erased (for example, when a window is resized).
        /// The message is sent to prepare an invalidated portion of a window for painting.
        /// </summary>
        WM_ERASEBKGND = 0x0014,

        /// <summary>
        /// Retrieves the font with which the control is currently drawing its text.
        /// </summary>
        WM_GETFONT = 0x0031,

        /// <summary>
        /// Copies the text that corresponds to a window into a buffer provided by the caller.
        /// </summary>
        WM_GETTEXT = 0x000D,

        /// <summary>
        /// Determines the length, in characters, of the text associated with a window.
        /// </summary>
        WM_GETTEXTLENGTH = 0x000E,

        /// <summary>
        /// Sets the font that a control is to use when drawing text.
        /// </summary>
        WM_SETFONT = 0x0030,

        /// <summary>
        /// Associates a new large or small icon with a window.
        /// The system displays the large icon in the ALT+TAB dialog box,
        /// and the small icon in the window caption.
        /// </summary>
        WM_SETICON = 0x0080,

        /// <summary>
        /// Sets the text of a window.
        /// </summary>
        WM_SETTEXT = 0x000C,

        #endregion

        #region Windows / Window Reference / Window Notifications
        // see https://learn.microsoft.com/en-us/windows/win32/winmsg/window-notifications

        /// <summary>
        /// Sent when a window belonging to a different application than the active window is
        /// about to be activated. The message is sent to the application whose window is being
        /// activated and to the application whose window is being deactivated.
        /// </summary>
        WM_ACTIVATEAPP = 0x001C,

        /// <summary>
        /// Sent to cancel certain modes, such as mouse capture. For example, the system sends this
        /// message to the active window when a dialog box or message box is displayed. Certain functions
        /// also send this message explicitly to the specified window regardless of whether it is the
        /// active window. For example, the EnableWindow function sends this message when disabling the
        /// specified window.
        /// </summary>
        WM_CANCELMODE = 0x001F,

        /// <summary>
        /// Sent to a child window when the user clicks the window's title bar or when the window
        /// is activated, moved, or sized.
        /// </summary>
        WM_CHILDACTIVATE = 0x0022,

        /// <summary>
        /// Sent as a signal that a window or an application should terminate.
        /// </summary>
        WM_CLOSE = 0x0010,

        /// <summary>
        /// Sent to all top-level windows when the system detects more than 12.5 percent of system
        /// time over a 30- to 60-second interval is being spent compacting memory. This indicates
        /// that system memory is low.
        /// </summary>
        WM_COMPACTING = 0x0041,

        /// <summary>
        /// Sent when an application requests that a window be created by calling the CreateWindowEx
        /// or CreateWindow function. (The message is sent before the function returns.) The window
        /// procedure of the new window receives this message after the window is created, but before
        /// the window becomes visible.
        /// </summary>
        WM_CREATE = 0x0001,

        /// <summary>
        /// Sent when a window is being destroyed. It is sent to the window procedure of the window
        /// being destroyed after the window is removed from the screen.
        ///
        /// This message is sent first to the window being destroyed and then to the child windows
        /// (if any) as they are destroyed.During the processing of the message, it can be assumed
        /// that all child windows still exist.
        /// </summary>
        WM_DESTROY = 0x0002,

        /// <summary>
        /// Sent when an application changes the enabled state of a window. It is sent to the window
        /// whose enabled state is changing. This message is sent before the EnableWindow function
        /// returns, but after the enabled state (WS_DISABLED style bit) of the window has changed.
        /// </summary>
        WM_ENABLE = 0x000A,

        /// <summary>
        /// Sent one time to a window after it enters the moving or sizing modal loop. The window
        /// enters the moving or sizing modal loop when the user clicks the window's title bar or
        /// sizing border, or when the window passes the WM_SYSCOMMAND message to the DefWindowProc
        /// function and the wParam parameter of the message specifies the SC_MOVE or SC_SIZE value.
        /// The operation is complete when DefWindowProc returns.
        ///
        /// The system sends the WM_ENTERSIZEMOVE message regardless of whether the dragging of
        /// full windows is enabled.
        /// </summary>
        WM_ENTERSIZEMOVE = 0x0231,

        /// <summary>
        /// Sent one time to a window, after it has exited the moving or sizing modal loop. The
        /// window enters the moving or sizing modal loop when the user clicks the window's title
        /// bar or sizing border, or when the window passes the WM_SYSCOMMAND message to the
        /// DefWindowProc function and the wParam parameter of the message specifies the SC_MOVE
        /// or SC_SIZE value. The operation is complete when DefWindowProc returns.
        /// </summary>
        WM_EXITSIZEMOVE = 0x0232,

        /// <summary>
        /// Sent to a window to retrieve a handle to the large or small icon associated with a
        /// window. The system displays the large icon in the ALT+TAB dialog, and the small
        /// icon in the window caption.
        /// </summary>
        WM_GETICON = 0x007F,

        /// <summary>
        /// Sent to a window when the size or position of the window is about to change. An
        /// application can use this message to override the window's default maximized size
        /// and position, or its default minimum or maximum tracking size.
        /// </summary>
        WM_GETMINMAXINFO = 0x0024,

        /// <summary>
        /// Sent to the topmost affected window after an application's input language has been
        /// changed. You should make any application-specific settings and pass the message to
        /// the DefWindowProc function, which passes the message to all first-level child windows.
        /// These child windows can pass the message to DefWindowProc to have it pass the message
        /// to their child windows, and so on.
        /// </summary>
        WM_INPUTLANGCHANGE = 0x0051,

        /// <summary>
        /// Posted to the window with the focus when the user chooses a new input language, either
        /// with the hotkey (specified in the Keyboard control panel application) or from the
        /// indicator on the system taskbar. An application can accept the change by passing the
        /// message to the DefWindowProc function or reject the change (and prevent it from taking
        /// place) by returning immediately.
        /// </summary>
        WM_INPUTLANGCHANGEREQUEST = 0x0050,

        /// <summary>
        /// Sent after a window has been moved.
        /// </summary>
        WM_MOVE = 0x0003,

        /// <summary>
        /// Sent to a window that the user is moving. By processing this message, an application
        /// can monitor the position of the drag rectangle and, if needed, change its position.
        /// </summary>
        WM_MOVING = 0x0216,

        /// <summary>
        /// Sent to a window when its nonclient area needs to be changed to indicate an active
        /// or inactive state.
        /// </summary>
        WM_NCACTIVATE = 0x0086,

        /// <summary>
        /// Sent when the size and position of a window's client area must be calculated. By
        /// processing this message, an application can control the content of the window's
        /// client area when the size or position of the window changes.
        /// </summary>
        WM_NCCALCSIZE = 0x0083,

        /// <summary>
        /// Sent prior to the WM_CREATE message when a window is first created.
        /// </summary>
        WM_NCCREATE = 0x0081,

        /// <summary>
        /// Notifies a window that its nonclient area is being destroyed. The DestroyWindow
        /// function sends the WM_NCDESTROY message to the window following the WM_DESTROY
        /// message.WM_DESTROY is used to free the allocated memory object associated with
        /// the window.
        ///
        /// The WM_NCDESTROY message is sent after the child windows have been destroyed.
        /// In contrast, WM_DESTROY is sent before the child windows are destroyed.
        /// </summary>
        WM_NCDESTROY = 0x0082,

        /// <summary>
        /// Performs no operation. An application sends the WM_NULL message if it wants to
        /// post a message that the recipient window will ignore.
        /// </summary>
        WM_NULL = 0x0000,

        /// <summary>
        /// Sent to a minimized (iconic) window. The window is about to be dragged by the
        /// user but does not have an icon defined for its class. An application can return
        /// a handle to an icon or cursor. The system displays this cursor or icon while
        /// the user drags the icon.
        /// </summary>
        WM_QUERYDRAGICON = 0x0037,

        /// <summary>
        /// Sent to an icon when the user requests that the window be restored to its
        /// previous size and position.
        /// </summary>
        WM_QUERYOPEN = 0x0013,

        /// <summary>
        /// Indicates a request to terminate an application, and is generated when the
        /// application calls the PostQuitMessage function. This message causes the
        /// GetMessage function to return zero.
        /// </summary>
        WM_QUIT = 0x0012,

        /// <summary>
        /// Sent to a window when the window is about to be hidden or shown.
        /// </summary>
        WM_SHOWWINDOW = 0x0018,

        /// <summary>
        /// Sent to a window after its size has changed.
        /// </summary>
        WM_SIZE = 0x0005,

        /// <summary>
        /// Sent to a window that the user is resizing. By processing this message, an
        /// application can monitor the size and position of the drag rectangle and, if
        /// needed, change its size or position.
        /// </summary>
        WM_SIZING = 0x0214,

        /// <summary>
        /// Sent to a window after the SetWindowLong function has changed one or more
        /// of the window's styles.
        /// </summary>
        WM_STYLECHANGED = 0x007D,

        /// <summary>
        /// Sent to a window when the SetWindowLong function is about to change one or
        /// more of the window's styles.
        /// </summary>
        WM_STYLECHANGING = 0x007C,

        /// <summary>
        /// Broadcast to every window following a theme change event. Examples of theme
        /// change events are the activation of a theme, the deactivation of a theme, or
        /// a transition from one theme to another.
        /// </summary>
        WM_THEMECHANGED = 0x031A,

        /// <summary>
        /// Sent to all windows after the user has logged on or off. When the user logs on
        /// or off, the system updates the user-specific settings. The system sends this
        /// message immediately after updating the settings.
        /// </summary>
        WM_USERCHANGED = 0x0054,

        /// <summary>
        /// Sent to a window whose size, position, or place in the Z order has changed as a
        /// result of a call to the SetWindowPos function or another window-management function.
        /// </summary>
        WM_WINDOWPOSCHANGED = 0x0047,

        /// <summary>
        /// Sent to a window whose size, position, or place in the Z order is about to change
        /// as a result of a call to the SetWindowPos function or another window-management function.
        /// </summary>
        WM_WINDOWPOSCHANGING = 0x0046,

        #endregion

        #endregion

        #region Learn / Windows / Apps / Win32 / Desktop Technologies / Graphics and Gaming / Windows GDI
        // see https://learn.microsoft.com/en-us/windows/win32/gdi/windows-gdi

        #region Painting and Drawing / Painting and Drawing Reference / Painting and Drawing Messages
        // see https://learn.microsoft.com/en-us/windows/win32/gdi/painting-and-drawing-messages

        /// <summary>
        /// The WM_NCPAINT message is sent to a window when its frame must be painted.
        /// </summary>
        WM_NCPAINT = 0x0085,

        /// <summary>
        /// The WM_PAINT message is sent when the system or another application makes a request
        /// to paint a portion of an application's window. The message is sent when the UpdateWindow
        /// or RedrawWindow function is called, or by the DispatchMessage function when the
        /// application obtains a WM_PAINT message by using the GetMessage or PeekMessage function.
        /// </summary>
        WM_PAINT = 0x000f,

        #endregion

        #endregion
    }
}
