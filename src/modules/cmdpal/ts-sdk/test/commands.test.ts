// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { describe, expect, it, vi } from 'vitest';
import { NoOpCommand, OpenUrlCommand } from '../src/index.js';
import { serializeCommandResult } from '../src/runtime/commandResult.js';

describe('NoOpCommand', () => {
  it('keeps the palette open', () => {
    expect(new NoOpCommand().invoke()).toEqual({ kind: 'keepOpen' });
  });
});

describe('OpenUrlCommand', () => {
  it('opens the URL through the injected opener and dismisses', () => {
    const opener = vi.fn();
    const command = new OpenUrlCommand('https://example.com/?a=1&b=2', 'Example', opener);

    const result = command.invoke();

    expect(opener).toHaveBeenCalledTimes(1);
    expect(opener).toHaveBeenCalledWith('https://example.com/?a=1&b=2');
    expect(result).toEqual({ kind: 'dismiss' });
  });

  it('does not lose the URL to a dropped result argument', () => {
    // Regression: the URL is delivered by the opener side effect, not by a
    // result argument that the dismiss serialization would discard.
    const opener = vi.fn();
    const command = new OpenUrlCommand('https://example.com/', undefined, opener);

    const wire = serializeCommandResult(command.invoke());

    expect(wire).toEqual({ Kind: 0 });
    expect(opener).toHaveBeenCalledWith('https://example.com/');
  });

  it('derives an id and defaults the name to the URL', () => {
    const command = new OpenUrlCommand('https://example.com/', undefined, vi.fn());
    expect(command.id).toBe('open-url:https://example.com/');
    expect(command.name).toBe('https://example.com/');
  });
});
