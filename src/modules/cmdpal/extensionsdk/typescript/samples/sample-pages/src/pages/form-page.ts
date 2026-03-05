// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import {
  ContentPage,
  MarkdownContent,
  FormContent,
  CommandResult,
  IContent,
  ICommandResult,
} from '@cmdpal/sdk';

// ---------------------------------------------------------------------------
// Adaptive Card template for the sample form
// ---------------------------------------------------------------------------

const FEEDBACK_FORM_TEMPLATE = JSON.stringify({
  $schema: 'http://adaptivecards.io/schemas/adaptive-card.json',
  type: 'AdaptiveCard',
  version: '1.6',
  body: [
    {
      type: 'TextBlock',
      text: '📝 Feedback Form',
      size: 'Large',
      weight: 'Bolder',
    },
    {
      type: 'TextBlock',
      text: 'Tell us what you think of the TypeScript SDK!',
      wrap: true,
    },
    {
      type: 'Input.Text',
      id: 'name',
      label: 'Your Name',
      placeholder: 'Enter your name',
      isRequired: true,
      errorMessage: 'Name is required',
    },
    {
      type: 'Input.Text',
      id: 'feedback',
      label: 'Feedback',
      placeholder: 'Share your thoughts...',
      isMultiline: true,
      maxLength: 500,
    },
    {
      type: 'Input.ChoiceSet',
      id: 'rating',
      label: 'Rating',
      value: '5',
      choices: [
        { title: '⭐⭐⭐⭐⭐ Excellent', value: '5' },
        { title: '⭐⭐⭐⭐ Good', value: '4' },
        { title: '⭐⭐⭐ Average', value: '3' },
        { title: '⭐⭐ Below Average', value: '2' },
        { title: '⭐ Poor', value: '1' },
      ],
    },
    {
      type: 'Input.Toggle',
      id: 'subscribe',
      title: 'Subscribe to updates',
      value: 'true',
      valueOn: 'true',
      valueOff: 'false',
    },
    {
      type: 'Input.ChoiceSet',
      id: 'features',
      label: 'Favorite Features (select multiple)',
      isMultiSelect: true,
      choices: [
        { title: 'List Pages', value: 'lists' },
        { title: 'Dynamic Search', value: 'search' },
        { title: 'Markdown Content', value: 'markdown' },
        { title: 'Forms', value: 'forms' },
        { title: 'Hot Reload', value: 'hotreload' },
        { title: 'Toasts & Confirmations', value: 'toasts' },
      ],
    },
  ],
  actions: [
    {
      type: 'Action.Submit',
      title: 'Submit Feedback',
      style: 'positive',
    },
  ],
});

const FEEDBACK_FORM_DATA = JSON.stringify({});

// ---------------------------------------------------------------------------
// Form content implementation
// ---------------------------------------------------------------------------

class FeedbackFormContent extends FormContent {
  get templateJson(): string {
    return FEEDBACK_FORM_TEMPLATE;
  }

  get dataJson(): string {
    return FEEDBACK_FORM_DATA;
  }

  get stateJson(): string {
    return '{}';
  }

  submitForm(inputs: string, data: string): ICommandResult {
    try {
      const parsed = JSON.parse(inputs);
      const name = parsed.name || 'Anonymous';
      const rating = parsed.rating || '?';
      const subscribe = parsed.subscribe === 'true' ? 'Yes' : 'No';
      const features = parsed.features || 'None selected';

      return CommandResult.showToast(
        `Thanks ${name}! Rating: ${rating}/5, Subscribe: ${subscribe}, Favorites: ${features}`,
      );
    } catch {
      return CommandResult.showToast('Error processing form submission');
    }
  }
}

// ---------------------------------------------------------------------------
// Form demo page
// ---------------------------------------------------------------------------

/**
 * Demonstrates a content page with:
 * - Introductory markdown text above the form
 * - An Adaptive Card form with various input types
 * - Form submission handling with a toast result
 */
export class FormDemoPage extends ContentPage {
  id = 'form-demo';
  name = 'Form Demo';
  commands = [];

  getContent(): IContent[] {
    return [
      new MarkdownContent(
        '# 📋 Adaptive Card Form\n\n' +
          'This page demonstrates how TypeScript extensions can collect user input ' +
          'using **Adaptive Cards**. The form below includes text fields, dropdowns, ' +
          'toggles, and multi-select options.\n\n' +
          'Fill out the form and click **Submit** to see the result as a toast notification.',
      ),
      new FeedbackFormContent(),
    ];
  }
}
