// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace Microsoft.PowerToys.Settings.UI.Controls;

public sealed partial class GPOInfoControl : ContentControl
{
    public static readonly DependencyProperty ShowWarningProperty =
      DependencyProperty.Register(
          nameof(ShowWarning),
          typeof(bool),
          typeof(GPOInfoControl),
          new PropertyMetadata(false, OnShowWarningPropertyChanged));

    public bool ShowWarning
    {
        get => (bool)GetValue(ShowWarningProperty);
        set => SetValue(ShowWarningProperty, value);
    }

    private static void OnShowWarningPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GPOInfoControl gpoInfoControl)
        {
            if (gpoInfoControl.ShowWarning)
            {
                gpoInfoControl.IsEnabled = false;
            }
        }
    }

    public GPOInfoControl()
    {
        DefaultStyleKey = typeof(GPOInfoControl);
    }
}
