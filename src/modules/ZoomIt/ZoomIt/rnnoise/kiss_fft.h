/*Copyright (c) 2003-2004, Mark Borgerding
  Lots of modifications by Jean-Marc Valin
  Copyright (c) 2005-2007, Xiph.Org Foundation
  Copyright (c) 2008,      Xiph.Org Foundation, CSIRO

  All rights reserved.

  Redistribution and use in source and binary forms, with or without
   modification, are permitted provided that the following conditions are met:

    * Redistributions of source code must retain the above copyright notice,
       this list of conditions and the following disclaimer.
    * Redistributions in binary form must reproduce the above copyright notice,
       this list of conditions and the following disclaimer in the
       documentation and/or other materials provided with the distribution.

  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
  AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
  IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
  ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
  LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
  CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
  SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
  INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
  CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
  ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
  POSSIBILITY OF SUCH DAMAGE.*/

#ifndef KISS_FFT_H
#define KISS_FFT_H

#include <stdlib.h>
#include <math.h>
#include "arch.h"

#include <stdlib.h>
#define opus_alloc(x) malloc(x)
#define opus_free(x) free(x)

#ifdef __cplusplus
extern "C" {
#endif

#ifdef USE_SIMD
# include <xmmintrin.h>
# define kiss_fft_scalar __m128
#define KISS_FFT_MALLOC(nbytes) memalign(16,nbytes)
#else
#define KISS_FFT_MALLOC opus_alloc
#endif

#ifdef FIXED_POINT
#include "arch.h"

#  define kiss_fft_scalar opus_int32
#  define kiss_twiddle_scalar opus_int16


#else
# ifndef kiss_fft_scalar
/*  default is float */
#   define kiss_fft_scalar float
#   define kiss_twiddle_scalar float
#   define KF_SUFFIX _celt_single
# endif
#endif

typedef struct {
    kiss_fft_scalar r;
    kiss_fft_scalar i;
}kiss_fft_cpx;

typedef struct {
   kiss_twiddle_scalar r;
   kiss_twiddle_scalar i;
}kiss_twiddle_cpx;

#define MAXFACTORS 8
/* e.g. an fft of length 128 has 4 factors
 as far as kissfft is concerned
 4*4*4*2
 */

typedef struct arch_fft_state{
   int is_supported;
   void *priv;
} arch_fft_state;

typedef struct kiss_fft_state{
    int nfft;
    opus_val16 scale;
#ifdef FIXED_POINT
    int scale_shift;
#endif
    int shift;
    opus_int16 factors[2*MAXFACTORS];
    const opus_int32 *bitrev;
    const kiss_twiddle_cpx *twiddles;
    arch_fft_state *arch_fft;
} kiss_fft_state;

#if defined(HAVE_ARM_NE10)
#include "arm/fft_arm.h"
#endif

/*typedef struct kiss_fft_state* kiss_fft_cfg;*/

/**
 *  opus_fft_alloc
 *
 *  Initialize a FFT (or IFFT) algorithm's cfg/state buffer.
 *
 *  typical usage:      kiss_fft_cfg mycfg=opus_fft_alloc(1024,0,NULL,NULL);
 *
 *  The return value from fft_alloc is a cfg buffer used internally
 *  by the fft routine or NULL.
 *
 *  If lenmem is NULL, then opus_fft_alloc will allocate a cfg buffer using malloc.
 *  The returned value should be free()d when done to avoid memory leaks.
 *
 *  The state can be placed in a user supplied buffer 'mem':
 *  If lenmem is not NULL and mem is not NULL and *lenmem is large enough,
 *      then the function places the cfg in mem and the size used in *lenmem
 *      and returns mem.
 *
 *  If lenmem is not NULL and ( mem is NULL or *lenmem is not large enough),
 *      then the function returns NULL and places the minimum cfg
 *      buffer size in *lenmem.
 * */

kiss_fft_state *rnn_fft_alloc_twiddles(int nfft,void * mem,size_t * lenmem, const kiss_fft_state *base, int arch);

kiss_fft_state *rnn_fft_alloc(int nfft,void * mem,size_t * lenmem, int arch);

/**
 * opus_fft(cfg,in_out_buf)
 *
 * Perform an FFT on a complex input buffer.
 * for a forward FFT,
 * fin should be  f[0] , f[1] , ... ,f[nfft-1]
 * fout will be   F[0] , F[1] , ... ,F[nfft-1]
 * Note that each element is complex and can be accessed like
    f[k].r and f[k].i
 * */
void rnn_fft_c(const kiss_fft_state *cfg,const kiss_fft_cpx *fin,kiss_fft_cpx *fout);
void rnn_ifft_c(const kiss_fft_state *cfg,const kiss_fft_cpx *fin,kiss_fft_cpx *fout);

void rnn_fft_impl(const kiss_fft_state *st,kiss_fft_cpx *fout);
void rnn_ifft_impl(const kiss_fft_state *st,kiss_fft_cpx *fout);

void rnn_fft_free(const kiss_fft_state *cfg, int arch);


void rnn_fft_free_arch_c(kiss_fft_state *st);
int rnn_fft_alloc_arch_c(kiss_fft_state *st);

#if !defined(OVERRIDE_OPUS_FFT)
/* Is run-time CPU detection enabled on this platform? */
#if defined(OPUS_HAVE_RTCD) && (defined(HAVE_ARM_NE10))

extern int (*const OPUS_FFT_ALLOC_ARCH_IMPL[OPUS_ARCHMASK+1])(
 kiss_fft_state *st);

#define opus_fft_alloc_arch(_st, arch) \
         ((*OPUS_FFT_ALLOC_ARCH_IMPL[(arch)&OPUS_ARCHMASK])(_st))

extern void (*const OPUS_FFT_FREE_ARCH_IMPL[OPUS_ARCHMASK+1])(
 kiss_fft_state *st);
#define opus_fft_free_arch(_st, arch) \
         ((*OPUS_FFT_FREE_ARCH_IMPL[(arch)&OPUS_ARCHMASK])(_st))

extern void (*const OPUS_FFT[OPUS_ARCHMASK+1])(const kiss_fft_state *cfg,
 const kiss_fft_cpx *fin, kiss_fft_cpx *fout);
#define opus_fft(_cfg, _fin, _fout, arch) \
   ((*OPUS_FFT[(arch)&OPUS_ARCHMASK])(_cfg, _fin, _fout))

extern void (*const OPUS_IFFT[OPUS_ARCHMASK+1])(const kiss_fft_state *cfg,
 const kiss_fft_cpx *fin, kiss_fft_cpx *fout);
#define opus_ifft(_cfg, _fin, _fout, arch) \
   ((*OPUS_IFFT[(arch)&OPUS_ARCHMASK])(_cfg, _fin, _fout))

#else /* else for if defined(OPUS_HAVE_RTCD) && (defined(HAVE_ARM_NE10)) */

#define rnn_fft_alloc_arch(_st, arch) \
         ((void)(arch), rnn_fft_alloc_arch_c(_st))

#define rnn_fft_free_arch(_st, arch) \
         ((void)(arch), rnn_fft_free_arch_c(_st))

#define rnn_fft(_cfg, _fin, _fout, arch) \
         ((void)(arch), rnn_fft_c(_cfg, _fin, _fout))

#define rnn_ifft(_cfg, _fin, _fout, arch) \
         ((void)(arch), rnn_ifft_c(_cfg, _fin, _fout))

#endif /* end if defined(OPUS_HAVE_RTCD) && (defined(HAVE_ARM_NE10)) */
#endif /* end if !defined(OVERRIDE_OPUS_FFT) */

#ifdef __cplusplus
}
#endif

#endif
