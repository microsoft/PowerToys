// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

using PowerOCR.Core.Models;
using PowerOCR.Models;

namespace PowerOCR.Services;

public interface IOverlayManager : IDisposable
{
    Task ShowAsync();

    Task CaptureAsync(DisplayCapture capture, PixelSelection selection, bool isClick);

    void CloseAll(bool cancelled);
}
