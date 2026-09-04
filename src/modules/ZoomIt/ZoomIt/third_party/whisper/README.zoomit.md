# whisper.cpp in ZoomIt

This directory contains the CPU-only sources from whisper.cpp v1.5.5,
commit `7395c70a748753e3800b63e3422a2b558a097c80`.

Upstream: https://github.com/ggerganov/whisper.cpp

ZoomIt embeds the quantized OpenAI Whisper `small.en` Q5_1 model as three
RCDATA resources. The source chunks are concatenated by the model loader and
are not written to disk at runtime.

Model SHA-256:
`bfdff4894dcb76bbf647d56263ea2a96645423f1669176f4844a1bf8e478ad30`

Model source:
https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.en-q5_1.bin
