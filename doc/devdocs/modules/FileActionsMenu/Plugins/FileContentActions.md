# File content actions

This plugin contains actions related to the content of files.

## Copy content as C string

This action replaces the following hex ascii values with the string in the right column.

| Hex value | Replace by string |
| --------- | ----------------- |
|5C|`\\`|
|07|`\a`|
|08|`\b`|
|09|`\t`|
|0A|`\n`|
|0B|`\v`|
|0C|`\f`|
|0D|`\r`|
|22|`\"`|
|27|`\'`|

## Copy content as data url

Converts the content to a data url. The mime type is determined by the file extension.

| File extension | Mime type |
| -------------- | --------- |
| .jpg | image/jpeg |
| .jpeg | image/jpeg |
| .png | image/png |
| .gif | image/gif |
| .bmp | image/bmp |
| .svg | image/svg+xml |
| .heic | image/heic |
| .heif | image/heif |
| .svgz | image/svg+xml |
| .ico | image/x-icon |
| .cur | image/x-icon |
| .tif | image/tiff |
| .tiff | image/tiff |
| .webp | image/webp |
| .avif | image/avif |
| .apng | image/apng |
| .jxl | image/jxl |
| .jpe | image/jpeg |
| .jfif | image/jpeg |
| .pjpeg | image/jpeg |
| .pjp | image |

If the mime type could not be determined `application/octet-stream` will be used.

## Copy content as plaintext

> No further annotations

## Copy content as URI encoded string

> No further annotations

## Copy content as XML encoded string

> No further annotations

## Collapse folder structure

> No further annotations

## Copy directory tree

This action copies the tree of the selected directory. It does this by executing the `tree` command through `cmd.exe` with the parameter `/f`.

## Merge file contents

> No further annotations
