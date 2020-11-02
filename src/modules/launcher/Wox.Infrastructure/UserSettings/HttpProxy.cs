// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Wox.Infrastructure.UserSettings
{
    public class HttpProxy
    {
        public bool Enabled { get; set; }

        public string Server { get; set; }

        public int Port { get; set; }

        public string UserName { get; set; }

        public string Password { get; set; }
    }
}
