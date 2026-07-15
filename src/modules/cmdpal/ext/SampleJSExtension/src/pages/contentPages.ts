// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { ContentPageBase } from '@microsoft/cmdpal-sdk';
import type { CommandResult, Content, FormContent } from '@microsoft/cmdpal-sdk';
import { icon } from '../util.js';

const loremIpsum =
  "Lorem Ipsum is simply dummy text of the printing and typesetting industry. Lorem Ipsum has been the industry's standard dummy text ever since the 1500s, when an unknown printer took a galley of type and scrambled it to make a type specimen book. It has survived not only five centuries, but also the leap into electronic typesetting, remaining essentially unchanged. It was popularised in the 1960s with the release of sheets containing Lorem Ipsum passages, and more recently with desktop publishing software like Aldus PageMaker including versions of Lorem Ipsum.";

const sampleFormTemplate = JSON.stringify({
  $schema: 'http://adaptivecards.io/schemas/adaptive-card.json',
  type: 'AdaptiveCard',
  version: '1.6',
  body: [
    { type: 'TextBlock', size: 'medium', weight: 'bolder', text: ' ${ParticipantInfoForm.title}', horizontalAlignment: 'center', wrap: true, style: 'heading' },
    { type: 'Input.Text', label: 'Name', style: 'text', id: 'SimpleVal', isRequired: true, errorMessage: 'Name is required', placeholder: 'Enter your name' },
    { type: 'Input.Text', label: 'Homepage', style: 'url', id: 'UrlVal', placeholder: 'Enter your homepage url' },
    { type: 'Input.Text', label: 'Email', style: 'email', id: 'EmailVal', placeholder: 'Enter your email' },
    { type: 'Input.Text', label: 'Phone', style: 'tel', id: 'TelVal', placeholder: 'Enter your phone number' },
    { type: 'Input.Text', label: 'Comments', style: 'text', isMultiline: true, id: 'MultiLineVal', placeholder: 'Enter any comments' },
    { type: 'Input.Number', label: 'Quantity (Minimum -5, Maximum 5)', min: -5, max: 5, value: 1, id: 'NumVal', errorMessage: 'The quantity must be between -5 and 5' },
    { type: 'Input.Date', label: 'Due Date', id: 'DateVal', value: '2017-09-20' },
    { type: 'Input.Time', label: 'Start time', id: 'TimeVal', value: '16:59' },
    { type: 'TextBlock', size: 'medium', weight: 'bolder', text: '${Survey.title} ', horizontalAlignment: 'center', wrap: true, style: 'heading' },
    { type: 'Input.ChoiceSet', id: 'CompactSelectVal', label: '${Survey.questions[0].question}', style: 'compact', value: '1', choices: [{ $data: '${Survey.questions[0].items}', title: '${choice}', value: '${value}' }] },
    { type: 'Input.ChoiceSet', id: 'SingleSelectVal', label: '${Survey.questions[1].question}', style: 'expanded', value: '1', choices: [{ $data: '${Survey.questions[1].items}', title: '${choice}', value: '${value}' }] },
    { type: 'Input.ChoiceSet', id: 'MultiSelectVal', label: '${Survey.questions[2].question}', isMultiSelect: true, value: '1,3', choices: [{ $data: '${Survey.questions[2].items}', title: '${choice}', value: '${value}' }] },
    { type: 'TextBlock', size: 'medium', weight: 'bolder', text: 'Input.Toggle', horizontalAlignment: 'center', wrap: true, style: 'heading' },
    { type: 'Input.Toggle', label: 'Please accept the terms and conditions:', title: '${Survey.questions[3].question}', valueOn: 'true', valueOff: 'false', id: 'AcceptsTerms', isRequired: true, errorMessage: 'Accepting the terms and conditions is required' },
    { type: 'Input.Toggle', label: 'How do you feel about red cars?', title: '${Survey.questions[4].question}', valueOn: 'RedCars', valueOff: 'NotRedCars', id: 'ColorPreference' },
  ],
  actions: [
    { type: 'Action.Submit', title: 'Submit', data: { id: '1234567890' } },
    { type: 'Action.ShowCard', title: 'Show Card', card: { type: 'AdaptiveCard', body: [{ type: 'Input.Text', label: 'Enter comment', style: 'text', id: 'CommentVal' }], actions: [{ type: 'Action.Submit', title: 'OK' }] } },
  ],
});

