// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;

namespace PowerDisplay.Ipc;

/// <summary>Processes one serialized CLI request and returns its serialized response.</summary>
public interface ICliRequestProcessor
{
    /// <summary>Processes a single request from the named-pipe server.</summary>
    Task<string> HandleAsync(string requestJson, CancellationToken cancellationToken);
}
