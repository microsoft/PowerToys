// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

/// <summary>Retains a shortcut's display name and launch path after its application is merged.</summary>
/// <param name="Name">The display name read from the shortcut.</param>
/// <param name="Path">The discovered shortcut path.</param>
public sealed record AppShortcut(string Name, string Path);
