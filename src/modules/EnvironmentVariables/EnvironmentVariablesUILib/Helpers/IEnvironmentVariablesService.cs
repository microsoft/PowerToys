// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EnvironmentVariablesUILib.Models;

namespace EnvironmentVariablesUILib.Helpers
{
    public interface IEnvironmentVariablesService : IDisposable
    {
        string ProfilesJsonFilePath { get; }

        List<ProfileVariablesSet> ReadProfiles();

        Task WriteAsync(IEnumerable<ProfileVariablesSet> profiles);
    }
}
