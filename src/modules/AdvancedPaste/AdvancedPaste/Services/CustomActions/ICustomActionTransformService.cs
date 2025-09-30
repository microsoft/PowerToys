// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

using AdvancedPaste.Settings;

namespace AdvancedPaste.Services.CustomActions
{
    public interface ICustomActionTransformService
    {
        Task<Windows.ApplicationModel.DataTransfer.DataPackage> TransformTextToDataPackageAsync(string prompt, string inputText, CancellationToken cancellationToken, IProgress<double> progress);

        Task<CustomActionTransformResult> TransformTextAsync(string prompt, string inputText, CancellationToken cancellationToken, IProgress<double> progress);

        Task<CustomActionTransformResult> TransformAsync(CustomActionTransformContext context, CancellationToken cancellationToken, IProgress<double> progress);
    }
}
