
#ifndef RNNOISE_DATA_H
#define RNNOISE_DATA_H

#include "nnet.h"


#define CONV1_OUT_SIZE 128

#define CONV1_IN_SIZE 65

#define CONV1_STATE_SIZE (65 * (2))

#define CONV1_DELAY 1

#define CONV2_OUT_SIZE 384

#define CONV2_IN_SIZE 128

#define CONV2_STATE_SIZE (128 * (2))

#define CONV2_DELAY 1

#define GRU1_OUT_SIZE 384

#define GRU1_STATE_SIZE 384

#define GRU2_OUT_SIZE 384

#define GRU2_STATE_SIZE 384

#define GRU3_OUT_SIZE 384

#define GRU3_STATE_SIZE 384

#define DENSE_OUT_OUT_SIZE 32

#define VAD_DENSE_OUT_SIZE 1

typedef struct {
    LinearLayer conv1;
    LinearLayer conv2;
    LinearLayer gru1_input;
    LinearLayer gru1_recurrent;
    LinearLayer gru2_input;
    LinearLayer gru2_recurrent;
    LinearLayer gru3_input;
    LinearLayer gru3_recurrent;
    LinearLayer dense_out;
    LinearLayer vad_dense;
} RNNoise;

int init_rnnoise(RNNoise *model, const WeightArray *arrays);

#endif /* RNNOISE_DATA_H */
