// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public abstract partial class ParametersPage : Page, IParametersPage
{
    public abstract IListItem Command { get; }

    public abstract IParameterRun[] Parameters { get; }
}
