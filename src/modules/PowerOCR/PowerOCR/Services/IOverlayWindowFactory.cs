// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerOCR.Models;
using PowerOCR.ViewModels;

namespace PowerOCR.Services;

internal interface IOverlayWindowFactory
{
    OCROverlay Create(DisplayCapture capture, OverlaySessionViewModel viewModel, IOverlayManager manager);
}
