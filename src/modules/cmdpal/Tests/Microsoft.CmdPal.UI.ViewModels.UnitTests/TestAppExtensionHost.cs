// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

internal sealed partial class TestAppExtensionHost : AppExtensionHost
{
    public override string? GetExtensionDisplayName() => "Test Host";
}
