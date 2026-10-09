# whisper.cpp in ZoomIt

This directory contains the CPU-only sources from whisper.cpp v1.5.5,
commit `7395c70a748753e3800b63e3422a2b558a097c80`.

Upstream: https://github.com/ggerganov/whisper.cpp

ZoomIt embeds the quantized OpenAI Whisper `base` Q5_1 model as three
RCDATA resources. The source chunks are concatenated by the model loader and
are not written to disk at runtime.

Recognition follows the current user's Windows display language. Unsupported
languages or a failed Windows locale lookup use automatic language detection
with a diagnostic. Transcription stays in the recognized language.

Model SHA-256:
`422f1ae452ade6f30a004d7e5c6a43195e4433bc370bf23fac9cc591f01a8898`

Model source:
https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base-q5_1.bin
