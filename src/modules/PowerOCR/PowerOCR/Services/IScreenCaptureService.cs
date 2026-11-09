// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;

using Microsoft.UI.Windowing;
using PowerOCR.Models;

namespace PowerOCR.Services;

internal interface IScreenCaptureService
{
    Task<DisplayCapture> CaptureAsync(DisplayArea display, CancellationToken cancellationToken);
}
