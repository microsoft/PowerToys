// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class TextCommandParameterInputItem : CommandParameterInputItem
{
    public override ParameterType ParameterType => ParameterType.Text;

    public string PlaceholderText { get; set; } = string.Empty;
}
