// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common.Services.HttpCaching;

/// <summary>
/// Preserves the last known final URI when fetching an HTTP resource fails.
/// </summary>
public sealed class HttpResourceRequestException : HttpRequestException
{
    public Uri? FinalUri { get; }

    public bool FinalUriAlreadyChecked { get; }

    internal HttpResourceRequestException(Uri? finalUri, HttpRequestException innerException, bool finalUriAlreadyChecked = true)
        : base(innerException.HttpRequestError, innerException.Message, innerException, innerException.StatusCode)
    {
        FinalUri = finalUri;
        FinalUriAlreadyChecked = finalUriAlreadyChecked;
    }
}
