// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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

    public static string TranslateOpenAICompatibleError(Exception exception, int apiRequestStatus)
    {
        var resourceLoader = ResourceLoaderInstance.ResourceLoader;
        if (exception is InvalidOperationException)
        {
            if (exception.Message.Contains("endpoint", StringComparison.OrdinalIgnoreCase))
            {
                return resourceLoader.GetString("OpenAICompatibleEndpointInvalid");
            }

            if (exception.Message.Contains("model name", StringComparison.OrdinalIgnoreCase))
            {
                return resourceLoader.GetString("OpenAICompatibleModelRequired");
            }
        }

        return (HttpStatusCode)apiRequestStatus switch
        {
            HttpStatusCode.BadRequest => resourceLoader.GetString("OpenAICompatibleRequestRejected"),
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => resourceLoader.GetString("OpenAICompatibleAuthenticationFailed"),
            HttpStatusCode.NotFound => resourceLoader.GetString("OpenAICompatibleEndpointOrModelNotFound"),
            HttpStatusCode.TooManyRequests => resourceLoader.GetString("OpenAICompatibleRateLimited"),
            HttpStatusCode.RequestTimeout or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout
                => resourceLoader.GetString("OpenAICompatibleServiceUnavailable"),
            0 => resourceLoader.GetString("OpenAICompatibleNetworkError"),
            _ when apiRequestStatus < 0 => resourceLoader.GetString("OpenAICompatibleNetworkError"),
            _ => string.Format(
                CultureInfo.InvariantCulture,
                resourceLoader.GetString("OpenAICompatibleRequestFailed"),
                apiRequestStatus),
        };
    }
}
