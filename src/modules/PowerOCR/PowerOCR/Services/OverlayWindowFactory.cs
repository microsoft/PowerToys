// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.Extensions.DependencyInjection;
using PowerOCR.Models;
using PowerOCR.ViewModels;

namespace PowerOCR.Services;

internal sealed class OverlayWindowFactory : IOverlayWindowFactory
{
    private readonly IServiceProvider _serviceProvider;

    public OverlayWindowFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public OCROverlay Create(DisplayCapture capture, OverlaySessionViewModel viewModel, IOverlayManager manager)
    {
        return ActivatorUtilities.CreateInstance<OCROverlay>(
            _serviceProvider,
            capture,
            viewModel,
            manager);
    }
}
