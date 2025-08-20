// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AdvancedPaste.Helpers;

namespace AdvancedPaste.Models;

public sealed class PasteActionModeratedException : PasteActionException
{
    public PasteActionModeratedException()
        : base(
            message: ResourceLoaderInstance.ResourceLoader.GetString("PasteError"),
            innerException: null,
            aiServiceMessage: ResourceLoaderInstance.ResourceLoader.GetString("PasteActionModerated"))
    {
    }

    /// <summary>
    /// Non-localized error description for logs, reports, telemetry, etc.
    /// </summary>
    public const string ErrorDescription = "Paste operation moderated";
}
