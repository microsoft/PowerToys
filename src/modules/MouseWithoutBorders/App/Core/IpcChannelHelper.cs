// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows.Forms;

namespace MouseWithoutBorders.Core;

internal sealed partial class IpcChannelHelper
{
    internal static bool IpcChannelCreated { get; set; }

    internal static T Retry<T>(string name, Func<T> func, Action<string> log, Action preRetry = null)
    {
        int count = 0;

        do
        {
            try
            {
                T rv = func();

                if (count > 0)
                {
                    log($"Trace: {name} has been successful after {count} retry.");
                }

                return rv;
            }
            catch (Exception)
            {
                count++;

                preRetry?.Invoke();

                if (count > 10)
                {
                    throw;
                }

                Application.DoEvents();
                Thread.Sleep(200);
            }
        }
        while (true);
    }
}
