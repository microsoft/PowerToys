# whisper.cpp in ZoomIt

This directory contains the CPU-only sources from whisper.cpp v1.5.5,
commit `7395c70a748753e3800b63e3422a2b558a097c80`.

Upstream: https://github.com/ggerganov/whisper.cpp

ZoomIt embeds the quantized OpenAI Whisper `base.en` Q5_1 model as three
RCDATA resources. The source chunks are concatenated by the model loader and
are not written to disk at runtime.

Model SHA-256:
`4baf70dd0d7c4247ba2b81fafd9c01005ac77c2f9ef064e00dcf195d0e2fdd2f`

Model source:
https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.en-q5_1.bin
