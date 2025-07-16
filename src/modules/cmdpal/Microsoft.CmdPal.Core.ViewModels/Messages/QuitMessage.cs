// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Core.ViewModels.Messages;

/// <summary>
/// Message which closes the application. Used by <see cref="QuitCommand"/> via <see cref="BuiltInsCommandProvider"/>.
/// </summary>
public record QuitMessage()
{
}
