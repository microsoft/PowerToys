// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { describe, expect, it } from 'vitest';
import type { ContextItem, Details, IListItem } from '../src/types.js';
import { WireSerializer } from '../src/runtime/serialize.js';

describe('WireSerializer.contextItems', () => {
  it('serializes a flat context item without a moreCommands field', () => {
    const items: ContextItem[] = [{ command: { id: 'copy', name: 'Copy' }, title: 'Copy' }];

    const wire = new WireSerializer().contextItems(items);

    expect(wire).toHaveLength(1);
    expect(wire[0]).not.toHaveProperty('moreCommands');
    expect(wire[0]?.command).toMatchObject({ id: 'copy', name: 'Copy' });
  });

  it('serializes nested moreCommands recursively two levels deep', () => {
    const items: ContextItem[] = [
      {
        command: { id: 'share', name: 'Share' },
        title: 'Share',
        moreCommands: [
          {
            command: { id: 'share-email', name: 'Email' },
            title: 'Email',
            moreCommands: [{ command: { id: 'share-email-work', name: 'Work' }, title: 'Work' }],
          },
          { command: { id: 'share-link', name: 'Copy link' }, title: 'Copy link' },
        ],
      },
    ];

    const wire = new WireSerializer().contextItems(items);

    const level1 = wire[0]?.moreCommands as Array<Record<string, unknown>>;
    expect(level1).toHaveLength(2);
    expect(level1[0]).toMatchObject({
      command: { id: 'share-email', name: 'Email' },
      title: 'Email',
    });

    const level2 = level1[0]?.moreCommands as Array<Record<string, unknown>>;
    expect(level2).toHaveLength(1);
    expect(level2[0]).toMatchObject({
      command: { id: 'share-email-work', name: 'Work' },
      title: 'Work',
    });
    expect(level2[0]).not.toHaveProperty('moreCommands');

    // The sibling without nested commands omits the moreCommands field entirely.
    expect(level1[1]).not.toHaveProperty('moreCommands');
  });

  it('registers every nested command so the host can invoke it later', () => {
    const registered: string[] = [];
    const items: ContextItem[] = [
      {
        command: { id: 'outer', name: 'Outer' },
        title: 'Outer',
        moreCommands: [{ command: { id: 'inner', name: 'Inner' }, title: 'Inner' }],
      },
    ];

    new WireSerializer((command) => registered.push(command.id)).contextItems(items);

    expect(registered).toContain('outer');
    expect(registered).toContain('inner');
  });
});

describe('WireSerializer.details', () => {
  it('serializes a string size', () => {
    const details: Details = { title: 'A', size: 'large' };

    const wire = new WireSerializer().details(details);

    expect(wire.size).toBe('large');
  });

  it('serializes a numeric ContentSize', () => {
    const details: Details = { title: 'B', size: 1 };

    const wire = new WireSerializer().details(details);

    expect(wire.size).toBe(1);
  });

  it('omits size when it is not set', () => {
    const details: Details = { title: 'C' };

    const wire = new WireSerializer().details(details);

    expect(wire).not.toHaveProperty('size');
  });
});

describe('WireSerializer.listItem', () => {
  it('serializes textToSuggest when set', () => {
    const item: IListItem = {
      command: { id: 'pick', name: 'Pick' },
      title: 'Person 1',
      textToSuggest: '@Person 1 ',
    };

    const wire = new WireSerializer().listItem(item);

    expect(wire.textToSuggest).toBe('@Person 1 ');
  });

  it('omits textToSuggest when not set', () => {
    const item: IListItem = { command: { id: 'plain', name: 'Plain' }, title: 'Plain' };

    const wire = new WireSerializer().listItem(item);

    expect(wire).not.toHaveProperty('textToSuggest');
  });

  it('serializes an empty textToSuggest', () => {
    const item: IListItem = {
      command: { id: 'empty', name: 'Empty' },
      title: 'Empty',
      textToSuggest: '',
    };

    const wire = new WireSerializer().listItem(item);

    expect(wire.textToSuggest).toBe('');
  });
});
