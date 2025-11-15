// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Windows.Foundation;
using Windows.UI;

namespace ClipPing;

internal interface IOverlay : IDisposable
{
    void Show(Rect area, Color color);
}
