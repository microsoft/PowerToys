// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Wox.Infrastructure.Storage
{
    /// <summary>
    /// Save plugin settings/cache,
    /// todo should be merged into a abstract class instead of separate interface
    /// </summary>
    public interface ISavable
    {
        void Save();
    }
}
