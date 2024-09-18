// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.Apps.Programs;

public interface IPackage
{
    string Name { get; }

    string FullName { get; }

    string FamilyName { get; }

    bool IsFramework { get; }

    bool IsDevelopmentMode { get; }

    string InstalledLocation { get; }
}
