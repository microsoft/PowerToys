// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;

namespace PowerDisplay.PowerDisplayXAML
{
    /// <summary>
    /// Interaction logic for IdentifyWindow.xaml
    /// </summary>
    public sealed partial class IdentifyWindow : Window
    {
        public IdentifyWindow(int number)
        {
            InitializeComponent();
            NumberText.Text = number.ToString(CultureInfo.InvariantCulture);

            // Auto close after 3 seconds
            Task.Delay(3000).ContinueWith(_ =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    Close();
                });
            });
        }
    }
}
