// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import {
  CommandProvider,
  CommandItem,
  ICommandItem,
  ICommand,
  InvokableCommand,
  CommandResult,
  ICommandResult,
  IconInfo,
  Settings,
  TextSetting,
  ToggleSetting,
  ChoiceSetSetting,
  CommandSettings,
  JsonSettingsManager,
} from '@cmdpal/sdk';
import * as path from 'path';

// ---------------------------------------------------------------------------
// Settings configuration
// ---------------------------------------------------------------------------

const extensionSettings = new Settings();

const greetingText = new TextSetting(
  'greeting.text',
  'Greeting Text',
  'The greeting message to display',
  'Hello, World!',
);

const showEmoji = new ToggleSetting(
  'greeting.showEmoji',
  'Show Emoji',
  'Include an emoji in the greeting',
  true,
);

const theme = new ChoiceSetSetting(
  'greeting.theme',
  'Theme',
  'Choose the greeting style',
  [
    { title: 'Friendly', value: 'friendly' },
    { title: 'Formal', value: 'formal' },
    { title: 'Pirate', value: 'pirate' },
  ],
  'friendly',
);

extensionSettings.add(greetingText);
extensionSettings.add(showEmoji);
extensionSettings.add(theme);

// Persist settings to a JSON file next to the extension
const settingsManager = new JsonSettingsManager(
  extensionSettings,
  path.join(__dirname, '..', 'settings.json'),
);
settingsManager.loadSettings();

// ---------------------------------------------------------------------------
// Commands
// ---------------------------------------------------------------------------

class GreetCommand extends InvokableCommand {
  id = 'greet';
  name = 'Show Greeting';

  invoke(): ICommandResult {
    const text = greetingText.value || 'Hello!';
    const emoji = showEmoji.value ? ' 👋' : '';

    let message: string;
    switch (theme.value) {
      case 'formal':
        message = `Good day. ${text}${emoji}`;
        break;
      case 'pirate':
        message = `Ahoy! ${text}${emoji} ☠️`;
        break;
      default:
        message = `${text}${emoji}`;
        break;
    }

    return CommandResult.showToast(message);
  }
}

// ---------------------------------------------------------------------------
// Provider
// ---------------------------------------------------------------------------

export class SampleSettingsProvider extends CommandProvider {
  private readonly _settings: CommandSettings;
  private readonly greetCommand: GreetCommand;

  constructor() {
    super();
    this._settings = new CommandSettings(extensionSettings);
    this.greetCommand = new GreetCommand();
  }

  get id(): string {
    return 'sample-settings-ts';
  }

  get displayName(): string {
    return 'Sample Settings (TypeScript)';
  }

  get icon() {
    return IconInfo.fromGlyph('\uE713');
  }

  /** Expose settings page to Command Palette. */
  override get settings(): CommandSettings {
    return this._settings;
  }

  topLevelCommands(): ICommandItem[] {
    return [
      new CommandItem({
        title: 'Show Greeting',
        subtitle: 'Uses values from settings',
        icon: IconInfo.fromGlyph('\uE76E'),
        command: this.greetCommand,
      }),
    ];
  }

  getCommand(id: string): ICommand | undefined {
    if (id === this.greetCommand.id) {
      return this.greetCommand;
    }
    return undefined;
  }
}
