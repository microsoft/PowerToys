/* Copyright (c) 2018 Mozilla
                 2008-2011 Octasic Inc.
                 2012-2017 Jean-Marc Valin */
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

#include <stdlib.h>
#include <math.h>
#include "opus_types.h"
#include "arch.h"
#include "nnet.h"
#include "common.h"
#include "vec.h"

#ifdef ENABLE_OSCE
#include "osce.h"
#endif

#ifdef NO_OPTIMIZATIONS
#if defined(_MSC_VER)
#pragma message ("Compiling without any vectorization. This code will be very slow")
#else
#warning Compiling without any vectorization. This code will be very slow
#endif
#endif


#define SOFTMAX_HACK


void compute_generic_dense(const LinearLayer *layer, float *output, const float *input, int activation, int arch)
{
   compute_linear(layer, output, input, arch);
   compute_activation(output, output, layer->nb_outputs, activation, arch);
}

#define MAX_RNN_NEURONS_ALL 1024

void compute_generic_gru(const LinearLayer *input_weights, const LinearLayer *recurrent_weights, float *state, const float *in, int arch)
{
  int i;
  int N;
  float zrh[3*MAX_RNN_NEURONS_ALL];
  float recur[3*MAX_RNN_NEURONS_ALL];
  float *z;
  float *r;
  float *h;
  celt_assert(3*recurrent_weights->nb_inputs == recurrent_weights->nb_outputs);
  celt_assert(input_weights->nb_outputs == recurrent_weights->nb_outputs);
  N = recurrent_weights->nb_inputs;
  z = zrh;
  r = &zrh[N];
  h = &zrh[2*N];
  celt_assert(recurrent_weights->nb_outputs <= 3*MAX_RNN_NEURONS_ALL);
  celt_assert(in != state);
  compute_linear(input_weights, zrh, in, arch);
  compute_linear(recurrent_weights, recur, state, arch);
  for (i=0;i<2*N;i++)
     zrh[i] += recur[i];
  compute_activation(zrh, zrh, 2*N, ACTIVATION_SIGMOID, arch);
  for (i=0;i<N;i++)
     h[i] += recur[2*N+i]*r[i];
  compute_activation(h, h, N, ACTIVATION_TANH, arch);
  for (i=0;i<N;i++)
     h[i] = z[i]*state[i] + (1-z[i])*h[i];
  for (i=0;i<N;i++)
     state[i] = h[i];
}

void compute_glu(const LinearLayer *layer, float *output, const float *input, int arch)
{
   int i;
   float act2[MAX_INPUTS];
   celt_assert(layer->nb_inputs == layer->nb_outputs);
   compute_linear(layer, act2, input, arch);
   compute_activation(act2, act2, layer->nb_outputs, ACTIVATION_SIGMOID, arch);
   if (input == output) {
     /* Give a vectorization hint to the compiler for the in-place case. */
     for (i=0;i<layer->nb_outputs;i++) output[i] = output[i]*act2[i];
   } else {
     for (i=0;i<layer->nb_outputs;i++) output[i] = input[i]*act2[i];
   }
}

#define MAX_CONV_INPUTS_ALL 1024

void compute_generic_conv1d(const LinearLayer *layer, float *output, float *mem, const float *input, int input_size, int activation, int arch)
{
   float tmp[MAX_CONV_INPUTS_ALL];
   celt_assert(input != output);
   celt_assert(layer->nb_inputs <= MAX_CONV_INPUTS_ALL);
   if (layer->nb_inputs!=input_size) RNN_COPY(tmp, mem, layer->nb_inputs-input_size);
   RNN_COPY(&tmp[layer->nb_inputs-input_size], input, input_size);
   compute_linear(layer, output, tmp, arch);
   compute_activation(output, output, layer->nb_outputs, activation, arch);
   if (layer->nb_inputs!=input_size) RNN_COPY(mem, &tmp[input_size], layer->nb_inputs-input_size);
}
