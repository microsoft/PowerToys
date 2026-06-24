// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Library;

namespace AdvancedPaste.Services.CustomActions
{
    public interface IPasteAIProvider
    {
        Task<bool> IsAvailableAsync(CancellationToken cancellationToken);

        Task<string> ProcessPasteAsync(PasteAIRequest request, CancellationToken cancellationToken, IProgress<double> progress);
    }
}
