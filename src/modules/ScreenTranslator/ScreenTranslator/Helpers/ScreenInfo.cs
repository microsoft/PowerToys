// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ScreenTranslator.Core.Translation;

namespace ScreenTranslator.Helpers;

public sealed record ScreenInfo(
    PhysicalRect Bounds,
    PhysicalRect WorkingArea,
    double DpiScaleX,
    double DpiScaleY,
    bool IsPrimary,
    IntPtr HMonitor);
