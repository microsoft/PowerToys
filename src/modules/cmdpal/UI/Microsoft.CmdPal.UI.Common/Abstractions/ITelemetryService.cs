// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Common.Abstractions;

public interface ITelemetryService
{
    void LogRunQuery(string query, int resultCount, ulong durationMs);

    void LogRunCommand(string command, bool asAdmin, bool success);

    void LogOpenUri(string uri, bool isWeb, bool success);
}
