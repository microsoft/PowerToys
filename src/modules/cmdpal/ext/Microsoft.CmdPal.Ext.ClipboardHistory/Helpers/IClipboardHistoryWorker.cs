// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;

internal interface IClipboardHistoryWorker : IAsyncDisposable
{
    Task RunAsync(Func<Task> action);
}
