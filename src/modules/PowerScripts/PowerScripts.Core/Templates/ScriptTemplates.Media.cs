// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerScripts.Core.Templates;

public static partial class ScriptTemplates
{
    private static string CreatePowerShellMedia(string category)
    {
        var encode = category == "image" ? """
            Add-Type -AssemblyName System.Drawing
            $__powerscriptBitmap = [Drawing.Bitmap]::new(64, 64)
            try {
                for ($__powerscriptY = 0; $__powerscriptY -lt 64; $__powerscriptY++) {
                    for ($__powerscriptX = 0; $__powerscriptX -lt 64; $__powerscriptX++) {
                        $__powerscriptCell = [int][Math]::Floor($__powerscriptX / 8) + 8 * [int][Math]::Floor($__powerscriptY / 8)
                        $__powerscriptColor = [Drawing.Color]::FromArgb(
                            $__powerscriptSeed[$__powerscriptCell % 32],
                            $__powerscriptSeed[($__powerscriptCell + 11) % 32],
                            $__powerscriptSeed[($__powerscriptCell + 22) % 32])
                        $__powerscriptBitmap.SetPixel($__powerscriptX, $__powerscriptY, $__powerscriptColor)
                    }
                }
                $__powerscriptBitmap.Save($__powerscriptBuffer, [Drawing.Imaging.ImageFormat]::Png)
            } finally {
                $__powerscriptBitmap.Dispose()
            }
            """ : """
            $__powerscriptFrequency = 220 + 2 * $__powerscriptSeed[0]
            $__powerscriptWriter = [IO.BinaryWriter]::new($__powerscriptBuffer)
            try {
                $__powerscriptWriter.Write([Text.Encoding]::ASCII.GetBytes('RIFF'))
                $__powerscriptWriter.Write([int]8036)
                $__powerscriptWriter.Write([Text.Encoding]::ASCII.GetBytes('WAVEfmt '))
                $__powerscriptWriter.Write([int]16)
                $__powerscriptWriter.Write([int16]1)
                $__powerscriptWriter.Write([int16]1)
                $__powerscriptWriter.Write([int]16000)
                $__powerscriptWriter.Write([int]32000)
                $__powerscriptWriter.Write([int16]2)
                $__powerscriptWriter.Write([int16]16)
                $__powerscriptWriter.Write([Text.Encoding]::ASCII.GetBytes('data'))
                $__powerscriptWriter.Write([int]8000)
                for ($__powerscriptI = 0; $__powerscriptI -lt 4000; $__powerscriptI++) {
                    $__powerscriptSample = [Math]::Sin(2 * [Math]::PI * $__powerscriptFrequency * $__powerscriptI / 16000)
                    $__powerscriptWriter.Write([int16]($__powerscriptSample * 3276))
                }
                $__powerscriptWriter.Flush()
                $__powerscriptEncoded = [Convert]::ToBase64String($__powerscriptBuffer.ToArray())
            } finally {
                $__powerscriptWriter.Dispose()
            }
            """;
        return $$"""
            $__powerscriptHasher = [Security.Cryptography.SHA256]::Create()
            try {
                $__powerscriptSeed = $__powerscriptHasher.ComputeHash([Text.Encoding]::UTF8.GetBytes($__powerscriptContent))
            } finally {
                $__powerscriptHasher.Dispose()
            }
            $__powerscriptBuffer = [IO.MemoryStream]::new()
            try {
                {{encode}}
                {{(category == "image" ? "$__powerscriptEncoded = [Convert]::ToBase64String($__powerscriptBuffer.ToArray())" : string.Empty)}}
            } finally {
                $__powerscriptBuffer.Dispose()
            }
            @{ {{category}} = @{ mimeType = '{{(category == "image" ? "image/png" : "audio/wav")}}'; data = $__powerscriptEncoded } } | ConvertTo-Json -Depth 4 -Compress
            """;
    }

    private static string CreatePythonMedia(string category)
    {
        var encode = category == "image" ? """
            import struct
            import zlib

            def png_chunk(kind, data):
                return (struct.pack('>I', len(data)) + kind + data
                        + struct.pack('>I', zlib.crc32(kind + data) & 0xffffffff))

            pixels = b''.join(
                b'\x00' + bytes(seed[(x // 8 + 8 * (y // 8) + channel * 11) % 32]
                                for x in range(64) for channel in range(3))
                for y in range(64))
            data = (b'\x89PNG\r\n\x1a\n'
                    + png_chunk(b'IHDR', struct.pack('>IIBBBBB', 64, 64, 8, 2, 0, 0, 0))
                    + png_chunk(b'IDAT', zlib.compress(pixels))
                    + png_chunk(b'IEND', b''))
            """ : """
            import io
            import math
            import struct
            import wave

            frequency = 220 + 2 * seed[0]
            samples = b''.join(struct.pack('<h', int(3276 * math.sin(2 * math.pi * frequency * i / 16000)))
                               for i in range(4000))
            with io.BytesIO() as buffer:
                with wave.open(buffer, 'wb') as output:
                    output.setparams((1, 2, 16000, 4000, 'NONE', 'not compressed'))
                    output.writeframes(samples)
                data = buffer.getvalue()
            """;
        return $$"""
            seed = hashlib.sha256(content.encode('utf-8')).digest()
            {{encode}}
            media = {
                'mimeType': '{{(category == "image" ? "image/png" : "audio/wav")}}',
                'data': base64.b64encode(data).decode('ascii'),
            }
            print(json.dumps({'{{category}}': media}))
            """;
    }
}
