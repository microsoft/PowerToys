# Hashes

Plugin to generate and check file hashes.

Following hash types are supported:

* CRC32 Decimal
* CRC32 Hex
* CRC64 Decimal
* CRC64 Hex
* SHA1
* SHA256
* SHA384
* SHA512
* SHA3-256
* SHA3-384
* SHA3-512

Following different generation/validation types are available:

## Copy hash to clipboard / Verify hash with clipboard

> No further annotations

## Save to single file / Verify with file called "Checksums"

The hashes are saved in a file called "Checksums" (in English, other languages use other names) with the hash type as file extension.

## Save to multiple files / Verify with multiple files

The hashes are saved to files with the same filenames as the oriiginal files with the hash type as file extension.

## Save to filename / Verify with filename

The filename (without the extension) contains the hash for the file content.
