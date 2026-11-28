// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>The outcome of handing the user's clipboard back after a paste.</summary>
internal enum ClipboardRestore
{
    /// <summary>Everything went back.</summary>
    Restored,

    /// <summary>Someone else wrote to the clipboard after our paste, so their copy stays and nothing was touched.</summary>
    Superseded,

    /// <summary>Nothing was touched: the clipboard couldn't be opened, or not in a way that may be written to.</summary>
    Untouched,

    /// <summary>The clipboard was emptied, but not everything went back: it changed, by our hand.</summary>
    Incomplete,
}
