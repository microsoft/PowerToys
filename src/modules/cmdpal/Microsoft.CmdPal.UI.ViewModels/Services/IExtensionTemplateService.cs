// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Creates new Command Palette extensions from the built-in project template.
/// </summary>
internal interface IExtensionTemplateService
{
    /// <summary>
    /// Scaffolds a new extension project by extracting the template archive,
    /// replacing placeholder tokens, and writing the result to <paramref name="outputPath"/>.
    /// </summary>
    /// <param name="extensionName">The code-safe name used for the project and namespaces.</param>
    /// <param name="displayName">The human-readable name shown in the extension catalog.</param>
    /// <param name="outputPath">The directory where the new extension project will be created.</param>
    void CreateExtension(string extensionName, string displayName, string outputPath);
}
