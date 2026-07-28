/* Copyright (c) 2003-2008 Jean-Marc Valin
   Copyright (c) 2007-2008 CSIRO
   Copyright (c) 2007-2009 Xiph.Org Foundation
   Written by Jean-Marc Valin */
/**
   @file arch.h
   @brief Various architecture definitions for CELT
*/
/*
   Redistribution and use in source and binary forms, with or without
   modification, are permitted provided that the following conditions
   are met:

   - Redistributions of source code must retain the above copyright
   notice, this list of conditions and the following disclaimer.

   - Redistributions in binary form must reproduce the above copyright
   notice, this list of conditions and the following disclaimer in the
   documentation and/or other materials provided with the distribution.

   THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
   ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
   LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
   A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER
   OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
   EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
   PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
   PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
   LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
   NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
   SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

#ifndef ARCH_H
#define ARCH_H

#include "opus_types.h"
#include "common.h"

# if !defined(__GNUC_PREREQ)
#  if defined(__GNUC__)&&defined(__GNUC_MINOR__)
#   define __GNUC_PREREQ(_maj,_min) \
 ((__GNUC__<<16)+__GNUC_MINOR__>=((_maj)<<16)+(_min))
#  else
#   define __GNUC_PREREQ(_maj,_min) 0
#  endif
# endif

#define CELT_SIG_SCALE 32768.f

#define celt_fatal(str) _celt_fatal(str, __FILE__, __LINE__);
#ifdef ENABLE_ASSERTIONS
#include <stdio.h>
#include <stdlib.h>
#ifdef __GNUC__
__attribute__((noreturn))
#endif
static OPUS_INLINE void _celt_fatal(const char *str, const char *file, int line)
{
   fprintf (stderr, "Fatal (internal) error in %s, line %d: %s\n", file, line, str);
   abort();
}
#define celt_assert(cond) {if (!(cond)) {celt_fatal("assertion failed: " #cond);}}
#define celt_assert2(cond, message) {if (!(cond)) {celt_fatal("assertion failed: " #cond "\n" message);}}
#else
#define celt_assert(cond)
#define celt_assert2(cond, message)
#endif

#define IMUL32(a,b) ((a)*(b))

#define MIN16(a,b) ((a) < (b) ? (a) : (b))   /**< Minimum 16-bit value.   */
#define MAX16(a,b) ((a) > (b) ? (a) : (b))   /**< Maximum 16-bit value.   */
#define MIN32(a,b) ((a) < (b) ? (a) : (b))   /**< Minimum 32-bit value.   */
#define MAX32(a,b) ((a) > (b) ? (a) : (b))   /**< Maximum 32-bit value.   */
#define IMIN(a,b) ((a) < (b) ? (a) : (b))   /**< Minimum int value.   */
#define IMAX(a,b) ((a) > (b) ? (a) : (b))   /**< Maximum int value.   */
#define UADD32(a,b) ((a)+(b))
#define USUB32(a,b) ((a)-(b))

/* Set this if opus_int64 is a native type of the CPU. */
/* Assume that all LP64 architectures have fast 64-bit types; also x86_64
   (which can be ILP32 for x32) and Win64 (which is LLP64). */
#if defined(__x86_64__) || defined(__LP64__) || defined(_WIN64)
#define OPUS_FAST_INT64 1
#else
#define OPUS_FAST_INT64 0
#endif

#define PRINT_MIPS(file)

#ifdef FIXED_POINT

typedef opus_int16 opus_val16;
typedef opus_int32 opus_val32;
typedef opus_int64 opus_val64;

typedef opus_val32 celt_sig;
typedef opus_val16 celt_norm;
typedef opus_val32 celt_ener;

#define Q15ONE 32767

#define SIG_SHIFT 12
/* Safe saturation value for 32-bit signals. Should be less than
   2^31*(1-0.85) to avoid blowing up on DC at deemphasis.*/
#define SIG_SAT (300000000)

#define NORM_SCALING 16384

#define DB_SHIFT 10

#define EPSILON 1
#define VERY_SMALL 0
#define VERY_LARGE16 ((opus_val16)32767)
#define Q15_ONE ((opus_val16)32767)

#define SCALEIN(a)      (a)
#define SCALEOUT(a)     (a)

#define ABS16(x) ((x) < 0 ? (-(x)) : (x))
#define ABS32(x) ((x) < 0 ? (-(x)) : (x))

static OPUS_INLINE opus_int16 SAT16(opus_int32 x) {
   return x > 32767 ? 32767 : x < -32768 ? -32768 : (opus_int16)x;
}

#ifdef FIXED_DEBUG
#include "fixed_debug.h"
#else

#include "fixed_generic.h"

