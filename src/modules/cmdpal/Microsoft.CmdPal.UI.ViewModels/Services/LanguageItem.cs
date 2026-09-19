// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Represents a language option to display in the UI and apply language selection.
/// </summary>
/// <param name="Tag">The IETF BCP 47 tag used to identify the language.</param>
/// <param name="DisplayName">The user-visible display name of the language.</param>
public record LanguageItem(string Tag, string DisplayName);
