// Copyright (c) Microsoft Corporation
// Licensed under the MIT License.

import { Cache } from '../cache';

describe('Cache', () => {
  beforeEach(() => {
    jest.useFakeTimers();
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  it('stores and retrieves values', () => {
    const cache = new Cache<string>(60_000);
    cache.set('key1', 'value1');
    expect(cache.get('key1')).toBe('value1');
  });

  it('returns undefined for missing keys', () => {
    const cache = new Cache<string>(60_000);
    expect(cache.get('nope')).toBeUndefined();
  });

  it('expires entries after TTL', () => {
    const cache = new Cache<string>(1000);
    cache.set('key1', 'value1');
    expect(cache.get('key1')).toBe('value1');

    jest.advanceTimersByTime(1001);
    expect(cache.get('key1')).toBeUndefined();
  });

  it('supports per-entry TTL override', () => {
    const cache = new Cache<string>(60_000);
    cache.set('short', 'val', 500);
    cache.set('long', 'val', 60_000);

    jest.advanceTimersByTime(501);
    expect(cache.get('short')).toBeUndefined();
    expect(cache.get('long')).toBe('val');
  });

  it('has() returns false for expired entries', () => {
    const cache = new Cache<string>(500);
    cache.set('key1', 'value1');
    expect(cache.has('key1')).toBe(true);

    jest.advanceTimersByTime(501);
    expect(cache.has('key1')).toBe(false);
  });

  it('delete() removes entries', () => {
    const cache = new Cache<string>(60_000);
    cache.set('key1', 'value1');
    cache.delete('key1');
    expect(cache.get('key1')).toBeUndefined();
  });

  it('clear() removes all entries', () => {
    const cache = new Cache<string>(60_000);
    cache.set('a', '1');
    cache.set('b', '2');
    cache.clear();
    expect(cache.size).toBe(0);
  });

  it('prune() removes only expired entries', () => {
    const cache = new Cache<string>(60_000);
    cache.set('short', 'val', 500);
    cache.set('long', 'val', 60_000);

    jest.advanceTimersByTime(501);
    cache.prune();

    expect(cache.get('short')).toBeUndefined();
    expect(cache.get('long')).toBe('val');
  });

  it('size reflects only non-expired entries', () => {
    const cache = new Cache<string>(60_000);
    cache.set('a', '1', 500);
    cache.set('b', '2', 60_000);
    expect(cache.size).toBe(2);

    jest.advanceTimersByTime(501);
    expect(cache.size).toBe(1);
  });
});
