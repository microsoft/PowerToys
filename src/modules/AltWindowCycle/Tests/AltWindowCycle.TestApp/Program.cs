// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.AltWindowCycle.TestApp;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length != 2)
        {
            throw new ArgumentException("Expected a window title and an HTML background color.");
        }

        ApplicationConfiguration.Initialize();
        using var window = new Form
        {
            Text = args[0],
            BackColor = ColorTranslator.FromHtml(args[1]),
            StartPosition = FormStartPosition.CenterScreen,
            ClientSize = new Size(1200, 650),
            AutoScaleMode = AutoScaleMode.None,
        };
        Application.Run(window);
    }
}
