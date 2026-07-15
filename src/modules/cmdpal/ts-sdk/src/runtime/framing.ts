// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * LSP-style `Content-Length` framing over a byte stream, as used by the
 * Command Palette JSON-RPC transport. Each message is:
 *
 * ```
 * Content-Length: <byte-count>\r\n
 * \r\n
 * <UTF-8 JSON body>
 * ```
 *
 * where `<byte-count>` is the UTF-8 byte length of the body, not its character
 * length.
 */

const HEADER_TERMINATOR = Buffer.from('\r\n\r\n', 'ascii');
const CONTENT_LENGTH_PREFIX = 'content-length:';

/** Serializes a value into a single framed message buffer. */
export function encodeMessage(message: unknown): Buffer {
  const body = Buffer.from(JSON.stringify(message), 'utf8');
  const header = Buffer.from(`Content-Length: ${String(body.length)}\r\n\r\n`, 'ascii');
  return Buffer.concat([header, body]);
}

/** Reads the `Content-Length` value from a header block, or `null` when absent. */
function parseContentLength(headerBlock: string): number | null {
  for (const line of headerBlock.split('\r\n')) {
    const separator = line.indexOf(':');
    if (separator === -1) {
      continue;
    }
    const name = line.slice(0, separator).trim().toLowerCase();
    if (`${name}:` !== CONTENT_LENGTH_PREFIX) {
      continue;
    }
    const value = Number.parseInt(line.slice(separator + 1).trim(), 10);
    return Number.isNaN(value) || value < 0 ? null : value;
  }
  return null;
}

/**
 * Incremental decoder for framed messages. Feed it raw chunks with
 * {@link MessageFramer.push}; it buffers partial data and returns each complete
 * message body once all of its bytes have arrived. It tolerates chunk
 * boundaries that fall anywhere, including in the middle of a multi-byte UTF-8
 * sequence, and coalesced chunks that carry several messages at once.
 */
export class MessageFramer {
  private buffer: Buffer = Buffer.alloc(0);
  private expectedLength: number | null = null;

  /** Appends a chunk and returns any newly completed message bodies. */
  push(chunk: Buffer): string[] {
    this.buffer = this.buffer.length === 0 ? chunk : Buffer.concat([this.buffer, chunk]);

    const messages: string[] = [];
    for (;;) {
      if (this.expectedLength === null) {
        const headerEnd = this.buffer.indexOf(HEADER_TERMINATOR);
        if (headerEnd === -1) {
          break;
        }
        const headerBlock = this.buffer.subarray(0, headerEnd).toString('ascii');
        const length = parseContentLength(headerBlock);
        this.buffer = this.buffer.subarray(headerEnd + HEADER_TERMINATOR.length);
        if (length === null) {
          // Malformed or unsupported header block; drop it and resynchronize.
          continue;
        }
        this.expectedLength = length;
      }

      if (this.buffer.length < this.expectedLength) {
        break;
      }

      const body = this.buffer.subarray(0, this.expectedLength).toString('utf8');
      this.buffer = this.buffer.subarray(this.expectedLength);
      this.expectedLength = null;
      messages.push(body);
    }
    return messages;
  }
}
