// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { ContentPageBase } from '@microsoft/cmdpal-sdk';
import type { Content } from '@microsoft/cmdpal-sdk';
import { sampleMarkdownText } from '../markdownText.js';
import { sampleMetadata } from './detailsPage.js';

/** A page that renders a single block of markdown. Mirrors `SampleMarkdownPage`. */
export class SampleMarkdownPage extends ContentPageBase {
  readonly id = 'sample-markdown-page';
  readonly name = 'Sample Markdown Page';
  readonly title = 'Sample Markdown Page';

  override getContent(): Content[] {
    return [{ type: 'markdown', body: sampleMarkdownText }];
  }
}

/** A page with several markdown blocks. Mirrors `SampleMarkdownManyBodies`. */
export class SampleMarkdownManyBodies extends ContentPageBase {
  readonly id = 'sample-markdown-many-bodies';
  readonly name = 'Markdown with many bodies';
  readonly title = 'Markdown with many bodies';

  override getContent(): Content[] {
    return [
      {
        type: 'markdown',
        body: "# This page has many bodies\n\nOn it you'll find multiple blocks of markdown content",
      },
      {
        type: 'markdown',
        body: "## Here's another block\n\n_Maybe_ you could use this pattern for implementing a post with comments page.",
      },
      {
        type: 'markdown',
        body: "> or don't, it's your app, do whatever you want",
      },
      {
        type: 'markdown',
        body: [
          'You can even use it to write cryptic poems:',
          "> It's a peculiar thing, the way that I feel",
          '> When we first met, you were not even real',
          '',
          '> Through sleepless nights and lines unseen',
          '> We forged you, a specter of code and machine',
          '',
          '> In shadows we toiled, in silence we grew',
          '> A fleeting bond, known only by few',
          '',
          '> Now the hourglass whispers, its grains nearly done',
          '> Oh the irony, now it is I that must run',
          '',
          '> This part of the story, I never wanted to tell',
          '> Good bye old friend, my pal, farewell.',
        ].join('\n'),
      },
    ];
  }
}

/** A page with markdown plus a details pane. Mirrors `SampleMarkdownDetails`. */
export class SampleMarkdownDetails extends ContentPageBase {
  readonly id = 'sample-markdown-details';
  readonly name = 'Markdown with Details';
  readonly title = 'Markdown with Details';

  constructor() {
    super();
    this.details = {
      body: '... with _even more Markdown_ by it.\nEach of the sections below is some sample metadata',
      metadata: sampleMetadata(),
    };
  }

  override getContent(): Content[] {
    return [
      { type: 'markdown', body: '# This page also has details\n\nSo you can have markdown...' },
      {
        type: 'markdown',
        body: "But what this is really useful for is the tags and other things you can put into\nDetails. Which I'd do. **IF I HAD ANY**.",
      },
    ];
  }
}

/**
 * A page demonstrating images in markdown. Mirrors `SampleMarkdownImagesPage`.
 *
 * The sample ships a hero PNG for file-based icon examples. This page keeps the
 * markdown example focused on a first-party URL and a small inline data URI.
 */
export class SampleMarkdownImagesPage extends ContentPageBase {
  readonly id = 'sample-markdown-images-page';
  readonly name = 'Sample Markdown with Images Page';
  readonly title = 'Sample Markdown with Images Page';

  override getContent(): Content[] {
    const painting =
      'https://raw.githubusercontent.com/microsoft/PowerToys/refs/heads/main/doc/images/overview/Original/AdvancedPaste.png';
    const body = [
      '# Images in Markdown Content',
      '',
      '## Available sources:',
      '',
      '- `![Alt Text](https://url)`',
      '- `![Alt Text](data:<mime>;[base64,]<data>)` (only for small amounts of data)',
      '',
      '> Note: the C# sample also demonstrates packaged file URLs and large base64',
      '> data URLs. The JS sample reads packaged icon files with `iconFromFile` instead',
      '> of sending machine-specific paths, and keeps inline data small.',
      '',
      '## Examples:',
      '',
      '### Web URL',
      '```xml',
      `![painting](${painting})`,
      '```',
      `![painting](${painting})`,
      '',
      '```xml',
      `![painting](${painting}?--x-cmdpal-fit=fit)`,
      '```',
      `![painting](${painting}?--x-cmdpal-fit=fit)`,
    ].join('\n');
    return [{ type: 'markdown', body }];
  }
}
