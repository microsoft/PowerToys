/* Copyright (c) 2018-2019 Mozilla
                 2023 Amazon */
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
   A PARTICULAR PURPOSE ARE DISCLAIMED.  IN NO EVENT SHALL THE FOUNDATION OR
   CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
   EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
   PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
   PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
   LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
   NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
   SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

#ifdef HAVE_CONFIG_H
#include "config.h"
#endif

#include "x86/x86cpu.h"
#include "nnet.h"

#ifdef RNN_ENABLE_X86_RTCD


void (*const RNN_COMPUTE_LINEAR_IMPL[OPUS_ARCHMASK + 1])(
         const LinearLayer *linear,
         float *out,
         const float *in
) = {
  compute_linear_c,                /* non-sse */
  MAY_HAVE_SSE4_1(compute_linear), /* sse4.1  */
  MAY_HAVE_AVX2(compute_linear)  /* avx  */
};

void (*const RNN_COMPUTE_ACTIVATION_IMPL[OPUS_ARCHMASK + 1])(
         float *output,
         const float *input,
         int N,
         int activation
) = {
  compute_activation_c,                /* non-sse */
  MAY_HAVE_SSE4_1(compute_activation), /* sse4.1  */
  MAY_HAVE_AVX2(compute_activation)  /* avx  */
};

void (*const RNN_COMPUTE_CONV2D_IMPL[OPUS_ARCHMASK + 1])(
         const Conv2dLayer *conv,
         float *out,
         float *mem,
         const float *in,
         int height,
         int hstride,
         int activation
) = {
  compute_conv2d_c,                /* non-sse */
  MAY_HAVE_SSE4_1(compute_conv2d), /* sse4.1  */
  MAY_HAVE_AVX2(compute_conv2d)  /* avx  */
};


#endif
