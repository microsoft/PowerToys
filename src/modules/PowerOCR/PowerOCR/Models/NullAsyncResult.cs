// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

namespace PowerOCR.Models;

public class NullAsyncResult : IAsyncResult
{
    public object? AsyncState => null;

    public WaitHandle AsyncWaitHandle => new NullWaitHandle();

    public bool CompletedSynchronously => true;

    public bool IsCompleted => true;
}
