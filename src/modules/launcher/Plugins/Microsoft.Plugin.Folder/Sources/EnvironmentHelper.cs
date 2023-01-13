// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;

namespace Microsoft.Plugin.Folder.Sources
{
    public class EnvironmentHelper : IEnvironmentHelper
    {
        public bool IsEnvironmentVariable(string search)
        {
            if (search == null)
            {
                throw new ArgumentNullException(paramName: nameof(search));
            }

            return search.StartsWith('%');
        }

        public IDictionary GetEnvironmentVariables()
        {
            return Environment.GetEnvironmentVariables();
        }
    }
}
