// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PowerOCR.Helpers;

namespace PowerOCR.Controls;

public sealed partial class SelectionCanvas : Canvas
{
    private InputSystemCursor? _cursor;

    internal CursorDiagnostics? Diagnostics { get; set; }

    internal bool HasCrossCursor => ProtectedCursor is InputSystemCursor cursor && cursor.CursorShape == InputSystemCursorShape.Cross;

    public SelectionCanvas()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Keep the override on the hit-tested surface, independent of hover events.
        _cursor ??= InputSystemCursor.Create(InputSystemCursorShape.Cross);
        ProtectedCursor = _cursor;
        Diagnostics?.RecordCanvasState("cross-assigned");
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ProtectedCursor = null;
        _cursor?.Dispose();
        _cursor = null;
        Diagnostics?.RecordCanvasState("cross-cleared");
    }
}
