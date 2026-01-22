// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

/// <summary>
/// Interface for UWP applications to enable testing and mocking
/// </summary>
public interface IUWPApplication : IProgram
{
    string AppListEntry { get; set; }

    string DisplayName { get; set; }

    string UserModelId { get; set; }

    string BackgroundColor { get; set; }

    string EntryPoint { get; set; }

    bool CanRunElevated { get; set; }

    string LogoPath { get; set; }

    LogoType LogoType { get; set; }

    UWP Package { get; set; }

    string LocationLocalized { get; }

    string GetAppIdentifier();

    List<IContextItem> GetCommands();

    void UpdateLogoPath(Utils.Theme theme);

    AppItem ToAppItem();
}
