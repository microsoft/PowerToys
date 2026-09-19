// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { describe, expect, it } from 'vitest';
import { encodeMessage, MessageFramer } from '../src/runtime/framing.js';

function decodeAll(framer: MessageFramer, chunk: Buffer): unknown[] {
  return framer.push(chunk).map((body) => JSON.parse(body) as unknown);
}

describe('encodeMessage', () => {
  it('emits an LSP Content-Length header followed by the UTF-8 body', () => {
    const encoded = encodeMessage({ hello: 'world' });
    const text = encoded.toString('utf8');
    expect(text).toBe('Content-Length: 17\r\n\r\n{"hello":"world"}');
  });

  it('uses the byte length of the body, not the character length', () => {
    const message = { text: 'cafe\u0301 \u{1F600} \u4E2D' };
    const body = Buffer.from(JSON.stringify(message), 'utf8');
    const encoded = encodeMessage(message);
    const headerText = encoded
      .subarray(0, encoded.indexOf(Buffer.from('\r\n\r\n')))
      .toString('ascii');
    expect(headerText).toBe(`Content-Length: ${String(body.length)}`);
    // The JSON string has fewer characters than the body has bytes.
    expect(JSON.stringify(message).length).toBeLessThan(body.length);
  });
});

describe('MessageFramer round-trip', () => {
  it('decodes a single message delivered in one chunk', () => {
    const framer = new MessageFramer();
    const message = { jsonrpc: '2.0', id: 1, method: 'initialize' };
    const decoded = decodeAll(framer, encodeMessage(message));
    expect(decoded).toEqual([message]);
  });

  it('round-trips a message containing multi-byte UTF-8 characters', () => {
    const framer = new MessageFramer();
    const message = { title: '\u4E2D\u6587 caf\u00E9 \u{1F680}\u{1F44D}', tag: '\u00F1' };
    const decoded = decodeAll(framer, encodeMessage(message));
    expect(decoded).toEqual([message]);
  });

  it('reassembles a message split across many partial reads', () => {
    const framer = new MessageFramer();
    const message = { value: '\u{1F600}\u{1F601}\u{1F602} spread across bytes' };
    const encoded = encodeMessage(message);

    const results: unknown[] = [];
    for (let i = 0; i < encoded.length; i += 1) {
      results.push(...decodeAll(framer, encoded.subarray(i, i + 1)));
    }
    expect(results).toEqual([message]);
  });

  it('does not emit a message until every body byte has arrived', () => {
    const framer = new MessageFramer();
    const message = { key: '\u{1F510}' };
    const encoded = encodeMessage(message);

    const split = encoded.length - 1;
    expect(framer.push(encoded.subarray(0, split))).toEqual([]);
    const finished = framer.push(encoded.subarray(split)).map((b) => JSON.parse(b) as unknown);
    expect(finished).toEqual([message]);
  });

  it('decodes several coalesced messages from a single chunk', () => {
    const framer = new MessageFramer();
    const first = { id: 1, method: 'a' };
    const second = { id: 2, method: 'b' };
    const third = { id: 3, method: 'c', params: { text: '\u4E2D' } };
    const combined = Buffer.concat([
      encodeMessage(first),
      encodeMessage(second),
      encodeMessage(third),
    ]);
    expect(decodeAll(framer, combined)).toEqual([first, second, third]);
  });

  it('resynchronizes after a header block with no Content-Length', () => {
    const framer = new MessageFramer();
    const garbage = Buffer.from('X-Nonsense: 1\r\n\r\n', 'ascii');
    const good = encodeMessage({ ok: true });
    expect(decodeAll(framer, Buffer.concat([garbage, good]))).toEqual([{ ok: true }]);
  });
});
