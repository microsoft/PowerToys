// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public abstract partial class PageFactoryCommand : Command, IPageFactoryCommand
{
    public IAsyncOperation<IPage> CreatePageAsync()
    {
        return System.Runtime.InteropServices.WindowsRuntime.AsyncInfo.Run(CreatePageAsync);
    }

    public abstract Task<IPage> CreatePageAsync(CancellationToken cancellationToken);
}
