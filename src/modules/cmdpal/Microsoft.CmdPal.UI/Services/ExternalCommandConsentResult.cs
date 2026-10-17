// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Services;

/// <summary>External-command consent result.</summary>
internal enum ExternalCommandConsentResult
{
    /// <summary>Denied or dismissed.</summary>
    Rejected,

    /// <summary>Authorized for the current activation.</summary>
    AllowOnce,

    /// <summary>Authorized with a request to persist consent.</summary>
    AlwaysAllow,
}
