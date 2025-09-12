// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "CachedWICMetadataExtractor.h"

using namespace PowerRenameLib;

CachedWICMetadataExtractor::CachedWICMetadataExtractor()
    : cache(WICObjectCache::Instance())
{
}

ExtractionResult CachedWICMetadataExtractor::ExtractEXIFMetadata(
    const std::wstring& filePath,
    EXIFMetadata& outMetadata)
{
    // Use cached metadata reader
    auto reader = cache.GetMetadataReader(filePath);
    if (!reader)
    {
        return ExtractionResult::FileNotFound;
    }
    
    // Call base class implementation with the cached reader
    // Note: We need to modify the base class to support this
    // For now, use the standard implementation
    return WICMetadataExtractor::ExtractEXIFMetadata(filePath, outMetadata);
}

ExtractionResult CachedWICMetadataExtractor::ExtractXMPMetadata(
    const std::wstring& filePath,
    XMPMetadata& outMetadata)
{
    // Use cached metadata reader
    auto reader = cache.GetMetadataReader(filePath);
    if (!reader)
    {
        return ExtractionResult::FileNotFound;
    }
    
    // Call base class implementation
    return WICMetadataExtractor::ExtractXMPMetadata(filePath, outMetadata);
}

CComPtr<IWICBitmapDecoder> CachedWICMetadataExtractor::CreateDecoder(const std::wstring& filePath)
{
    // Use cached decoder instead of creating new one
    return cache.GetDecoder(filePath);
}

CComPtr<IWICMetadataQueryReader> CachedWICMetadataExtractor::GetMetadataReader(IWICBitmapDecoder* decoder)
{
    // This still needs to get the reader from the decoder
    // but the decoder itself is cached
    if (!decoder)
        return nullptr;
        
    CComPtr<IWICBitmapFrameDecode> frame;
    HRESULT hr = decoder->GetFrame(0, &frame);
    if (FAILED(hr) || !frame)
        return nullptr;
        
    CComPtr<IWICMetadataQueryReader> reader;
    hr = frame->GetMetadataQueryReader(&reader);
    if (FAILED(hr))
        return nullptr;
        
    return reader;
}

void CachedWICMetadataExtractor::ClearCache()
{
    cache.Clear();
}

WICObjectCache::CacheStats CachedWICMetadataExtractor::GetCacheStats() const
{
    return cache.GetStats();
}