// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once
#include "MetadataTypes.h"
#include <shared_mutex>
#include <unordered_map>
#include <string>
#include <functional>

namespace PowerRenameLib
{
    class MetadataResultCache
    {
    public:
        using EXIFLoader = std::function<bool(EXIFMetadata&)>;
        using XMPLoader = std::function<bool(XMPMetadata&)>;

        bool GetOrLoadEXIF(const std::wstring& filePath, EXIFMetadata& outMetadata, const EXIFLoader& loader);
        bool GetOrLoadXMP(const std::wstring& filePath, XMPMetadata& outMetadata, const XMPLoader& loader);

        void ClearAll();

    private:
        // Wrapper to cache both success and failure states
        template<typename T>
        struct CacheEntry
        {
            bool wasSuccessful;
            T data;
        };

        mutable std::shared_mutex exifMutex;
        mutable std::shared_mutex xmpMutex;
        std::unordered_map<std::wstring, CacheEntry<EXIFMetadata>> exifCache;
        std::unordered_map<std::wstring, CacheEntry<XMPMetadata>> xmpCache;
    };
}
