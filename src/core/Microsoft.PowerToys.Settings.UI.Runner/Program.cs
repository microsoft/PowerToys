// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using interop;
using ManagedCommon;
using Windows.UI.Popups;

namespace Microsoft.PowerToys.Settings.UI.Runner
{
    public class Program
    {
        // Quantity of arguments
        private const int ArgumentsQty = 5;

        public static bool IsElevated { get; set; }

        public static bool IsUserAnAdmin { get; set; }

        public static int PowerToysPID { get; set; }

        public static Action<string> IPCMessageReceivedCallback { get; set; }

        [STAThread]
        public static void Main(string[] args)
        {
            using (new UI.App())
            {
                App app = new App();
                app.InitializeComponent();
            }
        }

        public static TwoWayPipeMessageIPCManaged GetTwoWayIPCManager()
        {
            return null;
        }
    }
}
