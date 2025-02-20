// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Net;

namespace AdvancedPaste.Helpers;

public static class ErrorHelpers
{
    public static string TranslateErrorText(int apiRequestStatus) => (HttpStatusCode)apiRequestStatus switch
    {
        HttpStatusCode.TooManyRequests => ResourceLoaderInstance.ResourceLoader.GetString("OpenAIApiKeyTooManyRequests"),
        HttpStatusCode.Unauthorized => ResourceLoaderInstance.ResourceLoader.GetString("OpenAIApiKeyUnauthorized"),
        HttpStatusCode.OK => string.Empty,
        _ => ResourceLoaderInstance.ResourceLoader.GetString("OpenAIApiKeyError") + apiRequestStatus.ToString(CultureInfo.InvariantCulture),
    };
}