const sampleFormData = JSON.stringify({
  ParticipantInfoForm: { title: 'Input.Text elements' },
  Survey: {
    title: 'Input ChoiceSet',
    questions: [
      { question: 'What color do you want? (compact)', items: [{ choice: 'Red', value: '1' }, { choice: 'Green', value: '2' }, { choice: 'Blue', value: '3' }] },
      { question: 'What color do you want? (expanded)', items: [{ choice: 'Red', value: '1' }, { choice: 'Green', value: '2' }, { choice: 'Blue', value: '3' }] },
      { question: 'What color do you want? (multiselect)', items: [{ choice: 'Red', value: '1' }, { choice: 'Green', value: '2' }, { choice: 'Blue', value: '3' }] },
      { question: 'I accept the terms and conditions (True/False)' },
      { question: 'Red cars are better than other cars' },
    ],
  },
});

function sampleContentForm(): FormContent {
  return {
    type: 'form',
    templateJson: sampleFormTemplate,
    dataJson: sampleFormData,
    submitForm(): CommandResult {
      return { kind: 'goHome' };
    },
  };
}

/** A page mixing markdown and a form. Mirrors the C# `SampleContentPage`. */
export class SampleContentPage extends ContentPageBase {
  readonly id = 'sample-content-page';
  readonly name = 'Open';
  readonly title = 'Sample Content';

  override icon = icon('\uECA5');

  override getContent(): Content[] {
    return [
      { type: 'markdown', body: '# Sample page with mixed content \n This page has both markdown, and form content' },
      sampleContentForm(),
    ];
  }
}

/** A page of plain text content. Mirrors the C# `SamplePlainTextContentPage`. */
export class SamplePlainTextContentPage extends ContentPageBase {
  readonly id = 'sample-plain-text-content-page';
  readonly name = 'Plain Text';
  readonly title = 'Sample Plain Text Content';

  override icon = icon('\uE8D2');

  override getContent(): Content[] {
    return [
      {
        type: 'plainText',
        text: `# Sample Plain Text Content\nThis is a sample plain text content page.\n\nYou can right-click the content and switch wrap mode on or off, or change the font.\n\n${loremIpsum}`,
      },
      {
        type: 'plainText',
        text: `# Sample Plain Text Content\nThis is a sample plain text content page. This one is monospace and wraps by default.\n\nYou can right-click the content and switch wrap mode on or off, or change the font.\n\n${loremIpsum}`,
        fontFamily: 'monospace',
        wrapWords: true,
      },
    ];
  }
}

/**
 * A page showing images. Mirrors the C# `SampleImageContentPage`.
 *
 * Approximation: the C# page loads packaged JPG and SVG assets. This sample
 * ships no binary assets, so a web-hosted image URL stands in.
 */
export class SampleImageContentPage extends ContentPageBase {
  readonly id = 'sample-image-content-page';
  readonly name = 'Image';
  readonly title = 'Sample Image Content';

  override icon = icon('\uE722');

  override getContent(): Content[] {
    const image = icon(
      'https://raw.githubusercontent.com/microsoft/PowerToys/main/doc/images/Logo.png',
    );
    return [
      { type: 'image', image },
      { type: 'image', image, maxWidth: 200, maxHeight: 200 },
    ];
  }
}

/** A page with a tree of nested content. Mirrors the C# `SampleTreeContentPage`. */
export class SampleTreeContentPage extends ContentPageBase {
  readonly id = 'sample-tree-content-page';
  readonly name = 'Sample Content';
  readonly title = 'Sample Content';

  override icon = icon('\uE81E');

  override getContent(): Content[] {
    const nestedForm: FormContent = {
      type: 'form',
      templateJson: JSON.stringify({
        $schema: 'http://adaptivecards.io/schemas/adaptive-card.json',
        type: 'AdaptiveCard',
        version: '1.6',
        body: [
          { type: 'TextBlock', size: 'medium', weight: 'bolder', text: "Mix and match why don't you", horizontalAlignment: 'center', wrap: true, style: 'heading' },
          { type: 'TextBlock', text: 'You can have forms here too', horizontalAlignment: 'Right', wrap: true },
        ],
        actions: [{ type: 'Action.Submit', title: "It's a form, you get it", data: { id: 'LoginVal' } }],
      }),
      dataJson: '{}',
      submitForm(): CommandResult {
        return { kind: 'goHome' };
      },
    };

    const tree: Content = {
      type: 'tree',
      rootContent: { type: 'markdown', body: '# This page has nested content' },
      getChildren(): Content[] {
        return [
          {
            type: 'tree',
            rootContent: { type: 'markdown', body: 'Yo dog' },
            getChildren(): Content[] {
              return [
                {
                  type: 'tree',
                  rootContent: { type: 'markdown', body: 'I heard you like content' },
                  getChildren(): Content[] {
                    return [
                      { type: 'markdown', body: 'So we put content in your content' },
                      nestedForm,
                      { type: 'markdown', body: 'Another markdown down here' },
                    ];
                  },
                },
                { type: 'markdown', body: '**slaps roof**' },
                { type: 'markdown', body: 'This baby can fit so much content' },
              ];
            },
          },
        ];
      },
    };

    return [tree];
  }
}
