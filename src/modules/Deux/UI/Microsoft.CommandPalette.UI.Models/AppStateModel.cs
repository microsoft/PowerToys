// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.UI.Models;

public partial class AppStateModel
{
    /*************************************************************************
    * Make sure that you make the setters public (JsonSerializer.Deserialize will fail silently otherwise)!
    * Make sure that any new types you add are added to JsonSerializationContext!
    *************************************************************************/

    public List<string> RunHistory { get; set; } = [];
}
