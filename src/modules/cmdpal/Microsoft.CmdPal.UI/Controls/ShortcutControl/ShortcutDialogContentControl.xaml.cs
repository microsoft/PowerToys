// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class ShortcutDialogContentControl : UserControl
{
    public ShortcutDialogContentControl()
    {
        this.InitializeComponent();
    }

    internal CheckBox WindowsKeyOption => WindowsKeyCheckBox;

    internal void FocusKeyCapture() => KeysControl.Focus(FocusState.Programmatic);

    internal bool IsKeyCaptureEnabled
    {
        get => KeysControl.IsEnabled;
        set
        {
            KeysControl.IsEnabled = value;
            ShortcutGuidance.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public List<object> Keys
    {
        get { return (List<object>)GetValue(KeysProperty); }
        set { SetValue(KeysProperty, value); }
    }

    public static readonly DependencyProperty KeysProperty = DependencyProperty.Register("Keys", typeof(List<object>), typeof(ShortcutDialogContentControl), new PropertyMetadata(default(string)));

    public bool IsError
    {
        get => (bool)GetValue(IsErrorProperty);
        set => SetValue(IsErrorProperty, value);
    }

    public static readonly DependencyProperty IsErrorProperty = DependencyProperty.Register("IsError", typeof(bool), typeof(ShortcutDialogContentControl), new PropertyMetadata(false));

    public bool IsWarningAltGr
    {
        get => (bool)GetValue(IsWarningAltGrProperty);
        set => SetValue(IsWarningAltGrProperty, value);
    }

    public static readonly DependencyProperty IsWarningAltGrProperty = DependencyProperty.Register("IsWarningAltGr", typeof(bool), typeof(ShortcutDialogContentControl), new PropertyMetadata(false));
}
