// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import {
  ContentPage,
  MarkdownContent,
  FormContent,
  TreeContent,
  CommandResult,
  IContent,
  ICommandResult,
} from '@cmdpal/sdk';

// ---------------------------------------------------------------------------
// Leaf content nodes
// ---------------------------------------------------------------------------

class IntroMarkdown extends MarkdownContent {
  constructor() {
    super(
      '## 🌲 Tree Content Demo\n\n' +
      'This page demonstrates **TreeContent** — a hierarchical content structure ' +
      'that can nest markdown, forms, and other tree nodes.\n\n' +
      'Below you\'ll see tree nodes containing different content types.',
    );
    this.id = 'tree-intro';
  }
}

class ChildMarkdown extends MarkdownContent {
  constructor(id: string, body: string) {
    super(body);
    this.id = id;
  }
}

// ---------------------------------------------------------------------------
// Form inside the tree
// ---------------------------------------------------------------------------

const FEEDBACK_FORM = JSON.stringify({
  $schema: 'http://adaptivecards.io/schemas/adaptive-card.json',
  type: 'AdaptiveCard',
  version: '1.5',
  body: [
    {
      type: 'TextBlock',
      text: 'Quick Feedback',
      size: 'Medium',
      weight: 'Bolder',
    },
    {
      type: 'Input.ChoiceSet',
      id: 'rating',
      label: 'How useful is this demo?',
      style: 'expanded',
      choices: [
        { title: '⭐ Very useful', value: '5' },
        { title: '👍 Somewhat useful', value: '3' },
        { title: '🤷 Not sure', value: '1' },
      ],
    },
    {
      type: 'Input.Text',
      id: 'comment',
      label: 'Comments (optional)',
      placeholder: 'Share your thoughts...',
      isMultiline: true,
    },
  ],
  actions: [
    {
      type: 'Action.Submit',
      title: 'Submit Feedback',
    },
  ],
});

class TreeFeedbackForm extends FormContent {
  id = 'tree-feedback-form';

  submitForm(inputs: string, _data: string): ICommandResult {
    const parsed = JSON.parse(inputs);
    const rating = parsed.rating || 'none';
    const comment = parsed.comment || '(no comment)';
    return CommandResult.showToast(`Feedback received! Rating: ${rating}, Comment: ${comment}`);
  }
}

// Wrap the FormContent with the Adaptive Card template for rendering
class TreeFeedbackFormContent extends TreeFeedbackForm {
  // The template property is read by the C# side for rendering
  get Template(): string {
    return FEEDBACK_FORM;
  }

  get Data(): string {
    return '{}';
  }
}

// ---------------------------------------------------------------------------
// Tree nodes
// ---------------------------------------------------------------------------

class LeafTreeNode extends TreeContent {
  private readonly _children: IContent[];

  constructor(id: string, children: IContent[]) {
    super();
    this.id = id;
    this._children = children;
  }

  getChildren(): IContent[] {
    return this._children;
  }
}

class RootTreeNode extends TreeContent {
  private readonly _children: IContent[];

  constructor(children: IContent[]) {
    super();
    this.id = 'tree-root';
    this._children = children;
  }

  getChildren(): IContent[] {
    return this._children;
  }
}

// ---------------------------------------------------------------------------
// Tree content page
// ---------------------------------------------------------------------------

/**
 * Demonstrates TreeContent — nested hierarchical content that can contain:
 * - MarkdownContent leaves
 * - FormContent leaves
 * - Other TreeContent nodes (nested trees)
 *
 * Similar to WinRT SampleTreeContentPage.
 */
export class TreeContentPage extends ContentPage {
  id = 'tree-content';
  name = 'Tree Content';

  getContent(): IContent[] {
    const intro = new IntroMarkdown();

    // Level 1 tree: contains markdown children
    const level1 = new LeafTreeNode('tree-level1', [
      new ChildMarkdown('tree-l1-child1', '### 📝 First Child\n\nA markdown node inside a tree.'),
      new ChildMarkdown('tree-l1-child2', '### 📝 Second Child\n\nAnother sibling markdown node.'),
    ]);

    // Level 2 tree: contains a nested tree
    const nestedTree = new LeafTreeNode('tree-nested', [
      new ChildMarkdown(
        'tree-nested-child',
        '### 🔄 Nested Node\n\n' +
        'This markdown is inside a **nested tree** — a tree within a tree!\n\n' +
        '> TreeContent can nest to arbitrary depth.',
      ),
    ]);

    const level2 = new LeafTreeNode('tree-level2', [
      new ChildMarkdown('tree-l2-intro', '### 🌳 Tree with Nested Tree\n\nThis tree contains a child tree:'),
      nestedTree,
    ]);

    // Level 3: tree with form content
    const level3 = new LeafTreeNode('tree-level3', [
      new ChildMarkdown(
        'tree-l3-intro',
        '### 📋 Tree with Form\n\nTree nodes can contain forms too:',
      ),
      new TreeFeedbackFormContent(),
    ]);

    // Root tree wrapping everything
    const root = new RootTreeNode([level1, level2, level3]);

    return [intro, root];
  }
}
