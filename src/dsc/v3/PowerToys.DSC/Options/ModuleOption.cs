// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace PowerToys.DSC.Options;

internal sealed class ModuleOption : Option<string?>
{
    public ModuleOption()
        : base("--module", "The module name")
    {
    }
}
