// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

using AdvancedPaste.Models.KernelQueryCache;
using AdvancedPaste.Services;

namespace AdvancedPaste.UnitTests.Mocks;

internal sealed class NoOpKernelQueryCacheService : IKernelQueryCacheService
{
    public CacheValue ReadOrNull(CacheKey cacheKey) => null;

    public Task WriteAsync(CacheKey cacheKey, CacheValue actionChain) => Task.CompletedTask;
}
