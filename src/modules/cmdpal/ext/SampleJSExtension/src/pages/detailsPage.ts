// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { ListItemBase, ListPageBase, NoOpCommand } from '@microsoft/cmdpal-sdk';
import type { DetailsElement, IListItem } from '@microsoft/cmdpal-sdk';
import { fileURLToPath } from 'node:url';
import { icon, randomColor, rgb, tag } from '../util.js';
import { sampleMarkdownText } from '../markdownText.js';
import { ProgressStatusCommand, StatusMessageCommand } from '../commands/statusCommands.js';

/**
 * Absolute path to the hero image that ships with the sample. The build copies
 * `assets/` into `dist/assets/`, so this file sits next to the compiled output
 * at `dist/assets/hero.png`. Resolving it from `import.meta.url` keeps the path
 * relative to wherever the extension is installed, and the host's icon loader
 * resolves an absolute file path the same way it resolves a glyph or URL. This
 * avoids depending on a network fetch to render the hero image.
 */
const heroImagePath = fileURLToPath(new URL('../assets/hero.png', import.meta.url));

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
    { key: '', data: { type: 'separator' } },
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
          buildProgressButton(
            'metadata-yes',
            'Do something amazing',
            'Doing something amazing...',
            'You clicked it! The details command button works.',
            '\uE945',
          ),
          buildStatusButton(
            'metadata-no',
            "Don't click me",
            'I warned you! The status banner is visible.',
            'error',
            '\uEA39',
          ),
        ],
      },
    },
  ];
}

function buildStatusButton(
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

function buildProgressButton(
  id: string,
  name: string,
  workingMessage: string,
  doneMessage: string,
  glyph: string,
): ProgressStatusCommand {
  const command = new ProgressStatusCommand(name, workingMessage, doneMessage, id);
  command.icon = icon(glyph);
  return command;
}

/**
 * A list page whose items each show a details pane with markdown, tags, links,
 * a hero image, and command metadata. Mirrors the C# `SampleListPageWithDetails`.
 * The hero image is a local asset that ships with the sample, so it renders
 * without a network connection.
 *
 * The details `size` field (small, medium, or large) controls how wide the
 * details pane is; the metadata item below asks for a `large` pane.
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
          heroImage: icon(heroImagePath),
          body: 'It is literally an image of a hero',
        },
      }),
      new ListItemBase({
        command: new NoOpCommand('details-metadata'),
        title: 'This one has metadata',
        subtitle: 'And a large details panel',
        details: {
          title: 'Metadata Example',
          body: 'Each of the sections below is some sample metadata. This item asks for a `large` details pane.',
          metadata: sampleMetadata(),
          size: 'large',
        },
      }),
    ];
  }
}
