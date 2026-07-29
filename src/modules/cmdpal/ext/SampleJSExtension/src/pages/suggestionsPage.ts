// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { DynamicListPageBase, ListItemBase, NoOpCommand } from '@microsoft/cmdpal-sdk';
import type { IListItem } from '@microsoft/cmdpal-sdk';
import { icon } from '../util.js';

/**
 * A demo of prefixed "nested" search suggestions. Mirrors the intent of the C#
 * `SampleSuggestionsPage`.
 *
 * Approximation: the C# version tracks the caret position and wraps picked
 * tokens in zero-width spaces, and it uses `IExtendedAttributesProvider` to opt
 * into token search. Neither the caret position nor extended attributes are
 * exposed to JS extensions, so this sample keys off the last word of the query
 * and uses `textToSuggest` to place a pick back into the search box.
 */
export class SampleSuggestionsPage extends DynamicListPageBase {
  readonly id = 'sample-suggestions-page';
  readonly name = 'Open';
  readonly title = 'Sample prefixed search';

  override icon = icon('\uE779');
  override placeholderText = "Type a query, and use '@' to add a person";

  override setSearchText(text: string): void {
    this.searchText = text;
    this.notifyItemsChanged();
  }

  override getItems(): IListItem[] {
    const text = this.searchText ?? '';
    const lastWord = text.split(/\s+/).pop() ?? '';

    if (lastWord.startsWith('@')) {
      return this.peopleItems(text, lastWord);
    }

    if (lastWord.startsWith('/')) {
      return this.commandItems(text, lastWord);
    }

    if (text.length === 0) {
      return [];
    }

    return [
      new ListItemBase({
        command: new NoOpCommand('suggestions-query'),
        title: text,
        subtitle: 'no tokens',
        icon: icon('\uE8F2'),
      }),
    ];
  }

  private peopleItems(fullText: string, prefixWord: string): IListItem[] {
    const base = fullText.slice(0, fullText.length - prefixWord.length);
    const items: IListItem[] = [];
    for (let i = 1; i <= 5; i++) {
      const name = `Person ${i}`;
      items.push(
        new ListItemBase({
          command: new NoOpCommand(`suggestions-person-${i}`),
          title: name,
          subtitle: `Email: person${i}@example.com`,
          textToSuggest: `${base}${name} `,
        }),
      );
    }
    return items;
  }

  private commandItems(fullText: string, prefixWord: string): IListItem[] {
    const base = fullText.slice(0, fullText.length - prefixWord.length);
    return [
      new ListItemBase({
        command: new NoOpCommand('suggestions-chat'),
        title: '/chat',
        subtitle: 'send a message',
        textToSuggest: `${base}chat `,
      }),
      new ListItemBase({
        command: new NoOpCommand('suggestions-status'),
        title: '/status',
        subtitle: 'set your status',
        textToSuggest: `${base}status `,
      }),
    ];
  }
}
