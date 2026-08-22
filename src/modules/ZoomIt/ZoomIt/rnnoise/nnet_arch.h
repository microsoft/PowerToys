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

#ifndef NNET_ARCH_H
#define NNET_ARCH_H

#include "nnet.h"
#include "arch.h"
#include "common.h"
#include "vec.h"

#define CAT_SUFFIX2(a,b) a ## b
#define CAT_SUFFIX(a,b) CAT_SUFFIX2(a, b)

#define RTCD_SUF(name) CAT_SUFFIX(name, RTCD_ARCH)

# if !defined(OPUS_GNUC_PREREQ)
#  if defined(__GNUC__)&&defined(__GNUC_MINOR__)
#   define OPUS_GNUC_PREREQ(_maj,_min) \
 ((__GNUC__<<16)+__GNUC_MINOR__>=((_maj)<<16)+(_min))
#  else
#   define OPUS_GNUC_PREREQ(_maj,_min) 0
#  endif
# endif


/* Force vectorization on for DNN code because some of the loops rely on
   compiler vectorization rather than explicitly using intrinsics. */
#if OPUS_GNUC_PREREQ(5,1)
#define GCC_POP_OPTIONS
#pragma GCC push_options
#pragma GCC optimize("tree-vectorize")
#endif


#define MAX_ACTIVATIONS (4096)

static OPUS_INLINE void vec_swish(float *y, const float *x, int N)
{
   int i;
   float tmp[MAX_ACTIVATIONS];
   celt_assert(N <= MAX_ACTIVATIONS);
   vec_sigmoid(tmp, x, N);
   for (i=0;i<N;i++)
      y[i] = x[i]*tmp[i];
}

static OPUS_INLINE float relu(float x)
{
   return x < 0 ? 0 : x;
}

/*#define HIGH_ACCURACY */

void RTCD_SUF(compute_activation_)(float *output, const float *input, int N, int activation)
{
   int i;
   if (activation == ACTIVATION_SIGMOID) {
#ifdef HIGH_ACCURACY
      for (int n=0; n<N; n++)
      {
         output[n] = 1.f  / (1 + exp(-input[n]));
      }
#else
      vec_sigmoid(output, input, N);
#endif
   } else if (activation == ACTIVATION_TANH) {
#ifdef HIGH_ACCURACY
      for (int n=0; n<N; n++)
      {
         output[n] = tanh(input[n]);
      }
#else
      vec_tanh(output, input, N);
#endif
   } else if (activation == ACTIVATION_SWISH) {
      vec_swish(output, input, N);
   } else if (activation == ACTIVATION_RELU) {
      for (i=0;i<N;i++)
         output[i] = relu(input[i]);
   } else if (activation == ACTIVATION_SOFTMAX) {
#ifdef SOFTMAX_HACK
      RNN_COPY(output, input, N);
      /*for (i=0;i<N;i++)
         output[i] = input[i];*/
#else
      float sum = 0;
      softmax(output, input, N);
      for (i=0;i<N;i++) {
         sum += output[i];
      }
      sum = 1.f/(sum+1e-30);
      for (i=0;i<N;i++)
         output[i] = sum*output[i];
#endif
   } else {
      celt_assert(activation == ACTIVATION_LINEAR);
      if (input != output) {
         for (i=0;i<N;i++)
            output[i] = input[i];
      }
   }
}


void RTCD_SUF(compute_linear_) (const LinearLayer *linear, float *out, const float *in)
{
   int i, M, N;
   const float *bias;
   celt_assert(in != out);
   bias = linear->bias;
   M = linear->nb_inputs;
   N = linear->nb_outputs;
   if (linear->float_weights != NULL) {
     if (linear->weights_idx != NULL) sparse_sgemv8x4(out, linear->float_weights, linear->weights_idx, N, in);
     else sgemv(out, linear->float_weights, N, M, N, in);
   } else if (linear->weights != NULL) {
     if (linear->weights_idx != NULL) sparse_cgemv8x4(out, linear->weights, linear->weights_idx, linear->scale, N, M, in);
     else cgemv8x4(out, linear->weights, linear->scale, N, M, in);
     /* Only use SU biases on for integer matrices on SU archs. */
#ifdef USE_SU_BIAS
     bias = linear->subias;
#endif
   }
   else RNN_CLEAR(out, N);
   if (bias != NULL) {
      for (i=0;i<N;i++) out[i] += bias[i];
   }
   if (linear->diag) {
      /* Diag is only used for GRU recurrent weights. */
      celt_assert(3*M == N);
      for (i=0;i<M;i++) {
         out[i] += linear->diag[i]*in[i];
         out[i+M] += linear->diag[i+M]*in[i];
         out[i+2*M] += linear->diag[i+2*M]*in[i];
      }
   }
}

/* Computes non-padded convolution for input [ ksize1 x in_channels x (len2+ksize2) ],
   kernel [ out_channels x in_channels x ksize1 x ksize2 ],
   storing the output as [ out_channels x len2 ].
   We assume that the output dimension along the ksize1 axis is 1,
   i.e. processing one frame at a time. */
