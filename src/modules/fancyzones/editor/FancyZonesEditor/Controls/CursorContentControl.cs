// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FancyZonesEditor.Controls
{
    /// <summary>
    /// A content host that applies a mouse cursor to its content.
    /// WinUI 3 replaced the WPF <c>FrameworkElement.Cursor</c> property with the protected
    /// <see cref="Microsoft.UI.Xaml.UIElement.ProtectedCursor"/>, which only a derived type can
    /// assign - and the controls that need a resize cursor (<c>Thumb</c>) are sealed, so the
    /// cursor is carried by this wrapper instead.
    /// </summary>
    public partial class CursorContentControl : ContentControl
    {
        private InputSystemCursorShape _cursorShape = InputSystemCursorShape.Arrow;

        public CursorContentControl()
        {
            // Reuse the built-in ContentControl template: a derived control has no implicit
            // style of its own in WinUI 3 and would otherwise render nothing.
            DefaultStyleKey = typeof(ContentControl);
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            VerticalContentAlignment = VerticalAlignment.Stretch;
            IsTabStop = false;
        }

        public InputSystemCursorShape CursorShape
        {
            get => _cursorShape;

            set
            {
                _cursorShape = value;
                ProtectedCursor = InputSystemCursor.Create(value);
            }
        }
    }
}
