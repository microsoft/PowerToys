// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.Cli;

internal interface IAdvancedPasteRuntime
{
    IReadOnlyList<CliActionDescriptor> GetActions();

    Task<DataPackage> ExecuteAsync(CliActionRequest request, DataPackageView input, CancellationToken cancellationToken, IProgress<double>? progress = null);
}
