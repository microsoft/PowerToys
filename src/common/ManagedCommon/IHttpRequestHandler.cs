// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ManagedCommon
{
    public interface IHttpRequestHandler
    {
        /// <summary>
        /// Handle an HTTP request for this module.
        /// </summary>
        /// <param name="context">The HTTP context containing request and response.</param>
        /// <param name="path">The requested path (after module prefix).</param>
        /// <returns>Task representing the async operation.</returns>
        Task HandleRequestAsync(HttpListenerContext context, string path);

        /// <summary>
        /// Get the module name used for URL routing (e.g., "awake", "fancyzones").
        /// </summary>
        string ModuleName { get; }

        /// <summary>
        /// Get the available endpoints for this module (for documentation).
        /// </summary>
        string[] GetAvailableEndpoints();
    }
}
