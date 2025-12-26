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
        Task<CustomActionTransformResult> TransformAsync(string prompt, string inputText, byte[] imageBytes, byte[] audioBytes, string audioMimeType, CancellationToken cancellationToken, IProgress<double> progress);
    }
}
