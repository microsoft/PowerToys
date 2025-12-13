// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Services;

public sealed class ResourcesSwappedEventArgs(string? name, Uri dictionaryUri) : EventArgs
{
    public string? Name { get; } = name;

    public Uri DictionaryUri { get; } = dictionaryUri ?? throw new ArgumentNullException(nameof(dictionaryUri));
}
