// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "WICObjectCache.h"

using namespace PowerRenameLib;

// Static member initialization
CComPtr<IWICImagingFactory> WICObjectCache::wicFactory;
std::once_flag WICObjectCache::factoryInitFlag;

WICObjectCache& WICObjectCache::Instance()
{
    static WICObjectCache instance;
    return instance;
}

void WICObjectCache::InitializeFactory()
{
    if (!wicFactory)
    {
        CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED | COINIT_DISABLE_OLE1DDE);
        HRESULT hr = CoCreateInstance(
            CLSID_WICImagingFactory,
            nullptr,
            CLSCTX_INPROC_SERVER,
            IID_PPV_ARGS(&wicFactory));
            
        if (FAILED(hr))
        {
            throw std::runtime_error("Failed to create WIC factory");
        }
    }
}

CComPtr<IWICBitmapDecoder> WICObjectCache::GetDecoder(const std::wstring& filePath)
{
    // Ensure factory is initialized
    std::call_once(factoryInitFlag, InitializeFactory);
    
    // Check cache with read lock first
    {
        std::shared_lock<std::shared_mutex> lock(cacheMutex);
        auto it = cache.find(filePath);
        if (it != cache.end())
        {
            // Check if entry is still valid
            if (IsEntryValid(filePath, it->second.first))
            {
                cacheHits++;
                // Update access time (requires write lock)
                lock.unlock();
                
                std::unique_lock<std::shared_mutex> writeLock(cacheMutex);
                it->second.first.lastAccess = std::chrono::steady_clock::now();
                UpdateLRU(filePath);
                return it->second.first.decoder;
            }
        }
    }
    
    // Cache miss - create new decoder
    cacheMisses++;
    
    CComPtr<IWICBitmapDecoder> decoder;
    HRESULT hr = wicFactory->CreateDecoderFromFilename(
        filePath.c_str(),
        nullptr,
        GENERIC_READ,
        WICDecodeMetadataCacheOnDemand,
        &decoder);
        
    if (FAILED(hr) || !decoder)
    {
        return nullptr;
    }
    
    // Add to cache with write lock
    std::unique_lock<std::shared_mutex> lock(cacheMutex);
    
    // Check cache size and evict if necessary
    if (cache.size() >= MAX_CACHE_SIZE)
    {
        EvictLRU();
    }
    
    // Get file modification time
    std::filesystem::file_time_type lastWriteTime;
    try
    {
        lastWriteTime = std::filesystem::last_write_time(filePath);
    }
    catch (...)
    {
        // If we can't get the modification time, use a default
        lastWriteTime = std::filesystem::file_time_type::min();
    }
    
    // Create cache entry
    DecoderInfo info;
    info.decoder = decoder;
    info.lastWriteTime = lastWriteTime;
    info.lastAccess = std::chrono::steady_clock::now();
    
    // Add to LRU list and cache
    lruList.push_front(filePath);
    cache[filePath] = std::make_pair(info, lruList.begin());
    
    return decoder;
}

CComPtr<IWICMetadataQueryReader> WICObjectCache::GetMetadataReader(const std::wstring& filePath)
{
    auto decoder = GetDecoder(filePath);
    if (!decoder)
    {
        return nullptr;
    }
    
    CComPtr<IWICBitmapFrameDecode> frame;
    HRESULT hr = decoder->GetFrame(0, &frame);
    if (FAILED(hr) || !frame)
    {
        return nullptr;
    }
    
    CComPtr<IWICMetadataQueryReader> reader;
    hr = frame->GetMetadataQueryReader(&reader);
    if (FAILED(hr))
    {
        return nullptr;
    }
    
    return reader;
}

bool WICObjectCache::IsEntryValid(const std::wstring& path, const DecoderInfo& info) const
{
    // Check if file has been modified
    try
    {
        auto currentWriteTime = std::filesystem::last_write_time(path);
        if (currentWriteTime != info.lastWriteTime)
        {
            return false;
        }
    }
    catch (...)
    {
        // If we can't check the file, assume it's invalid
        return false;
    }
    
    // Check if entry has expired
    auto now = std::chrono::steady_clock::now();
    if (now - info.lastAccess > CACHE_EXPIRY_TIME)
    {
        return false;
    }
    
    return true;
}

void WICObjectCache::EvictLRU()
{
    if (!lruList.empty())
    {
        // Remove the least recently used item
        auto lruPath = lruList.back();
        lruList.pop_back();
        cache.erase(lruPath);
    }
}

void WICObjectCache::UpdateLRU(const std::wstring& path)
{
    auto it = cache.find(path);
    if (it != cache.end())
    {
        // Move to front of LRU list
        lruList.erase(it->second.second);
        lruList.push_front(path);
        it->second.second = lruList.begin();
    }
}

void WICObjectCache::Clear()
{
    std::unique_lock<std::shared_mutex> lock(cacheMutex);
    cache.clear();
    lruList.clear();
    cacheHits = 0;
    cacheMisses = 0;
}

void WICObjectCache::CleanExpired()
{
    std::unique_lock<std::shared_mutex> lock(cacheMutex);
    
    auto now = std::chrono::steady_clock::now();
    std::vector<std::wstring> toRemove;
    
    for (const auto& [path, entry] : cache)
    {
        if (!IsEntryValid(path, entry.first))
        {
            toRemove.push_back(path);
        }
    }
    
    for (const auto& path : toRemove)
    {
        auto it = cache.find(path);
        if (it != cache.end())
        {
            lruList.erase(it->second.second);
            cache.erase(it);
        }
    }
}

WICObjectCache::CacheStats WICObjectCache::GetStats() const
{
    std::shared_lock<std::shared_mutex> lock(cacheMutex);
    return { cache.size(), cacheHits.load(), cacheMisses.load() };
}