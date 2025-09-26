// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once
#include <wincodec.h>
#include <atlbase.h>
#include <string>
#include <unordered_map>
#include <shared_mutex>
#include <filesystem>
#include <chrono>
#include <list>

namespace PowerRenameLib
{
    /// <summary>
    /// Thread-safe cache for WIC decoder objects to improve performance
    /// by avoiding repeated decoder creation for the same files
    /// </summary>
    class WICObjectCache
    {
    public:
        struct DecoderInfo
        {
            CComPtr<IWICBitmapDecoder> decoder;
            std::filesystem::file_time_type lastWriteTime;
            std::chrono::steady_clock::time_point lastAccess;
        };

    private:
        // Thread-safe cache storage
        mutable std::shared_mutex cacheMutex;
        
        // LRU cache implementation
        std::list<std::wstring> lruList;
        std::unordered_map<std::wstring, std::pair<DecoderInfo, std::list<std::wstring>::iterator>> cache;
        
        // Cache configuration
        static constexpr size_t MAX_CACHE_SIZE = 10;
        static constexpr auto CACHE_EXPIRY_TIME = std::chrono::minutes(5);
        
        // Singleton WIC factory
        static CComPtr<IWICImagingFactory> wicFactory;
        static std::once_flag factoryInitFlag;

        WICObjectCache() = default;
        
    public:
        static WICObjectCache& Instance();
        
        // Get or create decoder for a file
        CComPtr<IWICBitmapDecoder> GetDecoder(const std::wstring& filePath);
        
        // Get metadata reader from cached decoder
        CComPtr<IWICMetadataQueryReader> GetMetadataReader(const std::wstring& filePath);
        
        // Clear cache
        void Clear();
        
        // Remove expired entries
        void CleanExpired();
        
        // Get cache statistics
        struct CacheStats
        {
            size_t size;
            size_t hits;
            size_t misses;
        };
        CacheStats GetStats() const;
        
    private:
        // Initialize WIC factory
        static void InitializeFactory();
        
        // Check if cached entry is still valid
        bool IsEntryValid(const std::wstring& path, const DecoderInfo& info) const;
        
        // Evict least recently used entry
        void EvictLRU();
        
        // Update LRU order
        void UpdateLRU(const std::wstring& path);
        
        // Cache statistics
        mutable std::atomic<size_t> cacheHits{0};
        mutable std::atomic<size_t> cacheMisses{0};
    };
}