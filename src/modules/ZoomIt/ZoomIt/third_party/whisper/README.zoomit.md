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

## CPU compatibility

On x64, `whisper.cpp` must be compiled for the baseline SSE2 instruction set,
not AVX2. Its global maps and `non_speech_tokens` vector are initialized by
the CRT before `wWinMain`, before any CPU feature check. The entire translation
unit must remain baseline, including inline functions and shared STL template
instantiations; moving only the global constructors is not sufficient.

The four `ggml*.c` translation units retain AVX2 for inference. They have no
dynamic CRT initializers and are reached only after the existing
`WhisperRecognizer::Prepare` AVX2 check succeeds. Keep whole-program optimization
disabled for these files and `whisper.cpp` so LTCG cannot inline AVX2 code across
this boundary. CPUs without AVX2 continue to use the existing SAPI fallback.
ARM64 and Win32 compilation settings are unchanged.

After an x64 Release build, run the machine-code regression check from a Visual
Studio developer PowerShell at the repository root:

```powershell
.\src\modules\ZoomIt\unittests\Test-WhisperStartupIsa.ps1
```

Use `-ObjectDirectory` for a different intermediate output directory, such as
Debug. The check covers all of `whisper.obj`, not just the known constructors,
and rejects CRT initializers in the AVX2 objects. Also validate startup, exit and
SAPI dictation on a no-AVX CPU (or an emulator that actually rejects unsupported
instructions); masking CPUID alone does not reproduce this crash. On an AVX2
CPU, verify that embedded Whisper dictation still works.

Model SHA-256:
`422f1ae452ade6f30a004d7e5c6a43195e4433bc370bf23fac9cc591f01a8898`

Model source:
https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base-q5_1.bin
