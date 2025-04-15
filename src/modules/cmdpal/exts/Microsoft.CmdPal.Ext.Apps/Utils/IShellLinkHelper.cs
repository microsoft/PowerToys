// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.Apps.Utils;

public interface IShellLinkHelper
{
    string RetrieveTargetPath(string path);

    string Description { get; set; }

    string Arguments { get; set; }

    bool HasArguments { get; set; }
}
