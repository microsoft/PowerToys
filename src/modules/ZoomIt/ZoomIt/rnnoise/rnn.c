/* Copyright (c) 2008-2011 Octasic Inc.
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

#include <math.h>
#include "opus_types.h"
#include "common.h"
#include "arch.h"
#include "rnn.h"
#include "rnnoise_data.h"
#include <stdio.h>


#define INPUT_SIZE 42


void compute_rnn(const RNNoise *model, RNNState *rnn, float *gains, float *vad, const float *input, int arch) {
  float tmp[MAX_NEURONS];
  float cat[CONV2_OUT_SIZE + GRU1_OUT_SIZE + GRU2_OUT_SIZE + GRU3_OUT_SIZE];
  /*for (int i=0;i<INPUT_SIZE;i++) printf("%f ", input[i]);printf("\n");*/
  compute_generic_conv1d(&model->conv1, tmp, rnn->conv1_state, input, CONV1_IN_SIZE, ACTIVATION_TANH, arch);
  compute_generic_conv1d(&model->conv2, cat, rnn->conv2_state, tmp, CONV2_IN_SIZE, ACTIVATION_TANH, arch);
  compute_generic_gru(&model->gru1_input, &model->gru1_recurrent, rnn->gru1_state, cat, arch);
  compute_generic_gru(&model->gru2_input, &model->gru2_recurrent, rnn->gru2_state, rnn->gru1_state, arch);
  compute_generic_gru(&model->gru3_input, &model->gru3_recurrent, rnn->gru3_state, rnn->gru2_state, arch);
  RNN_COPY(&cat[CONV2_OUT_SIZE], rnn->gru1_state, GRU1_OUT_SIZE);
  RNN_COPY(&cat[CONV2_OUT_SIZE+GRU1_OUT_SIZE], rnn->gru2_state, GRU2_OUT_SIZE);
  RNN_COPY(&cat[CONV2_OUT_SIZE+GRU1_OUT_SIZE+GRU2_OUT_SIZE], rnn->gru3_state, GRU3_OUT_SIZE);
  compute_generic_dense(&model->dense_out, gains, cat, ACTIVATION_SIGMOID, arch);
  compute_generic_dense(&model->vad_dense, vad, cat, ACTIVATION_SIGMOID, arch);
  /*for (int i=0;i<22;i++) printf("%f ", gains[i]);printf("\n");*/
  /*printf("%f\n", *vad);*/
}
