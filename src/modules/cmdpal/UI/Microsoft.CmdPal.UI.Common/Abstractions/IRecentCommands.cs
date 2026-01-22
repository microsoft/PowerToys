// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Common.Abstractions;

public interface IRecentCommands
{
    int GetCommandHistoryWeight(string commandId);

    void AddHistoryItem(string commandId);
}
