// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Windows.Foundation;

namespace DeveloperCommandPalette;

public class SubmitFormArgs
{
    public required string FormData { get; set; }

    public required IForm Form { get; set; }
}
