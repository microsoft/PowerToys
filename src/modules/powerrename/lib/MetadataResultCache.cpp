// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "MetadataResultCache.h"

using namespace PowerRenameLib;

namespace
{
    template <typename Metadata, typename Cache, typename Mutex, typename Loader>
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
                outMetadata = it->second;
                return true;
            }
        }

        if (!loader)
        {
            // No loader provided
            return false;
        }

        Metadata loaded{};
        const bool result = loader(loaded);
        if (!result)
        {
            // Another thread might have succeeded while loader was running
            std::shared_lock sharedLock(mutex);
            auto existing = cache.find(filePath);
            if (existing != cache.end())
            {
                outMetadata = existing->second;
                return true;
            }

            return false;
        }

        {
            std::unique_lock uniqueLock(mutex);
            auto [it, inserted] = cache.emplace(filePath, loaded);
            if (!inserted)
            {
                it->second = loaded;
            }
            outMetadata = it->second;
        }

        return true;
    }
}

bool MetadataResultCache::GetOrLoadEXIF(const std::wstring& filePath,
    EXIFMetadata& outMetadata,
    const EXIFLoader& loader)
{
    return GetOrLoadInternal(filePath, outMetadata, exifCache, exifMutex, loader);
}

bool MetadataResultCache::GetOrLoadXMP(const std::wstring& filePath,
    XMPMetadata& outMetadata,
    const XMPLoader& loader)
{
    return GetOrLoadInternal(filePath, outMetadata, xmpCache, xmpMutex, loader);
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
