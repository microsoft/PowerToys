// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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

namespace KeyboardManagerEditorUI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        [DllImport("KeyboardManagerEditorLibraryWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool CheckIfRemappingsAreValid();

        public MainWindow()
        {
            this.InitializeComponent();
        }

        private void MyButton_Click(object sender, RoutedEventArgs e)
        {
            // Call the C++ function to check if the current remappings are valid
            myButton.Content = CheckIfRemappingsAreValid() ? "Valid" : "Invalid";
        }
    }
}
