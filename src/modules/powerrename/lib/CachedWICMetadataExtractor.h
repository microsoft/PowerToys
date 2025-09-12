// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once
#include "WICMetadataExtractor.h"
#include "WICObjectCache.h"

namespace PowerRenameLib
{
    /// <summary>
    /// Cached version of WICMetadataExtractor that reuses WIC objects
    /// for improved performance when processing multiple files
    /// </summary>
    class CachedWICMetadataExtractor : public WICMetadataExtractor
    {
    public:
        CachedWICMetadataExtractor();
        ~CachedWICMetadataExtractor() override = default;
        
        // Override to use cached decoder
        ExtractionResult ExtractEXIFMetadata(
            const std::wstring& filePath,
            EXIFMetadata& outMetadata) override;
            
        ExtractionResult ExtractXMPMetadata(
            const std::wstring& filePath,
            XMPMetadata& outMetadata) override;
            
        // Cache management
        void ClearCache();
        WICObjectCache::CacheStats GetCacheStats() const;
        
    protected:
        // Helper methods to use cached objects
        CComPtr<IWICBitmapDecoder> CreateDecoder(const std::wstring& filePath);
        CComPtr<IWICMetadataQueryReader> GetMetadataReader(IWICBitmapDecoder* decoder);
        
    private:
        WICObjectCache& cache;
    };
}