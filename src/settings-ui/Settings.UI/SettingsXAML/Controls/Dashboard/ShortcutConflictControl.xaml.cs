// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class ShortcutConflictControl : UserControl
    {
        public ShortcutConflictControl()
        {
            InitializeComponent();
            GetShortcutConflicts();
        }

        private void GetShortcutConflicts()
        {
            // TO DO: Implement the logic to retrieve and display shortcut conflicts. Make sure to Collapse this control if not conflicts are found.
        }

        private void ShortcutConflictBtn_Click(object sender, RoutedEventArgs e)
        {
            // TO DO: Handle the button click event to show the shortcut conflicts window.
        }
    }
}
