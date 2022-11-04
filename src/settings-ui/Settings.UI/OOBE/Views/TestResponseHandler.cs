// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System.Net;
using System.Net.Http.Formatting;
using System.Threading;

namespace Microsoft.PowerToys.Settings.UI.OOBE.Views
{
    using System.Net.Http;
    using System.Threading.Tasks;

    public class TestResponseHandler : DelegatingHandler
    {
        private static readonly MediaTypeFormatter _mediaTypeFormatter = new JsonMediaTypeFormatter();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Intercept the call and return a canned treatment assignment response
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ObjectContent<object>(
                    new
                    {
                        Flights = new
                        {
                            IG1 = "flt1",
                            IG2 = "flt2",
                        },
                        Configs = new[]
                    {
                            new
                            {
                                Id = "Namespace1",
                                Parameters = new
                                {
                                    A = 1,
                                    B = 2,
                                },
                            },
                            new
                            {
                                Id = "Namespace2",
                                Parameters = new
                                {
                                    A = 11,
                                    B = 22,
                                },
                            },
                    },
                        FlightingVersion = 1234567890,
                    },
                    _mediaTypeFormatter),
            });
        }
    }
}
