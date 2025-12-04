// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.Xaml.Interactivity;

namespace Microsoft.CmdPal.UI.Helpers;

public class OpenFlyoutAction : DependencyObject, IAction
{
    public object? Execute(object sender, object parameter)
    {
        FlyoutBase.ShowAttachedFlyout(TargetObject ?? (FrameworkElement)sender);
        return null;
    }

    public Control TargetObject
    {
        get { return (Control)GetValue(TargetObjectProperty); }
        set { SetValue(TargetObjectProperty, value); }
    }

    public static readonly DependencyProperty TargetObjectProperty =
        DependencyProperty.Register(nameof(TargetObject), typeof(Control), typeof(OpenFlyoutAction), new PropertyMetadata(null));
}
