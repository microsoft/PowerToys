// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace MouseWithoutBorders.Class
{
    internal static class Extensions
    {
        internal static int ReadEx(this Stream st, byte[] buf, int bufIndex, int length)
        {
            int bytesReceived = st.Read(buf, bufIndex, length);

            int receivedCount = bytesReceived;

            while (receivedCount != 0 && bytesReceived < length)
            {
                bytesReceived += receivedCount = st.Read(buf, bufIndex + bytesReceived, length - bytesReceived);
            }

            return bytesReceived;
        }

        internal static void KillProcess(this Process process, bool keepTrying = false)
        {
            string processName = process.ProcessName;
            int processId = process.Id;

            do
            {
                try
                {
                    process.Kill();
                    break;
                }
                catch (Win32Exception e)
                {
                    string log = $"The process {processName} (PID={processId}) could not be terminated, error: {e.Message}";
                    Common.TelemetryLogTrace(log, SeverityLevel.Error);
                    Common.ShowToolTip(log, 5000);

                    if (!keepTrying)
                    {
                        Thread.Sleep(1000);
                        break;
                    }
                }
            }
            while (true);
        }
    }
}
