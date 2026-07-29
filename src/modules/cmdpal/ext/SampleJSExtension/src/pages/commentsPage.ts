// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { ContentPageBase, ExtensionHost } from '@microsoft/cmdpal-sdk';
import type { CommandResult, Content, FormContent, TreeContent } from '@microsoft/cmdpal-sdk';
import { icon } from '../util.js';

const postTemplate = JSON.stringify({
  $schema: 'http://adaptivecards.io/schemas/adaptive-card.json',
  type: 'AdaptiveCard',
  version: '1.6',
  body: [{ type: 'TextBlock', text: '${postBody}', wrap: true }],
  actions: [
    {
      type: 'Action.ShowCard',
      title: '${replyCard.title}',
      card: {
        type: 'AdaptiveCard',
        $schema: 'http://adaptivecards.io/schemas/adaptive-card.json',
        version: '1.6',
        body: [
          {
            type: 'Container',
            id: '${replyCard.idPrefix}Properties',
            items: [
              {
                $data: '${replyCard.fields}',
                type: 'Input.Text',
                label: '${label}',
                id: '${id}',
                isRequired: '${required}',
                isMultiline: true,
                errorMessage: "'${label}' is required",
              },
            ],
          },
        ],
        actions: [{ type: 'Action.Submit', title: 'Post' }],
      },
    },
    { type: 'Action.Submit', title: 'Favorite' },
    { type: 'Action.Submit', title: 'View on web' },
  ],
});

/**
 * A single post in the comment tree. Mirrors the C# `PostContent`/`PostForm`.
 *
 * Each post owns a stable `formId` so the host can route a reply submission back
 * to this exact post even though the post's form lives deep in a lazily authored
 * tree. The ids are minted once per post instance, and the page keeps its post
 * instances alive (see `SampleCommentsPage`), so a reply added to a post is
 * retained on the model.
 *
 * Note on refresh: a content page's tree is serialized in full when the host
 * calls `contentPage/getContent`, and the host does not re-fetch that content in
 * response to a form submit (the content-page proxy exposes no live
 * `ItemsChanged` channel, and expanding a tree branch does not re-query the
 * extension). A reply therefore does not appear in the thread that is already on
 * screen; it shows up the next time the page's content is loaded, which happens
 * when the user navigates away and reopens the page. This sample is deliberately
 * honest about that rather than claiming an on-screen refresh it cannot perform.
 */
class Post implements TreeContent {
  readonly type = 'tree';
  readonly replies: Post[] = [];
  readonly formId: string;

  constructor(private readonly body: string) {
    this.formId = `comment-form-${nextPostId()}`;
  }

  get rootContent(): Content {
    const dataJson = JSON.stringify({
      postBody: this.body,
      replyCard: {
        title: 'Reply',
        idPrefix: 'reply',
        fields: [
          { label: 'Reply', id: 'ReplyBody', required: true, placeholder: 'Write a reply here' },
        ],
      },
    });

    const form: FormContent = {
      type: 'form',
      formId: this.formId,
      templateJson: postTemplate,
      dataJson,
      submitForm: (inputs: string): CommandResult => {
        try {
          const parsed = JSON.parse(inputs) as { ReplyBody?: string };
          const reply = parsed.ReplyBody;
          if (reply) {
            this.replies.push(new Post(reply));
            // The reply is saved to the model, but the thread already on screen
            // is not re-fetched (see the class note above), so the status is
            // honest about when it will be visible instead of implying a live
            // refresh.
            ExtensionHost.showStatus('Reply saved. Reopen this page to see it.', 'success');
          }
        } catch {
          // Ignore malformed form payloads.
        }
        return { kind: 'keepOpen' };
      },
    };
    return form;
  }

  getChildren(): Content[] {
    return [...this.replies];
  }
}

let postCounter = 0;
function nextPostId(): number {
  postCounter += 1;
  return postCounter;
}

function post(body: string, replies: string[] = []): Post {
  const p = new Post(body);
  for (const reply of replies) {
    p.replies.push(new Post(reply));
  }
  return p;
}

/** A page of nested comment threads. Mirrors the C# `SampleCommentsPage`. */
export class SampleCommentsPage extends ContentPageBase {
  readonly id = 'sample-comments-page';
  readonly name = 'View Posts';
  readonly title = 'View Posts';

  override icon = icon('\uE90A');

  private readonly posts: Post[] = [
    post('First', ["Oh very insightful. I hadn't considered that", 'Second', 'ah the ol switcheroo']),
    post('First\nEDIT: shoot', ['delete this']),
    post('Do you think they get the picture', ['Probably! Now go build and be happy']),
  ];

  private readonly tree: TreeContent = {
    type: 'tree',
    rootContent: {
      type: 'markdown',
      body: [
        '# Example of a thread of comments',
        'You can use TreeContent in combination with FormContent to build a structure like a page with comments.',
        '',
        'The forms on this page use the AdaptiveCard `Action.ShowCard` action to show a nested, hidden card on the form.',
      ].join('\n'),
    },
    getChildren: (): Content[] => [...this.posts],
  };

  override getContent(): Content[] {
    return [this.tree];
  }
}
