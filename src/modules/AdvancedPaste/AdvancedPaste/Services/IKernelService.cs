// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.Services;

public interface IKernelService
{
    Task<DataPackage> TransformClipboardAsync(string prompt, DataPackageView clipboardData, bool isSavedQuery, CancellationToken cancellationToken, IProgress<double> progress);
}
