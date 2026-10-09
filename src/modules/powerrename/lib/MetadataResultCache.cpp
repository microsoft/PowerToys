// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "MetadataResultCache.h"

using namespace PowerRenameLib;

namespace
{
    template <typename Metadata, typename CacheEntry, typename Cache, typename Mutex, typename Loader>
    bool GetOrLoadInternal(const std::wstring& filePath,
        Metadata& outMetadata,
        Cache& cache,
        Mutex& mutex,
        const Loader& loader)
    {
        {
            std::shared_lock sharedLock(mutex);
            auto it = cache.find(filePath);
            if (it != cache.end())
            {
                // Return cached result (success or failure)
                outMetadata = it->second.data;
                return it->second.wasSuccessful;
            }
        }

        if (!loader)
        {
            // No loader provided
            return false;
        }

        Metadata loaded{};
        const bool result = loader(loaded);

        // Cache the result (success or failure)
        {
            std::unique_lock uniqueLock(mutex);
            // Check if another thread cached it while we were loading
            auto it = cache.find(filePath);
            if (it == cache.end())
            {
                // Not cached yet, insert our result
                cache.emplace(filePath, CacheEntry{ result, loaded });
            }
            else
            {
                // Another thread cached it, use their result
                outMetadata = it->second.data;
                return it->second.wasSuccessful;
            }
        }

        outMetadata = loaded;
        return result;
    }
}

bool MetadataResultCache::GetOrLoadEXIF(const std::wstring& filePath,
    EXIFMetadata& outMetadata,
    const EXIFLoader& loader)
{
    return GetOrLoadInternal<EXIFMetadata, CacheEntry<EXIFMetadata>>(filePath, outMetadata, exifCache, exifMutex, loader);
}

bool MetadataResultCache::GetOrLoadXMP(const std::wstring& filePath,
    XMPMetadata& outMetadata,
    const XMPLoader& loader)
{
    return GetOrLoadInternal<XMPMetadata, CacheEntry<XMPMetadata>>(filePath, outMetadata, xmpCache, xmpMutex, loader);
}

void MetadataResultCache::ClearAll()
{
    {
        std::unique_lock lock(exifMutex);
        exifCache.clear();
    }

    {
        std::unique_lock lock(xmpMutex);
        xmpCache.clear();
    }
}
