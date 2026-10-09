// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;

namespace Microsoft.Plugin.Folder.Sources
{
    public interface IEnvironmentHelper
    {
        bool IsEnvironmentVariable(string search);

        IDictionary GetEnvironmentVariables();
    }
}
