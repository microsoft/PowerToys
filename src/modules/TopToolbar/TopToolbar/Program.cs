// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.UI.Xaml;

namespace TopToolbar
{
    public class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // Initialize logging (folder relative to standard PowerToys logs path conventions)
            try
            {
                Logger.InitializeLogger("\\TopToolbar\\Logs", true);
            }
            catch (Exception ex)
            {
                // Fallback: swallow logger init issues to avoid crash
                System.Diagnostics.Debug.WriteLine($"Logger init failed: {ex.Message}");
            }

            global::Microsoft.UI.Xaml.Application.Start((p) => { var app = new App(); });
        }
    }
}
