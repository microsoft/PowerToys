// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace Microsoft.Workspaces.TestApp
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                var options = LaunchOptions.Parse(args);
                using var startedEvent = OpenEvent(options.StartedEventName);
                startedEvent?.Set();

                using var waitEvent = OpenEvent(options.WaitEventName);
                using var readyEvent = OpenEvent(options.ReadyEventName);
                if (waitEvent is not null && !waitEvent.WaitOne(TimeSpan.FromSeconds(120)))
                {
                    throw new TimeoutException($"Timed out after 120 seconds waiting for event '{options.WaitEventName}'.");
                }

                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
                Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                using var form = new WorkspacesForm(options.Title, options.Payload, options.Minimized);
                form.Shown += (_, _) => readyEvent?.Set();
                Application.Run(form);
                return 0;
            }
            catch (Exception exception) when (exception is ArgumentException
                or WaitHandleCannotBeOpenedException
                or UnauthorizedAccessException
                or IOException
                or TimeoutException
                or Win32Exception)
            {
                Console.Error.WriteLine($"Workspaces.TestApp: {exception.Message}");
                return 1;
            }
        }

        private static EventWaitHandle? OpenEvent(string? name)
        {
            return name is null ? null : EventWaitHandle.OpenExisting(name);
        }
    }
}
