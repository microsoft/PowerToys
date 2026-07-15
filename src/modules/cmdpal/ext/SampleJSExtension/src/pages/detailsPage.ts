// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { ListItemBase, ListPageBase, NoOpCommand } from '@microsoft/cmdpal-sdk';
import type { DetailsElement, IListItem } from '@microsoft/cmdpal-sdk';
import { icon, randomColor, rgb, tag } from '../util.js';
import { sampleMarkdownText } from '../markdownText.js';
import { StatusMessageCommand } from '../commands/statusCommands.js';

/**
 * Builds the shared "metadata" rows demonstrated in both the details page and
 * the markdown-with-details page.
 *
 * Note: `DetailsLink` requires both a `link` and a `text` in the JS protocol.
 * The C# sample allows text-only or link-only rows; here an empty string is
 * used for the missing half.
 */
export function sampleMetadata(): DetailsElement[] {
  return [
    { key: 'Plain text', data: { type: 'link', link: '', text: 'Set just the text to get text metadata' } },
    {
      key: 'Links',
      data: { type: 'link', link: 'https://github.com/microsoft/PowerToys', text: 'Or metadata can be links' },
    },
    {
      key: 'CmdPal will display the URL if no text is given',
      data: { type: 'link', link: 'https://github.com/microsoft/PowerToys', text: '' },
    },
    { key: 'Above a separator', data: { type: 'link', link: '', text: 'Below me is a separator' } },
    { key: 'A separator', data: { type: 'separator' } },
    { key: 'Below a separator', data: { type: 'link', link: '', text: 'Above me is a separator' } },
    {
      key: 'Add Tags too',
      data: {
        type: 'tags',
        tags: [
          tag('simple text'),
          { text: 'Colored text', foreground: rgb(255, 0, 0) },
          { text: 'Colored backgrounds', background: rgb(0, 0, 255) },
          { text: 'Colored everything', foreground: rgb(255, 255, 0), background: rgb(0, 0, 255) },
          { text: 'Icons too', icon: icon('\uE735'), foreground: rgb(255, 255, 0) },
          { text: '', icon: icon('https://i.imgur.com/t9qgDTM.png') },
          { text: 'this', foreground: randomColor(), background: randomColor() },
          { text: 'baby', foreground: randomColor(), background: randomColor() },
          { text: 'can', foreground: randomColor(), background: randomColor() },
          { text: 'fit', foreground: randomColor(), background: randomColor() },
          { text: 'so', foreground: randomColor(), background: randomColor() },
          { text: 'many', foreground: randomColor(), background: randomColor() },
          { text: 'tags', foreground: randomColor(), background: randomColor() },
        ],
      },
    },
    {
      key: 'Commands',
      data: {
        type: 'commands',
        commands: [
          buildToast('metadata-yes', 'Do something amazing', 'Hey! You clicked it!', 'success', '\uE945'),
          buildToast('metadata-no', "Don't click me", 'I warned you!', 'error', '\uEA39'),
        ],
      },
    },
  ];
}

function buildToast(
  id: string,
  name: string,
  message: string,
  state: 'success' | 'error',
  glyph: string,
): StatusMessageCommand {
  const command = new StatusMessageCommand(message, state, id);
  command.name = name;
  command.icon = icon(glyph);
  return command;
}

/**
 * A list page whose items each show a details pane with markdown, tags, links,
 * a hero image, and command metadata. Mirrors the C# `SampleListPageWithDetails`.
 *
 * Not-yet-supported: the JS `Details` type has no `Size` (Small/Medium/Large),
 * so the C# size variants collapse into the single default size here.
 */
export class SampleListPageWithDetails extends ListPageBase {
  readonly id = 'sample-list-page-with-details';
  readonly name = 'Sample List Page with Details';
  readonly title = 'Sample List Page with Details';

  override icon = icon('\uE8A0');
  override showDetails = true;

  override getItems(): IListItem[] {
    return [
      new ListItemBase({
        command: new NoOpCommand('details-default'),
        title: 'Details on ListItems',
        details: {
          title: 'This item has default details size',
          body: 'Each of these items can have a `Body` formatted with **Markdown**',
        },
      }),
      new ListItemBase({
        command: new NoOpCommand('details-subtitle'),
        title: 'This one has a subtitle too',
        subtitle: 'Example Subtitle',
        details: { title: 'List Item 2', body: sampleMarkdownText },
      }),
      new ListItemBase({
        command: new NoOpCommand('details-tag'),
        title: 'This one has a tag too',
        subtitle: 'the one with a tag',
        tags: [tag('Sample Tag')],
        details: { title: 'List Item 3', body: '### Example of markdown details' },
      }),
      new ListItemBase({
        command: new NoOpCommand('details-hero'),
        title: 'This one has a hero image',
        details: {
          title: 'Hero Image Example',
          heroImage: icon(
            'https://raw.githubusercontent.com/microsoft/PowerToys/main/doc/images/Logo.png',
          ),
          body: 'It is literally an image of a hero',
        },
      }),
      new ListItemBase({
        command: new NoOpCommand('details-metadata'),
        title: 'This one has metadata',
        subtitle: 'And a details panel',
        details: {
          title: 'Metadata Example',
          body: 'Each of the sections below is some sample metadata',
          metadata: sampleMetadata(),
        },
      }),
    ];
  }
}
