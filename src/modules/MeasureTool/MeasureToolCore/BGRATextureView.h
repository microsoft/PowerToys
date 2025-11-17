#pragma once

#include <cinttypes>
#include <wil/resource.h>
#ifdef _M_ARM64
#include <arm64_neon.h.>
#else
#include <emmintrin.h>
#endif
#include <cassert>
#include <limits>
#include <d3d11.h>


//#define DEBUG_TEXTURE

#if defined(_M_ARM64)

// Adopted from https://github.com/DLTcollab/sse2neon/blob/master/sse2neon.h

using __m128i = int64x2_t;

inline __m128i _mm_cvtsi32_si128(int a)
{
    return vreinterpretq_s64_s32(vsetq_lane_s32(a, vdupq_n_s32(0), 0));
}

inline __m128i _mm_or_si128(__m128i a, __m128i b)
{
    return vreinterpretq_s64_s32(
        vorrq_s32(vreinterpretq_s32_s64(a), vreinterpretq_s32_s64(b)));
}

inline __m128i _mm_subs_epu8(__m128i a, __m128i b)
{
    return vreinterpretq_s64_u8(
        vqsubq_u8(vreinterpretq_u8_s64(a), vreinterpretq_u8_s64(b)));
}

inline __m128i _mm_sad_epu8(__m128i a, __m128i b)
{
    uint16x8_t t = vpaddlq_u8(vabdq_u8((uint8x16_t)a, (uint8x16_t)b));
    return vreinterpretq_s64_u64(vpaddlq_u32(vpaddlq_u16(t)));
}

inline __m128i _mm_setzero_si128(void)
{
    return vreinterpretq_s64_s32(vdupq_n_s32(0));
}

inline int _mm_cvtsi128_si32(__m128i a)
{
    return vgetq_lane_s32(vreinterpretq_s32_s64(a), 0);
}

inline __m128i _mm_set1_epi16(short w)
{
    return vreinterpretq_s64_s16(vdupq_n_s16(w));
}

inline __m128i _mm_cmpgt_epi16(__m128i a, __m128i b)
{
    return vreinterpretq_s64_u16(
        vcgtq_s16(vreinterpretq_s16_s64(a), vreinterpretq_s16_s64(b)));
}

inline __m128i _mm_cvtepu8_epi16(__m128i a)
{
    uint8x16_t u8x16 = vreinterpretq_u8_s64(a); /* xxxx xxxx HGFE DCBA */
    uint16x8_t u16x8 = vmovl_u8(vget_low_u8(u8x16)); /* 0H0G 0F0E 0D0C 0B0A */
    return vreinterpretq_s64_u16(u16x8);
}

inline int64_t _mm_cvtsi128_si64(__m128i a)
{
    return vgetq_lane_s64(a, 0);
}
#endif

inline __m128i distance_epu8(const __m128i a, __m128i b)
{
    return _mm_or_si128(_mm_subs_epu8(a, b),
                        _mm_subs_epu8(b, a));
}

struct BGRATextureView
{
    const uint32_t* pixels = nullptr;
    size_t pitch = {};
    size_t width = {};
    size_t height = {};

    BGRATextureView() = default;

    BGRATextureView(BGRATextureView&& rhs) = default;

    inline uint32_t GetPixel(const size_t x, const size_t y) const
    {
        assert(x < width && x >= 0);
        assert(y < height && y >= 0);
        return pixels[x + pitch * y];
    }

    template<bool perChannel>
    static inline bool PixelsClose(const uint32_t pixel1, const uint32_t pixel2, uint8_t tolerance)
    {
        const __m128i rgba1 = _mm_cvtsi32_si128(pixel1);
        const __m128i rgba2 = _mm_cvtsi32_si128(pixel2);
        const __m128i distances = distance_epu8(rgba1, rgba2);

        // Method 1: Test whether each channel distance is not greater than tolerance
        if constexpr (perChannel)
        {
            const __m128i tolerances = _mm_set1_epi16(tolerance);
            const auto gtResults128 = _mm_cmpgt_epi16(_mm_cvtepu8_epi16(distances), tolerances);
            return _mm_cvtsi128_si64(gtResults128) == 0;
        }
        else
        {
            // Method 2: Test whether sum of all channel differences is smaller than tolerance
            const int32_t score = _mm_cvtsi128_si32(_mm_sad_epu8(distances, _mm_setzero_si128())) & std::numeric_limits<uint8_t>::max();
            return score <= tolerance;
        }
    }

#if defined(DEBUG_TEXTURE)
    void SaveAsBitmap(const char* filename) const;
#endif
};

class MappedTextureView
{
    winrt::com_ptr<ID3D11DeviceContext> context;
    winrt::com_ptr<ID3D11Texture2D> texture;

public:
    BGRATextureView view;
    MappedTextureView(winrt::com_ptr<ID3D11Texture2D> _texture,
                      winrt::com_ptr<ID3D11DeviceContext> _context,
                      const size_t textureWidth,
                      const size_t textureHeight) :
        texture{ std::move(_texture) }, context{ std::move(_context) }
    {
        D3D11_TEXTURE2D_DESC desc;
        texture->GetDesc(&desc);

        D3D11_MAPPED_SUBRESOURCE resource = {};
        winrt::check_hresult(context->Map(texture.get(), D3D11CalcSubresource(0, 0, 0), D3D11_MAP_READ, 0, &resource));

        view.pixels = static_cast<const uint32_t*>(resource.pData);
        view.pitch = resource.RowPitch / 4;
        view.width = textureWidth;
        view.height = textureHeight;
    }

    MappedTextureView(MappedTextureView&&) = default;
    MappedTextureView& operator=(MappedTextureView&&) = default;

    inline winrt::com_ptr<ID3D11Texture2D> GetTexture() const
    {
        return texture;
    }

    ~MappedTextureView()
    {
        if (context && texture)
            context->Unmap(texture.get(), D3D11CalcSubresource(0, 0, 0));
    }
};