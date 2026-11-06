# Inline image/audio syntax

Read only for primary `image`/`audio`. Input/output envelope schema:

```json
{
  "type": "object",
  "additionalProperties": false,
  "properties": {
    "image": {
      "type": "object",
      "additionalProperties": false,
      "properties": {
        "mimeType": { "type": "string", "enum": ["image/png"] },
        "data": { "type": "string", "contentEncoding": "base64", "minLength": 1 }
      },
      "required": ["mimeType", "data"]
    }
  },
  "required": ["image"]
}
```

Request/result: `{"image":{"mimeType":"image/png","data":"<base64 bytes>"}}`.
Audio substitutes `audio`. Images: static PNG/JPEG/WebP (`image/png`,
`image/jpeg`, `image/webp`); audio: WAV/MP3/FLAC (`audio/wav`, `audio/mpeg`,
`audio/flac`). MIME labels must match actual bytes; relabeling is not transcoding.

Collections wrap the media item in
`{"type":"array","minItems":1,"maxItems":2,"items":{...}}`.
Keep array shape even for one item.

Each direction allows 16 MiB decoded/item, 32 MiB total, 64 items.
Base64 must be nonempty without whitespace. Handle decode/codec errors.
Animated images, SVG, video, and large media use `files`.
Verify the consumer's media adapter, not just shared engine support.
