// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// This is used for fuzz testing and ensures that the project links only to JsonHelper,
// avoiding unnecessary connections to additional files
namespace ManagedCommon
{
    public static class Logger
    {
        // An empty method to simulate logging information
        public static void LogTrace()
        {
            // Do nothing
        }

        // An empty method to simulate logging information
        public static void LogInfo(string message)
        {
            // Do nothing
        }

        // An empty method to simulate logging warnings
        public static void LogWarning(string message)
        {
            // Do nothing
        }

        // An empty method to simulate logging errors
        public static void LogError(string message, Exception? ex = null)
        {
            // Do nothing
        }

        public static void LogDebug(string message, Exception? ex = null)
        {
            // Do nothing
        }
    }
}
