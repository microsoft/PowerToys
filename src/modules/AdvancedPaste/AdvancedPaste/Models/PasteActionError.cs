// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using AdvancedPaste.Helpers;

namespace AdvancedPaste.Models;

public sealed class PasteActionError
{
    public static PasteActionError None => new() { Text = string.Empty, Details = string.Empty };

    public string Text { get; private init; }

    public string Details { get; private init; }

    public bool HasText => !string.IsNullOrEmpty(Text);

    public bool HasDetails => !string.IsNullOrEmpty(Details);

    public static PasteActionError FromResourceId(string resourceId) =>
        new()
        {
            Text = ResourceLoaderInstance.ResourceLoader.GetString(resourceId),
            Details = string.Empty,
        };

    public static PasteActionError FromException(Exception ex) =>
        new()
        {
            Text = ex is PasteActionException ? ex.Message : ResourceLoaderInstance.ResourceLoader.GetString(ex is OperationCanceledException ? "PasteActionCanceled" : "PasteError"),
            Details = (ex as PasteActionException)?.AIServiceMessage ?? string.Empty,
        };
}