static void conv2d_float(float *out, const float *weights, int in_channels, int out_channels, int ktime, int kheight, const float *in, int height, int hstride)
{
   int i;
   int in_stride;
   in_stride = height+kheight-1;
   for (i=0;i<out_channels;i++) {
      int m;
      RNN_CLEAR(&out[i*hstride], height);
      for (m=0;m<in_channels;m++) {
         int t;
         for (t=0;t<ktime;t++) {
            int h;
            for (h=0;h<kheight;h++) {
               int j;
               for (j=0;j<height;j++) {
                  out[i*hstride + j] += weights[i*in_channels*ktime*kheight + m*ktime*kheight + t*kheight + h] *
                                     in[t*in_channels*in_stride + m*in_stride + j + h];
               }
            }
         }
      }
   }
}

/* There's no intrinsics in this function (or the one above) because the gcc (and hopefully other compiler) auto-vectorizer is smart enough to
   produce the right code by itself based on the compile flags. */
static void conv2d_3x3_float(float *out, const float *weights, int in_channels, int out_channels, const float *in, int height, int hstride)
{
   int i;
   int in_stride;
   int kheight, ktime;
   kheight = ktime = 3;
   in_stride = height+kheight-1;
   for (i=0;i<out_channels;i++) {
      int m;
      RNN_CLEAR(&out[i*hstride], height);
      for (m=0;m<in_channels;m++) {
         int j;
         for (j=0;j<height;j++) {
            /* Unrolled version of previous function -- compiler will figure out the indexing simplifications. */
            out[i*hstride + j] += weights[i*in_channels*ktime*kheight + m*ktime*kheight + 0*kheight + 0]*in[0*in_channels*in_stride + m*in_stride + j + 0]
                                + weights[i*in_channels*ktime*kheight + m*ktime*kheight + 0*kheight + 1]*in[0*in_channels*in_stride + m*in_stride + j + 1]
                                + weights[i*in_channels*ktime*kheight + m*ktime*kheight + 0*kheight + 2]*in[0*in_channels*in_stride + m*in_stride + j + 2]
                                + weights[i*in_channels*ktime*kheight + m*ktime*kheight + 1*kheight + 0]*in[1*in_channels*in_stride + m*in_stride + j + 0]
                                + weights[i*in_channels*ktime*kheight + m*ktime*kheight + 1*kheight + 1]*in[1*in_channels*in_stride + m*in_stride + j + 1]
                                + weights[i*in_channels*ktime*kheight + m*ktime*kheight + 1*kheight + 2]*in[1*in_channels*in_stride + m*in_stride + j + 2]
                                + weights[i*in_channels*ktime*kheight + m*ktime*kheight + 2*kheight + 0]*in[2*in_channels*in_stride + m*in_stride + j + 0]
                                + weights[i*in_channels*ktime*kheight + m*ktime*kheight + 2*kheight + 1]*in[2*in_channels*in_stride + m*in_stride + j + 1]
                                + weights[i*in_channels*ktime*kheight + m*ktime*kheight + 2*kheight + 2]*in[2*in_channels*in_stride + m*in_stride + j + 2];
               }
      }
   }
}

#define MAX_CONV2D_INPUTS 8192

void RTCD_SUF(compute_conv2d_)(const Conv2dLayer *conv, float *out, float *mem, const float *in, int height, int hstride, int activation)
{
   int i;
   const float *bias;
   float in_buf[MAX_CONV2D_INPUTS];
   int time_stride;
   celt_assert(in != out);
   time_stride = conv->in_channels*(height+conv->kheight-1);
   celt_assert(conv->ktime*time_stride <= MAX_CONV2D_INPUTS);
   RNN_COPY(in_buf, mem, (conv->ktime-1)*time_stride);
   RNN_COPY(&in_buf[(conv->ktime-1)*time_stride], in, time_stride);
   RNN_COPY(mem, &in_buf[time_stride], (conv->ktime-1)*time_stride);
   bias = conv->bias;
   if (conv->kheight == 3 && conv->ktime == 3)
     conv2d_3x3_float(out, conv->float_weights, conv->in_channels, conv->out_channels, in_buf, height, hstride);
   else
     conv2d_float(out, conv->float_weights, conv->in_channels, conv->out_channels, conv->ktime, conv->kheight, in_buf, height, hstride);
   if (bias != NULL) {
     for (i=0;i<conv->out_channels;i++) {
       int j;
       for (j=0;j<height;j++) out[i*hstride+j] += bias[i];
     }
   }
   for (i=0;i<conv->out_channels;i++) {
     RTCD_SUF(compute_activation_)(&out[i*hstride], &out[i*hstride], height, activation);
   }
}

#ifdef GCC_POP_OPTIONS
#pragma GCC pop_options
#endif

#endif
