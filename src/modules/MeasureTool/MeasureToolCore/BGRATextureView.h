#pragma once

#include <cinttypes>
#include <wil/resource.h>
#ifdef _M_ARM64
#include <arm64_neon.h.>
#else
#include <emmintrin.h>
#endif
#include <cassert>

#if defined(_M_ARM64)
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

inline __m128i _mm_set1_epi8(signed char w)
{
    return vreinterpretq_s64_s8(vdupq_n_s8(w));
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

#endif

inline __m128i distance_epi8(const __m128i a, __m128i b)
{
    return _mm_or_si128(_mm_subs_epu8(a, b),
                        _mm_subs_epu8(b, a));
}

struct BGRATextureView
{
    const uint32_t* pixels = nullptr;
    size_t width = {};
    size_t height = {};

    BGRATextureView() = default;

    BGRATextureView(BGRATextureView&& rhs) noexcept
    {
        pixels = rhs.pixels;
        width = rhs.width;
        height = rhs.height;
    }

    inline uint32_t GetPixel(const size_t x, const size_t y) const
    {
        assert(x < width && x >= 0);
        assert(y < height && y >= 0);
        return pixels[x + width * y];
    }

    static inline bool PixelsClose(const uint32_t pixel1, const uint32_t pixel2, const uint8_t tolerance)
    {
        const __m128i rgba1 = _mm_cvtsi32_si128(pixel1);
        const __m128i rgba2 = _mm_cvtsi32_si128(pixel2);
        const __m128i distances = distance_epi8(rgba1, rgba2);

        // Method 1: Test whether each channel distance is not great than tolerance
        //const __m128i tolerances = _mm_set1_epi8(tolerance);
        //return _mm_cvtsi128_si32(_mm_cmpgt_epi8(distances, tolerances)) == 0;

        // Method 2: Test whether sum of all channel differences is smaller than tolerance
        return _mm_cvtsi128_si32(_mm_sad_epu8(distances, _mm_setzero_si128())) <= tolerance;
    }

#if !defined(NDEBUG)
    void SaveAsBitmap(const char* filename) const;
#endif
};
