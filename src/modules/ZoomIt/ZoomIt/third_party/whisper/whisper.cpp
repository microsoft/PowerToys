#include "whisper.h"

#ifdef WHISPER_USE_COREML
#include "coreml/whisper-encoder.h"
#endif

#ifdef GGML_USE_METAL
#include "ggml-metal.h"
#endif

#ifdef GGML_USE_CUDA
#include "ggml-cuda.h"
#endif

#ifdef GGML_USE_SYCL
#include "ggml-sycl.h"
#endif

#ifdef WHISPER_USE_OPENVINO
#include "openvino/whisper-openvino-encoder.h"
#endif

#include "ggml.h"
#include "ggml-alloc.h"
#include "ggml-backend.h"

#include <atomic>
#include <algorithm>
#include <cassert>
#define _USE_MATH_DEFINES
#include <cmath>
#include <cstdio>
#include <cstdarg>
#include <cstring>
#include <fstream>
#include <map>
#include <set>
#include <string>
#include <thread>
#include <vector>
#include <regex>
#include <random>
#include <functional>

#if defined(_MSC_VER)
#pragma warning(disable: 4244 4267) // possible loss of data
#endif

#if defined(GGML_BIG_ENDIAN)
#include <bit>

template<typename T>
static T byteswap(T value) {
    return std::byteswap(value);
}

template<>
float byteswap(float value) {
    return std::bit_cast<float>(byteswap(std::bit_cast<std::uint32_t>(value)));
}

template<typename T>
static void byteswap_tensor_data(ggml_tensor * tensor) {
    T * datum = reinterpret_cast<T *>(tensor->data);
    for (int i = 0; i < ggml_nelements(tensor); i++) {
        datum[i] = byteswap(datum[i]);
    }
}

static void byteswap_tensor(ggml_tensor * tensor) {
    switch (tensor->type) {
        case GGML_TYPE_I16: {
            byteswap_tensor_data<int16_t>(tensor);
            break;
        }
        case GGML_TYPE_F16: {
            byteswap_tensor_data<ggml_fp16_t>(tensor);
            break;
        }
        case GGML_TYPE_I32: {
            byteswap_tensor_data<int32_t>(tensor);
            break;
        }
        case GGML_TYPE_F32: {
            byteswap_tensor_data<float>(tensor);
            break;
        }
        default: { // GML_TYPE_I8
            break;
        }
    }
}

#define BYTESWAP_VALUE(d) d = byteswap(d)
#define BYTESWAP_FILTERS(f)            \
    do {                              \
        for (auto & datum : f.data) { \
            datum = byteswap(datum);  \
        }                             \
    } while (0)
#define BYTESWAP_TENSOR(t)       \
    do {                         \
        byteswap_tensor(t); \
    } while (0)
#else
#define BYTESWAP_VALUE(d) do {} while (0)
#define BYTESWAP_FILTERS(f) do {} while (0)
#define BYTESWAP_TENSOR(t) do {} while (0)
#endif

#ifdef __GNUC__
#ifdef __MINGW32__
#define WHISPER_ATTRIBUTE_FORMAT(...) __attribute__((format(gnu_printf, __VA_ARGS__)))
#else
#define WHISPER_ATTRIBUTE_FORMAT(...) __attribute__((format(printf, __VA_ARGS__)))
#endif
#else
#define WHISPER_ATTRIBUTE_FORMAT(...)
#endif

//
// logging
//

WHISPER_ATTRIBUTE_FORMAT(2, 3)
static void whisper_log_internal        (ggml_log_level level, const char * format, ...);
static void whisper_log_callback_default(ggml_log_level level, const char * text, void * user_data);

#define WHISPER_LOG_ERROR(...) whisper_log_internal(GGML_LOG_LEVEL_ERROR, __VA_ARGS__)
#define WHISPER_LOG_WARN(...)  whisper_log_internal(GGML_LOG_LEVEL_WARN , __VA_ARGS__)
#define WHISPER_LOG_INFO(...)  whisper_log_internal(GGML_LOG_LEVEL_INFO , __VA_ARGS__)

// define this to enable verbose trace logging - useful for debugging purposes
//#define WHISPER_DEBUG

#if defined(WHISPER_DEBUG)
#define WHISPER_LOG_DEBUG(...) whisper_log_internal(GGML_LOG_LEVEL_DEBUG, __VA_ARGS__)
#else
#define WHISPER_LOG_DEBUG(...)
#endif

