// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import {
  ChoiceSetSetting,
  ContentPageBase,
  ExtensionHost,
  Settings,
  TextSetting,
  ToggleSetting,
} from '@microsoft/cmdpal-sdk';
import type { Content, SettingChoice } from '@microsoft/cmdpal-sdk';
import { icon } from '../util.js';

const choices: SettingChoice[] = [
  { title: 'The first choice in the list is the default choice', value: '0' },
  { title: 'Choices have titles and values', value: '1' },
  { title: 'Title', value: 'Value' },
  { title: 'The options are endless', value: '3' },
  { title: 'So many choices', value: '4' },
];

/**
 * A demo of the settings helpers. Mirrors the C# `SampleSettingsPage`, which
 * builds a `Settings` object and renders it as a form via `ToContent()`.
 */
export class SampleSettingsPage extends ContentPageBase {
  readonly id = 'sample-settings-page';
  readonly name = 'Sample Settings';
  readonly title = 'Sample Settings';

  override icon = icon('\uE713');

  private readonly settings = new Settings();

  constructor() {
    super();
    this.settings.add(
      new ToggleSetting('onOff', 'This is a toggle', true, 'It produces a simple checkbox'),
    );
    this.settings.add(
      new TextSetting('someText', 'This is a text box', 'initial value', 'For some string of text'),
    );
    this.settings.add(
      new ChoiceSetSetting(
        'choiceSetExample',
        'It also has a label',
        choices,
        '0',
        'Describe your choice set setting here',
      ),
    );
  }

  override getContent(): Content[] | Promise<Content[]> {
    const onOff = this.settings.getSetting<ToggleSetting>('onOff')?.value;
    ExtensionHost.log(`SampleSettingsPage: current value of onOff is ${onOff}`);
    return this.settings.settingsPage.getContent();
  }
}