#ifdef OPUS_ARM_PRESUME_AARCH64_NEON_INTR
#include "arm/fixed_arm64.h"
#elif OPUS_ARM_INLINE_EDSP
#include "arm/fixed_armv5e.h"
#elif defined (OPUS_ARM_INLINE_ASM)
#include "arm/fixed_armv4.h"
#elif defined (BFIN_ASM)
#include "fixed_bfin.h"
#elif defined (TI_C5X_ASM)
#include "fixed_c5x.h"
#elif defined (TI_C6X_ASM)
#include "fixed_c6x.h"
#endif

#endif

#else /* FIXED_POINT */

typedef float opus_val16;
typedef float opus_val32;
typedef float opus_val64;

typedef float celt_sig;
typedef float celt_norm;
typedef float celt_ener;

#ifdef FLOAT_APPROX
/* This code should reliably detect NaN/inf even when -ffast-math is used.
   Assumes IEEE 754 format. */
static OPUS_INLINE int celt_isnan(float x)
{
   union {float f; opus_uint32 i;} in;
   in.f = x;
   return ((in.i>>23)&0xFF)==0xFF && (in.i&0x007FFFFF)!=0;
}
#else
#ifdef __FAST_MATH__
#error Cannot build libopus with -ffast-math unless FLOAT_APPROX is defined. This could result in crashes on extreme (e.g. NaN) input
#endif
#define celt_isnan(x) ((x)!=(x))
#endif

#define Q15ONE 1.0f

#define NORM_SCALING 1.f

#define EPSILON 1e-15f
#define VERY_SMALL 1e-30f
#define VERY_LARGE16 1e15f
#define Q15_ONE ((opus_val16)1.f)

/* This appears to be the same speed as C99's fabsf() but it's more portable. */
#define ABS16(x) ((float)fabs(x))
#define ABS32(x) ((float)fabs(x))

#define QCONST16(x,bits) (x)
#define QCONST32(x,bits) (x)

#define NEG16(x) (-(x))
#define NEG32(x) (-(x))
#define NEG32_ovflw(x) (-(x))
#define EXTRACT16(x) (x)
#define EXTEND32(x) (x)
#define SHR16(a,shift) (a)
#define SHL16(a,shift) (a)
#define SHR32(a,shift) (a)
#define SHL32(a,shift) (a)
#define PSHR32(a,shift) (a)
#define VSHR32(a,shift) (a)

#define PSHR(a,shift)   (a)
#define SHR(a,shift)    (a)
#define SHL(a,shift)    (a)
#define SATURATE(x,a)   (x)
#define SATURATE16(x)   (x)

#define ROUND16(a,shift)  (a)
#define SROUND16(a,shift) (a)
#define HALF16(x)       (.5f*(x))
#define HALF32(x)       (.5f*(x))

#define ADD16(a,b) ((a)+(b))
#define SUB16(a,b) ((a)-(b))
#define ADD32(a,b) ((a)+(b))
#define SUB32(a,b) ((a)-(b))
#define ADD32_ovflw(a,b) ((a)+(b))
#define SUB32_ovflw(a,b) ((a)-(b))
#define MULT16_16_16(a,b)     ((a)*(b))
#define MULT16_16(a,b)     ((opus_val32)(a)*(opus_val32)(b))
#define MAC16_16(c,a,b)     ((c)+(opus_val32)(a)*(opus_val32)(b))

#define MULT16_32_Q15(a,b)     ((a)*(b))
#define MULT16_32_Q16(a,b)     ((a)*(b))

#define MULT32_32_Q31(a,b)     ((a)*(b))

#define MAC16_32_Q15(c,a,b)     ((c)+(a)*(b))
#define MAC16_32_Q16(c,a,b)     ((c)+(a)*(b))

#define MULT16_16_Q11_32(a,b)     ((a)*(b))
#define MULT16_16_Q11(a,b)     ((a)*(b))
#define MULT16_16_Q13(a,b)     ((a)*(b))
#define MULT16_16_Q14(a,b)     ((a)*(b))
#define MULT16_16_Q15(a,b)     ((a)*(b))
#define MULT16_16_P15(a,b)     ((a)*(b))
#define MULT16_16_P13(a,b)     ((a)*(b))
#define MULT16_16_P14(a,b)     ((a)*(b))
#define MULT16_32_P16(a,b)     ((a)*(b))

#define DIV32_16(a,b)     (((opus_val32)(a))/(opus_val16)(b))
#define DIV32(a,b)     (((opus_val32)(a))/(opus_val32)(b))

#define SCALEIN(a)      ((a)*CELT_SIG_SCALE)
#define SCALEOUT(a)     ((a)*(1/CELT_SIG_SCALE))

#define SIG2WORD16(x) (x)

#endif /* !FIXED_POINT */

#ifndef GLOBAL_STACK_SIZE
#ifdef FIXED_POINT
#define GLOBAL_STACK_SIZE 120000
#else
#define GLOBAL_STACK_SIZE 120000
#endif
#endif

#endif /* ARCH_H */
