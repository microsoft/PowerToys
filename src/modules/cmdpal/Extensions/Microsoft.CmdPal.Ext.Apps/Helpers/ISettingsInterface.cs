// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.CmdPal.Ext.Apps.Helpers;

public interface ISettingsInterface
{
    public bool EnableStartMenuSource { get; }

    public bool EnableDesktopSource { get; }

    public bool EnableRegistrySource { get; }

    public bool EnablePathEnvironmentVariableSource { get; }

    public List<string> ProgramSuffixes { get; }

    public List<string> RunCommandSuffixes { get; }
}