#define WHISPER_ASSERT(x) \
    do { \
        if (!(x)) { \
            WHISPER_LOG_ERROR("WHISPER_ASSERT: %s:%d: %s\n", __FILE__, __LINE__, #x); \
            abort(); \
        } \
    } while (0)

//#define WHISPER_USE_FLASH_ATTN
//#define WHISPER_USE_FLASH_FF
#define WHISPER_MAX_DECODERS 8
#define WHISPER_MAX_NODES 4096

//
// ggml helpers
//

static bool ggml_graph_compute_helper(
          struct ggml_cgraph * graph,
        std::vector<uint8_t> & buf,
                         int   n_threads,
         ggml_abort_callback   abort_callback,
                        void * abort_callback_data) {
    struct ggml_cplan plan = ggml_graph_plan(graph, n_threads);

    plan.abort_callback      = abort_callback;
    plan.abort_callback_data = abort_callback_data;

    if (plan.work_size > 0) {
        buf.resize(plan.work_size);
        plan.work_data = buf.data();
    }

    return ggml_graph_compute(graph, &plan);
}

static bool ggml_graph_compute_helper(
       struct ggml_backend * backend,
        struct ggml_cgraph * graph,
                       int   n_threads) {
    if (ggml_backend_is_cpu(backend)) {
        ggml_backend_cpu_set_n_threads(backend, n_threads);
    }
#ifdef GGML_USE_METAL
    if (ggml_backend_is_metal(backend)) {
        ggml_backend_metal_set_n_cb(backend, n_threads);
    }
#endif
    return ggml_backend_graph_compute(backend, graph) == GGML_STATUS_SUCCESS;
}

// faster matrix multiplications for tensors that do not have dimension 0 divisible by "pad"
// the idea is to represent the original matrix multiplication:
//
//   Z = X @ Y
//
// with the sum of two matrix multiplications:
//
//   Z = (X_0 @ Y_0) + (X_1 @ Y_1)
//
// here X_0 and Y_0 are views of X and Y that have dimension 0 divisible by "pad"
// and X_1 and Y_1 are the remaining views. X_1 and Y_1 end up being small matrices that can be processed with more
// general-purpose kernels
//
static struct ggml_tensor * ggml_mul_mat_pad(struct ggml_context * ctx, struct ggml_tensor * x, struct ggml_tensor * y, int pad = 32) {
    // use padding only if dimension 0 is at least 8 times larger than the padding
    // else we won't get much benefit from the optimization
    const int n_pad_req = 8;

    if (x->ne[0] % pad == 0 || x->ne[0] / pad < n_pad_req) {
        return ggml_mul_mat(ctx, x, y);
    }

    struct ggml_tensor * x_0 = ggml_view_3d(ctx, x, (x->ne[0]/pad)*pad, x->ne[1], x->ne[2], x->nb[1], x->nb[2], 0);
    struct ggml_tensor * x_1 = ggml_view_3d(ctx, x,  x->ne[0]%pad,      x->ne[1], x->ne[2], x->nb[1], x->nb[2], x_0->ne[0]*x_0->nb[0]);

    struct ggml_tensor * y_0 = ggml_view_3d(ctx, y, (y->ne[0]/pad)*pad, y->ne[1], y->ne[2], y->nb[1], y->nb[2], 0);
    struct ggml_tensor * y_1 = ggml_view_3d(ctx, y,  y->ne[0]%pad,      y->ne[1], y->ne[2], y->nb[1], y->nb[2], y_0->ne[0]*y_0->nb[0]);

    return ggml_add(ctx,
            ggml_mul_mat(ctx, x_0, y_0),
            ggml_mul_mat(ctx, x_1, y_1));
}

// TODO: check if other platforms can benefit from this optimization
// TODO: CUDA is currently broken - seems ggml_mul_mat does not handle views correctly
#if defined(GGML_USE_METAL)
#define ggml_mul_mat ggml_mul_mat_pad
#endif

// available whisper models
enum e_model {
    MODEL_UNKNOWN,
    MODEL_TINY,
    MODEL_BASE,
    MODEL_SMALL,
    MODEL_MEDIUM,
    MODEL_LARGE,
};

static const std::map<e_model, std::string> g_model_name = {
    { MODEL_UNKNOWN,  "unknown"  },
    { MODEL_TINY,     "tiny"     },
    { MODEL_BASE,     "base"     },
    { MODEL_SMALL,    "small"    },
    { MODEL_MEDIUM,   "medium"   },
    { MODEL_LARGE,    "large"    },
};

static const std::map<std::string, std::pair<int, std::string>> g_lang = {
    { "en",  { 0,  "english",         } },
    { "zh",  { 1,  "chinese",         } },
    { "de",  { 2,  "german",          } },
    { "es",  { 3,  "spanish",         } },
    { "ru",  { 4,  "russian",         } },
    { "ko",  { 5,  "korean",          } },
    { "fr",  { 6,  "french",          } },
    { "ja",  { 7,  "japanese",        } },
    { "pt",  { 8,  "portuguese",      } },
    { "tr",  { 9,  "turkish",         } },
    { "pl",  { 10, "polish",          } },
    { "ca",  { 11,  "catalan",        } },
    { "nl",  { 12,  "dutch",          } },
    { "ar",  { 13,  "arabic",         } },
    { "sv",  { 14,  "swedish",        } },
    { "it",  { 15,  "italian",        } },
    { "id",  { 16,  "indonesian",     } },
    { "hi",  { 17,  "hindi",          } },
    { "fi",  { 18,  "finnish",        } },
    { "vi",  { 19,  "vietnamese",     } },
    { "he",  { 20,  "hebrew",         } },
    { "uk",  { 21,  "ukrainian",      } },
    { "el",  { 22,  "greek",          } },
    { "ms",  { 23,  "malay",          } },
    { "cs",  { 24,  "czech",          } },
    { "ro",  { 25,  "romanian",       } },
    { "da",  { 26,  "danish",         } },
    { "hu",  { 27,  "hungarian",      } },
    { "ta",  { 28,  "tamil",          } },
    { "no",  { 29,  "norwegian",      } },
    { "th",  { 30,  "thai",           } },
    { "ur",  { 31,  "urdu",           } },
    { "hr",  { 32,  "croatian",       } },
    { "bg",  { 33,  "bulgarian",      } },
    { "lt",  { 34,  "lithuanian",     } },
    { "la",  { 35,  "latin",          } },
    { "mi",  { 36,  "maori",          } },
    { "ml",  { 37,  "malayalam",      } },
    { "cy",  { 38,  "welsh",          } },
    { "sk",  { 39,  "slovak",         } },
    { "te",  { 40,  "telugu",         } },
    { "fa",  { 41,  "persian",        } },
    { "lv",  { 42,  "latvian",        } },
    { "bn",  { 43,  "bengali",        } },
    { "sr",  { 44,  "serbian",        } },
    { "az",  { 45,  "azerbaijani",    } },
    { "sl",  { 46,  "slovenian",      } },
    { "kn",  { 47,  "kannada",        } },
    { "et",  { 48,  "estonian",       } },
    { "mk",  { 49,  "macedonian",     } },
    { "br",  { 50,  "breton",         } },
    { "eu",  { 51,  "basque",         } },
    { "is",  { 52,  "icelandic",      } },
    { "hy",  { 53,  "armenian",       } },
    { "ne",  { 54,  "nepali",         } },
    { "mn",  { 55,  "mongolian",      } },
    { "bs",  { 56,  "bosnian",        } },
    { "kk",  { 57,  "kazakh",         } },
    { "sq",  { 58,  "albanian",       } },
    { "sw",  { 59,  "swahili",        } },
    { "gl",  { 60,  "galician",       } },
    { "mr",  { 61,  "marathi",        } },
    { "pa",  { 62,  "punjabi",        } },
    { "si",  { 63,  "sinhala",        } },
    { "km",  { 64,  "khmer",          } },
    { "sn",  { 65,  "shona",          } },
    { "yo",  { 66,  "yoruba",         } },
    { "so",  { 67,  "somali",         } },
    { "af",  { 68,  "afrikaans",      } },
    { "oc",  { 69,  "occitan",        } },
    { "ka",  { 70,  "georgian",       } },
    { "be",  { 71,  "belarusian",     } },
    { "tg",  { 72,  "tajik",          } },
    { "sd",  { 73,  "sindhi",         } },
    { "gu",  { 74,  "gujarati",       } },
    { "am",  { 75,  "amharic",        } },
    { "yi",  { 76,  "yiddish",        } },
    { "lo",  { 77,  "lao",            } },
    { "uz",  { 78,  "uzbek",          } },
    { "fo",  { 79,  "faroese",        } },
    { "ht",  { 80,  "haitian creole", } },
    { "ps",  { 81,  "pashto",         } },
    { "tk",  { 82,  "turkmen",        } },
    { "nn",  { 83,  "nynorsk",        } },
    { "mt",  { 84,  "maltese",        } },
    { "sa",  { 85,  "sanskrit",       } },
    { "lb",  { 86,  "luxembourgish",  } },
    { "my",  { 87,  "myanmar",        } },
    { "bo",  { 88,  "tibetan",        } },
    { "tl",  { 89,  "tagalog",        } },
    { "mg",  { 90,  "malagasy",       } },
    { "as",  { 91,  "assamese",       } },
    { "tt",  { 92,  "tatar",          } },
    { "haw", { 93,  "hawaiian",       } },
    { "ln",  { 94,  "lingala",        } },
    { "ha",  { 95,  "hausa",          } },
    { "ba",  { 96,  "bashkir",        } },
    { "jw",  { 97,  "javanese",       } },
    { "su",  { 98,  "sundanese",      } },
    { "yue", { 99,  "cantonese",      } },
};

// [EXPERIMENTAL] Token-level timestamps with DTW
static const whisper_ahead g_aheads_tiny_en[]   = { {1, 0}, {2, 0}, {2, 5}, {3, 0}, {3, 1}, {3, 2}, {3, 3}, {3, 4} };
static const whisper_ahead g_aheads_tiny[]      = { {2, 2}, {3, 0}, {3, 2}, {3, 3}, {3, 4}, {3, 5} };
static const whisper_ahead g_aheads_base_en[]   = { {3, 3}, {4, 7}, {5, 1}, {5, 5}, {5, 7} };
static const whisper_ahead g_aheads_base[]      = { {3, 1}, {4, 2}, {4, 3}, {4, 7}, {5, 1}, {5, 2}, {5, 4}, {5, 6} };
static const whisper_ahead g_aheads_small_en[]  = { {6, 6}, {7, 0}, {7, 3}, {7, 8}, {8, 2}, {8, 5}, {8, 7}, {9, 0}, {9, 4}, {9, 8}, {9, 10}, {10, 0}, {10, 1}, {10, 2}, {10, 3}, {10, 6}, {10, 11}, {11, 2}, {11, 4} };
static const whisper_ahead g_aheads_small[]     = { {5, 3}, {5, 9}, {8, 0}, {8, 4}, {8, 7}, {8, 8}, {9, 0}, {9, 7}, {9, 9}, {10, 5} };
static const whisper_ahead g_aheads_medium_en[] = { {11, 4}, {14, 1}, {14, 12}, {14, 14}, {15, 4}, {16, 0}, {16, 4}, {16, 9}, {17, 12}, {17, 14}, {18, 7}, {18, 10}, {18, 15}, {20, 0}, {20, 3}, {20, 9}, {20, 14}, {21, 12} };
static const whisper_ahead g_aheads_medium[]    = { {13, 15}, {15, 4}, {15, 15}, {16, 1}, {20, 0}, {23, 4} };
static const whisper_ahead g_aheads_large_v1[]  = { {9, 19}, {11, 2}, {11, 4}, {11, 17}, {22, 7}, {22, 11}, {22, 17}, {23, 2}, {23, 15} };
static const whisper_ahead g_aheads_large_v2[]  = { {10, 12}, {13, 17}, {16, 11}, {16, 12}, {16, 13}, {17, 15}, {17, 16}, {18, 4}, {18, 11}, {18, 19}, {19, 11}, {21, 2}, {21, 3}, {22, 3}, {22, 9}, {22, 12}, {23, 5}, {23, 7}, {23, 13}, {25, 5}, {26, 1}, {26, 12}, {27, 15} };
static const whisper_ahead g_aheads_large_v3[]  = { {7, 0}, {10, 17}, {12, 18}, {13, 12}, {16, 1}, {17, 14}, {19, 11}, {21, 4}, {24, 1}, {25, 6} };

static const std::map<whisper_alignment_heads_preset, whisper_aheads> g_aheads {
    { WHISPER_AHEADS_TINY_EN,   {  8, g_aheads_tiny_en   } },
    { WHISPER_AHEADS_TINY,      {  6, g_aheads_tiny      } },
    { WHISPER_AHEADS_BASE_EN,   {  5, g_aheads_base_en   } },
    { WHISPER_AHEADS_BASE,      {  8, g_aheads_base      } },
    { WHISPER_AHEADS_SMALL_EN,  { 19, g_aheads_small_en  } },
    { WHISPER_AHEADS_SMALL,     { 10, g_aheads_small     } },
    { WHISPER_AHEADS_MEDIUM_EN, { 18, g_aheads_medium_en } },
    { WHISPER_AHEADS_MEDIUM,    {  6, g_aheads_medium    } },
    { WHISPER_AHEADS_LARGE_V1,  {  9, g_aheads_large_v1  } },
    { WHISPER_AHEADS_LARGE_V2,  { 23, g_aheads_large_v2  } },
    { WHISPER_AHEADS_LARGE_V3,  { 10, g_aheads_large_v3  } },
};

static std::vector<uint32_t> get_alignment_heads_by_layer(const whisper_context_params & cparams, int il, int32_t n_text_layer, int32_t n_head);

struct whisper_mel {
    int n_len;
    int n_len_org;
    int n_mel;

    std::vector<float> data;
};

struct whisper_filters {
    int32_t n_mel;
    int32_t n_fft;

    std::vector<float> data;
};

struct whisper_vocab {
    using id    = int32_t;
    using token = std::string;

    int n_vocab = 51864;

    std::map<token, id> token_to_id;
    std::map<id, token> id_to_token;

    // reference: https://github.com/openai/whisper/blob/248b6cb124225dd263bb9bd32d060b6517e067f8/whisper/tokenizer.py#L334-L349
    id token_eot        = 50256;
    id token_sot        = 50257;
    // task tokens (used only for multilingual models)
    id token_translate  = 50357;
    id token_transcribe = 50358;
    // other special tokens
    id token_solm       = 50359; // [TDRZ] used by tinydiarize models to indicate speaker turn
    id token_prev       = 50360;
    id token_nosp       = 50361;
    id token_not        = 50362; // no timestamps
    id token_beg        = 50363; // begin timestamps

    bool is_multilingual() const {
        return n_vocab >= 51865;
    }

    int num_languages() const {
        return n_vocab - 51765 - (is_multilingual() ? 1 : 0);
    }
};

struct whisper_segment {
    int64_t t0;
    int64_t t1;

    std::string text;

    std::vector<whisper_token_data> tokens;

    bool speaker_turn_next;
};

struct whisper_batch {
    int32_t n_tokens;

    whisper_token  *  token;
    whisper_pos    *  pos;
    int32_t        *  n_seq_id; // always 1, here for consistency with llama.cpp
    whisper_seq_id ** seq_id;   // null terminated
    int8_t         *  logits;
};

static struct whisper_batch whisper_batch_init(int32_t n_tokens, int32_t n_seq_max) {
    whisper_batch batch = { 0, nullptr, nullptr, nullptr, nullptr, nullptr, };

    batch.token    = (whisper_token *  ) malloc(sizeof(whisper_token)    * (n_tokens));
    batch.pos      = (whisper_pos *)     malloc(sizeof(whisper_pos)      * (n_tokens));
    batch.n_seq_id = (int32_t *)         malloc(sizeof(int32_t)          * (n_tokens));
    batch.seq_id   = (whisper_seq_id **) malloc(sizeof(whisper_seq_id *) * (n_tokens + 1));
    for (int i = 0; i < n_tokens; ++i) {
        batch.seq_id[i] = (whisper_seq_id *) malloc(sizeof(whisper_seq_id)   * n_seq_max);
    }
    batch.seq_id[n_tokens] = nullptr;
    batch.logits   = (int8_t *)          malloc(sizeof(int8_t)           * n_tokens);

    return batch;
}

static void whisper_batch_free(struct whisper_batch batch) {
    if (batch.token)    free(batch.token);
    if (batch.pos)      free(batch.pos);
    if (batch.n_seq_id) free(batch.n_seq_id);
    if (batch.seq_id) {
        for (int i = 0; batch.seq_id[i]; ++i) {
            free(batch.seq_id[i]);
        }
        free(batch.seq_id);
    }
    if (batch.logits)   free(batch.logits);
}

static void whisper_batch_prep_legacy(whisper_batch & batch, const whisper_token * tokens, int n_tokens, int n_past, int seq_id) {
    batch.n_tokens = n_tokens;
    for (int i = 0; i < n_tokens; ++i) {
        if (tokens) {
            batch.token[i] = tokens[i];
        }
        batch.pos     [i]    = n_past + i;
        batch.n_seq_id[i]    = 1;
        batch.seq_id  [i][0] = seq_id;
        batch.logits  [i]    = 0;
    }
    batch.logits[n_tokens - 1] = 1;
}

// replace std::pair by using customized pair struct (reason: std::pair is very slow)
template<typename A, typename B>
struct whisper_pair {
    A first;
    B second;

    // Define a constructor that takes two arguments.
    whisper_pair(const A& a, const B& b) : first(a), second(b) {}
    // Define a constructor that takes no argument.
    whisper_pair() : first(A()), second(B()) {}
};

// ggml_allocr wrapper for whisper usage
struct whisper_allocr {
    ggml_gallocr_t alloc = nullptr;

    std::vector<uint8_t> meta;
};

static size_t whisper_allocr_size(struct whisper_allocr & allocr) {
    return allocr.meta.size() + ggml_gallocr_get_buffer_size(allocr.alloc, 0);
}

// measure the memory usage of a graph and prepare the allocr's internal data buffer
static bool whisper_allocr_graph_init(struct whisper_allocr & allocr, ggml_backend_t backend, std::function<struct ggml_cgraph *()> && get_graph) {
    auto & alloc = allocr.alloc;
    auto & meta  = allocr.meta;

    alloc = ggml_gallocr_new(ggml_backend_get_default_buffer_type(backend));

    meta.resize(ggml_tensor_overhead()*WHISPER_MAX_NODES + ggml_graph_overhead());

    // since there are dependencies between the different graphs,
    // we need to allocate them instead of only reserving to get the correct compute buffer size
    if (!ggml_gallocr_alloc_graph(alloc, get_graph())) {
        // failed to allocate the compute buffer
        WHISPER_LOG_ERROR("%s: failed to allocate the compute buffer\n", __func__);
        return false;
    }
    return true;
}

// medium
// hparams: {
// 'n_mels': 80,
// 'n_vocab': 51864,
// 'n_audio_ctx': 1500,
// 'n_audio_state': 1024,
// 'n_audio_head': 16,
// 'n_audio_layer': 24,
// 'n_text_ctx': 448,
// 'n_text_state': 1024,
// 'n_text_head': 16,
// 'n_text_layer': 24
// }
//
// default hparams (Whisper tiny)
struct whisper_hparams {
    int32_t n_vocab       = 51864;
    int32_t n_audio_ctx   = 1500;
    int32_t n_audio_state = 384;
    int32_t n_audio_head  = 6;
    int32_t n_audio_layer = 4;
    int32_t n_text_ctx    = 448;
    int32_t n_text_state  = 384;
    int32_t n_text_head   = 6;
    int32_t n_text_layer  = 4;
    int32_t n_mels        = 80;
    int32_t ftype         = 1;
    float   eps           = 1e-5f;
};

// audio encoding layer
struct whisper_layer_encoder {
    // encoder.blocks.*.attn_ln
    struct ggml_tensor * attn_ln_0_w;
    struct ggml_tensor * attn_ln_0_b;

    // encoder.blocks.*.attn.out
    struct ggml_tensor * attn_ln_1_w;
    struct ggml_tensor * attn_ln_1_b;

    // encoder.blocks.*.attn.query
    struct ggml_tensor * attn_q_w;
    struct ggml_tensor * attn_q_b;

    // encoder.blocks.*.attn.key
    struct ggml_tensor * attn_k_w;

    // encoder.blocks.*.attn.value
    struct ggml_tensor * attn_v_w;
    struct ggml_tensor * attn_v_b;

    // encoder.blocks.*.mlp_ln
    struct ggml_tensor * mlp_ln_w;
    struct ggml_tensor * mlp_ln_b;

    // encoder.blocks.*.mlp.0
    struct ggml_tensor * mlp_0_w;
    struct ggml_tensor * mlp_0_b;

    // encoder.blocks.*.mlp.2
    struct ggml_tensor * mlp_1_w;
    struct ggml_tensor * mlp_1_b;
};

// token decoding layer
struct whisper_layer_decoder {
    // decoder.blocks.*.attn_ln
    struct ggml_tensor * attn_ln_0_w;
    struct ggml_tensor * attn_ln_0_b;

    // decoder.blocks.*.attn.out
    struct ggml_tensor * attn_ln_1_w;
    struct ggml_tensor * attn_ln_1_b;

    // decoder.blocks.*.attn.query
    struct ggml_tensor * attn_q_w;
    struct ggml_tensor * attn_q_b;

    // decoder.blocks.*.attn.key
    struct ggml_tensor * attn_k_w;

    // decoder.blocks.*.attn.value
    struct ggml_tensor * attn_v_w;
    struct ggml_tensor * attn_v_b;

    // decoder.blocks.*.cross_attn_ln
    struct ggml_tensor * cross_attn_ln_0_w;
    struct ggml_tensor * cross_attn_ln_0_b;

    // decoder.blocks.*.cross_attn.out
    struct ggml_tensor * cross_attn_ln_1_w;
    struct ggml_tensor * cross_attn_ln_1_b;

    // decoder.blocks.*.cross_attn.query
    struct ggml_tensor * cross_attn_q_w;
    struct ggml_tensor * cross_attn_q_b;

    // decoder.blocks.*.cross_attn.key
    struct ggml_tensor * cross_attn_k_w;

    // decoder.blocks.*.cross_attn.value
    struct ggml_tensor * cross_attn_v_w;
    struct ggml_tensor * cross_attn_v_b;

    // decoder.blocks.*.mlp_ln
    struct ggml_tensor * mlp_ln_w;
    struct ggml_tensor * mlp_ln_b;

    // decoder.blocks.*.mlp.0
    struct ggml_tensor * mlp_0_w;
    struct ggml_tensor * mlp_0_b;

    // decoder.blocks.*.mlp.2
    struct ggml_tensor * mlp_1_w;
    struct ggml_tensor * mlp_1_b;
};

struct whisper_kv_cell {
    whisper_pos pos = -1;

    std::set<whisper_seq_id> seq_id;

    bool has_seq_id(const whisper_seq_id & id) const {
        return seq_id.find(id) != seq_id.end();
    }
};

struct whisper_kv_cache {
    uint32_t head = 0;
    uint32_t size = 0;

    // computed before each graph build
    uint32_t n = 0;

    std::vector<whisper_kv_cell> cells;

    struct ggml_tensor * k;
    struct ggml_tensor * v;

    struct ggml_context * ctx = nullptr;

    ggml_backend_buffer_t buffer = nullptr;
};

struct whisper_model {
    e_model type = MODEL_UNKNOWN;

    whisper_hparams hparams;
    whisper_filters filters;

    // encoder.positional_embedding
    struct ggml_tensor * e_pe;

    // encoder.conv1
    struct ggml_tensor * e_conv_1_w;
    struct ggml_tensor * e_conv_1_b;

    // encoder.conv2
    struct ggml_tensor * e_conv_2_w;
    struct ggml_tensor * e_conv_2_b;

    // encoder.ln_post
    struct ggml_tensor * e_ln_w;
    struct ggml_tensor * e_ln_b;

    // decoder.positional_embedding
    struct ggml_tensor * d_pe;

    // decoder.token_embedding
    struct ggml_tensor * d_te;

    // decoder.ln
    struct ggml_tensor * d_ln_w;
    struct ggml_tensor * d_ln_b;

    std::vector<whisper_layer_encoder> layers_encoder;
    std::vector<whisper_layer_decoder> layers_decoder;

    // ggml context that contains all the meta information about the model tensors
    struct ggml_context * ctx = nullptr;

    // the model backend data is read-only and can be shared between processors
    ggml_backend_buffer_t buffer = nullptr;

    // tensors
    int n_loaded;
    std::map<std::string, struct ggml_tensor *> tensors;
};

struct whisper_partial_utf8 {
    uint32_t value;    // bit value so far (unshifted)
    int      n_remain; // num bytes remaining; -1 indicates invalid sequence
};

struct whisper_grammar {
    /*const*/ std::vector<std::vector<whisper_grammar_element>> rules;
    std::vector<std::vector<const whisper_grammar_element *>>   stacks;

    // buffer for partially generated UTF-8 sequence from accepted tokens
    whisper_partial_utf8 partial_utf8;
};

struct whisper_grammar_candidate {
    whisper_token          id;
    const uint32_t       * code_points;
    whisper_partial_utf8   partial_utf8;
};

struct whisper_sequence {
    std::vector<whisper_token_data> tokens;

    // the accumulated transcription in the current iteration (used to truncate the tokens array)
    int result_len;

    double sum_logprobs_all; // the sum of the log probabilities of the tokens
    double sum_logprobs;     // the sum of the log probabilities of the tokens (first result_len tokens)
    double avg_logprobs;     // the average log probability of the tokens
    double entropy;          // the entropy of the tokens
    double score;            // likelihood rank score
};

// TAGS: WHISPER_DECODER_INIT
struct whisper_decoder {
    // the currently generated sequence of tokens
    whisper_sequence sequence;

    // grammar parse state of generated sequence of tokens
    whisper_grammar  grammar;

    int i_batch;    // the index of the token in the current batch
    int seek_delta; // the window shift found so far based on the decoded timestamp tokens

    bool failed;    // has the current segment failed to decode?
    bool completed; // has the decoder completed the current segment?
    bool has_ts;    // have we already sampled a non-beg timestamp token for the current segment?

    // new token probs, logits and logprobs after the last whisper_decode (1-dimensional array: [n_vocab])
    std::vector<float> probs;
    std::vector<float> logits;
    std::vector<float> logprobs;

    // work container used to avoid memory allocations
    std::vector<whisper_pair<double, whisper_vocab::id>> logits_id;

    mutable std::mt19937 rng; // used for sampling at t > 0.0
};

// [EXPERIMENTAL] Token-level timestamps with DTW
struct whisper_aheads_masks {
    std::vector<struct ggml_tensor *> m;    // One mask per text layer.
    struct ggml_context * ctx = nullptr;
    ggml_backend_buffer_t buffer = nullptr;
};

struct whisper_state {
    int64_t t_sample_us = 0;
    int64_t t_encode_us = 0;
    int64_t t_decode_us = 0;
    int64_t t_batchd_us = 0;
    int64_t t_prompt_us = 0;
    int64_t t_mel_us = 0;

    int32_t n_sample = 0; // number of tokens sampled
    int32_t n_encode = 0; // number of encoder calls
    int32_t n_decode = 0; // number of decoder calls with n_tokens == 1  (text-generation)
    int32_t n_batchd = 0; // number of decoder calls with n_tokens <  16 (batch decoding)
    int32_t n_prompt = 0; // number of decoder calls with n_tokens >  1  (prompt encoding)
    int32_t n_fail_p = 0; // number of logprob threshold failures
    int32_t n_fail_h = 0; // number of entropy threshold failures

    // unified self-attention KV cache for all decoders
    whisper_kv_cache kv_self;

    // cross-attention KV cache for the decoders
    // shared between all decoders
    whisper_kv_cache kv_cross;

    whisper_mel mel;

    whisper_batch batch;

    whisper_decoder decoders[WHISPER_MAX_DECODERS];

    ggml_backend_t backend = nullptr;

    // ggml-alloc:
    // - stores meta info about the intermediate tensors into the `meta` buffers
    // - stores the actual tensor data into the `data` buffers
    whisper_allocr alloc_conv;
    whisper_allocr alloc_encode;
    whisper_allocr alloc_cross;
    whisper_allocr alloc_decode;

    // result of the encoder
    struct ggml_tensor * embd_conv = nullptr;
    struct ggml_tensor * embd_enc  = nullptr;

    // helpers for GPU offloading
    std::vector<float> inp_mel;
    std::vector<float> inp_mask;

    // decode output (2-dimensional array: [n_tokens][n_vocab])
    std::vector<float> logits;

    std::vector<whisper_segment> result_all;
    std::vector<whisper_token>   prompt_past;

    int lang_id = 0; // english by default

    std::string path_model; // populated by whisper_init_from_file_with_params()

#ifdef WHISPER_USE_COREML
    whisper_coreml_context * ctx_coreml = nullptr;
#endif

#ifdef WHISPER_USE_OPENVINO
    whisper_openvino_context * ctx_openvino = nullptr;
#endif

    // [EXPERIMENTAL] token-level timestamps data
    int64_t t_beg  = 0;
    int64_t t_last = 0;

    whisper_token tid_last;

    std::vector<float> energy; // PCM signal energy

    // [EXPERIMENTAL] Token-level timestamps with DTW
    whisper_aheads_masks aheads_masks;
    ggml_tensor * aheads_cross_QKs = nullptr;
    std::vector<float> aheads_cross_QKs_data;

    // [EXPERIMENTAL] speed-up techniques
    int32_t exp_n_audio_ctx = 0; // 0 - use default
};

struct whisper_context {
    int64_t t_load_us  = 0;
    int64_t t_start_us = 0;

    ggml_type wtype = ggml_type::GGML_TYPE_F16; // weight type (FP32 / FP16 / QX)
    ggml_type itype = ggml_type::GGML_TYPE_F16; // intermediate type (FP32 or FP16)

    whisper_context_params params;

    whisper_model model;
    whisper_vocab vocab;

    whisper_state * state = nullptr;

    ggml_backend_t backend = nullptr;

    std::string path_model; // populated by whisper_init_from_file_with_params()
};

struct whisper_global {
    // We save the log callback globally
    ggml_log_callback log_callback = whisper_log_callback_default;
    void * log_callback_user_data = nullptr;
};

static whisper_global g_state;

template<typename T>
static void read_safe(whisper_model_loader * loader, T & dest) {
    loader->read(loader->context, &dest, sizeof(T));
    BYTESWAP_VALUE(dest);
}

static bool kv_cache_init(
        const struct whisper_hparams & hparams,
             struct whisper_kv_cache & cache,
                      ggml_backend_t   backend,
                           ggml_type   wtype,
                                 int   n_ctx) {
    const int64_t n_text_state = hparams.n_text_state;
    const int64_t n_text_layer = hparams.n_text_layer;

    const int64_t n_mem      = n_text_layer*n_ctx;
    const int64_t n_elements = n_text_state*n_mem;

    struct ggml_init_params params = {
        /*.mem_size   =*/ 2*ggml_tensor_overhead(),
        /*.mem_buffer =*/ nullptr,
        /*.no_alloc   =*/ true,
    };

    cache.head = 0;
    cache.size = n_ctx;

    cache.cells.clear();
    cache.cells.resize(n_ctx);

    cache.ctx = ggml_init(params);

    if (!cache.ctx) {
        WHISPER_LOG_ERROR("%s: failed to allocate memory for the kv cache context\n", __func__);
        return false;
    }

    cache.k = ggml_new_tensor_1d(cache.ctx, wtype, n_elements);
    cache.v = ggml_new_tensor_1d(cache.ctx, wtype, n_elements);

    cache.buffer = ggml_backend_alloc_ctx_tensors(cache.ctx, backend);
    if (!cache.buffer) {
        WHISPER_LOG_ERROR("%s: failed to allocate memory for the kv cache\n", __func__);
        return false;
    }

    return true;
}

static void kv_cache_free(struct whisper_kv_cache & cache) {
    ggml_free(cache.ctx);
    ggml_backend_buffer_free(cache.buffer);
    cache.ctx = nullptr;
}

static bool whisper_kv_cache_find_slot(
           struct whisper_kv_cache & cache,
        const struct whisper_batch & batch) {
    const uint32_t n_ctx    = cache.size;
    const uint32_t n_tokens = batch.n_tokens;

    if (n_tokens > n_ctx) {
        WHISPER_LOG_ERROR("%s: n_tokens=%d > n_ctx=%d\n", __func__, n_tokens, n_ctx);
        return false;
    }

    uint32_t n_tested = 0;

    while (true) {
        if (cache.head + n_tokens > n_ctx) {
            n_tested += n_ctx - cache.head;
            cache.head = 0;
            continue;
        }

        bool found = true;
        for (uint32_t i = 0; i < n_tokens; i++) {
            if (cache.cells[cache.head + i].pos >= 0) {
                found = false;
                cache.head += i + 1;
                n_tested   += i + 1;
                break;
            }
        }

        if (found) {
            break;
        }

        if (n_tested >= n_ctx) {
            //WHISPER_LOG_ERROR("%s: failed to find a slot for %d tokens\n", __func__, n_tokens);
            return false;
        }
    }

    for (uint32_t i = 0; i < n_tokens; i++) {
        cache.cells[cache.head + i].pos = batch.pos[i];

        for (int32_t j = 0; j < batch.n_seq_id[i]; j++) {
            cache.cells[cache.head + i].seq_id.insert(batch.seq_id[i][j]);
        }
    }

    return true;
}

// find how many cells are currently in use
static int32_t whisper_kv_cache_cell_max(const struct whisper_kv_cache & cache) {
    for (uint32_t i = cache.size - 1; i > 0; --i) {
        if (cache.cells[i].pos >= 0 && !cache.cells[i].seq_id.empty()) {
            return i + 1;
        }
    }

    return 1;
}

static void whisper_kv_cache_clear(struct whisper_kv_cache & cache) {
    for (int32_t i = 0; i < (int32_t) cache.size; ++i) {
        cache.cells[i].pos = -1;
        cache.cells[i].seq_id.clear();
    }
    cache.head = 0;
}

static void whisper_kv_cache_seq_rm(
        struct whisper_kv_cache & cache,
                 whisper_seq_id   seq_id,
                    whisper_pos   p0,
                    whisper_pos   p1) {
    uint32_t new_head = cache.size;

    if (p0 < 0) p0 = 0;
    if (p1 < 0) p1 = std::numeric_limits<whisper_pos>::max();

    for (uint32_t i = 0; i < cache.size; ++i) {
        if (cache.cells[i].pos >= p0 && cache.cells[i].pos < p1) {
            if (seq_id < 0) {
                cache.cells[i].seq_id.clear();
            } else if (cache.cells[i].has_seq_id(seq_id)) {
                cache.cells[i].seq_id.erase(seq_id);
            } else {
                continue;
            }
            if (cache.cells[i].seq_id.empty()) {
                cache.cells[i].pos = -1;
                if (new_head == cache.size) new_head = i;
            }
        }
    }

    // If we freed up a slot, set head to it so searching can start there.
    if (new_head != cache.size) cache.head = new_head;
}

static void whisper_kv_cache_seq_cp(
        struct whisper_kv_cache & cache,
                 whisper_seq_id   seq_id_src,
                 whisper_seq_id   seq_id_dst,
                    whisper_pos   p0,
                    whisper_pos   p1) {
    if (p0 < 0) p0 = 0;
    if (p1 < 0) p1 = std::numeric_limits<whisper_pos>::max();

    cache.head = 0;

    for (uint32_t i = 0; i < cache.size; ++i) {
        if (cache.cells[i].has_seq_id(seq_id_src) && cache.cells[i].pos >= p0 && cache.cells[i].pos < p1) {
            cache.cells[i].seq_id.insert(seq_id_dst);
        }
    }
}

// [EXPERIMENTAL] Token-level timestamps with DTW
static bool aheads_masks_init(
        const whisper_context_params & cparams,
               const whisper_hparams & hparams,
         struct whisper_aheads_masks & aheads_masks,
                      ggml_backend_t   backend) {

    const int32_t n_text_layer = hparams.n_text_layer;
    const int32_t n_head = hparams.n_text_head;

    // Sanity checks
    if (cparams.dtw_aheads_preset == WHISPER_AHEADS_NONE) {
        WHISPER_LOG_ERROR("%s: dtw_aheads_preset should be != DTW_AHEADS_NONE\n", __func__);
        return false;
    } else if (cparams.dtw_aheads_preset == WHISPER_AHEADS_N_TOP_MOST) {
        if (cparams.dtw_n_top > n_text_layer || cparams.dtw_n_top <= 0) {
            WHISPER_LOG_ERROR("%s: dtw_n_top must be between %d and %d for this model.", __func__, 1, n_text_layer);
            return false;
        }
    } else {
        const auto aheads = cparams.dtw_aheads_preset == WHISPER_AHEADS_CUSTOM ? cparams.dtw_aheads : g_aheads.at(cparams.dtw_aheads_preset);
        if (cparams.dtw_aheads_preset == WHISPER_AHEADS_CUSTOM) {
            if (aheads.n_heads == 0) {
                WHISPER_LOG_ERROR("%s: dtw_aheads.n_heads should be > 0", __func__);
                return false;
            }
            if (aheads.heads == NULL) {
                WHISPER_LOG_ERROR("%s: dtw_aheads.heads unset", __func__);
                return false;
            }
        }
        for (size_t i = 0; i < aheads.n_heads; ++i) {
            if (aheads.heads[i].n_text_layer >= n_text_layer) {
                WHISPER_LOG_ERROR("%s: tried to set alignment head on text layer %d, but model only has %d text layers", __func__, aheads.heads[i].n_text_layer + 1, n_text_layer);
                return false;
            }
            if (aheads.heads[i].n_text_layer < 0) {
                WHISPER_LOG_ERROR("%s: tried to set alignment head on text layer < 0", __func__);
                return false;
            }
            if (aheads.heads[i].n_head >= n_head) {
                WHISPER_LOG_ERROR("%s: tried to set alignment head on head %d, but model only has %d heads", __func__, aheads.heads[i].n_head + 1, n_head);
                return false;
            }
            if (aheads.heads[i].n_head < 0) {
                WHISPER_LOG_ERROR("%s: tried to set alignment head on head < 0", __func__);
                return false;
            }
        }
    }

    struct ggml_init_params params = {
        /*.mem_size   =*/ (size_t) static_cast<size_t>(n_text_layer)*ggml_tensor_overhead(),
        /*.mem_buffer =*/ nullptr,
        /*.no_alloc   =*/ true,
    };

    aheads_masks.ctx = ggml_init(params);

    if (!aheads_masks.ctx) {
        WHISPER_LOG_ERROR("%s: failed to allocate memory for the aheads_masks context\n", __func__);
        return false;
    }

    for (int64_t il = 0; il < n_text_layer; ++il) {
        auto aheads = get_alignment_heads_by_layer(cparams, il, n_text_layer, n_head);
        if (!aheads.empty()) {
            aheads_masks.m.push_back(ggml_new_tensor_2d(aheads_masks.ctx, GGML_TYPE_F32, n_head, aheads.size()));
        } else {
            aheads_masks.m.push_back(nullptr);
        }
    }

    aheads_masks.buffer = ggml_backend_alloc_ctx_tensors(aheads_masks.ctx, backend);
    if (!aheads_masks.buffer) {
        WHISPER_LOG_ERROR("%s: failed to allocate memory for aheads_masks\n", __func__);
        return false;
    }

    // Set data on mask tensors
    // Since this must be backend agnostic, we write our desired values on mask_data,
    // and send it to backend with ggml_backend_tensor_set.
    // Each mask in N_HEADS*N_ALIGNMENT_HEADS, one per text layer containing alignment
    // heads. Each row of the mask "marks" one alignment head. E.g. if some text layer
    // has a total of 10 heads and of those, heads 0,5,6 are alignment heads, the mask
    // should read:
    // 1 0 0 0 0 0 0 0 0 0
    // 0 0 0 0 0 1 0 0 0 0
    // 0 0 0 0 0 0 1 0 0 0
    std::vector<float> mask_data;
    for (int64_t il = 0; il < n_text_layer; ++il) {
        if (aheads_masks.m[il] != nullptr) {
            auto aheads = get_alignment_heads_by_layer(cparams, il, n_text_layer, n_head);

            size_t data_size = aheads_masks.m[il]->ne[0] * aheads_masks.m[il]->ne[1];
            size_t data_size_bytes = data_size * sizeof(float);
            mask_data.resize(data_size);

            std::fill(mask_data.begin(), mask_data.end(), 0);
            for (size_t ih = 0; ih < aheads.size(); ++ih) {
                size_t pos = (aheads[ih] + (ih * aheads_masks.m[il]->ne[0]));
                mask_data[pos] = 1.0f;
            }

            ggml_backend_tensor_set(aheads_masks.m[il], mask_data.data(), 0, data_size_bytes);
        }
    }

    if (aheads_masks.m.empty()) {
        WHISPER_LOG_ERROR("%s: \n", __func__);
        return false;
    }

    return true;
}

static void aheads_masks_free(struct whisper_aheads_masks & aheads_masks) {
    ggml_free(aheads_masks.ctx);
    ggml_backend_buffer_free(aheads_masks.buffer);
    aheads_masks.ctx = nullptr;
}

static size_t aheads_masks_nbytes(struct whisper_aheads_masks & aheads_masks) {
    size_t size = 0;
    for (size_t i = 0; i < aheads_masks.m.size(); ++i) {
        if (aheads_masks.m[i] != nullptr)
            size += ggml_nbytes(aheads_masks.m[i]);
    }
    return size;
}

static ggml_backend_t whisper_backend_init(const whisper_context_params & params) {
    ggml_backend_t backend_gpu = NULL;

    // initialize the backends
#ifdef GGML_USE_CUDA
    if (params.use_gpu) {
        WHISPER_LOG_INFO("%s: using CUDA backend\n", __func__);
        backend_gpu = ggml_backend_cuda_init(params.gpu_device);
        if (!backend_gpu) {
            WHISPER_LOG_ERROR("%s: ggml_backend_cuda_init() failed\n", __func__);
        }
    }
#endif

#ifdef GGML_USE_METAL
    if (params.use_gpu) {
        WHISPER_LOG_INFO("%s: using Metal backend\n", __func__);
        ggml_backend_metal_log_set_callback(g_state.log_callback, g_state.log_callback_user_data);
        backend_gpu = ggml_backend_metal_init();
        if (!backend_gpu) {
            WHISPER_LOG_ERROR("%s: ggml_backend_metal_init() failed\n", __func__);
        } else if (!ggml_backend_metal_supports_family(backend_gpu, 7)) {
            WHISPER_LOG_ERROR("%s: Metal GPU does not support family 7 - falling back to CPU\n", __func__);
            ggml_backend_free(backend_gpu);
            backend_gpu = NULL;
        }
    }
#endif

#ifdef GGML_USE_SYCL
    if (params.use_gpu) {
        WHISPER_LOG_INFO("%s: using SYCL backend\n", __func__);
        backend_gpu = ggml_backend_sycl_init(params.gpu_device);
        if (!backend_gpu) {
            WHISPER_LOG_ERROR("%s: ggml_backend_sycl_init() failed\n", __func__);
        }
    }
#endif

    if (backend_gpu) {
        return backend_gpu;
    }
    return ggml_backend_cpu_init();
}

// load the model from a ggml file
//
// file format:
//
//   - hparams
//   - pre-computed mel filters
//   - vocab
//   - weights
//
// see the convert-pt-to-ggml.py script for details
//
static bool whisper_model_load(struct whisper_model_loader * loader, whisper_context & wctx) {
    WHISPER_LOG_INFO("%s: loading model\n", __func__);

    const int64_t t_start_us = ggml_time_us();

    wctx.t_start_us = t_start_us;

    auto & model = wctx.model;
    auto & vocab = wctx.vocab;

    // verify magic
    {
        uint32_t magic;
        read_safe(loader, magic);
        if (magic != GGML_FILE_MAGIC) {
            WHISPER_LOG_ERROR("%s: invalid model data (bad magic)\n", __func__);
            return false;
        }
    }

    //load hparams
    {
        auto & hparams = model.hparams;

        read_safe(loader, hparams.n_vocab);
        read_safe(loader, hparams.n_audio_ctx);
        read_safe(loader, hparams.n_audio_state);
        read_safe(loader, hparams.n_audio_head);
        read_safe(loader, hparams.n_audio_layer);
        read_safe(loader, hparams.n_text_ctx);
        read_safe(loader, hparams.n_text_state);
        read_safe(loader, hparams.n_text_head);
        read_safe(loader, hparams.n_text_layer);
        read_safe(loader, hparams.n_mels);
        read_safe(loader, hparams.ftype);

        assert(hparams.n_text_state == hparams.n_audio_state);

        std::string mver = "";

        if (hparams.n_audio_layer == 4) {
            model.type = e_model::MODEL_TINY;
        }

        if (hparams.n_audio_layer == 6) {
            model.type = e_model::MODEL_BASE;
        }

        if (hparams.n_audio_layer == 12) {
            model.type = e_model::MODEL_SMALL;
        }

        if (hparams.n_audio_layer == 24) {
            model.type = e_model::MODEL_MEDIUM;
        }

        if (hparams.n_audio_layer == 32) {
            model.type = e_model::MODEL_LARGE;

            if (hparams.n_vocab == 51866) {
                mver = " v3";
            }
        }

        const int32_t qntvr = hparams.ftype / GGML_QNT_VERSION_FACTOR;

        hparams.ftype %= GGML_QNT_VERSION_FACTOR;

        // for the big tensors, we have the option to store the data in 16-bit floats or quantized
        // in order to save memory and also to speed up the computation
        wctx.wtype = ggml_ftype_to_ggml_type((ggml_ftype) (model.hparams.ftype));
        if (wctx.wtype == GGML_TYPE_COUNT) {
            WHISPER_LOG_ERROR("%s: invalid model (bad ftype value %d)\n", __func__, model.hparams.ftype);
            return false;
        }

        WHISPER_LOG_INFO("%s: n_vocab       = %d\n", __func__, hparams.n_vocab);
        WHISPER_LOG_INFO("%s: n_audio_ctx   = %d\n", __func__, hparams.n_audio_ctx);
        WHISPER_LOG_INFO("%s: n_audio_state = %d\n", __func__, hparams.n_audio_state);
        WHISPER_LOG_INFO("%s: n_audio_head  = %d\n", __func__, hparams.n_audio_head);
        WHISPER_LOG_INFO("%s: n_audio_layer = %d\n", __func__, hparams.n_audio_layer);
        WHISPER_LOG_INFO("%s: n_text_ctx    = %d\n", __func__, hparams.n_text_ctx);
        WHISPER_LOG_INFO("%s: n_text_state  = %d\n", __func__, hparams.n_text_state);
        WHISPER_LOG_INFO("%s: n_text_head   = %d\n", __func__, hparams.n_text_head);
        WHISPER_LOG_INFO("%s: n_text_layer  = %d\n", __func__, hparams.n_text_layer);
        WHISPER_LOG_INFO("%s: n_mels        = %d\n", __func__, hparams.n_mels);
        WHISPER_LOG_INFO("%s: ftype         = %d\n", __func__, model.hparams.ftype);
        WHISPER_LOG_INFO("%s: qntvr         = %d\n", __func__, qntvr);
        WHISPER_LOG_INFO("%s: type          = %d (%s%s)\n", __func__, model.type, g_model_name.at(model.type).c_str(), mver.c_str());
    }

    // load mel filters
    {
        auto & filters = wctx.model.filters;

        read_safe(loader, filters.n_mel);
        read_safe(loader, filters.n_fft);

        filters.data.resize(filters.n_mel * filters.n_fft);
        loader->read(loader->context, filters.data.data(), filters.data.size() * sizeof(float));
        BYTESWAP_FILTERS(filters);
    }

    // load vocab
    {
        int32_t n_vocab = 0;
        read_safe(loader, n_vocab);

        //if (n_vocab != model.hparams.n_vocab) {
        //    WHISPER_LOG_ERROR("%s: invalid model file '%s' (bad vocab size %d != %d)\n",
        //            __func__, fname.c_str(), n_vocab, model.hparams.n_vocab);
        //    return false;
        //}

        std::string word;
        std::vector<char> tmp;

        tmp.reserve(128);

        for (int i = 0; i < n_vocab; i++) {
            uint32_t len;
            read_safe(loader, len);

            if (len > 0) {
                tmp.resize(len);
                loader->read(loader->context, &tmp[0], tmp.size()); // read to buffer
                word.assign(&tmp[0], tmp.size());
            } else {
                // seems like we have an empty-string token in multi-language models (i = 50256)
                //WHISPER_LOG_WARN("%s: warning: empty-string token in vocab, i = %d\n", __func__, i);
                word = "";
            }

            vocab.token_to_id[word] = i;
            vocab.id_to_token[i] = word;

            //printf("%s: vocab[%d] = '%s'\n", __func__, i, word.c_str());
        }

        vocab.n_vocab = model.hparams.n_vocab;
        if (vocab.is_multilingual()) {
            vocab.token_eot++;
            vocab.token_sot++;

            // account for variable number of language tokens
            const int dt = vocab.num_languages() - 98;

            vocab.token_translate  += dt;
            vocab.token_transcribe += dt;
            vocab.token_solm       += dt;
            vocab.token_prev       += dt;
            vocab.token_nosp       += dt;
            vocab.token_not        += dt;
            vocab.token_beg        += dt;
        }

        if (n_vocab < model.hparams.n_vocab) {
            WHISPER_LOG_INFO("%s: adding %d extra tokens\n", __func__, model.hparams.n_vocab - n_vocab);
            for (int i = n_vocab; i < model.hparams.n_vocab; i++) {
                if (i > vocab.token_beg) {
                    word = "[_TT_" + std::to_string(i - vocab.token_beg) + "]";
                } else if (i == vocab.token_eot) {
                    word = "[_EOT_]";
                } else if (i == vocab.token_sot) {
                    word = "[_SOT_]";
                } else if (i == vocab.token_translate) {
                    word = "[_TRANSLATE_]";
                } else if (i == vocab.token_transcribe) {
                    word = "[_TRANSCRIBE_]";
                } else if (i == vocab.token_solm) {
                    word = "[_SOLM_]";
                } else if (i == vocab.token_prev) {
                    word = "[_PREV_]";
                } else if (i == vocab.token_nosp) {
                    word = "[_NOSP_]";
                } else if (i == vocab.token_not) {
                    word = "[_NOT_]";
                } else if (i == vocab.token_beg) {
                    word = "[_BEG_]";
                } else if (i > vocab.token_sot && i <= vocab.token_sot + vocab.num_languages()) {
                    word = "[_LANG_" + std::string(whisper_lang_str(i - vocab.token_sot - 1)) + "]";
                } else {
                    word = "[_extra_token_" + std::to_string(i) + "]";
                }
                vocab.token_to_id[word] = i;
                vocab.id_to_token[i] = word;
            }
        }

        WHISPER_LOG_INFO("%s: n_langs       = %d\n", __func__, vocab.num_languages());
    }

    const ggml_type wtype = wctx.wtype;
    const ggml_type vtype = wctx.wtype == GGML_TYPE_F32 ? GGML_TYPE_F32 : GGML_TYPE_F16; // conv type

    // create the ggml context
    {
        const auto & hparams = model.hparams;

        const int n_audio_layer = hparams.n_audio_layer;
        const int n_text_layer  = hparams.n_text_layer;

        const size_t n_tensors = 10 /* input */ + 15 + 15*n_audio_layer + 24*n_text_layer;

        struct ggml_init_params params = {
            /*.mem_size   =*/ n_tensors*ggml_tensor_overhead(),
            /*.mem_buffer =*/ nullptr,
            /*.no_alloc   =*/ true,
        };

        model.ctx = ggml_init(params);
        if (!model.ctx) {
            WHISPER_LOG_ERROR("%s: ggml_init() failed\n", __func__);
            return false;
        }
    }

    // prepare tensors for the weights
    {
        auto & ctx = model.ctx;

        const auto & hparams = model.hparams;

        const int n_vocab = hparams.n_vocab;

        const int n_audio_ctx   = hparams.n_audio_ctx;
        const int n_audio_state = hparams.n_audio_state;
        const int n_audio_layer = hparams.n_audio_layer;

        const int n_text_ctx   = hparams.n_text_ctx;
        const int n_text_state = hparams.n_text_state;
        const int n_text_layer = hparams.n_text_layer;

        const int n_mels = hparams.n_mels;

        model.layers_encoder.resize(n_audio_layer);
        model.layers_decoder.resize(n_text_layer);

        // encoder
        {
            model.e_pe = ggml_new_tensor_2d(ctx, GGML_TYPE_F32, n_audio_state, n_audio_ctx);

            model.e_conv_1_w     = ggml_new_tensor_3d(ctx, vtype,         3, n_mels,     n_audio_state);
            model.e_conv_1_b     = ggml_new_tensor_2d(ctx, GGML_TYPE_F32,         1,     n_audio_state);

            model.e_conv_2_w     = ggml_new_tensor_3d(ctx, vtype,         3, n_audio_state, n_audio_state);
            model.e_conv_2_b     = ggml_new_tensor_2d(ctx, GGML_TYPE_F32,                1, n_audio_state);

            model.e_ln_w = ggml_new_tensor_1d(ctx, GGML_TYPE_F32, n_audio_state);
            model.e_ln_b = ggml_new_tensor_1d(ctx, GGML_TYPE_F32, n_audio_state);

            // map by name
            model.tensors["encoder.positional_embedding"] = model.e_pe;

            model.tensors["encoder.conv1.weight"]         = model.e_conv_1_w;
            model.tensors["encoder.conv1.bias"]           = model.e_conv_1_b;

            model.tensors["encoder.conv2.weight"]         = model.e_conv_2_w;
            model.tensors["encoder.conv2.bias"]           = model.e_conv_2_b;

            model.tensors["encoder.ln_post.weight"]       = model.e_ln_w;
            model.tensors["encoder.ln_post.bias"]         = model.e_ln_b;

            for (int i = 0; i < n_audio_layer; ++i) {
                auto & layer = model.layers_encoder[i];

                layer.mlp_ln_w    = ggml_new_tensor_1d(ctx, GGML_TYPE_F32,   n_audio_state);
                layer.mlp_ln_b    = ggml_new_tensor_1d(ctx, GGML_TYPE_F32,   n_audio_state);

                layer.mlp_0_w     = ggml_new_tensor_2d(ctx, wtype,           n_audio_state, 4*n_audio_state);
                layer.mlp_0_b     = ggml_new_tensor_1d(ctx, GGML_TYPE_F32, 4*n_audio_state);

                layer.mlp_1_w     = ggml_new_tensor_2d(ctx, wtype,         4*n_audio_state, n_audio_state);
                layer.mlp_1_b     = ggml_new_tensor_1d(ctx, GGML_TYPE_F32,   n_audio_state);

                layer.attn_ln_0_w = ggml_new_tensor_1d(ctx, GGML_TYPE_F32,   n_audio_state);
                layer.attn_ln_0_b = ggml_new_tensor_1d(ctx, GGML_TYPE_F32,   n_audio_state);

                layer.attn_q_w    = ggml_new_tensor_2d(ctx, wtype,           n_audio_state, n_audio_state);
                layer.attn_q_b    = ggml_new_tensor_1d(ctx, GGML_TYPE_F32,   n_audio_state);

                layer.attn_k_w    = ggml_new_tensor_2d(ctx, wtype,           n_audio_state, n_audio_state);

                layer.attn_v_w    = ggml_new_tensor_2d(ctx, wtype,           n_audio_state, n_audio_state);
                layer.attn_v_b    = ggml_new_tensor_1d(ctx, GGML_TYPE_F32,   n_audio_state);

                layer.attn_ln_1_w = ggml_new_tensor_2d(ctx, wtype,           n_audio_state, n_audio_state);
                layer.attn_ln_1_b = ggml_new_tensor_1d(ctx, GGML_TYPE_F32,   n_audio_state);

                // map by name
                model.tensors["encoder.blocks." + std::to_string(i) + ".mlp_ln.weight"]     = layer.mlp_ln_w;
                model.tensors["encoder.blocks." + std::to_string(i) + ".mlp_ln.bias"]       = layer.mlp_ln_b;

                model.tensors["encoder.blocks." + std::to_string(i) + ".mlp.0.weight"]      = layer.mlp_0_w;
                model.tensors["encoder.blocks." + std::to_string(i) + ".mlp.0.bias"]        = layer.mlp_0_b;

                model.tensors["encoder.blocks." + std::to_string(i) + ".mlp.2.weight"]      = layer.mlp_1_w;
                model.tensors["encoder.blocks." + std::to_string(i) + ".mlp.2.bias"]        = layer.mlp_1_b;

                model.tensors["encoder.blocks." + std::to_string(i) + ".attn_ln.weight"]    = layer.attn_ln_0_w;
                model.tensors["encoder.blocks." + std::to_string(i) + ".attn_ln.bias"]      = layer.attn_ln_0_b;

                model.tensors["encoder.blocks." + std::to_string(i) + ".attn.query.weight"] = layer.attn_q_w;
                model.tensors["encoder.blocks." + std::to_string(i) + ".attn.query.bias"]   = layer.attn_q_b;

                model.tensors["encoder.blocks." + std::to_string(i) + ".attn.key.weight"]   = layer.attn_k_w;

                model.tensors["encoder.blocks." + std::to_string(i) + ".attn.value.weight"] = layer.attn_v_w;
                model.tensors["encoder.blocks." + std::to_string(i) + ".attn.value.bias"]   = layer.attn_v_b;

                model.tensors["encoder.blocks." + std::to_string(i) + ".attn.out.weight"]   = layer.attn_ln_1_w;
                model.tensors["encoder.blocks." + std::to_string(i) + ".attn.out.bias"]     = layer.attn_ln_1_b;
            }
        }

        // decoder
        {
            model.d_pe   = ggml_new_tensor_2d(ctx, GGML_TYPE_F32, n_text_state, n_text_ctx);

            model.d_te   = ggml_new_tensor_2d(ctx, wtype,         n_text_state, n_vocab);

            model.d_ln_w = ggml_new_tensor_1d(ctx, GGML_TYPE_F32, n_text_state);
            model.d_ln_b = ggml_new_tensor_1d(ctx, GGML_TYPE_F32, n_text_state);

            // map by name
            model.tensors["decoder.positional_embedding"]   = model.d_pe;

            model.tensors["decoder.token_embedding.weight"] = model.d_te;

            model.tensors["decoder.ln.weight"]              = model.d_ln_w;
            model.tensors["decoder.ln.bias"]                = model.d_ln_b;

            for (int i = 0; i < n_text_layer; ++i) {
                auto & layer = model.layers_decoder[i];

                layer.mlp_ln_w          = ggml_new_tensor_1d(ctx, GGML_TYPE_F32,   n_text_state);
                layer.mlp_ln_b          = ggml_new_tensor_1d(ctx, GGML_TYPE_F32,   n_text_state);

                layer.mlp_0_w           = ggml_new_tensor_2d(ctx, wtype,           n_text_state, 4*n_text_state);
                layer.mlp_0_b           = ggml_new_tensor_1d(ctx, GGML_TYPE_F32, 4*n_text_state);

                layer.mlp_1_w           = ggml_new_tensor_2d(ctx, wtype,         4*n_text_state, n_text_state);
                layer.mlp_1_b           = ggml_new_tensor_1d(ctx, GGML_TYPE_F32,   n_text_state);

                layer.attn_ln_0_w       = ggml_new_tensor_1d(ctx, GGML_TYPE_F32,   n_text_state);
                layer.attn_ln_0_b       = ggml_new_tensor_1d(ctx, GGML_TYPE_F32,   n_text_state);

                layer.attn_q_w          = ggml_new_tensor_2d(ctx, wtype,           n_text_state, n_text_state);
                layer.attn_q_b          = ggml_new_tensor_1d(ctx, GGML_TYPE_F32,   n_text_state);

                layer.attn_k_w          = ggml_new_tensor_2d(ctx, wtype,           n_text_state, n_text_state);

                layer.attn_v_w          = ggml_new_tensor_2d(ctx, wtype,           n_text_state, n_text_state);
                layer.attn_v_b          = ggml_new_tensor_1d(ctx, GGML_TYPE_F32,   n_text_state);

                layer.attn_ln_1_w       = ggml_new_tensor_2d(ctx, wtype,           n_text_state, n_text_state);
                layer.attn_ln_1_b       = ggml_new_tensor_1d(ctx, GGML_TYPE_F32,   n_text_state);

                layer.cross_attn_ln_0_w = ggml_new_tensor_1d(ctx, GGML_TYPE_F32,   n_text_state);
                layer.cross_attn_ln_0_b = ggml_new_tensor_1d(ctx, GGML_TYPE_F32,   n_text_state);

                layer.cross_attn_q_w    = ggml_new_tensor_2d(ctx, wtype,           n_text_state, n_text_state);
                layer.cross_attn_q_b    = ggml_new_tensor_1d(ctx, GGML_TYPE_F32,   n_text_state);

                layer.cross_attn_k_w    = ggml_new_tensor_2d(ctx, wtype,           n_text_state, n_text_state);

                layer.cross_attn_v_w    = ggml_new_tensor_2d(ctx, wtype,           n_text_state, n_text_state);
                layer.cross_attn_v_b    = ggml_new_tensor_1d(ctx, GGML_TYPE_F32,   n_text_state);

                layer.cross_attn_ln_1_w = ggml_new_tensor_2d(ctx, wtype,           n_text_state, n_text_state);
                layer.cross_attn_ln_1_b = ggml_new_tensor_1d(ctx, GGML_TYPE_F32,   n_text_state);

                // map by name
                model.tensors["decoder.blocks." + std::to_string(i) + ".mlp_ln.weight"]           = layer.mlp_ln_w;
                model.tensors["decoder.blocks." + std::to_string(i) + ".mlp_ln.bias"]             = layer.mlp_ln_b;

                model.tensors["decoder.blocks." + std::to_string(i) + ".mlp.0.weight"]            = layer.mlp_0_w;
                model.tensors["decoder.blocks." + std::to_string(i) + ".mlp.0.bias"]              = layer.mlp_0_b;

                model.tensors["decoder.blocks." + std::to_string(i) + ".mlp.2.weight"]            = layer.mlp_1_w;
                model.tensors["decoder.blocks." + std::to_string(i) + ".mlp.2.bias"]              = layer.mlp_1_b;

                model.tensors["decoder.blocks." + std::to_string(i) + ".attn_ln.weight"]          = layer.attn_ln_0_w;
                model.tensors["decoder.blocks." + std::to_string(i) + ".attn_ln.bias"]            = layer.attn_ln_0_b;

                model.tensors["decoder.blocks." + std::to_string(i) + ".attn.query.weight"]       = layer.attn_q_w;
                model.tensors["decoder.blocks." + std::to_string(i) + ".attn.query.bias"]         = layer.attn_q_b;

                model.tensors["decoder.blocks." + std::to_string(i) + ".attn.key.weight"]         = layer.attn_k_w;

                model.tensors["decoder.blocks." + std::to_string(i) + ".attn.value.weight"]       = layer.attn_v_w;
                model.tensors["decoder.blocks." + std::to_string(i) + ".attn.value.bias"]         = layer.attn_v_b;

                model.tensors["decoder.blocks." + std::to_string(i) + ".attn.out.weight"]         = layer.attn_ln_1_w;
                model.tensors["decoder.blocks." + std::to_string(i) + ".attn.out.bias"]           = layer.attn_ln_1_b;

                model.tensors["decoder.blocks." + std::to_string(i) + ".cross_attn_ln.weight"]    = layer.cross_attn_ln_0_w;
                model.tensors["decoder.blocks." + std::to_string(i) + ".cross_attn_ln.bias"]      = layer.cross_attn_ln_0_b;

                model.tensors["decoder.blocks." + std::to_string(i) + ".cross_attn.query.weight"] = layer.cross_attn_q_w;
                model.tensors["decoder.blocks." + std::to_string(i) + ".cross_attn.query.bias"]   = layer.cross_attn_q_b;

                model.tensors["decoder.blocks." + std::to_string(i) + ".cross_attn.key.weight"]   = layer.cross_attn_k_w;

                model.tensors["decoder.blocks." + std::to_string(i) + ".cross_attn.value.weight"] = layer.cross_attn_v_w;
                model.tensors["decoder.blocks." + std::to_string(i) + ".cross_attn.value.bias"]   = layer.cross_attn_v_b;

                model.tensors["decoder.blocks." + std::to_string(i) + ".cross_attn.out.weight"]   = layer.cross_attn_ln_1_w;
                model.tensors["decoder.blocks." + std::to_string(i) + ".cross_attn.out.bias"]     = layer.cross_attn_ln_1_b;
            }
        }
    }

    wctx.backend = whisper_backend_init(wctx.params);
    if (!wctx.backend) {
        WHISPER_LOG_ERROR("%s: failed to initialize the backend\n", __func__);
        return false;
    }

    // allocate tensors in the backend buffers
    model.buffer = ggml_backend_alloc_ctx_tensors(model.ctx, wctx.backend);
    if (!model.buffer) {
        WHISPER_LOG_ERROR("%s: failed to allocate memory for the model\n", __func__);
        return false;
    }

    size_t size_main = ggml_backend_buffer_get_size(model.buffer);
    WHISPER_LOG_INFO("%s: %8s total size = %8.2f MB\n", __func__, ggml_backend_name(wctx.backend), size_main / 1e6);

    // load weights
    {
        size_t total_size = 0;

        model.n_loaded = 0;

        std::vector<char> read_buf;

        while (true) {
            int32_t n_dims;
            int32_t length;
            int32_t ttype;

            read_safe(loader, n_dims);
            read_safe(loader, length);
            read_safe(loader, ttype);

            if (loader->eof(loader->context)) {
                break;
            }

            int32_t nelements = 1;
            int32_t ne[4] = { 1, 1, 1, 1 };
            for (int i = 0; i < n_dims; ++i) {
                read_safe(loader, ne[i]);
                nelements *= ne[i];
            }

            std::string name;
            std::vector<char> tmp(length); // create a buffer
            loader->read(loader->context, &tmp[0], tmp.size()); // read to buffer
            name.assign(&tmp[0], tmp.size());

            if (model.tensors.find(name) == model.tensors.end()) {
                WHISPER_LOG_ERROR("%s: unknown tensor '%s' in model file\n", __func__, name.data());
                return false;
            }

            auto tensor = model.tensors[name.data()];

            if (ggml_nelements(tensor) != nelements) {
                WHISPER_LOG_ERROR("%s: tensor '%s' has wrong size in model file\n", __func__, name.data());
                WHISPER_LOG_ERROR("%s: shape: [%d, %d, %d], expected: [%d, %d, %d]\n",
                        __func__, ne[0], ne[1], ne[2], (int) tensor->ne[0], (int) tensor->ne[1], (int) tensor->ne[2]);
                return false;
            }

            if (tensor->ne[0] != ne[0] || tensor->ne[1] != ne[1] || tensor->ne[2] != ne[2]) {
                WHISPER_LOG_ERROR("%s: tensor '%s' has wrong shape in model file: got [%d, %d, %d], expected [%d, %d, %d]\n",
                        __func__, name.data(), (int) tensor->ne[0], (int) tensor->ne[1], (int) tensor->ne[2], ne[0], ne[1], ne[2]);
                return false;
            }

            const size_t bpe = ggml_type_size(ggml_type(ttype));

            if ((nelements*bpe)/ggml_blck_size(tensor->type) != ggml_nbytes(tensor)) {
                WHISPER_LOG_ERROR("%s: tensor '%s' has wrong size in model file: got %zu, expected %zu\n",
                        __func__, name.data(), ggml_nbytes(tensor), nelements*bpe);
                return false;
            }

            //ggml_backend_t backend = wctx.backend;

            //printf("%s: [%5.5s] %s\n", __func__, ggml_backend_name(backend), name.c_str());

            if (ggml_backend_buffer_is_host(model.buffer)) {
                // for the CPU and Metal backend, we can read directly into the tensor
                loader->read(loader->context, tensor->data, ggml_nbytes(tensor));
                BYTESWAP_TENSOR(tensor);
            } else {
                // read into a temporary buffer first, then copy to device memory
                read_buf.resize(ggml_nbytes(tensor));

                loader->read(loader->context, read_buf.data(), read_buf.size());

                ggml_backend_tensor_set(tensor, read_buf.data(), 0, ggml_nbytes(tensor));
            }

            //printf("%48s - [%5d, %5d, %5d], type = %6s, %6.2f MB\n", name.data(), ne[0], ne[1], ne[2], ggml_type_name((ggml_type) ttype), ggml_nbytes(tensor)/1e6);
            total_size += ggml_nbytes(tensor);
            model.n_loaded++;
        }

        WHISPER_LOG_INFO("%s: model size    = %7.2f MB\n", __func__, total_size/1e6);

        if (model.n_loaded == 0) {
            WHISPER_LOG_WARN("%s: WARN no tensors loaded from model file - assuming empty model for testing\n", __func__);
        } else if (model.n_loaded != (int) model.tensors.size()) {
            WHISPER_LOG_ERROR("%s: ERROR not all tensors loaded from model file - expected %zu, got %d\n", __func__, model.tensors.size(), model.n_loaded);
            return false;
        }
    }

    wctx.t_load_us = ggml_time_us() - t_start_us;

    return true;
}

static bool whisper_encode_external(const whisper_state & wstate) {
    GGML_UNUSED(wstate);

#ifndef WHISPER_USE_COREML
    const bool use_coreml = false;
#else
    const bool use_coreml = wstate.ctx_coreml != nullptr;
#endif

#ifndef WHISPER_USE_OPENVINO
    const bool use_openvino = false;
#else
    const bool use_openvino = wstate.ctx_openvino != nullptr;
#endif

    return use_coreml || use_openvino;
}

static struct ggml_cgraph * whisper_build_graph_conv(
        whisper_context & wctx,
          whisper_state & wstate) {
    const auto & model   = wctx.model;
    const auto & hparams = model.hparams;

    const int n_ctx   = wstate.exp_n_audio_ctx > 0 ? wstate.exp_n_audio_ctx : hparams.n_audio_ctx;
    const int n_state = hparams.n_audio_state; GGML_UNUSED(n_state);

    const int n_mels = hparams.n_mels;

    struct ggml_init_params params = {
        /*.mem_size   =*/ wstate.alloc_conv.meta.size(),
        /*.mem_buffer =*/ wstate.alloc_conv.meta.data(),
        /*.no_alloc   =*/ true,
    };

    struct ggml_context * ctx0 = ggml_init(params);

    ggml_cgraph * gf = ggml_new_graph(ctx0);

    struct ggml_tensor * mel = ggml_new_tensor_2d(ctx0, GGML_TYPE_F32, 2*n_ctx, n_mels);
    ggml_set_name(mel, "mel");
    ggml_set_input(mel);

    struct ggml_tensor * cur = nullptr;

    if (!whisper_encode_external(wstate)) {
        // convolution + gelu
        {
            cur = ggml_conv_1d_ph(ctx0, model.e_conv_1_w, mel, 1, 1);
            cur = ggml_add(ctx0, cur, model.e_conv_1_b);

            cur = ggml_gelu(ctx0, cur);

            cur = ggml_conv_1d_ph(ctx0, model.e_conv_2_w, cur, 2, 1);
            cur = ggml_add(ctx0, cur, model.e_conv_2_b);

            cur = ggml_gelu(ctx0, cur);
        }

        ggml_set_name(cur, "embd_conv");
        wstate.embd_conv = cur;
    } else {
        ggml_build_forward_expand(gf, mel);

        cur = ggml_new_tensor_2d(ctx0, GGML_TYPE_F32, n_state, n_ctx);

        ggml_set_name(cur, "embd_enc");
        wstate.embd_enc = cur;
    }

    ggml_set_output(cur);

    ggml_build_forward_expand(gf, cur);

    ggml_free(ctx0);

    return gf;
}

static struct ggml_cgraph * whisper_build_graph_encoder(
        whisper_context & wctx,
          whisper_state & wstate) {
    const auto & model   = wctx.model;
    const auto & hparams = model.hparams;

    const int n_ctx   = wstate.exp_n_audio_ctx > 0 ? wstate.exp_n_audio_ctx : hparams.n_audio_ctx;
    const int n_state = hparams.n_audio_state;
    const int n_head  = hparams.n_audio_head;
    const int n_layer = hparams.n_audio_layer;

    struct ggml_init_params params = {
        /*.mem_size   =*/ wstate.alloc_encode.meta.size(),
        /*.mem_buffer =*/ wstate.alloc_encode.meta.data(),
        /*.no_alloc   =*/ true,
    };

    struct ggml_context * ctx0 = ggml_init(params);

    ggml_cgraph * gf = ggml_new_graph_custom(ctx0, WHISPER_MAX_NODES, false);

    struct ggml_tensor * cur = ggml_view_tensor(ctx0, wstate.embd_conv);

    const float KQscale = 1.0f/sqrtf(float(n_state)/n_head);

    // ===================================================================
    // NOTE: experimenting with partial evaluation of the encoder (ignore)
    //static int iter = -1;
    //const int n_iter = 1500/n_ctx;

    //iter = (iter + 1) % n_iter;

    //if (iter == 0) {
    //    memset(model.memory_cross_k->data, 0, ggml_nbytes(model.memory_cross_k));
    //    memset(model.memory_cross_v->data, 0, ggml_nbytes(model.memory_cross_v));
    //}

    static int iter = 0;

    const size_t e_pe_stride = model.e_pe->ne[0]*ggml_element_size(model.e_pe);
    const size_t e_pe_offset = model.e_pe->ne[0]*ggml_element_size(model.e_pe)*n_ctx*iter;

    struct ggml_tensor * e_pe = ggml_view_2d(ctx0, model.e_pe, model.e_pe->ne[0], n_ctx, e_pe_stride, e_pe_offset);
    cur = ggml_add(ctx0, e_pe, ggml_cont(ctx0, ggml_transpose(ctx0, cur)));

    // ===================================================================

    // original:
    //cur = ggml_add(ctx0, model.e_pe, ggml_transpose(ctx0, cur));

    struct ggml_tensor * inpL = cur;

    for (int il = 0; il < n_layer; ++il) {
        const auto & layer = model.layers_encoder[il];

        // norm
        {
            cur = ggml_norm(ctx0, inpL, hparams.eps);

            // cur = ln_0_w*cur + ln_0_b
            cur = ggml_add(ctx0,
                    ggml_mul(ctx0, cur, layer.attn_ln_0_w),
                    layer.attn_ln_0_b);
        }

        // self-attention
        {
            struct ggml_tensor * Qcur = ggml_mul_mat(ctx0,
                    layer.attn_q_w,
                    cur);

            Qcur = ggml_add(ctx0, Qcur, layer.attn_q_b);

            //Qcur = ggml_scale(ctx0, Qcur, pow(float(n_state)/n_head, -0.25));

            // note: no bias for Key
            struct ggml_tensor * Kcur = ggml_mul_mat(ctx0,
                    layer.attn_k_w,
                    cur);

            //Kcur = ggml_scale(ctx0, Kcur, pow(float(n_state)/n_head, -0.25));

            struct ggml_tensor * Vcur = ggml_mul_mat(ctx0,
                    layer.attn_v_w,
                    cur);

            Vcur = ggml_add(ctx0, Vcur, layer.attn_v_b);

            // ------

#ifdef WHISPER_USE_FLASH_ATTN
            struct ggml_tensor * Q =
                ggml_permute(ctx0,
                        ggml_cpy(ctx0,
                            Qcur,
                            ggml_new_tensor_3d(ctx0, wctx.itype, n_state/n_head, n_head, n_ctx)),
                        0, 2, 1, 3);

            struct ggml_tensor * K =
                ggml_permute(ctx0,
                        ggml_cpy(ctx0,
                            Kcur,
                            ggml_new_tensor_3d(ctx0, wctx.itype, n_state/n_head, n_head, n_ctx)),
                        0, 2, 1, 3);

            struct ggml_tensor * V =
                ggml_cpy(ctx0,
                        ggml_permute(ctx0,
                            ggml_reshape_3d(ctx0,
                                Vcur,
                                n_state/n_head, n_head, n_ctx),
                            1, 2, 0, 3),
                        ggml_new_tensor_3d(ctx0, wctx.itype, n_ctx, n_state/n_head, n_head));

            struct ggml_tensor * KQV = ggml_flash_attn(ctx0, Q, K, V, false);
#else
            struct ggml_tensor * Q =
                ggml_permute(ctx0,
                        ggml_cpy(ctx0,
                            Qcur,
                            ggml_new_tensor_3d(ctx0, GGML_TYPE_F32, n_state/n_head, n_head, n_ctx)),
                        0, 2, 1, 3);

            struct ggml_tensor * K =
                ggml_permute(ctx0,
                        ggml_cpy(ctx0,
                            Kcur,
                            ggml_new_tensor_3d(ctx0, wctx.itype, n_state/n_head, n_head, n_ctx)),
                        0, 2, 1, 3);

            // K * Q
            struct ggml_tensor * KQ = ggml_mul_mat(ctx0, K, Q);

            struct ggml_tensor * KQ_scaled = ggml_scale(ctx0, KQ, KQscale);

            struct ggml_tensor * KQ_soft_max = ggml_soft_max(ctx0, KQ_scaled);

            struct ggml_tensor * V =
                ggml_cpy(ctx0,
                        ggml_permute(ctx0,
                            ggml_reshape_3d(ctx0,
                                Vcur,
                                n_state/n_head, n_head, n_ctx),
                            1, 2, 0, 3),
                        ggml_new_tensor_3d(ctx0, wctx.itype, n_ctx, n_state/n_head, n_head)
                        );

            struct ggml_tensor * KQV = ggml_mul_mat(ctx0, V, KQ_soft_max);
#endif
            struct ggml_tensor * KQV_merged = ggml_permute(ctx0, KQV, 0, 2, 1, 3);

            cur = ggml_cpy(ctx0,
                    KQV_merged,
                    ggml_new_tensor_2d(ctx0, GGML_TYPE_F32, n_state, n_ctx));
        }

        // projection
        {
            cur = ggml_mul_mat(ctx0,
                    layer.attn_ln_1_w,
                    cur);

            cur = ggml_add(ctx0, cur, layer.attn_ln_1_b);
        }

        // add the input
        cur = ggml_add(ctx0, cur, inpL);

        struct ggml_tensor * inpFF = cur;

        // feed-forward network
        {
            // norm
            {
                cur = ggml_norm(ctx0, inpFF, hparams.eps);

                // cur = mlp_ln_w*cur + mlp_ln_b
                cur = ggml_add(ctx0,
                        ggml_mul(ctx0, cur, layer.mlp_ln_w),
                        layer.mlp_ln_b);
            }

#ifdef WHISPER_USE_FLASH_FF
            cur = ggml_flash_ff(ctx0,
                    ggml_cpy(ctx0, cur, ggml_new_tensor_2d(ctx0, wstate.itype, n_state, n_ctx)),
                    layer.mlp_0_w, layer.mlp_0_b, layer.mlp_1_w, layer.mlp_1_b);
#else
            // fully connected
            cur = ggml_mul_mat(ctx0,
                    layer.mlp_0_w,
                    cur);

            cur = ggml_add(ctx0, cur, layer.mlp_0_b);

            // GELU activation
            cur = ggml_gelu(ctx0, cur);

            // projection
            cur = ggml_mul_mat(ctx0,
                    layer.mlp_1_w,
                    cur);

            cur = ggml_add(ctx0, cur, layer.mlp_1_b);
#endif
        }

        inpL = ggml_add(ctx0, cur, inpFF);
    }

    cur = inpL;

    // norm
    {
        cur = ggml_norm(ctx0, cur, hparams.eps);

        // cur = ln_f_g*cur + ln_f_b
        cur = ggml_add(ctx0,
                ggml_mul(ctx0, cur, model.e_ln_w),
                model.e_ln_b);
    }

    ggml_build_forward_expand(gf, cur);

    wstate.embd_enc = cur;

    //ggml_graph_print(gf);

    ////////////////////////////////////////////////////////////////////////////

    //printf("%s: used_mem = %f MB, %f MB, %f MB %f MB %f MB\n", __func__,
    //        ggml_used_mem(ctx0)/1e6,
    //        wstate.get_buf_max_mem(0)/1e6,
    //        wstate.get_buf_max_mem(1)/1e6,
    //        wstate.get_buf_max_mem(2)/1e6,
    //        wstate.get_buf_max_mem(3)/1e6);

    ggml_free(ctx0);

    return gf;
}

// pre-compute cross-attention memory
static struct ggml_cgraph * whisper_build_graph_cross(
        whisper_context & wctx,
          whisper_state & wstate) {
    const auto & model   = wctx.model;
    const auto & hparams = model.hparams;

    const int n_ctx   = wstate.exp_n_audio_ctx > 0 ? wstate.exp_n_audio_ctx : hparams.n_audio_ctx;
    const int n_state = hparams.n_audio_state;
    const int n_head  = hparams.n_audio_head;

    struct ggml_init_params params = {
        /*.mem_size   =*/ wstate.alloc_cross.meta.size(),
        /*.mem_buffer =*/ wstate.alloc_cross.meta.data(),
        /*.no_alloc   =*/ true,
    };

    struct ggml_context * ctx0 = ggml_init(params);

    ggml_cgraph * gf = ggml_new_graph(ctx0);

    struct ggml_tensor * cur = ggml_view_tensor(ctx0, wstate.embd_enc);

    const float  Kscale = pow(float(n_state) / n_head, -0.25);

    for (int il = 0; il < model.hparams.n_text_layer; ++il) {
        auto & layer = model.layers_decoder[il];

        struct ggml_tensor* Kcross = ggml_mul_mat(ctx0,
                layer.cross_attn_k_w,
                cur);

        Kcross = ggml_scale(ctx0, Kcross, Kscale);

        struct ggml_tensor* Vcross = ggml_mul_mat(ctx0,
                layer.cross_attn_v_w,
                cur);

        Vcross = ggml_add(ctx0,
                    Vcross,
                    layer.cross_attn_v_b);

        Vcross = ggml_transpose(ctx0, ggml_reshape_2d(ctx0, Vcross, n_state, n_ctx));

        struct ggml_tensor * k = ggml_view_1d(ctx0, wstate.kv_cross.k,
                n_state*n_ctx,
                (ggml_element_size(wstate.kv_cross.k)*n_state)*(il*n_ctx));

        struct ggml_tensor * v = ggml_view_2d(ctx0, wstate.kv_cross.v, n_ctx, n_state,
                (   n_ctx)*ggml_element_size(wstate.kv_cross.v),
                (il*n_ctx)*ggml_element_size(wstate.kv_cross.v)*n_state);

        ggml_build_forward_expand(gf, ggml_cpy(ctx0, Kcross, k));
        ggml_build_forward_expand(gf, ggml_cpy(ctx0, Vcross, v));
    }

    //ggml_graph_print(gf);

    ggml_free(ctx0);

    return gf;
}

// evaluate the encoder with the given state
//
// given audio recording (more specifically, its log mel spectrogram), runs forward pass of the encoder
// part of the transformer model and returns the encoded features
//
//   - wctx:      the model
//   - wstate:     the state of the encoder
//   - n_threads:  number of threads to use
//   - mel_offset: offset in the mel spectrogram (i.e. audio offset)
//
static bool whisper_encode_internal(
        whisper_context & wctx,
          whisper_state & wstate,
              const int   mel_offset,
              const int   n_threads,
    ggml_abort_callback   abort_callback,
                   void * abort_callback_data) {
    const int64_t t_start_us = ggml_time_us();

    // conv
    {
        auto & alloc = wstate.alloc_conv.alloc;

        ggml_cgraph * gf = whisper_build_graph_conv(wctx, wstate);

        if (!ggml_gallocr_alloc_graph(alloc, gf)) {
            // should never happen as we pre-allocate the memory
            return false;
        }

        struct ggml_tensor * mel = ggml_graph_get_tensor(gf, "mel");

        // set the input
        {
            const auto & mel_inp = wstate.mel;
            const int n_ctx      = wstate.exp_n_audio_ctx > 0 ? wstate.exp_n_audio_ctx : wctx.model.hparams.n_audio_ctx;

            assert(mel->type == GGML_TYPE_F32);
            assert(mel_inp.n_mel == wctx.model.hparams.n_mels);

            wstate.inp_mel.resize(ggml_nelements(mel));

            float * dst = wstate.inp_mel.data();
            memset(dst, 0, ggml_nbytes(mel));

            const int i0 = std::min(mel_offset,           mel_inp.n_len);
            const int i1 = std::min(mel_offset + 2*n_ctx, mel_inp.n_len);

            for (int j = 0; j < mel_inp.n_mel; ++j) {
                for (int i = i0; i < i1; ++i) {
                    dst[j*2*n_ctx + (i - i0)] = mel_inp.data[j*mel_inp.n_len + i];
                }
            }

            ggml_backend_tensor_set(mel, wstate.inp_mel.data(), 0, ggml_nelements(mel)*sizeof(float));
        }

        if (!whisper_encode_external(wstate)) {
            if (!ggml_graph_compute_helper(wstate.backend, gf, n_threads)) {
                return false;
            }
        } else {
#if defined(WHISPER_USE_COREML)
            whisper_coreml_encode(wstate.ctx_coreml, mel->ne[0], mel->ne[1], (float *) mel->data, (float *) wstate.embd_enc->data);
#elif defined(WHISPER_USE_OPENVINO)
            whisper_openvino_encode(wstate.ctx_openvino, mel, wstate.embd_enc);
#endif
        }
    }

    // encoder
    if (!whisper_encode_external(wstate)) {
        auto & alloc = wstate.alloc_encode.alloc;

        ggml_cgraph * gf = whisper_build_graph_encoder(wctx, wstate);

        if (!ggml_gallocr_alloc_graph(alloc, gf)) {
            // should never happen as we pre-allocate the memory
            return false;
        }

        if (!ggml_graph_compute_helper(wstate.backend, gf, n_threads)) {
            return false;
        }
    }

    // cross
    {
        auto & alloc = wstate.alloc_cross.alloc;

        ggml_cgraph * gf = whisper_build_graph_cross(wctx, wstate);

        if (!ggml_gallocr_alloc_graph(alloc, gf)) {
            // should never happen as we pre-allocate the memory
            return false;
        }

        if (!ggml_graph_compute_helper(wstate.backend, gf, n_threads)) {
            return false;
        }
    }

    wstate.t_encode_us += ggml_time_us() - t_start_us;
    wstate.n_encode++;

    return !(abort_callback && abort_callback(abort_callback_data));
}

static struct ggml_cgraph * whisper_build_graph_decoder(
         whisper_context & wctx,
         whisper_state   & wstate,
     const whisper_batch & batch,
                    bool   save_alignment_heads_QKs,
                    bool   worst_case) {
    const auto & model   = wctx.model;
    const auto & hparams = model.hparams;

    auto & kv_self = wstate.kv_self;

    WHISPER_ASSERT(!!kv_self.ctx);

    const int n_ctx   = kv_self.size;
    const int n_state = hparams.n_text_state;
    const int n_head  = hparams.n_text_head;
    const int n_layer = hparams.n_text_layer;

    const int n_tokens    = batch.n_tokens;
    const int n_audio_ctx = wstate.exp_n_audio_ctx > 0 ? wstate.exp_n_audio_ctx : hparams.n_audio_ctx;

    const int32_t n_kv     = worst_case ? n_ctx            : kv_self.n;
    const int32_t kv_head  = worst_case ? n_ctx - n_tokens : kv_self.head;

    //WHISPER_LOG_DEBUG("%s: n_past = %d, n_tokens = %d, n_audio_ctx = %d, n_ctx = %d\n", __func__, n_past, n_tokens, n_audio_ctx, n_ctx);

    struct ggml_init_params params = {
        /*.mem_size   =*/ wstate.alloc_decode.meta.size(),
        /*.mem_buffer =*/ wstate.alloc_decode.meta.data(),
        /*.no_alloc   =*/ true,
    };

    struct ggml_context * ctx0 = ggml_init(params);

    ggml_cgraph * gf = ggml_new_graph_custom(ctx0, WHISPER_MAX_NODES, false);

    struct ggml_tensor * embd = ggml_new_tensor_1d(ctx0, GGML_TYPE_I32, n_tokens);
    ggml_set_name(embd, "embd");
    ggml_set_input(embd);

    struct ggml_tensor * position = ggml_new_tensor_1d(ctx0, GGML_TYPE_I32, n_tokens);
    ggml_set_name(position, "position");
    ggml_set_input(position);

    const float KQscale = pow(float(n_state)/n_head, -0.25);

    struct ggml_tensor * KQ_mask = ggml_new_tensor_3d(ctx0, GGML_TYPE_F32, n_kv, n_tokens, 1);
    ggml_set_name(KQ_mask, "KQ_mask");
    ggml_set_input(KQ_mask);

    // token encoding + position encoding
    struct ggml_tensor * cur =
        ggml_add(ctx0,
                ggml_get_rows(ctx0, model.d_te, embd),
                ggml_get_rows(ctx0, model.d_pe, position));

    struct ggml_tensor * inpL = cur;

    // [EXPERIMENTAL] Token-level timestamps with DTW
    struct ggml_tensor * aheads_cross_QKs = nullptr;

    for (int il = 0; il < n_layer; ++il) {
        const auto & layer = model.layers_decoder[il];

        // norm
        {
            cur = ggml_norm(ctx0, inpL, hparams.eps);

            // cur = ln_0_w*cur + ln_0_b
            cur = ggml_add(ctx0,
                    ggml_mul(ctx0,
                        cur,
                        layer.attn_ln_0_w),
                    layer.attn_ln_0_b);
        }

        // self-attention
        {
            struct ggml_tensor * Qcur = ggml_mul_mat(ctx0,
                    layer.attn_q_w,
                    cur);

            Qcur = ggml_add(ctx0,
                        Qcur,
                        layer.attn_q_b);

            Qcur = ggml_scale(ctx0, Qcur, KQscale);

            // note: no bias for Key
            struct ggml_tensor * Kcur = ggml_mul_mat(ctx0,
                    layer.attn_k_w,
                    cur);

            Kcur = ggml_scale(ctx0, Kcur, KQscale);

            // store key and value to memory
            {
                struct ggml_tensor * Vcur = ggml_mul_mat(ctx0,
                        layer.attn_v_w,
                        cur);

                Vcur = ggml_add(ctx0,
                            Vcur,
                            layer.attn_v_b);

                Vcur = ggml_transpose(ctx0, ggml_reshape_2d(ctx0, Vcur, n_state, n_tokens));

                struct ggml_tensor * k = ggml_view_1d(ctx0, kv_self.k, n_tokens*n_state, (ggml_element_size(kv_self.k)*n_state)*(il*n_ctx + kv_head));
                struct ggml_tensor * v = ggml_view_2d(ctx0, kv_self.v, n_tokens, n_state,
                        (   n_ctx)*ggml_element_size(kv_self.v),
                        (il*n_ctx)*ggml_element_size(kv_self.v)*n_state + kv_head*ggml_element_size(kv_self.v));

                ggml_build_forward_expand(gf, ggml_cpy(ctx0, Kcur, k));
                ggml_build_forward_expand(gf, ggml_cpy(ctx0, Vcur, v));
            }

            // ------

            struct ggml_tensor * Q =
                ggml_permute(ctx0,
                        ggml_reshape_3d(ctx0, Qcur, n_state/n_head, n_head, n_tokens),
                        0, 2, 1, 3);

            struct ggml_tensor * K =
                ggml_view_3d(ctx0, kv_self.k,
                        n_state/n_head, n_kv, n_head,
                        ggml_element_size(kv_self.k)*n_state,
                        ggml_element_size(kv_self.k)*n_state/n_head,
                        ggml_element_size(kv_self.k)*n_state*n_ctx*il);

            // K * Q
            struct ggml_tensor * KQ = ggml_mul_mat(ctx0, K, Q);

            //struct ggml_tensor * KQ_scaled = ggml_scale(ctx0, KQ, KQ_scale);

            //struct ggml_tensor * KQ_masked = ggml_diag_mask_inf(ctx0, KQ, n_past);
            struct ggml_tensor * KQ_masked = ggml_add(ctx0, KQ, KQ_mask);

            struct ggml_tensor * KQ_soft_max = ggml_soft_max(ctx0, KQ_masked);

            struct ggml_tensor * V =
                ggml_view_3d(ctx0, kv_self.v,
                        n_kv, n_state/n_head, n_head,
                        n_ctx*ggml_element_size(kv_self.v),
                        n_ctx*ggml_element_size(kv_self.v)*n_state/n_head,
                        n_ctx*ggml_element_size(kv_self.v)*n_state*il);

            struct ggml_tensor * KQV = ggml_mul_mat(ctx0, V, KQ_soft_max);

            struct ggml_tensor * KQV_merged = ggml_permute(ctx0, KQV, 0, 2, 1, 3);

            cur = ggml_cpy(ctx0,
                    KQV_merged,
                    ggml_new_tensor_2d(ctx0, GGML_TYPE_F32, n_state, n_tokens));
        }

        // projection
        {
            cur = ggml_mul_mat(ctx0,
                    layer.attn_ln_1_w,
                    cur);

            cur = ggml_add(ctx0,
                    cur,
                    layer.attn_ln_1_b);
        }

        // add the input
        struct ggml_tensor * inpCA = ggml_add(ctx0, cur, inpL);

        // norm
        {
            cur = ggml_norm(ctx0, inpCA, hparams.eps); // note: we use inpCA here

            // cur = ln_0_w*cur + ln_0_b
            cur = ggml_add(ctx0,
                    ggml_mul(ctx0,
                        cur,
                        layer.cross_attn_ln_0_w),
                    layer.cross_attn_ln_0_b);
        }

        // cross-attention
        {
            struct ggml_tensor * Qcur = ggml_mul_mat(ctx0,
                    layer.cross_attn_q_w,
                    cur);

            Qcur = ggml_add(ctx0,
                        Qcur,
                        layer.cross_attn_q_b);

            Qcur = ggml_scale(ctx0, Qcur, KQscale);

            // Kcross is already scaled
            struct ggml_tensor * Kcross =
                ggml_view_3d(ctx0, wstate.kv_cross.k,
                        n_state/n_head, n_audio_ctx, n_head,
                        ggml_element_size(wstate.kv_cross.k)*n_state,
                        ggml_element_size(wstate.kv_cross.k)*n_state/n_head,
                        ggml_element_size(wstate.kv_cross.k)*n_state*n_audio_ctx*il);

            //struct ggml_tensor * Vcross =
            //    ggml_reshape_3d(ctx0,
            //            ggml_view_1d(ctx0, wstate.kv_cross.v, n_audio_ctx*n_state, il*n_audio_ctx*ggml_element_size(wstate.kv_cross.v)*n_state),
            //            n_state/n_head, n_head, n_audio_ctx);

            //struct ggml_tensor * V_trans =
            //    ggml_cpy(ctx0,
            //            ggml_permute(ctx0, Vcross, 1, 2, 0, 3),
            //            ggml_new_tensor_3d(ctx0, Vcross->type, n_audio_ctx, n_state/n_head, n_head));

            struct ggml_tensor * V =
                ggml_view_3d(ctx0, wstate.kv_cross.v,
                        n_audio_ctx, n_state/n_head, n_head,
                        n_audio_ctx*ggml_element_size(wstate.kv_cross.v),
                        n_audio_ctx*ggml_element_size(wstate.kv_cross.v)*n_state/n_head,
                        n_audio_ctx*ggml_element_size(wstate.kv_cross.v)*n_state*il);

            // ------

            struct ggml_tensor * Q =
                ggml_permute(ctx0,
                        ggml_reshape_3d(ctx0, Qcur, n_state/n_head, n_head, n_tokens),
                        0, 2, 1, 3);

            // K * Q
            struct ggml_tensor * KQ = ggml_mul_mat(ctx0, Kcross, Q);

            //struct ggml_tensor * KQ_scaled =
            //    ggml_scale(ctx0,
            //            KQ,
            //            ggml_new_f32(ctx0, 1.0f/sqrt(float(n_state)/n_head))
            //            );

            // no masking for cross-attention
            //struct ggml_tensor * KQ_masked = ggml_diag_mask_inf(ctx0, KQ_scaled, n_past);

            struct ggml_tensor * KQ_soft_max = ggml_soft_max(ctx0, KQ);

            // [EXPERIMENTAL] Token-level timestamps with DTW
            if (wctx.params.dtw_token_timestamps) {
                if (wstate.aheads_masks.m[il] != nullptr) {
                    struct ggml_tensor * aheads_KQs = ggml_reshape_2d(ctx0, KQ_soft_max, KQ_soft_max->ne[0] * KQ_soft_max->ne[1], KQ_soft_max->ne[2]);
                    aheads_KQs = ggml_transpose(ctx0, aheads_KQs);
                    aheads_KQs = ggml_cont(ctx0, aheads_KQs);
                    aheads_KQs = ggml_mul_mat(ctx0, wstate.aheads_masks.m[il], aheads_KQs);
                    aheads_KQs = ggml_transpose(ctx0, aheads_KQs);
                    aheads_KQs = ggml_cont(ctx0, aheads_KQs);
                    aheads_KQs = ggml_reshape_3d(ctx0, aheads_KQs, KQ_soft_max->ne[0], KQ_soft_max->ne[1], wstate.aheads_masks.m[il]->ne[1]);
                    if (aheads_cross_QKs == NULL) {
                        aheads_cross_QKs = aheads_KQs;
                    } else {
                        aheads_cross_QKs = ggml_concat(ctx0, aheads_cross_QKs, aheads_KQs);
                    }
                }
            }

            struct ggml_tensor * KQV = ggml_mul_mat(ctx0, V, KQ_soft_max);

            struct ggml_tensor * KQV_merged = ggml_permute(ctx0, KQV, 0, 2, 1, 3);

            // cur = KQV_merged.contiguous().view(n_state, n_tokens)
            cur = ggml_cpy(ctx0,
                    KQV_merged,
                    ggml_new_tensor_2d(ctx0, GGML_TYPE_F32, n_state, n_tokens));
        }

        // projection
        {
            cur = ggml_mul_mat(ctx0,
                    layer.cross_attn_ln_1_w,
                    cur);

            cur = ggml_add(ctx0,
                    cur,
                    layer.cross_attn_ln_1_b);
        }

        // add the input
        cur = ggml_add(ctx0, cur, inpCA);

        struct ggml_tensor * inpFF = cur;

        // feed-forward network
        {
            // norm
            {
                cur = ggml_norm(ctx0, inpFF, hparams.eps);

                // cur = mlp_ln_w*cur + mlp_ln_b
                cur = ggml_add(ctx0,
                        ggml_mul(ctx0,
                            cur,
                            layer.mlp_ln_w),
                        layer.mlp_ln_b);
            }

            // fully connected
            cur = ggml_mul_mat(ctx0,
                    layer.mlp_0_w,
                    cur);

            cur = ggml_add(ctx0,
                    cur,
                    layer.mlp_0_b);

            // GELU activation
            cur = ggml_gelu(ctx0, cur);

            // projection
            cur = ggml_mul_mat(ctx0,
                    layer.mlp_1_w,
                    cur);

            cur = ggml_add(ctx0,
                    cur,
                    layer.mlp_1_b);
        }

        inpL = ggml_add(ctx0, cur, inpFF);
    }

    cur = inpL;

    // norm
    {
        cur = ggml_norm(ctx0, cur, hparams.eps);

        cur = ggml_add(ctx0,
                ggml_mul(ctx0,
                    cur,
                    model.d_ln_w),
                model.d_ln_b);
    }

    // compute logits only for the last token
    // comment this line to compute logits for all n_tokens
    // might be useful in the future
    //cur = ggml_view_2d(ctx0, cur, cur->ne[0], 1, cur->nb[1], (cur->ne[1] - 1)*cur->nb[1]);

    struct ggml_tensor * logits = ggml_mul_mat(ctx0, model.d_te, cur);

    // [EXPERIMENTAL] Token-level timestamps with DTW
    if (wctx.params.dtw_token_timestamps && aheads_cross_QKs != nullptr) {
        aheads_cross_QKs = ggml_transpose(ctx0, aheads_cross_QKs);
        aheads_cross_QKs = ggml_cont(ctx0, aheads_cross_QKs);
        if (save_alignment_heads_QKs) {
            ggml_build_forward_expand(gf, aheads_cross_QKs);
            wstate.aheads_cross_QKs = aheads_cross_QKs;
        }
    }

    ggml_build_forward_expand(gf, logits);

    ggml_free(ctx0);

    return gf;
}

// evaluate the decoder
//
// given text prompt + audio features -> computes the logits for the next token
//
//   - model:      the model
//   - n_threads:  number of threads to use
//   - tokens:     text prompt
//   - n_tokens:   number of tokens in the prompt
//   - n_past:     number of past tokens to prefix the prompt with
//
static bool whisper_decode_internal(
        whisper_context & wctx,
          whisper_state & wstate,
    const whisper_batch & batch,
              const int   n_threads,
                   bool   save_alignment_heads_QKs,
    ggml_abort_callback   abort_callback,
                   void * abort_callback_data) {
    const int64_t t_start_us = ggml_time_us();

    const auto & model   = wctx.model;
    const auto & hparams = model.hparams;

    const int n_vocab  = hparams.n_vocab;
    const int n_tokens = batch.n_tokens;

    auto & logits_out = wstate.logits;

    struct ggml_tensor * logits;

    // find KV slot for the batch
    {
        auto & kv_self = wstate.kv_self;

        if (!whisper_kv_cache_find_slot(kv_self, batch)) {
            return false;
        }

        kv_self.n = whisper_kv_cache_cell_max(kv_self);
        //kv_self.n = std::min((int32_t) hparams.n_text_ctx, std::max(32, whisper_kv_cache_cell_max(kv_self)));
        //printf("n_tokens = %5d, kv_self.head = %5d, kv_self.n = %5d, seq_id = %5d\n", batch.n_tokens, kv_self.head, kv_self.n, batch.seq_id[0][0]);
    }

    // decoder
    {
        auto & alloc = wstate.alloc_decode.alloc;

        ggml_cgraph * gf = whisper_build_graph_decoder(wctx, wstate, batch, save_alignment_heads_QKs, false);

        if (!ggml_gallocr_alloc_graph(alloc, gf)) {
            // should never happen as we pre-allocate the memory
            return false;
        }

        // set the inputs
        {
            struct ggml_tensor * embd = ggml_graph_get_tensor(gf, "embd");
            ggml_backend_tensor_set(embd, batch.token, 0, n_tokens*ggml_element_size(embd));
        }

        {
            struct ggml_tensor * position = ggml_graph_get_tensor(gf, "position");
            for (int i = 0; i < n_tokens; ++i) {
                const int32_t val = batch.pos[i];
                ggml_backend_tensor_set(position, &val, i*sizeof(int32_t), sizeof(int32_t));
            }
        }

        {
            struct ggml_tensor * KQ_mask = ggml_graph_get_tensor(gf, "KQ_mask");

            auto & kv_self = wstate.kv_self;
            const int32_t n_kv     = kv_self.n;

            wstate.inp_mask.resize(n_kv*n_tokens);

            float * data = wstate.inp_mask.data();
            memset(data, 0, ggml_nbytes(KQ_mask));

            for (int h = 0; h < 1; ++h) {
                for (int j = 0; j < n_tokens; ++j) {
                    const whisper_pos    pos    = batch.pos[j];
                    const whisper_seq_id seq_id = batch.seq_id[j][0];

                    for (int i = 0; i < n_kv; ++i) {
                        if (!kv_self.cells[i].has_seq_id(seq_id) || kv_self.cells[i].pos > pos) {
                            data[h*(n_kv*n_tokens) + j*n_kv + i] = -INFINITY;
                        }
                    }
                }
            }

            ggml_backend_tensor_set(KQ_mask, wstate.inp_mask.data(), 0, ggml_nelements(KQ_mask)*sizeof(float));
        }

        logits = gf->nodes[gf->n_nodes - 1];

        if (!ggml_graph_compute_helper(wstate.backend, gf, n_threads)) {
            return false;
        }
    }

    logits_out.resize(n_tokens*n_vocab);
    for (int i = 0; i < n_tokens; i++) {
        if (batch.logits[i] == 0) {
            continue;
        }
        ggml_backend_tensor_get(logits, logits_out.data() + (n_vocab*i), sizeof(float)*(n_vocab*i), sizeof(float)*n_vocab);
    }

    if (batch.n_tokens > 1) {
        //printf("%s: used_mem = %f MB, %f MB, %f MB %f MB %f MB\n", __func__,
        //        ggml_used_mem(ctx0)/1e6,
        //        wstate.get_buf_max_mem(0)/1e6,
        //        wstate.get_buf_max_mem(1)/1e6,
        //        wstate.get_buf_max_mem(2)/1e6,
        //        wstate.get_buf_max_mem(3)/1e6);
    }

    if (batch.n_tokens == 1) {
        wstate.t_decode_us += ggml_time_us() - t_start_us;
        wstate.n_decode++;
    } else if (batch.n_tokens < 16) {
        wstate.t_batchd_us += ggml_time_us() - t_start_us;
        wstate.n_batchd += n_tokens;
    } else {
        wstate.t_prompt_us += ggml_time_us() - t_start_us;
        wstate.n_prompt += n_tokens;
    }

    return !(abort_callback && abort_callback(abort_callback_data));
}

//  500 -> 00:05.000
// 6000 -> 01:00.000
static std::string to_timestamp(int64_t t, bool comma = false) {
    int64_t msec = t * 10;
    int64_t hr = msec / (1000 * 60 * 60);
    msec = msec - hr * (1000 * 60 * 60);
    int64_t min = msec / (1000 * 60);
    msec = msec - min * (1000 * 60);
    int64_t sec = msec / 1000;
    msec = msec - sec * 1000;

    char buf[32];
    snprintf(buf, sizeof(buf), "%02d:%02d:%02d%s%03d", (int) hr, (int) min, (int) sec, comma ? "," : ".", (int) msec);

    return std::string(buf);
}

#define SIN_COS_N_COUNT WHISPER_N_FFT
static float sin_vals[SIN_COS_N_COUNT];
static float cos_vals[SIN_COS_N_COUNT];

// In FFT, we frequently use sine and cosine operations with the same values.
// We can use precalculated values to speed up the process.
static void fill_sin_cos_table() {
    static bool is_filled = false;
    if (is_filled) return;
    for (int i = 0; i < SIN_COS_N_COUNT; i++) {
        double theta = (2*M_PI*i)/SIN_COS_N_COUNT;
        sin_vals[i] = sinf(theta);
        cos_vals[i] = cosf(theta);
    }
    is_filled = true;
}

// naive Discrete Fourier Transform
// input is real-valued
// output is complex-valued
static void dft(const std::vector<float> & in, std::vector<float> & out) {
    int N = in.size();

    out.resize(N*2);
    const int sin_cos_step = SIN_COS_N_COUNT / N;

    for (int k = 0; k < N; k++) {
        float re = 0;
        float im = 0;

        for (int n = 0; n < N; n++) {
            int idx = (k * n * sin_cos_step) % (SIN_COS_N_COUNT); // t = 2*M_PI*k*n/N
            re += in[n]*cos_vals[idx]; // cos(t)
            im -= in[n]*sin_vals[idx]; // sin(t)
        }

        out[k*2 + 0] = re;
        out[k*2 + 1] = im;
    }
}

// Cooley-Tukey FFT
// poor man's implementation - use something better
// input is real-valued
// output is complex-valued
static void fft(const std::vector<float> & in, std::vector<float> & out) {
    out.resize(in.size()*2);

    int N = in.size();

    if (N == 1) {
        out[0] = in[0];
        out[1] = 0;
        return;
    }

    if (N%2 == 1) {
        dft(in, out);
        return;
    }

    std::vector<float> even;
    std::vector<float> odd;

    even.reserve(N/2);
    odd.reserve(N/2);

    for (int i = 0; i < N; i++) {
        if (i % 2 == 0) {
            even.push_back(in[i]);
        } else {
            odd.push_back(in[i]);
        }
    }

    std::vector<float> even_fft;
    std::vector<float> odd_fft;

    fft(even, even_fft);
    fft(odd, odd_fft);

    const int sin_cos_step = SIN_COS_N_COUNT / N;
    for (int k = 0; k < N/2; k++) {
        int idx = k * sin_cos_step; // t = 2*M_PI*k/N
        float re = cos_vals[idx]; // cos(t)
        float im = -sin_vals[idx]; // sin(t)

        float re_odd = odd_fft[2*k + 0];
        float im_odd = odd_fft[2*k + 1];

        out[2*k + 0] = even_fft[2*k + 0] + re*re_odd - im*im_odd;
        out[2*k + 1] = even_fft[2*k + 1] + re*im_odd + im*re_odd;

        out[2*(k + N/2) + 0] = even_fft[2*k + 0] - re*re_odd + im*im_odd;
        out[2*(k + N/2) + 1] = even_fft[2*k + 1] - re*im_odd - im*re_odd;
    }
}

static bool hann_window(int length, bool periodic, std::vector<float> & output) {
    if (output.size() < static_cast<size_t>(length)) {
        output.resize(length);
    }
    int offset = -1;
    if (periodic) {
        offset = 0;
    }
    for (int i = 0; i < length; i++) {
        output[i] = 0.5*(1.0 - cosf((2.0*M_PI*i)/(length + offset)));
    }

    return true;
}

static void log_mel_spectrogram_worker_thread(int ith, const std::vector<float> & hann, const std::vector<float> & samples,
                                              int n_samples, int frame_size, int frame_step, int n_threads,
                                              const whisper_filters & filters, whisper_mel & mel) {
    std::vector<float> fft_in(frame_size, 0.0);
    std::vector<float> fft_out(2 * frame_step);
    // make sure n_fft == 1 + (WHISPER_N_FFT / 2), bin_0 to bin_nyquist
    int n_fft = 1 + (frame_size / 2);
    int i = ith;

    // calculate FFT only when fft_in are not all zero
    for (; i < std::min(n_samples / frame_step + 1, mel.n_len); i += n_threads) {
        const int offset = i * frame_step;

        // apply Hanning window (~10% faster)
        for (int j = 0; j < std::min(frame_size, n_samples - offset); j++) {
            fft_in[j] = hann[j] * samples[offset + j];
        }
        // fill the rest with zeros
        if (n_samples - offset < frame_size) {
            std::fill(fft_in.begin() + (n_samples - offset), fft_in.end(), 0.0);
        }

        // FFT
        fft(fft_in, fft_out);

        // Calculate modulus^2 of complex numbers
        // Use pow(fft_out[2 * j + 0], 2) + pow(fft_out[2 * j + 1], 2) causes inference quality problem? Interesting.
        for (int j = 0; j < frame_size; j++) {
            fft_out[j] = (fft_out[2 * j + 0] * fft_out[2 * j + 0] + fft_out[2 * j + 1] * fft_out[2 * j + 1]);
        }

        // mel spectrogram
        for (int j = 0; j < mel.n_mel; j++) {
            double sum = 0.0;

            // unroll loop (suggested by GH user @lunixbochs)
            int k = 0;
            for (k = 0; k < n_fft - 3; k += 4) {
                sum +=
                        fft_out[k + 0] * filters.data[j * n_fft + k + 0] +
                        fft_out[k + 1] * filters.data[j * n_fft + k + 1] +
                        fft_out[k + 2] * filters.data[j * n_fft + k + 2] +
                        fft_out[k + 3] * filters.data[j * n_fft + k + 3];
            }

            // handle n_fft remainder
            for (; k < n_fft; k++) {
                sum += fft_out[k] * filters.data[j * n_fft + k];
            }

            sum = log10(std::max(sum, 1e-10));

            mel.data[j * mel.n_len + i] = sum;
        }
    }

    // Otherwise fft_out are all zero
    double sum = log10(1e-10);
    for (; i < mel.n_len; i += n_threads) {
        for (int j = 0; j < mel.n_mel; j++) {
            mel.data[j * mel.n_len + i] = sum;
        }
    }
}

// ref: https://github.com/openai/whisper/blob/main/whisper/audio.py#L110-L157
static bool log_mel_spectrogram(
              whisper_state & wstate,
              const float * samples,
              const int   n_samples,
              const int   /*sample_rate*/,
              const int   frame_size,
              const int   frame_step,
              const int   n_mel,
              const int   n_threads,
              const whisper_filters & filters,
              const bool   debug,
              whisper_mel & mel) {
    const int64_t t_start_us = ggml_time_us();

    // Hanning window (Use cosf to eliminate difference)
    // ref: https://pytorch.org/docs/stable/generated/torch.hann_window.html
    // ref: https://github.com/openai/whisper/blob/main/whisper/audio.py#L147
    std::vector<float> hann;
    hann_window(frame_size, true, hann);


    // Calculate the length of padding
    int64_t stage_1_pad = WHISPER_SAMPLE_RATE * 30;
    int64_t stage_2_pad = frame_size / 2;

    // Initialize a vector and copy data from C array to it.
    std::vector<float> samples_padded;
    samples_padded.resize(n_samples + stage_1_pad + stage_2_pad * 2);
    std::copy(samples, samples + n_samples, samples_padded.begin() + stage_2_pad);

    // pad 30 seconds of zeros at the end of audio (480,000 samples) + reflective pad 200 samples at the end of audio
    std::fill(samples_padded.begin() + n_samples + stage_2_pad, samples_padded.begin() + n_samples + stage_1_pad + 2 * stage_2_pad, 0);

    // reflective pad 200 samples at the beginning of audio
    std::reverse_copy(samples + 1, samples + 1 + stage_2_pad, samples_padded.begin());

    mel.n_mel     = n_mel;
    // https://github.com/pytorch/pytorch/blob/main/aten/src/ATen/native/SpectralOps.cpp#L936
    // Calculate number of frames + remove the last frame
    mel.n_len     = (samples_padded.size() - frame_size) / frame_step;
    // Calculate semi-padded sample length to ensure compatibility
    mel.n_len_org = 1 + (n_samples + stage_2_pad - frame_size) / frame_step;
    mel.data.resize(mel.n_mel * mel.n_len);


    {
        std::vector<std::thread> workers(n_threads - 1);
        for (int iw = 0; iw < n_threads - 1; ++iw) {
            workers[iw] = std::thread(
                    log_mel_spectrogram_worker_thread, iw + 1, std::cref(hann), samples_padded,
                    n_samples + stage_2_pad, frame_size, frame_step, n_threads,
                    std::cref(filters), std::ref(mel));
        }

        // main thread
        log_mel_spectrogram_worker_thread(0, hann, samples_padded, n_samples + stage_2_pad, frame_size, frame_step, n_threads, filters, mel);

        for (int iw = 0; iw < n_threads - 1; ++iw) {
            workers[iw].join();
        }
    }

    // clamping and normalization
    double mmax = -1e20;
    for (int i = 0; i < mel.n_mel*mel.n_len; i++) {
        if (mel.data[i] > mmax) {
            mmax = mel.data[i];
        }
    }

    mmax -= 8.0;

    for (int i = 0; i < mel.n_mel*mel.n_len; i++) {
        if (mel.data[i] < mmax) {
            mel.data[i] = mmax;
        }

        mel.data[i] = (mel.data[i] + 4.0)/4.0;
    }

    wstate.t_mel_us += ggml_time_us() - t_start_us;

    // Dump log_mel_spectrogram
    if (debug) {
        std::ofstream outFile("log_mel_spectrogram.json");
        outFile << "[";
        for (uint64_t i = 0; i < mel.data.size() - 1; i++) {
            outFile << mel.data[i] << ", ";
        }
        outFile << mel.data[mel.data.size() - 1] << "]";
        outFile.close();
    }

    return true;
}

// split text into tokens
//
// ref: https://github.com/openai/gpt-2/blob/a74da5d99abaaba920de8131d64da2862a8f213b/src/encoder.py#L53
//
// Regex (Python):
// r"""'s|'t|'re|'ve|'m|'ll|'d| ?\p{L}+| ?\p{N}+| ?[^\s\p{L}\p{N}]+|\s+(?!\S)|\s+"""
//
// Regex (C++):
// R"('s|'t|'re|'ve|'m|'ll|'d| ?[[:alpha:]]+| ?[[:digit:]]+| ?[^\s[:alpha:][:digit:]]+|\s+(?!\S)|\s+)"
//
static std::vector<whisper_vocab::id> tokenize(const whisper_vocab & vocab, const std::string & text) {
    std::vector<std::string> words;

    // first split the text into words
    {
        std::string str = text;
        std::string pat = R"('s|'t|'re|'ve|'m|'ll|'d| ?[[:alpha:]]+| ?[[:digit:]]+| ?[^\s[:alpha:][:digit:]]+|\s+(?!\S)|\s+)";

        std::regex re(pat);
        std::smatch m;

        while (std::regex_search(str, m, re)) {
            for (auto x : m) {
                words.push_back(x);
            }
            str = m.suffix();
        }
    }

    // find the longest tokens that form the words:
    std::vector<whisper_vocab::id> tokens;
    for (const auto & word : words) {
        if (word.empty()) continue;

        int i = 0;
        int n = word.size();
        while (i < n) {
            int j = n;
            bool found = false;
            while (j > i) {
                auto sub = word.substr(i, j-i);
                auto it = vocab.token_to_id.find(sub);
                if (it != vocab.token_to_id.end()) {
                    tokens.push_back(it->second);
                    i = j;
                    found = true;
                    break;
                }
                --j;
            }
            if (!found) {
                WHISPER_LOG_ERROR("unknown token\n");
                ++i;
            }
        }
    }

    return tokens;
}

//
// interface implementation
//

#ifdef WHISPER_USE_COREML
// replace .bin with -encoder.mlmodelc
static std::string whisper_get_coreml_path_encoder(std::string path_bin) {
    auto pos = path_bin.rfind('.');
    if (pos != std::string::npos) {
        path_bin = path_bin.substr(0, pos);
    }

    // match "-qx_x"
    pos = path_bin.rfind('-');
    if (pos != std::string::npos) {
        auto sub = path_bin.substr(pos);
        if (sub.size() == 5 && sub[1] == 'q' && sub[3] == '_') {
            path_bin = path_bin.substr(0, pos);
        }
    }

    path_bin += "-encoder.mlmodelc";

    return path_bin;
}
#endif

#ifdef WHISPER_USE_OPENVINO
// replace .bin with-encoder-openvino.xml
static std::string whisper_openvino_get_path_encoder(std::string path_bin) {
    auto pos = path_bin.rfind('.');
    if (pos != std::string::npos) {
        path_bin = path_bin.substr(0, pos);
    }

    path_bin += "-encoder-openvino.xml";

    return path_bin;
}

static std::string whisper_openvino_get_path_cache(std::string path_bin) {
    auto pos = path_bin.rfind('.');
    if (pos != std::string::npos) {
        path_bin = path_bin.substr(0, pos);
    }

    path_bin += "-encoder-openvino-cache";

    return path_bin;
}
#endif

struct whisper_state * whisper_init_state(whisper_context * ctx) {
    fill_sin_cos_table();

    whisper_state * state = new whisper_state;

    state->backend = whisper_backend_init(ctx->params);
    if (!state->backend) {
        WHISPER_LOG_ERROR("%s: whisper_backend_init() failed\n", __func__);
        whisper_free_state(state);
        return nullptr;
    }

    // at this point, we don't know yet how many decoders will be used, so we overallocate 3x ctx
    // in theory, there can be a case where this is not enough, but in practice it should always be enough
    const int factor = 3;

    if (!kv_cache_init(ctx->model.hparams, state->kv_self, ctx->backend, ctx->itype, factor*ctx->model.hparams.n_text_ctx)) {
        WHISPER_LOG_ERROR("%s: kv_cache_init() failed for self-attention cache\n", __func__);
        whisper_free_state(state);
        return nullptr;
    }

    {
        const size_t memory_size = ggml_nbytes(state->kv_self.k) + ggml_nbytes(state->kv_self.v);
        WHISPER_LOG_INFO("%s: kv self size  = %7.2f MB\n", __func__, memory_size / 1e6);
    }

    if (!kv_cache_init(ctx->model.hparams, state->kv_cross, ctx->backend, ctx->itype, ctx->model.hparams.n_audio_ctx)) {
        WHISPER_LOG_ERROR("%s: kv_cache_init() failed for cross-attention cache\n", __func__);
        whisper_free_state(state);
        return nullptr;
    }

    {
        const size_t memory_size = ggml_nbytes(state->kv_cross.k) + ggml_nbytes(state->kv_cross.v);
        WHISPER_LOG_INFO("%s: kv cross size = %7.2f MB\n", __func__, memory_size / 1e6);
    }

    // [EXPERIMENTAL] Token-level timestamps with DTW
    if (ctx->params.dtw_token_timestamps) {
        if (!aheads_masks_init(ctx->params, ctx->model.hparams, state->aheads_masks, ctx->backend)) {
            WHISPER_LOG_ERROR("%s: aheads_masks_init() failed for alignment heads masks\n", __func__);
            whisper_free_state(state);
            return nullptr;
        }
        const size_t memory_size = aheads_masks_nbytes(state->aheads_masks);
        WHISPER_LOG_INFO("%s: alignment heads masks size = %ld B\n", __func__, memory_size);
    }

#ifdef WHISPER_USE_COREML
    const auto path_coreml = whisper_get_coreml_path_encoder(ctx->path_model);

    WHISPER_LOG_INFO("%s: loading Core ML model from '%s'\n", __func__, path_coreml.c_str());
    WHISPER_LOG_INFO("%s: first run on a device may take a while ...\n", __func__);

    state->ctx_coreml = whisper_coreml_init(path_coreml.c_str());
    if (!state->ctx_coreml) {
        WHISPER_LOG_ERROR("%s: failed to load Core ML model from '%s'\n", __func__, path_coreml.c_str());
#ifndef WHISPER_COREML_ALLOW_FALLBACK
        whisper_free_state(state);
        return nullptr;
#endif
    } else {
        WHISPER_LOG_INFO("%s: Core ML model loaded\n", __func__);
    }
#endif

    state->logits.reserve(ctx->vocab.n_vocab * ctx->model.hparams.n_text_ctx);

    state->batch = whisper_batch_init(ctx->model.hparams.n_text_ctx, WHISPER_MAX_DECODERS);

    // TAGS: WHISPER_DECODER_INIT
    state->decoders[0].sequence.tokens.reserve(ctx->model.hparams.n_text_ctx);

    state->decoders[0].probs.reserve    (ctx->vocab.n_vocab);
    state->decoders[0].logits.reserve   (ctx->vocab.n_vocab);
    state->decoders[0].logprobs.reserve (ctx->vocab.n_vocab);
    state->decoders[0].logits_id.reserve(ctx->model.hparams.n_vocab);

    state->decoders[0].rng = std::mt19937(0);

    // conv allocator
    {
        bool ok = whisper_allocr_graph_init(state->alloc_conv, ctx->backend,
                [&]() {
                    return whisper_build_graph_conv(*ctx, *state);
                });

        if (!ok) {
            WHISPER_LOG_ERROR("%s: failed to init conv allocator\n", __func__);
            whisper_free_state(state);
            return nullptr;
        }

        WHISPER_LOG_INFO("%s: compute buffer (conv)   = %7.2f MB\n", __func__, whisper_allocr_size(state->alloc_conv) / 1e6);
    }

    // encoder allocator
    if (!whisper_encode_external(*state)) {
        bool ok = whisper_allocr_graph_init(state->alloc_encode, ctx->backend,
                [&]() {
                    return whisper_build_graph_encoder(*ctx, *state);
                });

        if (!ok) {
            WHISPER_LOG_ERROR("%s: failed to init encoder allocator\n", __func__);
            whisper_free_state(state);
            return nullptr;
        }

        WHISPER_LOG_INFO("%s: compute buffer (encode) = %7.2f MB\n", __func__, whisper_allocr_size(state->alloc_encode) / 1e6);
    }

    // cross allocator
    {
        bool ok = whisper_allocr_graph_init(state->alloc_cross, ctx->backend,
                [&]() {
                    return whisper_build_graph_cross(*ctx, *state);
                });

        if (!ok) {
            WHISPER_LOG_ERROR("%s: failed to init cross allocator\n", __func__);
            whisper_free_state(state);
            return nullptr;
        }

        WHISPER_LOG_INFO("%s: compute buffer (cross)  = %7.2f MB\n", __func__, whisper_allocr_size(state->alloc_cross) / 1e6);
    }

    // decoder allocator
    {
        bool ok = whisper_allocr_graph_init(state->alloc_decode, ctx->backend,
                [&]() {
                    const auto & hparams = ctx->model.hparams;

                    // TODO: make sure this is the worst-case scenario
                    const int n_tokens = hparams.n_text_ctx;
                    const int n_past   = 0;

                    whisper_batch_prep_legacy(state->batch, nullptr, n_tokens, n_past, 0);

                    return whisper_build_graph_decoder(*ctx, *state, state->batch, ctx->params.dtw_token_timestamps, true);
                });

        if (!ok) {
            WHISPER_LOG_ERROR("%s: failed to init decoder allocator\n", __func__);
            whisper_free_state(state);
            return nullptr;
        }

        WHISPER_LOG_INFO("%s: compute buffer (decode) = %7.2f MB\n", __func__, whisper_allocr_size(state->alloc_decode) / 1e6);
    }

    return state;
}

int whisper_ctx_init_openvino_encoder(
        struct whisper_context * ctx,
                    const char * model_path,
                    const char * device,
                    const char * cache_dir) {
#ifndef WHISPER_USE_OPENVINO
    (void)(ctx);
    (void)(model_path);
    (void)(device);
    (void)(cache_dir);

    return 1;
#else
    if (!model_path && ctx->path_model.empty()) {
        WHISPER_LOG_ERROR("%s: model_path is nullptr, and ctx has no model_path set.\n", __func__);
        return 1;
    }

    std::string path_encoder;
    if (!model_path) {
        //if model_path is not set, attempt to find it in the same directory as ggml-<model>.bin model
        path_encoder = whisper_openvino_get_path_encoder(ctx->path_model);
    } else {
        path_encoder = model_path;
    }

    std::string path_cache;
    if (!cache_dir) {
        //if cache_dir is not set, set it as a dir residing next to ggml-<model>.bin
        path_cache = whisper_openvino_get_path_cache(ctx->path_model);
    } else {
        path_cache = cache_dir;
    }

    WHISPER_LOG_INFO("%s: loading OpenVINO model from '%s'\n", __func__, path_encoder.c_str());
    WHISPER_LOG_INFO("%s: first run on a device may take a while ...\n", __func__);

    ctx->state->ctx_openvino = whisper_openvino_init(path_encoder.c_str(), device, path_cache.c_str());
    if (!ctx->state->ctx_openvino) {
        WHISPER_LOG_ERROR("%s: failed to init OpenVINO encoder from '%s'\n", __func__, path_encoder.c_str());
        return 1;
    } else {
        WHISPER_LOG_INFO("%s: OpenVINO model loaded\n", __func__);
    }

    return 0;
#endif
}

struct whisper_context_params whisper_context_default_params() {
    struct whisper_context_params result = {
        /*.use_gpu              =*/ true,
        /*.gpu_device           =*/ 0,

        /*.dtw_token_timestamps =*/ false,
        /*.dtw_aheads_preset    =*/ WHISPER_AHEADS_NONE,
        /*.dtw_n_top            =*/ -1,
        /*.dtw_aheads           =*/ {
            /*.n_heads          =*/ 0,
            /*.heads            =*/ NULL,
        },
        /*.dtw_mem_size         =*/ 1024*1024*128,
    };
    return result;
}

struct whisper_context * whisper_init_from_file_with_params_no_state(const char * path_model, struct whisper_context_params params) {
    WHISPER_LOG_INFO("%s: loading model from '%s'\n", __func__, path_model);

    auto fin = std::ifstream(path_model, std::ios::binary);
    if (!fin) {
        WHISPER_LOG_ERROR("%s: failed to open '%s'\n", __func__, path_model);
        return nullptr;
    }

    whisper_model_loader loader = {};

    loader.context = &fin;

    loader.read = [](void * ctx, void * output, size_t read_size) {
        std::ifstream * fin = (std::ifstream*)ctx;
        fin->read((char *)output, read_size);
        return read_size;
    };

    loader.eof = [](void * ctx) {
        std::ifstream * fin = (std::ifstream*)ctx;
        return fin->eof();
    };

    loader.close = [](void * ctx) {
        std::ifstream * fin = (std::ifstream*)ctx;
        fin->close();
    };

    auto ctx = whisper_init_with_params_no_state(&loader, params);

    if (ctx) {
        ctx->path_model = path_model;
    }

    return ctx;
}

struct whisper_context * whisper_init_from_buffer_with_params_no_state(void * buffer, size_t buffer_size, struct whisper_context_params params) {
    struct buf_context {
        uint8_t* buffer;
        size_t size;
        size_t current_offset;
    };

    buf_context ctx = { reinterpret_cast<uint8_t*>(buffer), buffer_size, 0 };

    WHISPER_LOG_INFO("%s: loading model from buffer\n", __func__);

    whisper_model_loader loader = {};

    loader.context = &ctx;

    loader.read = [](void * ctx, void * output, size_t read_size) {
        buf_context * buf = reinterpret_cast<buf_context *>(ctx);

        size_t size_to_copy = buf->current_offset + read_size < buf->size ? read_size : buf->size - buf->current_offset;

        memcpy(output, buf->buffer + buf->current_offset, size_to_copy);
        buf->current_offset += size_to_copy;

        return size_to_copy;
    };

    loader.eof = [](void * ctx) {
        buf_context * buf = reinterpret_cast<buf_context *>(ctx);

        return buf->current_offset >= buf->size;
    };

    loader.close = [](void * /*ctx*/) { };

    return whisper_init_with_params_no_state(&loader, params);
}

struct whisper_context * whisper_init_with_params_no_state(struct whisper_model_loader * loader, struct whisper_context_params params) {
    ggml_time_init();

    whisper_context * ctx = new whisper_context;
    ctx->params = params;

    if (!whisper_model_load(loader, *ctx)) {
        loader->close(loader->context);
        WHISPER_LOG_ERROR("%s: failed to load model\n", __func__);
        delete ctx;
        return nullptr;
    }

    loader->close(loader->context);

    return ctx;
}

struct whisper_context * whisper_init_from_file_with_params(const char * path_model, struct whisper_context_params params) {
    whisper_context * ctx = whisper_init_from_file_with_params_no_state(path_model, params);
    if (!ctx) {
        return nullptr;
    }

    ctx->state = whisper_init_state(ctx);
    if (!ctx->state) {
        whisper_free(ctx);
        return nullptr;
    }

    return ctx;
}

struct whisper_context * whisper_init_from_buffer_with_params(void * buffer, size_t buffer_size, struct whisper_context_params params) {
    whisper_context * ctx = whisper_init_from_buffer_with_params_no_state(buffer, buffer_size, params);
    if (!ctx) {
        return nullptr;
    }

    ctx->state = whisper_init_state(ctx);
    if (!ctx->state) {
        whisper_free(ctx);
        return nullptr;
    }

    return ctx;
}

struct whisper_context * whisper_init_with_params(struct whisper_model_loader * loader, struct whisper_context_params params) {
    whisper_context * ctx = whisper_init_with_params_no_state(loader, params);
    if (!ctx) {
        return nullptr;
    }

    ctx->state = whisper_init_state(ctx);
    if (!ctx->state) {
        whisper_free(ctx);
        return nullptr;
    }

    return ctx;
}

struct whisper_context * whisper_init_from_file(const char * path_model) {
    return whisper_init_from_file_with_params(path_model, whisper_context_default_params());
}

struct whisper_context * whisper_init_from_buffer(void * buffer, size_t buffer_size) {
    return whisper_init_from_buffer_with_params(buffer, buffer_size, whisper_context_default_params());
}

struct whisper_context * whisper_init(struct whisper_model_loader * loader) {
    return whisper_init_with_params(loader, whisper_context_default_params());
}

struct whisper_context * whisper_init_from_file_no_state(const char * path_model) {
    return whisper_init_from_file_with_params_no_state(path_model, whisper_context_default_params());
}

struct whisper_context * whisper_init_from_buffer_no_state(void * buffer, size_t buffer_size) {
    return whisper_init_from_buffer_with_params_no_state(buffer, buffer_size, whisper_context_default_params());
}

struct whisper_context * whisper_init_no_state(struct whisper_model_loader * loader) {
    return whisper_init_with_params_no_state(loader, whisper_context_default_params());
}

void whisper_free_state(struct whisper_state * state) {
    if (state) {
        kv_cache_free(state->kv_self);
        kv_cache_free(state->kv_cross);

#ifdef WHISPER_USE_COREML
        if (state->ctx_coreml != nullptr) {
            whisper_coreml_free(state->ctx_coreml);
            state->ctx_coreml = nullptr;
        }
#endif

#ifdef WHISPER_USE_OPENVINO
        if (state->ctx_openvino != nullptr) {
            whisper_openvino_free(state->ctx_openvino);
            state->ctx_openvino = nullptr;
        }
#endif

        whisper_batch_free(state->batch);

        ggml_gallocr_free(state->alloc_conv.alloc);
        ggml_gallocr_free(state->alloc_encode.alloc);
        ggml_gallocr_free(state->alloc_cross.alloc);
        ggml_gallocr_free(state->alloc_decode.alloc);

        ggml_backend_free(state->backend);

        // [EXPERIMENTAL] Token-level timestamps with DTW
        aheads_masks_free(state->aheads_masks);

        delete state;
    }
}

void whisper_free(struct whisper_context * ctx) {
    if (ctx) {
        ggml_free(ctx->model.ctx);

        ggml_backend_buffer_free(ctx->model.buffer);

        whisper_free_state(ctx->state);

        ggml_backend_free(ctx->backend);

        delete ctx;
    }
}

void whisper_free_context_params(struct whisper_context_params * params) {
    if (params) {
        delete params;
    }
}

void whisper_free_params(struct whisper_full_params * params) {
    if (params) {
        delete params;
    }
}

int whisper_pcm_to_mel_with_state(struct whisper_context * ctx, struct whisper_state * state, const float * samples, int n_samples, int n_threads) {
    if (!log_mel_spectrogram(*state, samples, n_samples, WHISPER_SAMPLE_RATE, WHISPER_N_FFT, WHISPER_HOP_LENGTH, ctx->model.filters.n_mel, n_threads, ctx->model.filters, false, state->mel)) {
        WHISPER_LOG_ERROR("%s: failed to compute mel spectrogram\n", __func__);
        return -1;
    }

    return 0;
}

int whisper_pcm_to_mel(struct whisper_context * ctx, const float * samples, int n_samples, int n_threads) {
    return whisper_pcm_to_mel_with_state(ctx, ctx->state, samples, n_samples, n_threads);
}

// same as whisper_pcm_to_mel, but applies a Phase Vocoder to speed up the audio x2 (PV without phase lock is not good)
int whisper_pcm_to_mel_phase_vocoder_with_state(struct whisper_context * ctx, struct whisper_state * state, const float * samples, int n_samples, int n_threads) {
    if (!log_mel_spectrogram(*state, samples, n_samples, WHISPER_SAMPLE_RATE, 2 * WHISPER_N_FFT, 2 * WHISPER_HOP_LENGTH, ctx->model.filters.n_mel, n_threads, ctx->model.filters, false, state->mel)) {
        WHISPER_LOG_ERROR("%s: failed to compute mel spectrogram\n", __func__);
        return -1;
    }

    return 0;
}

// same as whisper_pcm_to_mel, but applies a Phase Vocoder to speed up the audio x2 (PV without phase lock is not good)
int whisper_pcm_to_mel_phase_vocoder(struct whisper_context * ctx, const float * samples, int n_samples, int n_threads) {
    return whisper_pcm_to_mel_phase_vocoder_with_state(ctx, ctx->state, samples, n_samples, n_threads);
}

// same as whisper_pcm_to_mel, but applies WSOLA to speed up the audio x2
// TODO

// same as whisper_pcm_to_mel, but applies HPTSM to speed up the audio x2
// TODO

// same as whisper_pcm_to_mel, but applies PV (with phase lock) to speed up the audio x2
// TODO

int whisper_set_mel_with_state(
        struct whisper_context * ctx,
          struct whisper_state * state,
                   const float * data,
                           int   n_len,
                           int   n_mel) {
    if (n_mel != ctx->model.filters.n_mel) {
        WHISPER_LOG_ERROR("%s: invalid number of mel bands: %d (expected %d)\n", __func__, n_mel, ctx->model.filters.n_mel);
        return -1;
    }

    state->mel.n_len     = n_len;
    state->mel.n_len_org = n_len;
    state->mel.n_mel     = n_mel;

    state->mel.data.resize(n_len*n_mel);
    memcpy(state->mel.data.data(), data, n_len*n_mel*sizeof(float));

    return 0;
}

int whisper_set_mel(
        struct whisper_context * ctx,
        const float * data,
        int n_len,
        int n_mel) {
    return whisper_set_mel_with_state(ctx, ctx->state, data, n_len, n_mel);
}

int whisper_encode_with_state(struct whisper_context * ctx, struct whisper_state * state, int offset, int n_threads) {
    if (!whisper_encode_internal(*ctx, *state, offset, n_threads, nullptr, nullptr)) {
        WHISPER_LOG_ERROR("%s: failed to eval\n", __func__);
        return -1;
    }

    return 0;
}

int whisper_encode(struct whisper_context * ctx, int offset, int n_threads) {
    if (!whisper_encode_internal(*ctx, *ctx->state, offset, n_threads, nullptr, nullptr)) {
        WHISPER_LOG_ERROR("%s: failed to eval\n", __func__);
        return -1;
    }

    return 0;
}

int whisper_decode_with_state(struct whisper_context * ctx, struct whisper_state * state, const whisper_token * tokens, int n_tokens, int n_past, int n_threads) {
    whisper_batch_prep_legacy(state->batch, tokens, n_tokens, n_past, 0);

    whisper_kv_cache_seq_rm(state->kv_self, 0, n_past, -1);

    if (!whisper_decode_internal(*ctx, *state, state->batch, n_threads, false, nullptr, nullptr)) {
        WHISPER_LOG_ERROR("%s: failed to eval\n", __func__);
        return 1;
    }

    return 0;
}

int whisper_decode(struct whisper_context * ctx, const whisper_token * tokens, int n_tokens, int n_past, int n_threads) {
    if (ctx->state == nullptr) {
        WHISPER_LOG_ERROR("%s: ERROR state was not loaded.\n", __func__);
        return -1;
    }

    return whisper_decode_with_state(ctx, ctx->state, tokens, n_tokens, n_past, n_threads);
}

int whisper_tokenize(struct whisper_context * ctx, const char * text, whisper_token * tokens, int n_max_tokens) {
    const auto res = tokenize(ctx->vocab, text);

    if (n_max_tokens < (int) res.size()) {
        WHISPER_LOG_ERROR("%s: too many resulting tokens: %d (max %d)\n", __func__, (int) res.size(), n_max_tokens);
        return -(int) res.size();
    }

    for (int i = 0; i < (int) res.size(); i++) {
        tokens[i] = res[i];
    }

    return res.size();
}

int whisper_token_count(struct whisper_context * ctx, const char * text) {
    return -whisper_tokenize(ctx, text, NULL, 0);
}

int whisper_lang_max_id() {
    auto max_id = 0;
    for (const auto & kv : g_lang) {
        max_id = std::max(max_id, kv.second.first);
    }

    return max_id;
}

int whisper_lang_id(const char * lang) {
    if (!g_lang.count(lang)) {
        for (const auto & kv : g_lang) {
            if (kv.second.second == lang) {
                return kv.second.first;
            }
        }

        WHISPER_LOG_ERROR("%s: unknown language '%s'\n", __func__, lang);
        return -1;
    }
    return g_lang.at(lang).first;
}

const char * whisper_lang_str(int id) {
    for (const auto & kv : g_lang) {
        if (kv.second.first == id) {
            return kv.first.c_str();
        }
    }

    WHISPER_LOG_ERROR("%s: unknown language id %d\n", __func__, id);
    return nullptr;
}

const char * whisper_lang_str_full(int id) {
   for (const auto & kv : g_lang) {
        if (kv.second.first == id) {
            return kv.second.second.c_str();
        }
    }

    WHISPER_LOG_ERROR("%s: unknown language id %d\n", __func__, id);
    return nullptr;
}

int whisper_lang_auto_detect_with_state(
        struct whisper_context * ctx,
          struct whisper_state * state,
                           int   offset_ms,
                           int   n_threads,
                         float * lang_probs) {
    const int seek = offset_ms/10;

    if (seek < 0) {
        WHISPER_LOG_ERROR("%s: offset %dms is before the start of the audio\n", __func__, offset_ms);
        return -1;
    }

    if (seek >= state->mel.n_len_org) {
        WHISPER_LOG_ERROR("%s: offset %dms is past the end of the audio (%dms)\n", __func__, offset_ms, state->mel.n_len_org*10);
        return -2;
    }

    // run the encoder
    if (whisper_encode_with_state(ctx, state, seek, n_threads) != 0) {
        WHISPER_LOG_ERROR("%s: failed to encode\n", __func__);
        return -6;
    }

    const std::vector<whisper_token> prompt = { whisper_token_sot(ctx) };

    if (whisper_decode_with_state(ctx, state, prompt.data(), prompt.size(), 0, n_threads) != 0) {
        WHISPER_LOG_ERROR("%s: failed to decode\n", __func__);
        return -7;
    }

    auto & logits_id = state->decoders[0].logits_id;
    logits_id.clear();

    for (const auto & kv : g_lang) {
        const auto token_lang = whisper_token_lang(ctx, kv.second.first);
        logits_id.emplace_back(state->logits[token_lang], kv.second.first);
    }

    // sort descending
    {
        using pair_type = std::remove_reference<decltype(logits_id)>::type::value_type;
        std::sort(logits_id.begin(), logits_id.end(), [](const pair_type & a, const pair_type & b) {
            return a.first > b.first;
        });
    }

    // softmax
    {
        const auto max = logits_id[0].first;

        double sum = 0.0f;
        for (auto & kv : logits_id) {
            kv.first = exp(kv.first - max);
            sum += kv.first;
        }

        for (auto & kv : logits_id) {
            kv.first /= sum;
        }
    }

    {
        for (const auto & prob : logits_id) {
            if (lang_probs) {
                lang_probs[prob.second] = prob.first;
            }

            //printf("%s: lang %2d (%3s): %f\n", __func__, prob.second, whisper_lang_str(prob.second), prob.first);
        }
    }

    return logits_id[0].second;
}

int whisper_lang_auto_detect(
        struct whisper_context * ctx,
                           int   offset_ms,
                           int   n_threads,
                         float * lang_probs) {
    return whisper_lang_auto_detect_with_state(ctx, ctx->state, offset_ms, n_threads, lang_probs);
}

int whisper_model_n_vocab(struct whisper_context * ctx) {
    return ctx->model.hparams.n_vocab;
}

int whisper_model_n_audio_ctx(struct whisper_context * ctx) {
    return ctx->model.hparams.n_audio_ctx;
}

int whisper_model_n_audio_state(struct whisper_context * ctx) {
    return ctx->model.hparams.n_audio_state;
}

int whisper_model_n_audio_head(struct whisper_context * ctx) {
    return ctx->model.hparams.n_audio_head;
}

int whisper_model_n_audio_layer(struct whisper_context * ctx) {
    return ctx->model.hparams.n_audio_layer;
}

int whisper_model_n_text_ctx(struct whisper_context * ctx) {
    return ctx->model.hparams.n_text_ctx;
}

int whisper_model_n_text_state(struct whisper_context * ctx) {
    return ctx->model.hparams.n_text_state;
}

int whisper_model_n_text_head(struct whisper_context * ctx) {
    return ctx->model.hparams.n_text_head;
}

int whisper_model_n_text_layer(struct whisper_context * ctx) {
    return ctx->model.hparams.n_text_layer;
}

int whisper_model_n_mels(struct whisper_context * ctx) {
    return ctx->model.hparams.n_mels;
}

int whisper_model_ftype(struct whisper_context * ctx) {
    return ctx->model.hparams.ftype;
}

int whisper_model_type(struct whisper_context * ctx) {
    return ctx->model.type;
}

const char *whisper_model_type_readable(struct whisper_context * ctx) {
    switch (ctx->model.type) {
    case e_model::MODEL_TINY:
        return "tiny";
    case e_model::MODEL_BASE:
        return "base";
    case e_model::MODEL_SMALL:
        return "small";
    case e_model::MODEL_MEDIUM:
        return "medium";
    case e_model::MODEL_LARGE:
        return "large";
    default:
        return "unknown";
    }
}

int whisper_n_len_from_state(struct whisper_state * state) {
    return state->mel.n_len_org;
}

int whisper_n_len(struct whisper_context * ctx) {
    return ctx->state->mel.n_len_org;
}

int whisper_n_vocab(struct whisper_context * ctx) {
    return ctx->vocab.n_vocab;
}

int whisper_n_text_ctx(struct whisper_context * ctx) {
    return ctx->model.hparams.n_text_ctx;
}

int whisper_n_audio_ctx(struct whisper_context * ctx) {
    return ctx->model.hparams.n_audio_ctx;
}

int whisper_is_multilingual(struct whisper_context * ctx) {
    return ctx->vocab.is_multilingual() ? 1 : 0;
}

float * whisper_get_logits(struct whisper_context * ctx) {
    return ctx->state->logits.data();
}

float * whisper_get_logits_from_state(struct whisper_state * state) {
    return state->logits.data();
}

const char * whisper_token_to_str(struct whisper_context * ctx, whisper_token token) {
    return ctx->vocab.id_to_token.at(token).c_str();
}

whisper_token whisper_token_eot(struct whisper_context * ctx) {
    return ctx->vocab.token_eot;
}

whisper_token whisper_token_sot(struct whisper_context * ctx) {
    return ctx->vocab.token_sot;
}

whisper_token whisper_token_solm(struct whisper_context * ctx) {
    return ctx->vocab.token_solm;
}

whisper_token whisper_token_prev(struct whisper_context * ctx) {
    return ctx->vocab.token_prev;
}

whisper_token whisper_token_nosp(struct whisper_context * ctx) {
    return ctx->vocab.token_nosp;
}

whisper_token whisper_token_not(struct whisper_context * ctx) {
    return ctx->vocab.token_not;
}

whisper_token whisper_token_beg(struct whisper_context * ctx) {
    return ctx->vocab.token_beg;
}

whisper_token whisper_token_lang(struct whisper_context * ctx, int lang_id) {
    return whisper_token_sot(ctx) + 1 + lang_id;
}

whisper_token whisper_token_translate(struct whisper_context * ctx) {
    return ctx->vocab.token_translate;
}

whisper_token whisper_token_transcribe(struct whisper_context * ctx) {
    return ctx->vocab.token_transcribe;
}

void whisper_print_timings(struct whisper_context * ctx) {
    const int64_t t_end_us = ggml_time_us();

    WHISPER_LOG_INFO("\n");
    WHISPER_LOG_INFO("%s:     load time = %8.2f ms\n", __func__, ctx->t_load_us / 1000.0f);
    if (ctx->state != nullptr) {

        const int32_t n_sample = std::max(1, ctx->state->n_sample);
        const int32_t n_encode = std::max(1, ctx->state->n_encode);
        const int32_t n_decode = std::max(1, ctx->state->n_decode);
        const int32_t n_batchd = std::max(1, ctx->state->n_batchd);
        const int32_t n_prompt = std::max(1, ctx->state->n_prompt);

        WHISPER_LOG_INFO("%s:     fallbacks = %3d p / %3d h\n", __func__, ctx->state->n_fail_p, ctx->state->n_fail_h);
        WHISPER_LOG_INFO("%s:      mel time = %8.2f ms\n", __func__, ctx->state->t_mel_us / 1000.0f);
        WHISPER_LOG_INFO("%s:   sample time = %8.2f ms / %5d runs (%8.2f ms per run)\n", __func__, 1e-3f * ctx->state->t_sample_us, n_sample, 1e-3f * ctx->state->t_sample_us / n_sample);
        WHISPER_LOG_INFO("%s:   encode time = %8.2f ms / %5d runs (%8.2f ms per run)\n", __func__, 1e-3f * ctx->state->t_encode_us, n_encode, 1e-3f * ctx->state->t_encode_us / n_encode);
        WHISPER_LOG_INFO("%s:   decode time = %8.2f ms / %5d runs (%8.2f ms per run)\n", __func__, 1e-3f * ctx->state->t_decode_us, n_decode, 1e-3f * ctx->state->t_decode_us / n_decode);
        WHISPER_LOG_INFO("%s:   batchd time = %8.2f ms / %5d runs (%8.2f ms per run)\n", __func__, 1e-3f * ctx->state->t_batchd_us, n_batchd, 1e-3f * ctx->state->t_batchd_us / n_batchd);
        WHISPER_LOG_INFO("%s:   prompt time = %8.2f ms / %5d runs (%8.2f ms per run)\n", __func__, 1e-3f * ctx->state->t_prompt_us, n_prompt, 1e-3f * ctx->state->t_prompt_us / n_prompt);
    }
    WHISPER_LOG_INFO("%s:    total time = %8.2f ms\n", __func__, (t_end_us - ctx->t_start_us)/1000.0f);
}

void whisper_reset_timings(struct whisper_context * ctx) {
    ctx->t_start_us = ggml_time_us();
    if (ctx->state != nullptr) {
        ctx->state->t_mel_us = 0;
        ctx->state->t_sample_us = 0;
        ctx->state->t_encode_us = 0;
        ctx->state->t_decode_us = 0;
        ctx->state->t_batchd_us = 0;
        ctx->state->t_prompt_us = 0;
        ctx->state->n_sample = 0;
        ctx->state->n_encode = 0;
        ctx->state->n_decode = 0;
        ctx->state->n_batchd = 0;
        ctx->state->n_prompt = 0;
    }
}

static int whisper_has_coreml(void) {
#ifdef WHISPER_USE_COREML
    return 1;
#else
    return 0;
#endif
}

static int whisper_has_openvino(void) {
#ifdef WHISPER_USE_OPENVINO
    return 1;
#else
    return 0;
#endif
}

const char * whisper_print_system_info(void) {
    static std::string s;

    s  = "";
    s += "AVX = "       + std::to_string(ggml_cpu_has_avx())       + " | ";
    s += "AVX2 = "      + std::to_string(ggml_cpu_has_avx2())      + " | ";
    s += "AVX512 = "    + std::to_string(ggml_cpu_has_avx512())    + " | ";
    s += "FMA = "       + std::to_string(ggml_cpu_has_fma())       + " | ";
    s += "NEON = "      + std::to_string(ggml_cpu_has_neon())      + " | ";
    s += "ARM_FMA = "   + std::to_string(ggml_cpu_has_arm_fma())   + " | ";
    s += "METAL = "     + std::to_string(ggml_cpu_has_metal())     + " | ";
    s += "F16C = "      + std::to_string(ggml_cpu_has_f16c())      + " | ";
    s += "FP16_VA = "   + std::to_string(ggml_cpu_has_fp16_va())   + " | ";
    s += "WASM_SIMD = " + std::to_string(ggml_cpu_has_wasm_simd()) + " | ";
    s += "BLAS = "      + std::to_string(ggml_cpu_has_blas())      + " | ";
    s += "SSE3 = "      + std::to_string(ggml_cpu_has_sse3())      + " | ";
    s += "SSSE3 = "     + std::to_string(ggml_cpu_has_ssse3())     + " | ";
    s += "VSX = "       + std::to_string(ggml_cpu_has_vsx())       + " | ";
    s += "CUDA = "      + std::to_string(ggml_cpu_has_cuda())      + " | ";
    s += "COREML = "    + std::to_string(whisper_has_coreml())     + " | ";
    s += "OPENVINO = "  + std::to_string(whisper_has_openvino())          ;

    return s.c_str();
}

//////////////////////////////////
// Grammar - ported from llama.cpp
//////////////////////////////////

// Decodes a UTF-8 string which may end in an incomplete sequence. Adds a terminating 0 for use as
// pointer. If an invalid sequence is encountered, returns `whisper_partial_utf8.n_remain == -1`.
std::pair<std::vector<uint32_t>, whisper_partial_utf8> decode_utf8(
        const char         * src,
        whisper_partial_utf8   partial_start) {
    static const int      lookup[] = { 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 2, 2, 3, 4 };
    const char          * pos      = src;
    std::vector<uint32_t> code_points;
    uint32_t              value    = partial_start.value;
    int                   n_remain = partial_start.n_remain;

    // continue previous decode, if applicable
    while (*pos != 0 && n_remain > 0) {
        uint8_t next_byte = static_cast<uint8_t>(*pos);
        if ((next_byte >> 6) != 2) {
            // invalid sequence, abort
            code_points.push_back(0);
            return std::make_pair(std::move(code_points), whisper_partial_utf8{ 0, -1 });
        }
        value = (value << 6) + (next_byte & 0x3F);
        ++pos;
        --n_remain;
    }

    if (partial_start.n_remain > 0 && n_remain == 0) {
        code_points.push_back(value);
    }

    // decode any subsequent utf-8 sequences, which may end in an incomplete one
    while (*pos != 0) {
        uint8_t  first_byte = static_cast<uint8_t>(*pos);
        uint8_t  highbits   = first_byte >> 4;
                 n_remain   = lookup[highbits] - 1;

        if (n_remain < 0) {
            // invalid sequence, abort
            code_points.clear();
            code_points.push_back(0);
            return std::make_pair(std::move(code_points), whisper_partial_utf8{ 0, n_remain });
        }

        uint8_t  mask       = (1 << (7 - n_remain)) - 1;
                 value      = first_byte & mask;
        ++pos;
        while (*pos != 0 && n_remain > 0) {
            value = (value << 6) + (static_cast<uint8_t>(*pos) & 0x3F);
            ++pos;
            --n_remain;
        }
        if (n_remain == 0) {
            code_points.push_back(value);
        }
    }
    code_points.push_back(0);

    return std::make_pair(std::move(code_points), whisper_partial_utf8{ value, n_remain });
}

// returns true iff pos points to the end of one of the definitions of a rule
static bool whisper_grammar_is_end_of_sequence(const whisper_grammar_element * pos) {
    switch (pos->type) {
        case WHISPER_GRETYPE_END: return true;  // NOLINT
        case WHISPER_GRETYPE_ALT: return true;  // NOLINT
        default:                return false;
    }
}

// returns true iff chr satisfies the char range at pos (regular or inverse range)
// asserts that pos is pointing to a char range element
static std::pair<bool, const whisper_grammar_element *> whisper_grammar_match_char(
        const whisper_grammar_element * pos,
        const uint32_t                chr) {

    bool found            = false;
    bool is_positive_char = pos->type == WHISPER_GRETYPE_CHAR;

    WHISPER_ASSERT(is_positive_char || pos->type == WHISPER_GRETYPE_CHAR_NOT); // NOLINT

    do {
        if (pos[1].type == WHISPER_GRETYPE_CHAR_RNG_UPPER) {
            // inclusive range, e.g. [a-z]
            found = found || (pos->value <= chr && chr <= pos[1].value);
            pos += 2;
        } else {
            // exact char match, e.g. [a] or "a"
            found = found || pos->value == chr;
            pos += 1;
        }
    } while (pos->type == WHISPER_GRETYPE_CHAR_ALT);

    return std::make_pair(found == is_positive_char, pos);
}

// returns true iff some continuation of the given partial UTF-8 sequence could satisfy the char
// range at pos (regular or inverse range)
// asserts that pos is pointing to a char range element
static bool whisper_grammar_match_partial_char(
        const whisper_grammar_element * pos,
        const whisper_partial_utf8      partial_utf8) {

    bool is_positive_char = pos->type == WHISPER_GRETYPE_CHAR;
    WHISPER_ASSERT(is_positive_char || pos->type == WHISPER_GRETYPE_CHAR_NOT);

    uint32_t partial_value = partial_utf8.value;
    int      n_remain      = partial_utf8.n_remain;

    // invalid sequence or 7-bit char split across 2 bytes (overlong)
    if (n_remain < 0 || (n_remain == 1 && partial_value < 2)) {
        return false;
    }

    // range of possible code points this partial UTF-8 sequence could complete to
    uint32_t low  = partial_value << (n_remain * 6);
    uint32_t high = low | ((1 << (n_remain * 6)) - 1);

    if (low == 0) {
        if (n_remain == 2) {
            low = 1 << 11;
        } else if (n_remain == 3) {
            low = 1 << 16;
        }
    }

    do {
        if (pos[1].type == WHISPER_GRETYPE_CHAR_RNG_UPPER) {
            // inclusive range, e.g. [a-z]
            if (pos->value <= high && low <= pos[1].value) {
                return is_positive_char;
            }
            pos += 2;
        } else {
            // exact char match, e.g. [a] or "a"
            if (low <= pos->value && pos->value <= high) {
                return is_positive_char;
            }
            pos += 1;
        }
    } while (pos->type == WHISPER_GRETYPE_CHAR_ALT);

    return !is_positive_char;
}


// transforms a grammar pushdown stack into N possible stacks, all ending
// at a character range (terminal element)
static void whisper_grammar_advance_stack(
        const std::vector<std::vector<whisper_grammar_element>>   & rules,
        const std::vector<const whisper_grammar_element *>        & stack,
        std::vector<std::vector<const whisper_grammar_element *>> & new_stacks) {

    if (stack.empty()) {
        new_stacks.push_back(stack);
        return;
    }

    const whisper_grammar_element * pos = stack.back();

    switch (pos->type) {
        case WHISPER_GRETYPE_RULE_REF: {
            const size_t                  rule_id = static_cast<size_t>(pos->value);
            const whisper_grammar_element * subpos  = rules[rule_id].data();
            do {
                // init new stack without the top (pos)
                std::vector<const whisper_grammar_element *> new_stack(stack.begin(), stack.end() - 1);
                if (!whisper_grammar_is_end_of_sequence(pos + 1)) {
                    // if this rule ref is followed by another element, add that to stack
                    new_stack.push_back(pos + 1);
                }
                if (!whisper_grammar_is_end_of_sequence(subpos)) {
                    // if alternate is nonempty, add to stack
                    new_stack.push_back(subpos);
                }
                whisper_grammar_advance_stack(rules, new_stack, new_stacks);
                while (!whisper_grammar_is_end_of_sequence(subpos)) {
                    // scan to end of alternate def
                    subpos++;
                }
                if (subpos->type == WHISPER_GRETYPE_ALT) {
                    // there's another alternate def of this rule to process
                    subpos++;
                } else {
                    break;
                }
            } while (true);
            break;
        }
        case WHISPER_GRETYPE_CHAR:
        case WHISPER_GRETYPE_CHAR_NOT:
            new_stacks.push_back(stack);
            break;
        default:
            // end of alternate (WHISPER_GRETYPE_END, WHISPER_GRETYPE_ALT) or middle of char range
            // (WHISPER_GRETYPE_CHAR_ALT, WHISPER_GRETYPE_CHAR_RNG_UPPER); stack should never be left on
            // those
            WHISPER_ASSERT(false);
    }
}

// takes a set of possible pushdown stacks on a grammar, which are required to
// be positioned at a character range (see `whisper_grammar_advance_stack`), and
// produces the N possible stacks if the given char is accepted at those
// positions
static std::vector<std::vector<const whisper_grammar_element *>> whisper_grammar_accept(
        const std::vector<std::vector<whisper_grammar_element>>         & rules,
        const std::vector<std::vector<const whisper_grammar_element *>> & stacks,
        const uint32_t                                                  chr) {

    std::vector<std::vector<const whisper_grammar_element *>> new_stacks;

    for (const auto & stack : stacks) {
        if (stack.empty()) {
            continue;
        }

        auto match = whisper_grammar_match_char(stack.back(), chr);
        if (match.first) {
            const whisper_grammar_element * pos = match.second;

            // update top of stack to next element, if any
            std::vector<const whisper_grammar_element *> new_stack(stack.begin(), stack.end() - 1);
            if (!whisper_grammar_is_end_of_sequence(pos)) {
                new_stack.push_back(pos);
            }
            whisper_grammar_advance_stack(rules, new_stack, new_stacks);
        }
    }

    return new_stacks;
}

static std::vector<whisper_grammar_candidate> whisper_grammar_reject_candidates(
        const std::vector<std::vector<whisper_grammar_element>>         & rules,
        const std::vector<std::vector<const whisper_grammar_element *>> & stacks,
        const std::vector<whisper_grammar_candidate>                    & candidates);

static std::vector<whisper_grammar_candidate> whisper_grammar_reject_candidates_for_stack(
        const std::vector<std::vector<whisper_grammar_element>> & rules,
        const std::vector<const whisper_grammar_element *>      & stack,
        const std::vector<whisper_grammar_candidate>            & candidates) {

    std::vector<whisper_grammar_candidate> rejects;

    if (stack.empty()) {
        for (auto tok : candidates) {
            if (*tok.code_points != 0 || tok.partial_utf8.n_remain != 0) {
                rejects.push_back(tok);
            }
        }
        return rejects;
    }

    const whisper_grammar_element * stack_pos = stack.back();

    std::vector<whisper_grammar_candidate> next_candidates;
    for (auto tok : candidates) {
        if (*tok.code_points == 0) {
            // reached end of full codepoints in token, reject iff it ended in a partial sequence
            // that cannot satisfy this position in grammar
            if (tok.partial_utf8.n_remain != 0 && !whisper_grammar_match_partial_char(stack_pos, tok.partial_utf8)) {
                rejects.push_back(tok);
            }
        } else if (whisper_grammar_match_char(stack_pos, *tok.code_points).first) {
            next_candidates.push_back({ tok.id, tok.code_points + 1, tok.partial_utf8 });
        } else {
            rejects.push_back(tok);
        }
    }

    const auto * stack_pos_after = whisper_grammar_match_char(stack_pos, 0).second;

    // update top of stack to next element, if any
    std::vector<const whisper_grammar_element *> stack_after(stack.begin(), stack.end() - 1);
    if (!whisper_grammar_is_end_of_sequence(stack_pos_after)) {
        stack_after.push_back(stack_pos_after);
    }
    std::vector<std::vector<const whisper_grammar_element *>> next_stacks;
    whisper_grammar_advance_stack(rules, stack_after, next_stacks);

    auto next_rejects = whisper_grammar_reject_candidates(rules, next_stacks, next_candidates);
    for (auto tok : next_rejects) {
        rejects.push_back({ tok.id, tok.code_points - 1, tok.partial_utf8 });
    }

    return rejects;
}

static std::vector<whisper_grammar_candidate> whisper_grammar_reject_candidates(
        const std::vector<std::vector<whisper_grammar_element>>         & rules,
        const std::vector<std::vector<const whisper_grammar_element *>> & stacks,
        const std::vector<whisper_grammar_candidate>                    & candidates) {
    if (candidates.empty() || stacks.empty()) {
        return std::vector<whisper_grammar_candidate>();
    }

    auto rejects = whisper_grammar_reject_candidates_for_stack(rules, stacks.front(), candidates);

    for (size_t i = 1, size = stacks.size(); i < size; ++i) {
        rejects = whisper_grammar_reject_candidates_for_stack(rules, stacks[i], rejects);
    }
    return rejects;
}

static struct whisper_grammar whisper_grammar_init(
            const whisper_grammar_element ** rules,
                                 size_t      n_rules,
                                 size_t      i_start_rule) {
    const whisper_grammar_element * pos;

    // copy rule definitions into vectors
    std::vector<std::vector<whisper_grammar_element>> vec_rules(n_rules);
    for (size_t i = 0; i < n_rules; i++) {
        for (pos = rules[i]; pos->type != WHISPER_GRETYPE_END; pos++) {
            vec_rules[i].push_back(*pos);
        }
        vec_rules[i].push_back({WHISPER_GRETYPE_END, 0});
    }

    // loop over alternates of start rule to build initial stacks
    std::vector<std::vector<const whisper_grammar_element *>> stacks;
    pos = rules[i_start_rule];
    do {
        std::vector<const whisper_grammar_element *> stack;
        if (!whisper_grammar_is_end_of_sequence(pos)) {
            // if alternate is nonempty, add to stack
            stack.push_back(pos);
        }
        whisper_grammar_advance_stack(vec_rules, stack, stacks);
        while (!whisper_grammar_is_end_of_sequence(pos)) {
            // scan to end of alternate def
            pos++;
        }
        if (pos->type == WHISPER_GRETYPE_ALT) {
            // there's another alternate def of this rule to process
            pos++;
        } else {
            break;
        }
    } while (true);

    return { std::move(vec_rules), std::move(stacks), {} };
}

static void whisper_suppress_invalid_grammar(
             whisper_context  & ctx,
    const whisper_full_params & params,
           std::vector<float> & logits,
    const     whisper_grammar & grammar) {

    if (grammar.rules.empty() || grammar.stacks.empty()) {
        return;
    }

    //bool allow_eot = false;
    //for (const auto & stack : grammar.stacks) {
    //    if (stack.empty()) {
    //        allow_eot = true;
    //        break;
    //    }
    //}

    const whisper_token eot = whisper_token_eot(&ctx);

    std::vector<std::pair<std::vector<uint32_t>, whisper_partial_utf8>> candidates_decoded;
    std::vector<whisper_grammar_candidate>                              candidates_grammar;

    for (whisper_token id = 0; id < eot; ++id) {
        const std::string & text = ctx.vocab.id_to_token[id];
        if (!text.empty()) {
            candidates_decoded.push_back(decode_utf8(text.c_str(), grammar.partial_utf8));
            candidates_grammar.push_back({ id, candidates_decoded.back().first.data(), candidates_decoded.back().second });
        }
    }

    const auto rejects = whisper_grammar_reject_candidates(grammar.rules, grammar.stacks, candidates_grammar);

    for (const auto & reject : rejects) {
        logits[reject.id] -= params.grammar_penalty;
    }

    // when the grammar allows a continuation, we penalize the end-of-text token
    //if (!allow_eot) {
    //    logits[eot] -= params.grammar_penalty;
    //}
    //fprintf(stderr, "Allowed: (%zu tokens)\n", size - rejects.size());
}

static void whisper_grammar_accept_token(whisper_context & ctx, whisper_grammar & grammar, whisper_token token) {
    if (grammar.rules.empty() || grammar.stacks.empty()) {
        return;
    }

    //fprintf(stderr, "Accept: '%s'\n", ctx.vocab.id_to_token[token].c_str());

    const std::string & text = ctx.vocab.id_to_token[token];

    if (text.rfind("[_", 0) == 0) {
        // fprintf(stderr, " (skipped)\n");
        return;
    }
    // fprintf(stderr, "\n");

    // Note terminating 0 in decoded string
    const auto   decoded     = decode_utf8(text.c_str(), grammar.partial_utf8);
    const auto & code_points = decoded.first;
    for (auto it = code_points.begin(), end = code_points.end() - 1; it != end; ++it) {
        grammar.stacks = whisper_grammar_accept(grammar.rules, grammar.stacks, *it);
    }
    grammar.partial_utf8 = decoded.second;
}

//////////////
// END grammar
//////////////

////////////////////////////////////////////////////////////////////////////

struct whisper_context_params * whisper_context_default_params_by_ref() {
    struct whisper_context_params params = whisper_context_default_params();

    struct whisper_context_params* result = new whisper_context_params();
    *result = params;
    return result;
}

struct whisper_full_params * whisper_full_default_params_by_ref(enum whisper_sampling_strategy strategy) {
    struct whisper_full_params params = whisper_full_default_params(strategy);

    struct whisper_full_params* result = new whisper_full_params();
    *result = params;
    return result;
}

struct whisper_full_params whisper_full_default_params(enum whisper_sampling_strategy strategy) {
    struct whisper_full_params result = {
        /*.strategy          =*/ strategy,

        /*.n_threads         =*/ std::min(4, (int32_t) std::thread::hardware_concurrency()),
        /*.n_max_text_ctx    =*/ 16384,
        /*.offset_ms         =*/ 0,
        /*.duration_ms       =*/ 0,

        /*.translate         =*/ false,
        /*.no_context        =*/ true,
        /*.no_timestamps     =*/ false,
        /*.single_segment    =*/ false,
        /*.print_special     =*/ false,
        /*.print_progress    =*/ true,
        /*.print_realtime    =*/ false,
        /*.print_timestamps  =*/ true,

        /*.token_timestamps  =*/ false,
        /*.thold_pt          =*/ 0.01f,
        /*.thold_ptsum       =*/ 0.01f,
        /*.max_len           =*/ 0,
        /*.split_on_word     =*/ false,
        /*.max_tokens        =*/ 0,

        /*.speed_up          =*/ false,
        /*.debug_mode        =*/ false,
        /*.audio_ctx         =*/ 0,

        /*.tdrz_enable       =*/ false,

        /* suppress_regex    =*/ nullptr,

        /*.initial_prompt    =*/ nullptr,
        /*.prompt_tokens     =*/ nullptr,
        /*.prompt_n_tokens   =*/ 0,

        /*.language          =*/ "en",
        /*.detect_language   =*/ false,

        /*.suppress_blank    =*/ true,
        /*.suppress_non_speech_tokens =*/ false,

        /*.temperature       =*/  0.0f,
        /*.max_initial_ts    =*/  1.0f,
        /*.length_penalty    =*/ -1.0f,

        /*.temperature_inc   =*/  0.2f,
        /*.entropy_thold     =*/  2.4f,
        /*.logprob_thold     =*/ -1.0f,
        /*.no_speech_thold   =*/  0.6f,

        /*.greedy            =*/ {
            /*.best_of   =*/ -1,
        },

        /*.beam_search      =*/ {
            /*.beam_size =*/ -1,

            /*.patience  =*/ -1.0f,
        },

        /*.new_segment_callback           =*/ nullptr,
        /*.new_segment_callback_user_data =*/ nullptr,

        /*.progress_callback           =*/ nullptr,
        /*.progress_callback_user_data =*/ nullptr,

        /*.encoder_begin_callback           =*/ nullptr,
        /*.encoder_begin_callback_user_data =*/ nullptr,

        /*.abort_callback                   =*/ nullptr,
        /*.abort_callback_user_data         =*/ nullptr,

        /*.logits_filter_callback           =*/ nullptr,
        /*.logits_filter_callback_user_data =*/ nullptr,

        /*.grammar_rules   =*/ nullptr,
        /*.n_grammar_rules =*/ 0,
        /*.i_start_rule    =*/ 0,
        /*.grammar_penalty =*/ 100.0f,
    };

    switch (strategy) {
        case WHISPER_SAMPLING_GREEDY:
            {
                result.greedy = {
                    /*.best_of   =*/ 5,
                };
            } break;
        case WHISPER_SAMPLING_BEAM_SEARCH:
            {
                result.beam_search = {
                    /*.beam_size =*/ 5,

                    /*.patience  =*/ -1.0f,
                };
            } break;
    }

    return result;
}

// forward declarations
static std::vector<float> get_signal_energy(const float * signal, int n_samples, int n_samples_per_half_window);
static void whisper_exp_compute_token_level_timestamps(
        struct whisper_context & ctx,
          struct whisper_state & state,
                           int   i_segment,
                         float   thold_pt,
                         float   thold_ptsum);

static inline bool should_split_on_word(const char * txt, bool split_on_word) {
    if (!split_on_word) return true;

    return txt[0] == ' ';
}

static void whisper_exp_compute_token_level_timestamps_dtw(
            struct whisper_context * ctx,
              struct whisper_state * state,
        struct whisper_full_params   params,
                               int   i_segment,
                            size_t   n_segments,
                               int   seek,
                               int   n_frames,
                               int   medfilt_width,
                               int   n_threads);

// wrap the last segment to max_len characters
// returns the number of new segments
static int whisper_wrap_segment(struct whisper_context & ctx, struct whisper_state & state, int max_len, bool split_on_word) {
    auto segment = state.result_all.back();

    int res = 1;
    int acc = 0;

    std::string text;

    for (int i = 0; i < (int) segment.tokens.size(); i++) {
        const auto & token = segment.tokens[i];
        if (token.id >= whisper_token_eot(&ctx)) {
            continue;
        }

        const auto txt = whisper_token_to_str(&ctx, token.id);
        const int cur = strlen(txt);

        if (acc + cur > max_len && i > 0 && should_split_on_word(txt, split_on_word)) {
            state.result_all.back().text = std::move(text);
            state.result_all.back().t1 = token.t0;
            state.result_all.back().tokens.resize(i);
            state.result_all.back().speaker_turn_next = false;

            state.result_all.push_back({});
            state.result_all.back().t0 = token.t0;
            state.result_all.back().t1 = segment.t1;

            // add tokens [i, end] to the new segment
            state.result_all.back().tokens.insert(
                state.result_all.back().tokens.end(),
                    segment.tokens.begin() + i,
                    segment.tokens.end());

            state.result_all.back().speaker_turn_next = segment.speaker_turn_next;

            acc = 0;
            text = "";

            segment = state.result_all.back();
            i = -1;

            res++;
        } else {
            acc += cur;
            text += txt;
        }
    }

    state.result_all.back().text = std::move(text);

    return res;
}

static const std::vector<std::string> non_speech_tokens = {
    "\"", "#", "(", ")", "*", "+", "/", ":", ";", "<", "=", ">", "@", "[", "\\", "]", "^",
    "_", "`", "{", "|", "}", "~", "「", "」", "『", "』", "<<", ">>", "<<<", ">>>", "--",
    "---", "-(", "-[", "('", "(\"", "((", "))", "(((", ")))", "[[", "]]", "{{", "}}", "♪♪",
    "♪♪♪","♩", "♪", "♫", "♬", "♭", "♮", "♯"
};

// process the logits for the selected decoder
// - applies logit filters
// - computes logprobs and probs
// TODO: optimize
static void whisper_process_logits(
              struct whisper_context & ctx,
               struct whisper_state  & state,
              struct whisper_decoder & decoder,
    const struct whisper_full_params   params,
                               float   temperature) {
    const auto & vocab      = ctx.vocab;
    const auto & tokens_cur = decoder.sequence.tokens;

    const bool is_initial = tokens_cur.size() == 0;
    const int  n_logits   = vocab.id_to_token.size();

    WHISPER_ASSERT(n_logits == ctx.vocab.n_vocab);

    // extract the logits for the last token
    // we will be mutating, and therefore we don't want to use the ctx.logits buffer directly
    auto & probs    = decoder.probs;
    auto & logits   = decoder.logits;
    auto & logprobs = decoder.logprobs;
    {
        logits.resize(n_logits);
        memcpy(logits.data(), state.logits.data() + decoder.i_batch*n_logits, n_logits*sizeof(float));

        if (temperature > 0.0f) {
            for (int i = 0; i < n_logits; i++) {
                logits[i] /= temperature;
            }
        }

        // will be populated a bit later
        probs.resize(n_logits);
        logprobs.resize(n_logits);
    }

    // apply logit filters here
    // ref: https://github.com/openai/whisper/blob/0b1ba3d46ebf7fe6f953acfd8cad62a4f851b49f/whisper/decoding.py#L480-L493
    {
        // suppress blank
        // https://github.com/openai/whisper/blob/0b1ba3d46ebf7fe6f953acfd8cad62a4f851b49f/whisper/decoding.py#L388-L390
        if (params.suppress_blank) {
            if (is_initial) {
                logits[vocab.token_eot]           = -INFINITY;
                logits[vocab.token_to_id.at(" ")] = -INFINITY;
            }
        }

        // suppress <|notimestamps|> token
        // ref: https://github.com/openai/whisper/blob/0b1ba3d46ebf7fe6f953acfd8cad62a4f851b49f/whisper/decoding.py#L410-L412
        logits[vocab.token_not] = -INFINITY;
        if (params.no_timestamps) {
            for (int i = vocab.token_beg; i < n_logits; ++i) {
                logits[i] = -INFINITY;
            }
        }

        // suppress sot and nosp tokens
        logits[vocab.token_sot]  = -INFINITY;
        logits[vocab.token_nosp] = -INFINITY; // TODO: ignore this token for now

        // [TDRZ] when tinydiarize is disabled, suppress solm token
        if (params.tdrz_enable == false) {
            logits[vocab.token_solm] = -INFINITY;
        }

        // suppress task tokens
        logits[vocab.token_translate]  = -INFINITY;
        logits[vocab.token_transcribe] = -INFINITY;
        logits[vocab.token_prev]       = -INFINITY;

        // suppress lang tokens
        for (size_t i = 0; i < g_lang.size(); ++i) {
            logits[whisper_token_lang(&ctx, i)] = -INFINITY;
        }

        // suppress prev token
        logits[vocab.token_prev] = -INFINITY;

        if (params.logits_filter_callback) {
            params.logits_filter_callback(&ctx, &state, tokens_cur.data(), tokens_cur.size(), logits.data(), params.logits_filter_callback_user_data);
        }

        // suppress any tokens matching a regular expression
        // ref: https://github.com/openai/whisper/discussions/1041
        if (params.suppress_regex != nullptr) {
            std::regex re(params.suppress_regex);
            for (std::pair<whisper_vocab::token, whisper_vocab::id> token_id : vocab.token_to_id) {
                if (std::regex_match(token_id.first, re)) {
                    logits[token_id.second] = -INFINITY;
                }
            }
        }

        // suppress non-speech tokens
        // ref: https://github.com/openai/whisper/blob/7858aa9c08d98f75575035ecd6481f462d66ca27/whisper/tokenizer.py#L224-L253
        if (params.suppress_non_speech_tokens) {
            for (const std::string & token : non_speech_tokens) {
                const std::string suppress_tokens[] = {token, " " + token};
                for (const std::string & suppress_token : suppress_tokens) {
                    if (vocab.token_to_id.find(suppress_token) != vocab.token_to_id.end()) {
                        logits[vocab.token_to_id.at(suppress_token)] = -INFINITY;
                    }
                }
            }

            // allow hyphens "-" and single quotes "'" between words, but not at the beginning of a word
            if (vocab.token_to_id.find(" -") != vocab.token_to_id.end()) {
                logits[vocab.token_to_id.at(" -")] = -INFINITY;
            }
            if (vocab.token_to_id.find(" '") != vocab.token_to_id.end()) {
                logits[vocab.token_to_id.at(" '")] = -INFINITY;
            }
        }

        // timestamps have to appear in pairs, except directly before EOT; mask logits accordingly
        // https://github.com/openai/whisper/blob/0b1ba3d46ebf7fe6f953acfd8cad62a4f851b49f/whisper/decoding.py#L414-L424
        {
            const bool last_was_timestamp        = tokens_cur.size() > 0 && tokens_cur.back().id >= vocab.token_beg;
            const bool penultimate_was_timestamp = tokens_cur.size() < 2 || tokens_cur[tokens_cur.size() - 2].id >= vocab.token_beg;

            //WHISPER_LOG_INFO("last_was_timestamp=%d penultimate_was_timestamp=%d\n", last_was_timestamp, penultimate_was_timestamp);

            if (last_was_timestamp) {
                if (penultimate_was_timestamp) {
                    for (int i = vocab.token_beg; i < n_logits; ++i) {
                        logits[i] = -INFINITY;
                    }
                } else {
                    for (int i = 0; i < vocab.token_eot; ++i) {
                        logits[i] = -INFINITY;
                    }
                }
            }
        }

        // the initial timestamp cannot be larger than max_initial_ts
        // ref: https://github.com/openai/whisper/blob/0b1ba3d46ebf7fe6f953acfd8cad62a4f851b49f/whisper/decoding.py#L426-L429
        if (is_initial && params.max_initial_ts > 0.0f) {
            const float precision = float(WHISPER_CHUNK_SIZE)/ctx.model.hparams.n_audio_ctx;
            const int   tid0      = std::round(params.max_initial_ts/precision);

            for (int i = vocab.token_beg + tid0 + 1; i < n_logits; ++i) {
                logits[i] = -INFINITY;
            }
        }

        // condition timestamp tokens to be increasing
        // ref: https://github.com/openai/whisper/pull/831#issuecomment-1385910556
        if (decoder.has_ts) {
            const int tid0 = decoder.seek_delta/2;

            for (int i = vocab.token_beg; i < vocab.token_beg + tid0; ++i) {
                logits[i] = -INFINITY;
            }
        }

        // populate the logprobs array (log_softmax)
        {
            const float logit_max = *std::max_element(logits.begin(), logits.end());
            float logsumexp = 0.0f;
            for (int i = 0; i < n_logits; ++i) {
                if (logits[i] > -INFINITY) {
                    logsumexp += expf(logits[i] - logit_max);
                }
            }
            logsumexp = logf(logsumexp) + logit_max;

            for (int i = 0; i < n_logits; ++i) {
                if (logits[i] > -INFINITY) {
                    logprobs[i] = logits[i] - logsumexp;
                } else {
                    logprobs[i] = -INFINITY;
                }
            }
        }

        // if sum of probability over timestamps is above any other token, sample timestamp
        // ref: https://github.com/openai/whisper/blob/0b1ba3d46ebf7fe6f953acfd8cad62a4f851b49f/whisper/decoding.py#L431-L437
        {
            // logsumexp over timestamps
            float timestamp_logprob = -INFINITY;
            {
                float logsumexp = 0.0f;
                const float logprob_max = *std::max_element(logprobs.begin() + vocab.token_beg, logprobs.end());
                for (int i = vocab.token_beg; i < n_logits; ++i) {
                    if (logprobs[i] > -INFINITY) {
                        logsumexp += expf(logprobs[i] - logprob_max);
                    }
                }
                if (logsumexp > 0.0f) {
                    timestamp_logprob = logf(logsumexp) + logprob_max;
                }
            }

            const float max_text_token_logprob = *std::max_element(logprobs.begin(), logprobs.begin() + vocab.token_beg);

            //WHISPER_LOG_INFO("timestamp_logprob=%f max_text_token_logprob=%f\n", timestamp_logprob, max_text_token_logprob);

            if (timestamp_logprob > max_text_token_logprob) {
                for (int i = 0; i < vocab.token_beg; ++i) {
                    logits[i]   = -INFINITY;
                    logprobs[i] = -INFINITY;
                }
            } else {
                if (params.n_grammar_rules > 0) {
                    whisper_suppress_invalid_grammar(ctx, params, logits, decoder.grammar);

                    // populate the logprobs array (log_softmax)
                    {
                        const float logit_max = *std::max_element(logits.begin(), logits.end());
                        float logsumexp = 0.0f;
                        for (int i = 0; i < n_logits; ++i) {
                            if (logits[i] > -INFINITY) {
                                logsumexp += expf(logits[i] - logit_max);
                            }
                        }
                        logsumexp = logf(logsumexp) + logit_max;

                        for (int i = 0; i < n_logits; ++i) {
                            if (logits[i] > -INFINITY) {
                                logprobs[i] = logits[i] - logsumexp;
                            } else {
                                logprobs[i] = -INFINITY;
                            }
                        }
                    }
                }
            }
        }
    }

    // compute probs
    {
        for (int i = 0; i < n_logits; ++i) {
            if (logits[i] == -INFINITY) {
                probs[i] = 0.0f;
            } else {
                probs[i] = expf(logprobs[i]);
            }
        }
    }

#if 0
    // print first 100 logits - token string : logit
    //for (int i = 0; i < 10; i++) {
    //    const auto token   = vocab.id_to_token.at(i);
    //    const auto prob    = probs[i];
    //    const auto logit   = logits[i];
    //    const auto logprob = logprobs[i];
    //    printf("%16s : prob=%9.5f logit=%9.5f logprob=%9.5f\n", token.c_str(), prob, logit, logprob);
    //}

    // print sorted
    {
        std::vector<std::pair<float, int>> pairs;

        for (int i = 0; i < n_logits; ++i) {
            pairs.push_back(std::make_pair(probs[i], i));
        }

        std::sort(pairs.begin(), pairs.end(), [](const std::pair<float, int>& a, const std::pair<float, int>& b) {
            return a.first > b.first;
        });

        for (int i = 0; i < 10; i++) {
            const auto token   = vocab.id_to_token.at(pairs[i].second);
            const auto prob    = pairs[i].first;
            const auto logit   = logits[pairs[i].second];
            const auto logprob = logprobs[pairs[i].second];
            printf("%16s : id=%6d prob=%9.5f logit=%9.5f logprob=%9.5f '%s'\n", token.c_str(), pairs[i].second, prob, logit, logprob, token.c_str());
        }

        printf("----------------\n");
    }

    // "And", "and", " And", " and"
    //printf("logits[\"and\"]  = %f\n", logits[vocab.token_to_id.at("and")]);
    //printf("logits[\"And\"]  = %f\n", logits[vocab.token_to_id.at("And")]);
    //printf("logits[\" and\"] = %f\n", logits[vocab.token_to_id.at(" and")]);
    //printf("logits[\" And\"] = %f\n", logits[vocab.token_to_id.at(" And")]);
    //printf("logits[\" so\"]  = %f\n", logits[vocab.token_to_id.at(" so")]);

    //printf("logprobs[\"and\"]  = %f\n", logprobs[vocab.token_to_id.at("and")]);
    //printf("logprobs[\"And\"]  = %f\n", logprobs[vocab.token_to_id.at("And")]);
    //printf("logprobs[\" and\"] = %f\n", logprobs[vocab.token_to_id.at(" and")]);
    //printf("logprobs[\" And\"] = %f\n", logprobs[vocab.token_to_id.at(" And")]);
    //printf("logprobs[\" so\"]  = %f\n", logprobs[vocab.token_to_id.at(" so")]);

    //printf("probs[\"and\"]  = %f\n", probs[vocab.token_to_id.at("and")]);
    //printf("probs[\"And\"]  = %f\n", probs[vocab.token_to_id.at("And")]);
    //printf("probs[\" and\"] = %f\n", probs[vocab.token_to_id.at(" and")]);
    //printf("probs[\" And\"] = %f\n", probs[vocab.token_to_id.at(" And")]);
    //printf("probs[\" so\"]  = %f\n", probs[vocab.token_to_id.at(" so")]);
#endif
}

static bool whisper_sequence_tokens_equal(const whisper_sequence & a, const whisper_sequence & b) {
    if (a.tokens.size() != b.tokens.size()) {
        return false;
    }
    // sequences are more likely to diverge at the end
    for (int i = a.tokens.size() - 1; i >= 0; i--) {
        if (a.tokens[i].id != b.tokens[i].id) {
            return false;
        }
    }
    return true;
}

static whisper_token_data whisper_sample_token(
            whisper_context & ctx,
      const whisper_decoder & decoder,
                       bool   best) {
    whisper_token_data result = {
        0, 0, 0.0f, 0.0f, 0.0f, 0.0f, -1, -1, -1, 0.0f,
    };

    const auto & vocab = ctx.vocab;

    const auto & probs    = decoder.probs;
    const auto & logprobs = decoder.logprobs;

    const int n_logits = vocab.n_vocab;

    {
        double sum_ts = 0.0;
        double max_ts = 0.0;

        for (int i = vocab.token_beg; i < n_logits; i++) {
            if (probs[i] == -INFINITY) {
                continue;
            }

            sum_ts += probs[i];
            if (max_ts < probs[i]) {
                max_ts = probs[i];
                result.tid = i;
            }
        }

        result.pt    = max_ts/(sum_ts + 1e-10);
        result.ptsum = sum_ts;
    }

    if (best) {
        for (int i = 0; i < n_logits; ++i) {
            if (result.p < probs[i]) {
                result.id   = i;
                result.p    = probs[i];
                result.plog = logprobs[i];
            }
        }
    } else {
        std::discrete_distribution<> dist(probs.begin(), probs.end());

        result.id   = dist(decoder.rng);
        result.p    = probs[result.id];
        result.plog = logprobs[result.id];
    }

    if (result.id >= vocab.token_beg) {
        result.tid = result.id;
        result.pt  = result.p;
    }

    return result;
}

static std::vector<whisper_token_data> whisper_sample_token_topk(
            whisper_context & ctx,
            whisper_decoder & decoder,
                        int   k) {
    const auto & vocab = ctx.vocab;

    const auto & probs    = decoder.probs;
    const auto & logits   = decoder.logits;
    const auto & logprobs = decoder.logprobs;

    const int n_logits = vocab.n_vocab;

    auto & logits_id = decoder.logits_id;

    logits_id.resize(n_logits);
    for (int i = 0; i < n_logits; ++i) {
        logits_id[i].first = logits[i];
        logits_id[i].second = i;
    }

    {
        using pair_type = std::remove_reference<decltype(logits_id)>::type::value_type;
        std::partial_sort(
                logits_id.begin(),
                logits_id.begin() + k, logits_id.end(),
                [](const pair_type & a, const pair_type & b) {
            return a.first > b.first;
        });
    }

    std::vector<whisper_token_data> result;
    result.reserve(k);

    whisper_token tid = vocab.token_beg;

    float pt    = 0.0;
    float ptsum = 0.0;

    {
        double sum_ts = 0.0;
        double max_ts = 0.0;

        for (int i = vocab.token_beg; i < n_logits; i++) {
            if (probs[i] == -INFINITY) {
                continue;
            }

            sum_ts += probs[i];
            if (max_ts < probs[i]) {
                max_ts = probs[i];
                tid = i;
            }
        }

        pt    = max_ts/(sum_ts + 1e-10);
        ptsum = sum_ts;
    }

    std::discrete_distribution<> dist(probs.begin(), probs.end());

    for (int i = 0; i < k; ++i) {
        const auto id = dist(decoder.rng);
        //printf("XXX %d %d %f %f %f %f\n", id, tid, probs[id], logprobs[id], pt, ptsum);

        result.push_back({ id, tid, probs[id], logprobs[id], pt, ptsum, -1, -1, -1, 0.0f, });

        if (result[i].id >= vocab.token_beg) {
            result[i].tid = result[i].id;
            result[i].pt  = result[i].p;
        }
    }

    return result;
}

// ref: https://github.com/openai/whisper/blob/0b1ba3d46ebf7fe6f953acfd8cad62a4f851b49f/whisper/decoding.py#L178-L192
static void whisper_sequence_score(
        const struct whisper_full_params & params,
                        whisper_sequence & sequence) {
    if (sequence.result_len == 0) {
        return;
    }

    double result = 0.0f;

    for (int i = 0; i < sequence.result_len; ++i) {
        result += sequence.tokens[i].plog;
    }

    sequence.sum_logprobs = result;
    sequence.avg_logprobs = result/sequence.result_len;

    double penalty = sequence.result_len;

    if (params.length_penalty > 0.0f) {
        penalty = pow((5.0 + penalty)/6.0, params.length_penalty);
    }

    sequence.score = result/penalty;

    // compute the entropy of the sequence of the last 32 tokens
    {
        const int n = 32;

        int cnt = 0;
        double entropy = 0.0f;

        std::map<whisper_token, int> token_counts;
        for (int i = std::max(0, sequence.result_len - n); i < sequence.result_len; ++i) {
            token_counts[sequence.tokens[i].id]++;
            cnt++;
        }

        for (const auto & kv : token_counts) {
            const auto p = kv.second/(double)cnt;
            entropy -= p*log(p);

            //WHISPER_LOG_DEBUG("entropy: %d %f %f, count %d\n", kv.first, p, log(p), kv.second);
        }

        sequence.entropy = entropy;
    }
}

int whisper_full_with_state(
        struct whisper_context * ctx,
          struct whisper_state * state,
    struct whisper_full_params   params,
                   const float * samples,
                           int   n_samples) {
    // clear old results
    auto & result_all = state->result_all;

    result_all.clear();

    if (n_samples > 0) {
        // compute log mel spectrogram
        if (params.speed_up) {
            // TODO: Replace PV with more advanced algorithm
            WHISPER_LOG_ERROR("%s: failed to compute log mel spectrogram\n", __func__);
            return -1;
        } else {
            if (whisper_pcm_to_mel_with_state(ctx, state, samples, n_samples, params.n_threads) != 0) {
                WHISPER_LOG_ERROR("%s: failed to compute log mel spectrogram\n", __func__);
                return -2;
            }
        }
    }

    // auto-detect language if not specified
    if (params.language == nullptr || strlen(params.language) == 0 || strcmp(params.language, "auto") == 0 || params.detect_language) {
        std::vector<float> probs(whisper_lang_max_id() + 1, 0.0f);

        const auto lang_id = whisper_lang_auto_detect_with_state(ctx, state, 0, params.n_threads, probs.data());
        if (lang_id < 0) {
            WHISPER_LOG_ERROR("%s: failed to auto-detect language\n", __func__);
            return -3;
        }
        state->lang_id = lang_id;
        params.language = whisper_lang_str(lang_id);

        WHISPER_LOG_INFO("%s: auto-detected language: %s (p = %f)\n", __func__, params.language, probs[whisper_lang_id(params.language)]);
        if (params.detect_language) {
            return 0;
        }
    }

    if (params.token_timestamps) {
        state->t_beg    = 0;
        state->t_last   = 0;
        state->tid_last = 0;
        if (n_samples > 0) {
            state->energy = get_signal_energy(samples, n_samples, 32);
        }
    }

    const int seek_start = params.offset_ms/10;
    const int seek_end = params.duration_ms == 0 ? whisper_n_len_from_state(state) : seek_start + params.duration_ms/10;

    // if length of spectrogram is less than 1.0s (100 frames), then return
    // basically don't process anything that is less than 1.0s
    // see issue #39: https://github.com/ggerganov/whisper.cpp/issues/39
    if (seek_end < seek_start + (params.speed_up ? 50 : 100)) {
        WHISPER_LOG_DEBUG("%s: input is too short - %d ms < 1000 ms\n", __func__, (seek_end - seek_start)*10);
        return 0;
    }

    // a set of temperatures to use
    // [ t0, t0 + delta, t0 + 2*delta, ..., < 1.0f + 1e-6f ]
    std::vector<float> temperatures;
    if (params.temperature_inc > 0.0f) {
        for (float t = params.temperature; t < 1.0f + 1e-6f; t += params.temperature_inc) {
            temperatures.push_back(t);
        }
    } else {
        temperatures.push_back(params.temperature);
    }

    // initialize the decoders
    int n_decoders = 1;

    switch (params.strategy) {
        case WHISPER_SAMPLING_GREEDY:
            {
                n_decoders = params.greedy.best_of;
            } break;
        case WHISPER_SAMPLING_BEAM_SEARCH:
            {
                n_decoders = std::max(params.greedy.best_of, params.beam_search.beam_size);
            } break;
    };

    n_decoders = std::max(1, n_decoders);

    if (n_decoders > WHISPER_MAX_DECODERS) {
        WHISPER_LOG_ERROR("%s: too many decoders requested (%d), max = %d\n", __func__, n_decoders, WHISPER_MAX_DECODERS);
        return -4;
    }

    // TAGS: WHISPER_DECODER_INIT
    for (int j = 1; j < n_decoders; j++) {
        auto & decoder = state->decoders[j];

        decoder.sequence.tokens.reserve(state->decoders[0].sequence.tokens.capacity());

        decoder.probs.resize   (ctx->vocab.n_vocab);
        decoder.logits.resize  (ctx->vocab.n_vocab);
        decoder.logprobs.resize(ctx->vocab.n_vocab);
        decoder.logits_id.reserve(ctx->model.hparams.n_vocab);

        decoder.rng = std::mt19937(0);
    }

    // the accumulated text context so far
    auto & prompt_past = state->prompt_past;
    if (params.no_context) {
        prompt_past.clear();
    }

    // prepare prompt
    {
        std::vector<whisper_token> prompt_tokens;

        // initial prompt
        if (!params.prompt_tokens && params.initial_prompt) {
            prompt_tokens.resize(1024);
            int n_needed = whisper_tokenize(ctx, params.initial_prompt, prompt_tokens.data(), prompt_tokens.size());
            if (n_needed < 0) {
                prompt_tokens.resize(-n_needed);
                n_needed = whisper_tokenize(ctx, params.initial_prompt, prompt_tokens.data(), prompt_tokens.size());
            }
            prompt_tokens.resize(n_needed);
            params.prompt_tokens   = prompt_tokens.data();
            params.prompt_n_tokens = prompt_tokens.size();
        }

        // prepend the prompt tokens to the prompt_past
        if (params.prompt_tokens && params.prompt_n_tokens > 0) {
            // parse tokens from the pointer
            for (int i = 0; i < params.prompt_n_tokens; i++) {
                prompt_past.push_back(params.prompt_tokens[i]);
            }
            std::rotate(prompt_past.begin(), prompt_past.end() - params.prompt_n_tokens, prompt_past.end());
        }
    }

    // overwrite audio_ctx, max allowed is hparams.n_audio_ctx
    if (params.audio_ctx > whisper_n_audio_ctx(ctx)) {
        WHISPER_LOG_ERROR("%s: audio_ctx is larger than the maximum allowed (%d > %d)\n", __func__, params.audio_ctx, whisper_n_audio_ctx(ctx));
        return -5;
    }
    state->exp_n_audio_ctx = params.audio_ctx;

    // these tokens determine the task that will be performed
    std::vector<whisper_token> prompt_init = { whisper_token_sot(ctx), };

    if (whisper_is_multilingual(ctx)) {
        const int lang_id = whisper_lang_id(params.language);
        state->lang_id = lang_id;
        prompt_init.push_back(whisper_token_lang(ctx, lang_id));
        if (params.translate) {
            prompt_init.push_back(whisper_token_translate(ctx));
        } else {
            prompt_init.push_back(whisper_token_transcribe(ctx));
        }
    }

    // first release distilled models require the "no_timestamps" token
    {
        const bool is_distil = ctx->model.hparams.n_text_layer == 2 && ctx->model.hparams.n_vocab != 51866;
        if (is_distil && !params.no_timestamps) {
            WHISPER_LOG_WARN("%s: using first release distilled models - forcing no_timestamps\n", __func__);
            params.no_timestamps = true;
        }
    }

    if (params.no_timestamps) {
        prompt_init.push_back(whisper_token_not(ctx));
    }

    int seek = seek_start;

    std::vector<whisper_token> prompt;
    prompt.reserve(whisper_n_text_ctx(ctx));

    struct beam_candidate {
        int decoder_idx;
        int seek_delta;

        bool has_ts;

        whisper_sequence sequence;
        whisper_grammar grammar;
    };

    std::vector<std::vector<beam_candidate>> bc_per_dec(n_decoders);
    std::vector<beam_candidate> beam_candidates;

    // main loop
    while (true) {
        if (params.progress_callback) {
            const int progress_cur = (100*(seek - seek_start))/(seek_end - seek_start);

            params.progress_callback(
                ctx, state, progress_cur, params.progress_callback_user_data);
        }

        // if only 1 second left, then stop
        if (seek + 100 >= seek_end) {
            break;
        }

        if (params.encoder_begin_callback) {
            if (params.encoder_begin_callback(ctx, state, params.encoder_begin_callback_user_data) == false) {
                WHISPER_LOG_ERROR("%s: encoder_begin_callback returned false - aborting\n", __func__);
                break;
            }
        }

        // encode audio features starting at offset seek
        if (!whisper_encode_internal(*ctx, *state, seek, params.n_threads, params.abort_callback, params.abort_callback_user_data)) {
            WHISPER_LOG_ERROR("%s: failed to encode\n", __func__);
            return -6;
        }

        // if there is a very short audio segment left to process, we remove any past prompt since it tends
        // to confuse the decoder and often make it repeat or hallucinate stuff
        if (seek > seek_start && seek + 500 >= seek_end) {
            prompt_past.clear();
        }

        int best_decoder_id = 0;

        for (int it = 0; it < (int) temperatures.size(); ++it) {
            const float t_cur = temperatures[it];

            int n_decoders_cur = 1;

            switch (params.strategy) {
                case whisper_sampling_strategy::WHISPER_SAMPLING_GREEDY:
                    {
                        if (t_cur > 0.0f) {
                            n_decoders_cur = params.greedy.best_of;
                        }
                    } break;
                case whisper_sampling_strategy::WHISPER_SAMPLING_BEAM_SEARCH:
                    {
                        if (t_cur > 0.0f) {
                            n_decoders_cur = params.greedy.best_of;
                        } else {
                            n_decoders_cur = params.beam_search.beam_size;
                        }
                    } break;
            };

            n_decoders_cur = std::max(1, n_decoders_cur);

            WHISPER_LOG_DEBUG("\n%s: strategy = %d, decoding with %d decoders, temperature = %.2f\n", __func__, params.strategy, n_decoders_cur, t_cur);

            // TAGS: WHISPER_DECODER_INIT
            for (int j = 0; j < n_decoders_cur; ++j) {
                auto & decoder = state->decoders[j];

                decoder.sequence.tokens.clear();
                decoder.sequence.result_len       = 0;
                decoder.sequence.sum_logprobs_all = 0.0;
                decoder.sequence.sum_logprobs     = -INFINITY;
                decoder.sequence.avg_logprobs     = -INFINITY;
                decoder.sequence.entropy          = 0.0;
                decoder.sequence.score            = -INFINITY;

                decoder.seek_delta = 100*WHISPER_CHUNK_SIZE;

                decoder.failed    = false;
                decoder.completed = false;
                decoder.has_ts    = false;

                if (params.grammar_rules != nullptr) {
                    decoder.grammar = whisper_grammar_init(params.grammar_rules, params.n_grammar_rules, params.i_start_rule);
                } else {
                    decoder.grammar = {};
                }
            }

            // init prompt and kv cache for the current iteration
            // TODO: do not recompute the prompt if it is the same as previous time
            {
                prompt.clear();

                // if we have already generated some text, use it as a prompt to condition the next generation
                if (!prompt_past.empty() && t_cur < 0.5f && params.n_max_text_ctx > 0) {
                    int n_take = std::min(std::min(params.n_max_text_ctx, whisper_n_text_ctx(ctx)/2), int(prompt_past.size()));

                    prompt = { whisper_token_prev(ctx) };
                    prompt.insert(prompt.begin() + 1, prompt_past.end() - n_take, prompt_past.end());
                }

                // init new transcription with sot, language (opt) and task tokens
                prompt.insert(prompt.end(), prompt_init.begin(), prompt_init.end());

                // print the prompt
                WHISPER_LOG_DEBUG("\n\n");
                for (int i = 0; i < (int) prompt.size(); i++) {
                    WHISPER_LOG_DEBUG("%s: prompt[%d] = %s\n", __func__, i, ctx->vocab.id_to_token.at(prompt[i]).c_str());
                }
                WHISPER_LOG_DEBUG("\n\n");

                whisper_kv_cache_clear(state->kv_self);

                whisper_batch_prep_legacy(state->batch, prompt.data(), prompt.size(), 0, 0);

                if (!whisper_decode_internal(*ctx, *state, state->batch, params.n_threads, false, params.abort_callback, params.abort_callback_user_data)) {
                    WHISPER_LOG_ERROR("%s: failed to decode\n", __func__);
                    return -7;
                }

                {
                    const int64_t t_start_sample_us = ggml_time_us();

                    state->decoders[0].i_batch = prompt.size() - 1;

                    whisper_process_logits(*ctx, *state, state->decoders[0], params, t_cur);

                    for (int j = 1; j < n_decoders_cur; ++j) {
                        auto & decoder = state->decoders[j];

                        whisper_kv_cache_seq_cp(state->kv_self, 0, j, -1, -1);

                        memcpy(decoder.probs.data(),    state->decoders[0].probs.data(),    decoder.probs.size()*sizeof(decoder.probs[0]));
                        memcpy(decoder.logits.data(),   state->decoders[0].logits.data(),   decoder.logits.size()*sizeof(decoder.logits[0]));
                        memcpy(decoder.logprobs.data(), state->decoders[0].logprobs.data(), decoder.logprobs.size()*sizeof(decoder.logprobs[0]));
                    }

                    state->t_sample_us += ggml_time_us() - t_start_sample_us;
                }
            }

            for (int i = 0, n_max = whisper_n_text_ctx(ctx)/2 - 4; i < n_max; ++i) {
                const int64_t t_start_sample_us = ggml_time_us();

                if (params.strategy == whisper_sampling_strategy::WHISPER_SAMPLING_BEAM_SEARCH) {
                    for (auto & bc : bc_per_dec) {
                        bc.clear();
                    }
                }

                // sampling
                // TODO: avoid memory allocations, optimize, avoid threads?
                {
                    std::atomic<int> j_cur(0);

                    auto process = [&]() {
                        while (true) {
                            const int j = j_cur.fetch_add(1);

                            if (j >= n_decoders_cur) {
                                break;
                            }

                            auto & decoder = state->decoders[j];

                            if (decoder.completed || decoder.failed) {
                                continue;
                            }

                            switch (params.strategy) {
                                case whisper_sampling_strategy::WHISPER_SAMPLING_GREEDY:
                                    {
                                        if (t_cur < 1e-6f) {
                                            decoder.sequence.tokens.push_back(whisper_sample_token(*ctx, decoder, true));
                                        } else {
                                            decoder.sequence.tokens.push_back(whisper_sample_token(*ctx, decoder, false));
                                        }

                                        decoder.sequence.sum_logprobs_all += decoder.sequence.tokens.back().plog;
                                    } break;
                                case whisper_sampling_strategy::WHISPER_SAMPLING_BEAM_SEARCH:
                                    {
                                        const auto tokens_new = whisper_sample_token_topk(*ctx, decoder, params.beam_search.beam_size);

                                        for (const auto & token : tokens_new) {
                                            bc_per_dec[j].push_back({ j, decoder.seek_delta, decoder.has_ts, decoder.sequence, decoder.grammar, });
                                            bc_per_dec[j].back().sequence.tokens.push_back(token);
                                            bc_per_dec[j].back().sequence.sum_logprobs_all += token.plog;
                                        }
                                    } break;
                            };
                        }
                    };

                    const int n_threads = std::min(params.n_threads, n_decoders_cur);

                    if (n_threads == 1) {
                        process();
                    } else {
                        std::vector<std::thread> threads(n_threads - 1);

                        for (int t = 0; t < n_threads - 1; ++t) {
                            threads[t] = std::thread(process);
                        }

                        process();

                        for (int t = 0; t < n_threads - 1; ++t) {
                            threads[t].join();
                        }
                    }
                }

                beam_candidates.clear();
                for (const auto & bc : bc_per_dec) {
                    beam_candidates.insert(beam_candidates.end(), bc.begin(), bc.end());

                    if (!bc.empty()) {
                        state->n_sample += 1;
                    }
                }

                // for beam-search, choose the top candidates and update the KV caches
                if (params.strategy == whisper_sampling_strategy::WHISPER_SAMPLING_BEAM_SEARCH) {
                    std::sort(
                            beam_candidates.begin(),
                            beam_candidates.end(),
                            [](const beam_candidate & a, const beam_candidate & b) {
                        if (a.sequence.sum_logprobs_all != b.sequence.sum_logprobs_all) {
                            return a.sequence.sum_logprobs_all > b.sequence.sum_logprobs_all;
                        }
                        return a.decoder_idx < b.decoder_idx;
                    });

                    uint32_t cur_c = 0;

                    for (int j = 0; j < n_decoders_cur; ++j) {
                        auto & decoder = state->decoders[j];

                        if (decoder.completed || decoder.failed) {
                            continue;
                        }

                        if (cur_c >= beam_candidates.size()) {
                            cur_c = 0;
                        }

                        auto & cur = beam_candidates[cur_c++];

                        while (beam_candidates.size() > cur_c && whisper_sequence_tokens_equal(beam_candidates[cur_c].sequence, cur.sequence) && i > 0) {
                            ++cur_c;
                        }

                        decoder.seek_delta = cur.seek_delta;
                        decoder.has_ts     = cur.has_ts;
                        decoder.sequence   = cur.sequence;
                        decoder.grammar    = cur.grammar;

                        whisper_kv_cache_seq_cp(state->kv_self, cur.decoder_idx, WHISPER_MAX_DECODERS + j, -1, -1);

                        WHISPER_LOG_DEBUG("%s: beam search: decoder %d: from decoder %d: token = %10s, plog = %8.5f, sum_logprobs = %8.5f\n",
                                __func__, j, cur.decoder_idx, ctx->vocab.id_to_token.at(decoder.sequence.tokens.back().id).c_str(), decoder.sequence.tokens.back().plog, decoder.sequence.sum_logprobs_all);
                    }

                    for (int j = 0; j < n_decoders_cur; ++j) {
                        auto & decoder = state->decoders[j];

                        if (decoder.completed || decoder.failed) {
                            continue;
                        }

                        whisper_kv_cache_seq_rm(state->kv_self, j,                           -1, -1);
                        whisper_kv_cache_seq_cp(state->kv_self, WHISPER_MAX_DECODERS + j, j, -1, -1);
                        whisper_kv_cache_seq_rm(state->kv_self, WHISPER_MAX_DECODERS + j,    -1, -1);
                    }
                }

                // update the decoder state
                // - check if the sequence is completed
                // - check if the sequence is failed
                // - update sliding window based on timestamp tokens
                for (int j = 0; j < n_decoders_cur; ++j) {
                    auto & decoder = state->decoders[j];

                    if (decoder.completed || decoder.failed) {
                        continue;
                    }

                    auto & has_ts     = decoder.has_ts;
                    auto & failed     = decoder.failed;
                    auto & completed  = decoder.completed;
                    auto & seek_delta = decoder.seek_delta;
                    auto & result_len = decoder.sequence.result_len;

                    {
                        const auto & token = decoder.sequence.tokens.back();

                        // timestamp token - update sliding window
                        if (token.id > whisper_token_beg(ctx)) {
                            const int seek_delta_new = 2*(token.id - whisper_token_beg(ctx));

                            // do not allow to go back in time
                            if (has_ts && seek_delta > seek_delta_new && result_len < i) {
                                WHISPER_LOG_DEBUG("%s: decoder %d: failed due to seek_delta (%d > %d)\n", __func__, j, seek_delta, seek_delta_new);
                                failed = true; // TODO: maybe this is not a failure ?
                                continue;
                            }

                            seek_delta = seek_delta_new;
                            result_len = i + 1;
                            has_ts = true;
                        }

                        whisper_grammar_accept_token(*ctx, decoder.grammar, token.id);

#ifdef WHISPER_DEBUG
                        {
                            const auto tt = token.pt > 0.10 ? ctx->vocab.id_to_token.at(token.tid) : "[?]";
                            WHISPER_LOG_DEBUG("%s: id = %3d, decoder = %d, token = %6d, p = %6.3f, ts = %10s, %6.3f, result_len = %4d '%s'\n",
                                    __func__, i, j, token.id, token.p, tt.c_str(), token.pt, result_len, ctx->vocab.id_to_token.at(token.id).c_str());
                        }
#endif

                        // end of segment
                        if (token.id == whisper_token_eot(ctx) ||               // end of text token
                           (params.max_tokens > 0 && i >= params.max_tokens) || // max tokens per segment reached
                           (has_ts && seek + seek_delta + 100 >= seek_end)      // end of audio reached
                           ) {
                            if (result_len == 0 && !params.no_timestamps) {
                                if (seek + seek_delta + 100 >= seek_end) {
                                    result_len = i + 1;
                                } else {
                                    WHISPER_LOG_DEBUG("%s: decoder %d failed (result_len = 0)\n", __func__, j);
                                    failed = true;
                                    continue;
                                }
                            }

                            if (params.single_segment || params.no_timestamps) {
                                result_len = i + 1;
                                seek_delta = 100*WHISPER_CHUNK_SIZE;
                            }

                            WHISPER_LOG_DEBUG("%s: decoder %d completed\n", __func__, j);
                            completed = true;
                            continue;
                        }

                        // TESTS: if no tensors are loaded, it means we are running tests
                        if (ctx->model.n_loaded == 0) {
                            seek_delta = 100*WHISPER_CHUNK_SIZE;
                            completed = true;
                            continue;
                        }
                    }

                    // sometimes, the decoding can get stuck in a repetition loop
                    // this is an attempt to mitigate such cases - we flag the decoding as failed and use a fallback strategy
                    if (i == n_max - 1 && (result_len == 0 || seek_delta < 100*WHISPER_CHUNK_SIZE/2)) {
                        WHISPER_LOG_DEBUG("%s: decoder %d: failed due to repetition loop\n", __func__, j);
                        failed = true;
                        continue;
                    }
                }

                // check if all decoders have finished (i.e. completed or failed)
                {
                    bool completed_all = true;

                    for (int j = 0; j < n_decoders_cur; ++j) {
                        auto & decoder = state->decoders[j];

                        if (decoder.completed || decoder.failed) {
                            continue;
                        }

                        completed_all = false;
                    }

                    if (completed_all) {
                        break;
                    }
                }

                state->t_sample_us += ggml_time_us() - t_start_sample_us;

                // obtain logits for the next token
                {
                    auto & batch = state->batch;

                    batch.n_tokens = 0;

                    const int n_past = prompt.size() + i;

                    for (int j = 0; j < n_decoders_cur; ++j) {
                        auto & decoder = state->decoders[j];

                        if (decoder.failed || decoder.completed) {
                            continue;
                        }

                        //WHISPER_LOG_DEBUG("%s: decoder %d: token %d, seek_delta %d\n", __func__, j, decoder.sequence.tokens.back().id, decoder.seek_delta);

                        decoder.i_batch = batch.n_tokens;

                        batch.token   [batch.n_tokens]    = decoder.sequence.tokens.back().id;
                        batch.pos     [batch.n_tokens]    = n_past;
                        batch.n_seq_id[batch.n_tokens]    = 1;
                        batch.seq_id  [batch.n_tokens][0] = j;
                        batch.logits  [batch.n_tokens]    = 1;
                        batch.n_tokens++;
                    }

                    assert(batch.n_tokens > 0);

                    if (!whisper_decode_internal(*ctx, *state, state->batch, params.n_threads, false, params.abort_callback, params.abort_callback_user_data)) {
                        WHISPER_LOG_ERROR("%s: failed to decode\n", __func__);
                        return -8;
                    }

                    const int64_t t_start_sample_us = ggml_time_us();

                    // TODO: avoid memory allocations, optimize, avoid threads?
                    {
                        std::atomic<int> j_cur(0);

                        auto process = [&]() {
                            while (true) {
                                const int j = j_cur.fetch_add(1);

                                if (j >= n_decoders_cur) {
                                    break;
                                }

                                auto & decoder = state->decoders[j];

                                if (decoder.failed || decoder.completed) {
                                    continue;
                                }

                                whisper_process_logits(*ctx, *state, decoder, params, t_cur);
                            }
                        };

                        const int n_threads = std::min(params.n_threads, n_decoders_cur);

                        if (n_threads == 1) {
                            process();
                        } else {
                            std::vector<std::thread> threads(n_threads - 1);

                            for (int t = 0; t < n_threads - 1; ++t) {
                                threads[t] = std::thread(process);
                            }

                            process();

                            for (int t = 0; t < n_threads - 1; ++t) {
                                threads[t].join();
                            }
                        }
                    }

                    state->t_sample_us += ggml_time_us() - t_start_sample_us;
                }
            }

            // rank the resulting sequences and select the best one
            {
                double best_score = -INFINITY;

                for (int j = 0; j < n_decoders_cur; ++j) {
                    auto & decoder = state->decoders[j];

                    if (decoder.failed) {
                        continue;
                    }

                    decoder.sequence.tokens.resize(decoder.sequence.result_len);
                    whisper_sequence_score(params, decoder.sequence);

                    WHISPER_LOG_DEBUG("%s: decoder %2d: score = %8.5f, result_len = %3d, avg_logprobs = %8.5f, entropy = %8.5f\n",
                            __func__, j, decoder.sequence.score, decoder.sequence.result_len, decoder.sequence.avg_logprobs, decoder.sequence.entropy);

                    if (decoder.sequence.result_len > 32 && decoder.sequence.entropy < params.entropy_thold) {
                        WHISPER_LOG_DEBUG("%s: decoder %2d: failed due to entropy %8.5f < %8.5f\n",
                                __func__, j, decoder.sequence.entropy, params.entropy_thold);

                        decoder.failed = true;
                        state->n_fail_h++;

                        continue;
                    }

                    if (best_score < decoder.sequence.score) {
                        best_score = decoder.sequence.score;
                        best_decoder_id = j;
                    }
                }

                WHISPER_LOG_DEBUG("%s: best decoder = %d\n", __func__, best_decoder_id);
            }

            bool success = true;

            // was the decoding successful for the current temperature?
            // do fallback only if:
            // - we are not at the last temperature
            if (it != (int) temperatures.size() - 1) {
                const auto & decoder = state->decoders[best_decoder_id];

                if (decoder.failed || decoder.sequence.avg_logprobs < params.logprob_thold) {
                    WHISPER_LOG_DEBUG("%s: failed due to avg_logprobs %8.5f < %8.5f\n", __func__, decoder.sequence.avg_logprobs, params.logprob_thold);
                    success = false;
                    state->n_fail_p++;
                }
            }

            if (success) {
                //for (auto & token : ctx->decoders[best_decoder_id].sequence.tokens) {
                //    WHISPER_LOG_DEBUG("%s: token = %d, p = %6.3f, pt = %6.3f, ts = %s, str = %s\n", __func__, token.id, token.p, token.pt, ctx->vocab.id_to_token.at(token.tid).c_str(), ctx->vocab.id_to_token.at(token.id).c_str());
                //}

                break;
            }

            WHISPER_LOG_DEBUG("\n%s: failed to decode with temperature = %.2f\n", __func__, t_cur);
        }

        // output results through a user-provided callback
        {
            const auto & best_decoder = state->decoders[best_decoder_id];

            const auto seek_delta = best_decoder.seek_delta;
            const auto result_len = best_decoder.sequence.result_len;

            const auto & tokens_cur = best_decoder.sequence.tokens;

            // [EXPERIMENTAL] Token-level timestamps with DTW
            const auto n_segments_before = state->result_all.size();

            //WHISPER_LOG_DEBUG("prompt_init.size() = %d, prompt.size() = %d, result_len = %d, seek_delta = %d\n", prompt_init.size(), prompt.size(), result_len, seek_delta);

            // update prompt_past
            prompt_past.clear();
            if (prompt.front() == whisper_token_prev(ctx)) {
                prompt_past.insert(prompt_past.end(), prompt.begin() + 1, prompt.end() - prompt_init.size());
            }

            for (int i = 0; i < result_len; ++i) {
                prompt_past.push_back(tokens_cur[i].id);
            }

            if (!tokens_cur.empty() && ctx->model.n_loaded > 0) {
                int  i0 = 0;
                auto t0 = seek + 2*(tokens_cur.front().tid - whisper_token_beg(ctx));

                std::string text;
                bool speaker_turn_next = false;

                for (int i = 0; i < (int) tokens_cur.size(); i++) {
                    //printf("%s: %18s %6.3f %18s %6.3f\n", __func__,
                    //        ctx->vocab.id_to_token[tokens_cur[i].id].c_str(), tokens_cur[i].p,
                    //        ctx->vocab.id_to_token[tokens_cur[i].tid].c_str(), tokens_cur[i].pt);

                    if (params.print_special || tokens_cur[i].id < whisper_token_eot(ctx)) {
                        text += whisper_token_to_str(ctx, tokens_cur[i].id);
                    }

                    // [TDRZ] record if speaker turn was predicted after current segment
                    if (params.tdrz_enable && tokens_cur[i].id == whisper_token_solm(ctx)) {
                        speaker_turn_next = true;
                    }

                    if (tokens_cur[i].id > whisper_token_beg(ctx) && !params.single_segment) {
                        const auto t1 = seek + 2*(tokens_cur[i].tid - whisper_token_beg(ctx));

                        if (!text.empty()) {
                            const auto tt0 = params.speed_up ? 2*t0 : t0;
                            const auto tt1 = params.speed_up ? 2*t1 : t1;

                            if (params.print_realtime) {
                                if (params.print_timestamps) {
                                    printf("[%s --> %s]  %s\n", to_timestamp(tt0).c_str(), to_timestamp(tt1).c_str(), text.c_str());
                                } else {
                                    printf("%s", text.c_str());
                                    fflush(stdout);
                                }
                            }

                            //printf("tt0 = %d, tt1 = %d, text = %s, token = %s, token_id = %d, tid = %d\n", tt0, tt1, text.c_str(), ctx->vocab.id_to_token[tokens_cur[i].id].c_str(), tokens_cur[i].id, tokens_cur[i].tid);

                            result_all.push_back({ tt0, tt1, text, {}, speaker_turn_next });
                            for (int j = i0; j <= i; j++) {
                                result_all.back().tokens.push_back(tokens_cur[j]);
                            }

                            int n_new = 1;

                            if (params.token_timestamps) {
                                whisper_exp_compute_token_level_timestamps(
                                        *ctx, *state, result_all.size() - 1, params.thold_pt, params.thold_ptsum);

                                if (params.max_len > 0) {
                                    n_new = whisper_wrap_segment(*ctx, *state, params.max_len, params.split_on_word);
                                }
                            }
                            if (params.new_segment_callback) {
                                params.new_segment_callback(ctx, state, n_new, params.new_segment_callback_user_data);
                            }
                        }
                        text = "";
                        while (i < (int) tokens_cur.size() && tokens_cur[i].id > whisper_token_beg(ctx)) {
                            i++;
                        }
                        i--;
                        t0 = t1;
                        i0 = i + 1;
                        speaker_turn_next = false;
                    }
                }

                if (!text.empty()) {
                    const auto t1 = seek + seek_delta;

                    const auto tt0 = params.speed_up ? 2*t0 : t0;
                    const auto tt1 = params.speed_up ? 2*t1 : t1;

                    if (params.print_realtime) {
                        if (params.print_timestamps) {
                            printf("[%s --> %s]  %s\n", to_timestamp(tt0).c_str(), to_timestamp(tt1).c_str(), text.c_str());
                        } else {
                            printf("%s", text.c_str());
                            fflush(stdout);
                        }
                    }

                    result_all.push_back({ tt0, tt1, text, {} , speaker_turn_next });
                    for (int j = i0; j < (int) tokens_cur.size(); j++) {
                        result_all.back().tokens.push_back(tokens_cur[j]);
                    }

                    int n_new = 1;

                    if (params.token_timestamps) {
                        whisper_exp_compute_token_level_timestamps(
                                *ctx, *state, result_all.size() - 1, params.thold_pt, params.thold_ptsum);

                        if (params.max_len > 0) {
                            n_new = whisper_wrap_segment(*ctx, *state, params.max_len, params.split_on_word);
                        }
                    }
                    if (params.new_segment_callback) {
                        params.new_segment_callback(ctx, state, n_new, params.new_segment_callback_user_data);
                    }
                }
            }

            // FIXME: will timestamp offsets be correct?
            // [EXPERIMENTAL] Token-level timestamps with DTW
            {
                const auto n_segments = state->result_all.size() - n_segments_before;
                if (ctx->params.dtw_token_timestamps && n_segments) {
                    const int n_frames = std::min(std::min(WHISPER_CHUNK_SIZE * 100, seek_delta), seek_end - seek);
                    whisper_exp_compute_token_level_timestamps_dtw(
                            ctx, state, params, result_all.size() - n_segments, n_segments, seek, n_frames, 7, params.n_threads);
                }
            }

            // update audio window
            seek += seek_delta;

            WHISPER_LOG_DEBUG("seek = %d, seek_delta = %d\n", seek, seek_delta);
        }
    }

    return 0;
}

int whisper_full(
        struct whisper_context * ctx,
    struct whisper_full_params   params,
                   const float * samples,
                           int   n_samples) {
    return whisper_full_with_state(ctx, ctx->state, params, samples, n_samples);
}

int whisper_full_parallel(
        struct whisper_context * ctx,
        struct whisper_full_params params,
        const float * samples,
        int n_samples,
        int n_processors) {
    if (n_processors == 1) {
        return whisper_full(ctx, params, samples, n_samples);
    }
    int ret = 0;

    // prepare separate states for each thread
    std::vector<whisper_state*> states;

    const int offset_samples = (WHISPER_SAMPLE_RATE*params.offset_ms)/1000;
    const int n_samples_per_processor = (n_samples - offset_samples)/n_processors;

    // the calling thread will process the first chunk
    // while the other threads will process the remaining chunks

    std::vector<std::thread> workers(n_processors - 1);
    for (int i = 0; i < n_processors - 1; ++i) {
        // create a new state for each thread
        states.push_back(whisper_init_state(ctx));

        const int start_samples = offset_samples + (i + 1)*n_samples_per_processor;
        const int n_samples_cur = (i == n_processors - 2) ? n_samples - start_samples : n_samples_per_processor;

        auto params_cur = params;

        params_cur.offset_ms = 0;
        params_cur.print_progress = false;
        params_cur.print_realtime = false;

        params_cur.new_segment_callback = nullptr;
        params_cur.new_segment_callback_user_data = nullptr;

        params_cur.progress_callback = nullptr;
        params_cur.progress_callback_user_data = nullptr;

        workers[i] = std::thread(whisper_full_with_state, ctx, states[i], std::move(params_cur), samples + start_samples, n_samples_cur);
    }

    {
        auto params_cur = params;

        // We need to disable the print real-time for this one as well, otherwise it will show only for the first chunk.
        params_cur.print_realtime = false;

        // Run the first transformation using default state but only for the first chunk.
        ret = whisper_full_with_state(ctx, ctx->state, std::move(params_cur), samples, offset_samples + n_samples_per_processor);
    }

    for (int i = 0; i < n_processors - 1; ++i) {
        workers[i].join();
    }

    const int64_t offset_t = (int64_t) params.offset_ms/10.0;

    // combine results into result_state->result_all from all other states
    for (int i = 0; i < n_processors - 1; ++i) {
        auto& results_i = states[i]->result_all;

        for (auto& result : results_i) {
            // correct the segment timestamp taking into account the offset
            result.t0 += 100 * ((i + 1) * n_samples_per_processor) / WHISPER_SAMPLE_RATE + offset_t;
            result.t1 += 100 * ((i + 1) * n_samples_per_processor) / WHISPER_SAMPLE_RATE + offset_t;

            // make sure that segments are not overlapping
            if (!ctx->state->result_all.empty()) {
                result.t0 = std::max(result.t0, ctx->state->result_all.back().t1);
            }

            ctx->state->result_all.push_back(std::move(result));

            // call the new_segment_callback for each segment
            if (params.new_segment_callback) {
                params.new_segment_callback(ctx, ctx->state, 1, params.new_segment_callback_user_data);
            }
        }

        ctx->state->t_mel_us += states[i]->t_mel_us;

        ctx->state->t_sample_us += states[i]->t_sample_us;
        ctx->state->t_encode_us += states[i]->t_encode_us;
        ctx->state->t_decode_us += states[i]->t_decode_us;
        ctx->state->t_batchd_us += states[i]->t_batchd_us;
        ctx->state->t_prompt_us += states[i]->t_prompt_us;

        ctx->state->n_sample += states[i]->n_sample;
        ctx->state->n_encode += states[i]->n_encode;
        ctx->state->n_decode += states[i]->n_decode;
        ctx->state->n_batchd += states[i]->n_batchd;
        ctx->state->n_prompt += states[i]->n_prompt;

        whisper_free_state(states[i]);
    }

    // average the timings
    ctx->state->t_mel_us    /= n_processors;
    ctx->state->t_sample_us /= n_processors;
    ctx->state->t_encode_us /= n_processors;
    ctx->state->t_decode_us /= n_processors;

    // print information about the audio boundaries
    WHISPER_LOG_WARN("\n");
    WHISPER_LOG_WARN("%s: the audio has been split into %d chunks at the following times:\n", __func__, n_processors);
    for (int i = 0; i < n_processors - 1; ++i) {
        WHISPER_LOG_WARN("%s: split %d - %s\n", __func__, (i + 1), to_timestamp(100*((i + 1)*n_samples_per_processor)/WHISPER_SAMPLE_RATE + offset_t).c_str());
    }
    WHISPER_LOG_WARN("%s: the transcription quality may be degraded near these boundaries\n", __func__);

    return ret;
}

int whisper_full_n_segments_from_state(struct whisper_state * state) {
    return state->result_all.size();
}

int whisper_full_n_segments(struct whisper_context * ctx) {
    return ctx->state->result_all.size();
}

int whisper_full_lang_id_from_state(struct whisper_state * state) {
    return state->lang_id;
}

int whisper_full_lang_id(struct whisper_context * ctx) {
    return ctx->state->lang_id;
}

int64_t whisper_full_get_segment_t0_from_state(struct whisper_state * state, int i_segment) {
    return state->result_all[i_segment].t0;
}

int64_t whisper_full_get_segment_t0(struct whisper_context * ctx, int i_segment) {
    return ctx->state->result_all[i_segment].t0;
}

int64_t whisper_full_get_segment_t1_from_state(struct whisper_state * state, int i_segment) {
    return state->result_all[i_segment].t1;
}

int64_t whisper_full_get_segment_t1(struct whisper_context * ctx, int i_segment) {
    return ctx->state->result_all[i_segment].t1;
}

bool whisper_full_get_segment_speaker_turn_next_from_state(struct whisper_state * state, int i_segment) {
    return state->result_all[i_segment].speaker_turn_next;
}

bool whisper_full_get_segment_speaker_turn_next(struct whisper_context * ctx, int i_segment) {
    return ctx->state->result_all[i_segment].speaker_turn_next;
}

const char * whisper_full_get_segment_text_from_state(struct whisper_state * state, int i_segment) {
    return state->result_all[i_segment].text.c_str();
}

const char * whisper_full_get_segment_text(struct whisper_context * ctx, int i_segment) {
    return ctx->state->result_all[i_segment].text.c_str();
}

int whisper_full_n_tokens_from_state(struct whisper_state * state, int i_segment) {
    return state->result_all[i_segment].tokens.size();
}

int whisper_full_n_tokens(struct whisper_context * ctx, int i_segment) {
    return ctx->state->result_all[i_segment].tokens.size();
}

const char * whisper_full_get_token_text_from_state(struct whisper_context * ctx, struct whisper_state * state, int i_segment, int i_token) {
    return ctx->vocab.id_to_token[state->result_all[i_segment].tokens[i_token].id].c_str();
}

const char* whisper_full_get_token_text(struct whisper_context * ctx, int i_segment, int i_token) {
    return ctx->vocab.id_to_token[ctx->state->result_all[i_segment].tokens[i_token].id].c_str();
}

whisper_token whisper_full_get_token_id_from_state(struct whisper_state * state, int i_segment, int i_token) {
    return state->result_all[i_segment].tokens[i_token].id;
}

whisper_token whisper_full_get_token_id(struct whisper_context * ctx, int i_segment, int i_token) {
    return ctx->state->result_all[i_segment].tokens[i_token].id;
}

struct whisper_token_data whisper_full_get_token_data_from_state(struct whisper_state * state, int i_segment, int i_token) {
    return state->result_all[i_segment].tokens[i_token];
}

struct whisper_token_data whisper_full_get_token_data(struct whisper_context * ctx, int i_segment, int i_token) {
    return ctx->state->result_all[i_segment].tokens[i_token];
}

float whisper_full_get_token_p_from_state(struct whisper_state * state, int i_segment, int i_token) {
    return state->result_all[i_segment].tokens[i_token].p;
}

float whisper_full_get_token_p(struct whisper_context * ctx, int i_segment, int i_token) {
    return ctx->state->result_all[i_segment].tokens[i_token].p;
}

// =================================================================================================

//
// Temporary interface needed for exposing ggml interface
// Will be removed in the future when ggml becomes a separate library
//

WHISPER_API int whisper_bench_memcpy(int n_threads) {
    fputs(whisper_bench_memcpy_str(n_threads), stderr);
    return 0;
}

WHISPER_API const char * whisper_bench_memcpy_str(int n_threads) {
    static std::string s;
    s = "";
    char strbuf[256];

    ggml_time_init();

    size_t n    = 20;
    size_t arr  = n_threads > 0 ? 1024llu : n_threads; // trick to avoid compiler optimizations

    // 1GB array
    const size_t size = arr*1e6;

    double sum  = 0.0;

    // heat-up
    {
        char * src = (char *) malloc(size);
        char * dst = (char *) malloc(size);

        for (size_t i = 0; i < size; i++) src[i] = i;

        memcpy(dst, src, size); // heat-up

        double tsum = 0.0;

        for (size_t i = 0; i < n; i++) {
            const int64_t t0 = ggml_time_us();

            memcpy(dst, src, size);

            const int64_t t1 = ggml_time_us();

            tsum += (t1 - t0)*1e-6;

            src[rand() % size] = rand() % 256;
        }

        snprintf(strbuf, sizeof(strbuf), "memcpy: %7.2f GB/s (heat-up)\n", (double) (n*size)/(tsum*1e9));
        s += strbuf;

        // needed to prevent the compiler from optimizing the memcpy away
        {
            for (size_t i = 0; i < size; i++) sum += dst[i];
        }

        free(src);
        free(dst);
    }

    // single-thread
    {
        char * src = (char *) malloc(size);
        char * dst = (char *) malloc(size);

        for (size_t i = 0; i < size; i++) src[i] = i;

        memcpy(dst, src, size); // heat-up

        double tsum = 0.0;

        for (size_t i = 0; i < n; i++) {
            const int64_t t0 = ggml_time_us();

            memcpy(dst, src, size);

            const int64_t t1 = ggml_time_us();

            tsum += (t1 - t0)*1e-6;

            src[rand() % size] = rand() % 256;
        }

        snprintf(strbuf, sizeof(strbuf), "memcpy: %7.2f GB/s ( 1 thread)\n", (double) (n*size)/(tsum*1e9));
        s += strbuf;

        // needed to prevent the compiler from optimizing the memcpy away
        {
            for (size_t i = 0; i < size; i++) sum += dst[i];
        }

        free(src);
        free(dst);
    }

    // multi-thread

    for (int32_t k = 1; k <= n_threads; k++) {
        char * src = (char *) malloc(size);
        char * dst = (char *) malloc(size);

        for (size_t i = 0; i < size; i++) src[i] = i;

        memcpy(dst, src, size); // heat-up

        double tsum = 0.0;

        auto helper = [&](int th) {
            const int64_t i0 = (th + 0)*size/k;
            const int64_t i1 = (th + 1)*size/k;

            for (size_t i = 0; i < n; i++) {
                memcpy(dst + i0, src + i0, i1 - i0);

                src[i0 + rand() % (i1 - i0)] = rand() % 256;
            };
        };

        const int64_t t0 = ggml_time_us();

        std::vector<std::thread> threads(k - 1);
        for (int32_t th = 0; th < k - 1; ++th) {
            threads[th] = std::thread(helper, th);
        }

        helper(k - 1);

        for (int32_t th = 0; th < k - 1; ++th) {
            threads[th].join();
        }

        const int64_t t1 = ggml_time_us();

        tsum += (t1 - t0)*1e-6;

        snprintf(strbuf, sizeof(strbuf), "memcpy: %7.2f GB/s (%2d thread)\n", (double) (n*size)/(tsum*1e9), k);
        s += strbuf;

        // needed to prevent the compiler from optimizing the memcpy away
        {
            for (size_t i = 0; i < size; i++) sum += dst[i];
        }

        free(src);
        free(dst);
    }

    snprintf(strbuf, sizeof(strbuf), "sum:    %f\n", sum);
    s += strbuf;

    return s.c_str();
}

WHISPER_API int whisper_bench_ggml_mul_mat(int n_threads) {
    fputs(whisper_bench_ggml_mul_mat_str(n_threads), stderr);
    return 0;
}

WHISPER_API const char * whisper_bench_ggml_mul_mat_str(int n_threads) {
    static std::string s;
    s = "";
    char strbuf[256];

    ggml_time_init();

    const int n_max = 128;

    const std::vector<size_t> sizes = {
        64, 128, 256, 512, 1024, 2048, 4096,
    };

    const size_t N_max = sizes.back();

    // a: N*N*sizeof(float)
    // b: N*N*sizeof(float)
    // c: N*N*sizeof(float)
    // when F16 is used, there is an extra work buffer of size N*N*sizeof(float)
    std::vector<uint8_t> buf(3llu*N_max*N_max*sizeof(float) + 3*ggml_tensor_overhead() + ggml_graph_overhead());
    std::vector<uint8_t> work;

    // put a bunch of random data in the buffer
    for (size_t i = 0; i < buf.size(); i++) buf[i] = i;

    for (int j = 0; j < (int) sizes.size(); j++) {
        int n_q4_0 = 0;
        int n_q4_1 = 0;
        int n_q5_0 = 0;
        int n_q5_1 = 0;
        int n_q8_0 = 0;
        int n_fp16 = 0;
        int n_fp32 = 0;

        // GFLOPS/s
        double s_q4_0 = 0.0;
        double s_q4_1 = 0.0;
        double s_q5_0 = 0.0;
        double s_q5_1 = 0.0;
        double s_q8_0 = 0.0;
        double s_fp16 = 0.0;
        double s_fp32 = 0.0;

        const size_t N = sizes[j];

        for (int k = 0; k < 7; ++k) {
            const ggml_type wtype =
                k == 0 ? GGML_TYPE_Q4_0 :
                k == 1 ? GGML_TYPE_Q4_1 :
                k == 2 ? GGML_TYPE_Q5_0 :
                k == 3 ? GGML_TYPE_Q5_1 :
                k == 4 ? GGML_TYPE_Q8_0 :
                k == 5 ? GGML_TYPE_F16  : GGML_TYPE_F32;

            double & s = k == 0 ? s_q4_0 : k == 1 ? s_q4_1 : k == 2 ? s_q5_0 : k == 3 ? s_q5_1 : k == 4 ? s_q8_0 : k == 5 ? s_fp16 : /*k == 6*/ s_fp32;
            int    & n = k == 0 ? n_q4_0 : k == 1 ? n_q4_1 : k == 2 ? n_q5_0 : k == 3 ? n_q5_1 : k == 4 ? n_q8_0 : k == 5 ? n_fp16 : /*k == 6*/ n_fp32;

            struct ggml_init_params gparams = {
                /*.mem_size   =*/ buf.size(),
                /*.mem_buffer =*/ buf.data(),
                /*.no_alloc   =*/ false,
            };

            struct ggml_context * ctx0 = ggml_init(gparams);

            struct ggml_tensor * a = ggml_new_tensor_2d(ctx0, wtype,         N, N);
            struct ggml_tensor * b = ggml_new_tensor_2d(ctx0, GGML_TYPE_F32, N, N);

            struct ggml_tensor * c = ggml_mul_mat(ctx0, a, b);

            struct ggml_cgraph * gf = ggml_new_graph(ctx0);

            ggml_build_forward_expand(gf, c);

            double tsum = 0.0;

            // heat-up
            ggml_graph_compute_helper(gf, work, n_threads, nullptr, nullptr);

            for (int i = 0; i < n_max; ++i) {
                const int64_t t0 = ggml_time_us();

                ggml_graph_compute_helper(gf, work, n_threads, nullptr, nullptr);

                const int64_t t1 = ggml_time_us();

                tsum += (t1 - t0)*1e-6;
                n++;

                if (tsum > 1.0 && n >= 3) {
                    break;
                }
            }

            ggml_free(ctx0);

            s = ((2.0*N*N*N*n)/tsum)*1e-9;
        }

        // Q4_0 | Q4_1
        snprintf(strbuf, sizeof(strbuf), "%4zu x %4zu: Q4_0 %7.1f GFLOPS (%3d runs) | Q4_1 %7.1f GFLOPS (%3d runs)\n",
                N, N, s_q4_0, n_q4_0, s_q4_1, n_q4_1);
        s += strbuf;

        // Q5_0 | Q5_1 | Q8_0
        snprintf(strbuf, sizeof(strbuf), "%4zu x %4zu: Q5_0 %7.1f GFLOPS (%3d runs) | Q5_1 %7.1f GFLOPS (%3d runs) | Q8_0 %7.1f GFLOPS (%3d runs)\n",
                N, N, s_q5_0, n_q5_0, s_q5_1, n_q5_1, s_q8_0, n_q8_0);
        s += strbuf;

        // F16 | F32
        snprintf(strbuf, sizeof(strbuf), "%4zu x %4zu: F16  %7.1f GFLOPS (%3d runs) | F32  %7.1f GFLOPS (%3d runs)\n",
                N, N, s_fp16, n_fp16, s_fp32, n_fp32);
        s += strbuf;
    }

    return s.c_str();
}

// =================================================================================================

// =================================================================================================

//
// Experimental stuff below
//
// Not sure if these should be part of the library at all, because the quality of the results is not
// guaranteed. Might get removed at some point unless a robust algorithm implementation is found
//

// =================================================================================================

//
// token-level timestamps
//

static int timestamp_to_sample(int64_t t, int n_samples) {
    return std::max(0, std::min((int) n_samples - 1, (int) ((t*WHISPER_SAMPLE_RATE)/100)));
}

static int64_t sample_to_timestamp(int i_sample) {
    return (100ll*i_sample)/WHISPER_SAMPLE_RATE;
}

// a cost-function / heuristic that is high for text that takes longer to pronounce
// obviously, can be improved
static float voice_length(const std::string & text) {
    float res = 0.0f;

    for (char c : text) {
        if (c == ' ') {
            res += 0.01f;
        } else if (c == ',') {
            res += 2.00f;
        } else if (c == '.') {
            res += 3.00f;
        } else if (c == '!') {
            res += 3.00f;
        } else if (c == '?') {
            res += 3.00f;
        } else if (c >= '0' && c <= '9') {
            res += 3.00f;
        } else {
            res += 1.00f;
        }
    }

    return res;
}

// average the fabs of the signal
static std::vector<float> get_signal_energy(const float * signal, int n_samples, int n_samples_per_half_window) {
    const int hw = n_samples_per_half_window;

    std::vector<float> result(n_samples);

    for (int i = 0; i < n_samples; i++) {
        float sum = 0;
        for (int j = -hw; j <= hw; j++) {
            if (i + j >= 0 && i + j < n_samples) {
                sum += fabs(signal[i + j]);
            }
        }
        result[i] = sum/(2*hw + 1);
    }

    return result;
}

static void whisper_exp_compute_token_level_timestamps(
        struct whisper_context & ctx,
          struct whisper_state & state,
                           int   i_segment,
                         float   thold_pt,
                         float   thold_ptsum) {
    auto & segment = state.result_all[i_segment];
    auto & tokens  = segment.tokens;

    const int n_samples = state.energy.size();

    if (n_samples == 0) {
        WHISPER_LOG_ERROR("%s: no signal data available\n", __func__);
        return;
    }

    const int64_t t0 = segment.t0;
    const int64_t t1 = segment.t1;

    const int n = tokens.size();

    if (n == 0) {
        return;
    }

    if (n == 1) {
        tokens[0].t0 = t0;
        tokens[0].t1 = t1;

        return;
    }

    auto & t_beg    = state.t_beg;
    auto & t_last   = state.t_last;
    auto & tid_last = state.tid_last;

    for (int j = 0; j < n; ++j) {
        auto & token = tokens[j];

        if (j == 0) {
            if (token.id == whisper_token_beg(&ctx)) {
                tokens[j    ].t0 = t0;
                tokens[j    ].t1 = t0;
                tokens[j + 1].t0 = t0;

                t_beg    = t0;
                t_last   = t0;
                tid_last = whisper_token_beg(&ctx);
            } else {
                tokens[j    ].t0 = t_last;
            }
        }

        const int64_t tt = t_beg + 2*(token.tid - whisper_token_beg(&ctx));

        tokens[j].id    = token.id;
        tokens[j].tid   = token.tid;
        tokens[j].p     = token.p;
        tokens[j].pt    = token.pt;
        tokens[j].ptsum = token.ptsum;

        tokens[j].vlen = voice_length(whisper_token_to_str(&ctx, token.id));

        if (token.pt > thold_pt && token.ptsum > thold_ptsum && token.tid > tid_last && tt <= t1) {
            if (j > 0) {
                tokens[j - 1].t1 = tt;
            }
            tokens[j].t0 = tt;
            tid_last = token.tid;
        }
    }

    tokens[n - 2].t1 = t1;
    tokens[n - 1].t0 = t1;
    tokens[n - 1].t1 = t1;

    t_last = t1;

    // find intervals of tokens with unknown timestamps
    // fill the timestamps by proportionally splitting the interval based on the token voice lengths
    {
        int p0 = 0;
        int p1 = 0;

        while (true) {
            while (p1 < n && tokens[p1].t1 < 0) {
                p1++;
            }

            if (p1 >= n) {
                p1--;
            }

            //printf("p0=%d p1=%d t0=%lld t1=%lld\n", p0, p1, tokens[p0].t0, tokens[p1].t1);

            if (p1 > p0) {
                double psum = 0.0;
                for (int j = p0; j <= p1; j++) {
                    psum += tokens[j].vlen;
                }

                //printf("analyzing %d - %d, psum = %f\n", p0, p1, psum);

                const double dt = tokens[p1].t1 - tokens[p0].t0;

                // split the time proportionally to the voice length
                for (int j = p0 + 1; j <= p1; j++) {
                    const double ct = tokens[j - 1].t0 + dt*tokens[j - 1].vlen/psum;

                    tokens[j - 1].t1 = ct;
                    tokens[j    ].t0 = ct;
                }
            }

            p1++;
            p0 = p1;
            if (p1 >= n) {
                break;
            }
        }
    }

    // fix up (just in case)
    for (int j = 0; j < n - 1; j++) {
        if (tokens[j].t1 < 0) {
            tokens[j + 1].t0 = tokens[j].t1;
        }

        if (j > 0) {
            if (tokens[j - 1].t1 > tokens[j].t0) {
                tokens[j].t0 = tokens[j - 1].t1;
                tokens[j].t1 = std::max(tokens[j].t0, tokens[j].t1);
            }
        }
    }

    // VAD
    // expand or contract tokens based on voice activity
    {
        const int hw = WHISPER_SAMPLE_RATE/8;

        for (int j = 0; j < n; j++) {
            if (tokens[j].id >= whisper_token_eot(&ctx)) {
                continue;
            }

            int s0 = timestamp_to_sample(tokens[j].t0, n_samples);
            int s1 = timestamp_to_sample(tokens[j].t1, n_samples);

            const int ss0 = std::max(s0 - hw, 0);
            const int ss1 = std::min(s1 + hw, n_samples);

            const int ns = ss1 - ss0;

            float sum = 0.0f;

            for (int k = ss0; k < ss1; k++) {
                sum += state.energy[k];
            }

            const float thold = 0.5*sum/ns;

            {
                int k = s0;
                if (state.energy[k] > thold && j > 0) {
                    while (k > 0 && state.energy[k] > thold) {
                        k--;
                    }
                    tokens[j].t0 = sample_to_timestamp(k);
                    if (tokens[j].t0 < tokens[j - 1].t1) {
                        tokens[j].t0 = tokens[j - 1].t1;
                    } else {
                        s0 = k;
                    }
                } else {
                    while (state.energy[k] < thold && k < s1) {
                        k++;
                    }
                    s0 = k;
                    tokens[j].t0 = sample_to_timestamp(k);
                }
            }

            {
                int k = s1;
                if (state.energy[k] > thold) {
                    while (k < n_samples - 1 && state.energy[k] > thold) {
                        k++;
                    }
                    tokens[j].t1 = sample_to_timestamp(k);
                    if (j < ns - 1 && tokens[j].t1 > tokens[j + 1].t0) {
                        tokens[j].t1 = tokens[j + 1].t0;
                    } else {
                        s1 = k;
                    }
                } else {
                    while (state.energy[k] < thold && k > s0) {
                        k--;
                    }
                    s1 = k;
                    tokens[j].t1 = sample_to_timestamp(k);
                }
            }
        }
    }

    // fixed token expand (optional)
    //{
    //    const int t_expand = 0;

    //    for (int j = 0; j < n; j++) {
    //        if (j > 0) {
    //            tokens[j].t0 = std::max(0, (int) (tokens[j].t0 - t_expand));
    //        }
    //        if (j < n - 1) {
    //            tokens[j].t1 = tokens[j].t1 + t_expand;
    //        }
    //    }
    //}

    // debug info
    //for (int j = 0; j < n; ++j) {
    //    const auto & token = tokens[j];
    //    const auto tt = token.pt > thold_pt && token.ptsum > 0.01 ? whisper_token_to_str(&ctx, token.tid) : "[?]";
    //    printf("%s: %10s %6.3f %6.3f %6.3f %6.3f %5d %5d '%s'\n", __func__,
    //            tt, token.p, token.pt, token.ptsum, token.vlen, (int) token.t0, (int) token.t1, whisper_token_to_str(&ctx, token.id));

    //    if (tokens[j].id >= whisper_token_eot(&ctx)) {
    //        continue;
    //    }
    //}
}

//
// token level timestamps - dtw version
//

// n_text_layer -> total text layers on model
// n_head -> total heads per text layer on model
static std::vector<uint32_t> get_alignment_heads_by_layer(const whisper_context_params & cparams, int il, int n_text_layer, int n_head) {
    std::vector<uint32_t> ret;
    if (cparams.dtw_aheads_preset == WHISPER_AHEADS_NONE) {
        return ret;
    } else if (cparams.dtw_aheads_preset == WHISPER_AHEADS_N_TOP_MOST) {
        if (il >= n_text_layer - cparams.dtw_n_top) {
            for (int32_t i = 0; i < n_head; ++i) {
                ret.push_back(i);
            }
        }
    } else {
        const auto aheads = cparams.dtw_aheads_preset == WHISPER_AHEADS_CUSTOM ? cparams.dtw_aheads : g_aheads.at(cparams.dtw_aheads_preset);
        for (size_t i = 0; i < aheads.n_heads; ++i) {
            if (aheads.heads[i].n_text_layer == il) {
                ret.push_back(aheads.heads[i].n_head);
            }
        }
    }
    return ret;
}

// dtw + backtrace to return found path
// based on
// https://github.com/openai/whisper/blob/main/whisper/timing.py#L83
static ggml_tensor * dtw_and_backtrace(ggml_context * ctx, ggml_tensor * x) {
    WHISPER_ASSERT(ggml_n_dims(x) == 2);

    int64_t N = x->ne[0];
    int64_t M = x->ne[1];
    struct ggml_tensor * cost = ggml_new_tensor_2d(ctx, GGML_TYPE_F32, N + 1, M + 1);
    struct ggml_tensor * trace = ggml_new_tensor_2d(ctx, GGML_TYPE_I32, N + 1, M + 1);

    cost = ggml_set_f32(cost, INFINITY);
    trace = ggml_set_f32(trace, -1);
    ggml_set_f32_nd(cost, 0, 0, 0, 0, 0.0);

    // dtw
    // supposedly can be optmized by computing diagonals in parallel ?
    // Not sure it is worth it since x will be GENERATED_TOKENS*1500 size at most.
    for (int64_t j = 1; j < M + 1; ++j) {
        for (int64_t i = 1; i < N + 1; ++i) {
            float c0 = ggml_get_f32_nd(cost, i - 1, j - 1, 0, 0);
            float c1 = ggml_get_f32_nd(cost, i - 1, j, 0, 0);
            float c2 = ggml_get_f32_nd(cost, i, j - 1, 0, 0);

            float c;
            int32_t t;
            if (c0 < c1 && c0 < c2) {
                c = c0;
                t = 0;
            } else if (c1 < c0 && c1 < c2) {
                c = c1;
                t = 1;
            } else {
                c = c2;
                t = 2;
            }

            c = ggml_get_f32_nd(x, i - 1, j - 1, 0, 0) + c;
            ggml_set_f32_nd(cost, i, j, 0, 0, c);
            ggml_set_i32_nd(trace, i, j, 0, 0, t);
        }
    }

    // Backtrace
    const int64_t BT_MAX_ROWS = N + M - 1;
    struct ggml_tensor * bt = ggml_new_tensor_2d(ctx, GGML_TYPE_I32, BT_MAX_ROWS, 2);
    // trace[0, :] = 2;
    for (int64_t i = 0; i < M + 1; ++i)
        ggml_set_i32_nd(trace, 0, i, 0, 0, 2);
    //trace[:, 0] = 1;
    for (int64_t i = 0; i < N + 1; ++i)
        ggml_set_i32_nd(trace, i, 0, 0, 0, 1);
    int bt_row_idx = BT_MAX_ROWS - 1;
    int64_t i = N;
    int64_t j = M;
    while (i > 0 || j > 0) {
        ggml_set_i32_nd(bt, bt_row_idx, 0, 0, 0, i - 1);
        ggml_set_i32_nd(bt, bt_row_idx, 1, 0, 0, j - 1);
        --bt_row_idx;

        int32_t t = ggml_get_i32_nd(trace, i, j, 0, 0);
        if (t == 0) {
            --i;
            --j;
        } else if (t == 1) {
            --i;
        } else if (t == 2) {
            --j;
        } else {
            WHISPER_ASSERT(0);
        }
    }

    // FIXME: manual clip/transpose might not be the most efficient way? (e.g. use ggml funcs)
    // Clip + transpose
    // This might not be entirely necessary for our case, but leaving it for now so output matrix
    // is identical to dtw on openAI timing.py
    const int64_t result_n_cols = BT_MAX_ROWS-bt_row_idx-1;
    ggml_tensor * r = ggml_new_tensor_2d(ctx, GGML_TYPE_I32, 2, result_n_cols);
    for (int64_t i = 0; i < 2; ++i) {
        for (int64_t j = 0; j < result_n_cols; ++j) {
            int32_t v = ggml_get_i32_nd(bt, j+bt_row_idx+1, i, 0, 0);
            ggml_set_i32_nd(r, i, j, 0, 0, v);
        }
    }

    return r;
}

struct median_filter_user_data {
    int filter_width;
};

static void median_filter(struct ggml_tensor * dst , const struct ggml_tensor * a, int ith, int nth, void * userdata) {
    int filter_width = ((median_filter_user_data *) userdata)->filter_width;
    WHISPER_ASSERT(nth == 1);
    WHISPER_ASSERT(ith == 0);
    WHISPER_ASSERT(filter_width < a->ne[2]);
    WHISPER_ASSERT(filter_width % 2);
    WHISPER_ASSERT(ggml_n_dims(a) == 3);
    WHISPER_ASSERT(a->type == GGML_TYPE_F32);

    std::vector<float> filter;
    filter.reserve(filter_width);
    for (int64_t i = 0; i < a->ne[0]; ++i) {
        for (int64_t j = 0; j < a->ne[1]; ++j) {
            for (int64_t k = 0; k < a->ne[2]; ++k) {
                for (int64_t off = -filter_width/2; off <= filter_width/2; ++off) {
                    // "reflect" padding
                    int64_t idx = k + off;
                    if (idx < 0) {
                        idx = -idx;
                    } else if (idx >= a->ne[2]) {
                        idx = 2*(a->ne[2] - 1) - idx;
                    }

                    filter.push_back(ggml_get_f32_nd(a, i, j, idx, 0));
                }
                std::sort(filter.begin(), filter.end());
                const float v = filter[filter.size()/2];
                ggml_set_f32_nd(dst, i, j, k, 0, v);
                filter.clear();
            }
        }
    }
}

static void whisper_exp_compute_token_level_timestamps_dtw(
            struct whisper_context * ctx,
              struct whisper_state * state,
        struct whisper_full_params   params,
                               int   i_segment,
                            size_t   n_segments,
                               int   seek,
                               int   n_frames,
                               int   medfilt_width,
                               int   n_threads)
{
    const int n_audio_ctx = state->exp_n_audio_ctx > 0 ? state->exp_n_audio_ctx : ctx->model.hparams.n_audio_ctx;
    WHISPER_ASSERT(medfilt_width % 2);
    WHISPER_ASSERT(n_frames <= n_audio_ctx * 2);
    WHISPER_ASSERT(ctx->params.dtw_aheads_preset != WHISPER_AHEADS_NONE);

    // FIXME: Allocating mem everytime we call this func
    // Our ggml buffer should be pre-allocated somewhere during init and reused
    // when we call this function
    struct ggml_init_params gparams = {
        /*.mem_size   =*/ ctx->params.dtw_mem_size,
        /*.mem_buffer =*/ NULL,
        /*.no_alloc   =*/ false,
    };
    struct ggml_context * gctx = ggml_init(gparams);

    // Build token sequence that will be passed to decoder
    // sot + [lang] + text result + eot
    std::vector<whisper_token> tokens = { whisper_token_sot(ctx), };
    if (whisper_is_multilingual(ctx)) {
        const int lang_id = whisper_lang_id(params.language);
        state->lang_id = lang_id;
        tokens.push_back(whisper_token_lang(ctx, lang_id));
    }
    const size_t sot_sequence_length = tokens.size();
    tokens.push_back(whisper_token_not(ctx));
    for (size_t i = i_segment; i < i_segment + n_segments; ++i) {
        auto & segment = state->result_all[i];
        for (auto &t: segment.tokens) {
            // Only text tokens
            if (t.id < whisper_token_eot(ctx)) {
                tokens.push_back(t.id);
            }
        }
    }
    tokens.push_back(whisper_token_eot(ctx));

    // Get result tokens, pass then along to decoder to get cross attention QKs
    // used in timestamping
    // Decoder already returns only alignment head QKs, already concatenated in
    // one tensor.
    whisper_kv_cache_clear(state->kv_self);
    whisper_batch_prep_legacy(state->batch, tokens.data(), tokens.size(), 0, 0);
    whisper_kv_cache_seq_rm(state->kv_self, 0, 0, -1);
    if (!whisper_decode_internal(*ctx, *state, state->batch, n_threads, true, nullptr, nullptr)) {
        WHISPER_LOG_INFO("DECODER FAILED\n");
        WHISPER_ASSERT(0);
    }
    WHISPER_ASSERT(state->aheads_cross_QKs != nullptr);

    const auto n_audio_tokens = n_frames/2;
    WHISPER_ASSERT(state->aheads_cross_QKs != NULL);
    WHISPER_ASSERT(n_audio_tokens <= state->aheads_cross_QKs->ne[1]);
    const auto n_tokens = state->aheads_cross_QKs->ne[0];
    const auto n_heads = state->aheads_cross_QKs->ne[2];

    // Copy data from decoder buffer to a local CPU tensor, discarding unused audio
    // tokens (i.e. discarding rows at the end of tensor)
    // IN: Tensor with N_TOKENS*audio_ctx*N_ALIGNMENT_HEADS dims
    // OUT: Tensor with N_TOKENS*N_AUDIO_TOKENS*N_ALIGNMENT_HEADS dims
    WHISPER_ASSERT(state->aheads_cross_QKs->type == GGML_TYPE_F32);
    WHISPER_ASSERT(ggml_is_contiguous(state->aheads_cross_QKs));
    ggml_tensor * w = ggml_new_tensor_3d(gctx, GGML_TYPE_F32, n_tokens, n_audio_tokens, n_heads);
    auto & data = state->aheads_cross_QKs_data;
    data.resize(n_tokens * n_audio_ctx * n_heads);
    ggml_backend_tensor_get(state->aheads_cross_QKs, data.data(), 0, sizeof(float) * n_tokens * n_audio_ctx * n_heads);
    for (int k = 0; k < n_heads; ++k) {
        for (int j = 0; j < n_audio_tokens; ++j) {
            memcpy(
                (char *) w->data + j * w->nb[1] + k * w->nb[2],
                data.data() + j * n_tokens + k * n_tokens * n_audio_ctx,
                n_tokens * sizeof(float)
            );
        }
    }

    // Normalize - in original OpenAI code, this is done over dim=-2. In this case,
    // we already permuted N_TOKENS dimension to columns on last loop, becase ggml_norm
    // operates over columns. Afterwards, permute to a shape that facilitates mean
    // operation (after median filter)
    // IN: Tensor with N_TOKENS*N_AUDIO_TOKENS*N_ALIGNMENT_HEADS dims
    // OUT: Tensor with N_ALIGNMENT_HEADS*N_TOKENS*N_AUDIO_TOKENS dims
    w = ggml_norm(gctx, w, 1e-9);
    w = ggml_permute(gctx, ggml_permute(gctx, w, 2, 1, 0 ,3), 0, 2, 1, 3);

    // Pass median filter - this is done over AUDIO_TOKENS dimension.
    // IN: Tensor with N_ALIGNMENT_HEADS*N_TOKENS*N_AUDIO_TOKENS dims
    // OUT: Same dims
    median_filter_user_data mf_user_data = {medfilt_width};
    w = ggml_map_custom1(gctx, w, median_filter, 1, &mf_user_data);

    // Take mean over columns, scale by -1, reshape to 2D tensor, remove SOT sequence and EOT
    // IN: Tensor with N_ALIGNMENT_HEADS*N_TOKENS*N_AUDIO_TOKENS dims
    // OUT: Tensor with N_TOKENS*N_AUDIO_TOKENS dims
    w = ggml_mean(gctx, w);
    w = ggml_scale(gctx, w, -1.0);
    w = ggml_reshape_2d(gctx, w, w->ne[1], w->ne[2]);

    // Remove SOT sequence and EOT
    // Out dimension is (N_TOKENS-sot_sequence_length-1)*N_AUDIO_TOKENS
    w = ggml_view_2d(gctx, w, w->ne[0] - sot_sequence_length - 1, w->ne[1], w->nb[1], sot_sequence_length * w->nb[0]);

    // Compute
    struct ggml_cgraph * gf = ggml_new_graph(gctx);
    ggml_build_forward_expand(gf, w);
    ggml_graph_compute_with_ctx(gctx, gf, n_threads);

    ggml_tensor * alignment = dtw_and_backtrace(gctx, w);

    // Place timestamps on segments
    int32_t last_v = 0;
    auto seg_i = state->result_all.begin() + i_segment;
    auto tok_i = seg_i->tokens.begin();
    for (int i = 0; i < alignment->ne[1]; ++i) {
        int32_t v = ggml_get_i32_nd(alignment, 0, i, 0, 0);
        if (v != last_v) {
            int32_t time_index = ggml_get_i32_nd(alignment, 1, i, 0, 0);
            int64_t timestamp = (time_index * 2) + seek; // Each index on DTW result = 20mS audio
            last_v = v;

            // Skip non-text tokens
            while (!(tok_i->id < whisper_token_eot(ctx))) {
                ++tok_i;
                if (tok_i == seg_i->tokens.end()) {
                    ++seg_i;
                    tok_i = seg_i->tokens.begin();
                }
            }

            tok_i->t_dtw = timestamp;
            ++tok_i;
            if (tok_i == seg_i->tokens.end()) {
                ++seg_i;
                tok_i = seg_i->tokens.begin();
            }
        }
    }

    // Print DTW timestamps
    /*for (size_t i = i_segment; i < i_segment + n_segments; ++i) {
        auto & segment = state->result_all[i];
        for (auto &t: segment.tokens) {
            const char * tok = whisper_token_to_str(ctx, t.id);
            fprintf(stderr, "|%s|(%.2f) ", tok, (float)t.t_dtw/100);
        }
        fprintf(stderr, "\n");
    }*/

    ggml_free(gctx);
}

void whisper_log_set(ggml_log_callback log_callback, void * user_data) {
    g_state.log_callback = log_callback ? log_callback : whisper_log_callback_default;
    g_state.log_callback_user_data = user_data;
}

GGML_ATTRIBUTE_FORMAT(2, 3)
static void whisper_log_internal(ggml_log_level level, const char * format, ...) {
    va_list args;
    va_start(args, format);
    char buffer[1024];
    int len = vsnprintf(buffer, 1024, format, args);
    if (len < 1024) {
        g_state.log_callback(level, buffer, g_state.log_callback_user_data);
    } else {
        char* buffer2 = new char[len+1];
        vsnprintf(buffer2, len+1, format, args);
        buffer2[len] = 0;
        g_state.log_callback(level, buffer2, g_state.log_callback_user_data);
        delete[] buffer2;
    }
    va_end(args);
}

static void whisper_log_callback_default(ggml_log_level level, const char * text, void * user_data) {
    (void) level;
    (void) user_data;
    fputs(text, stderr);
    fflush(stderr);
}
