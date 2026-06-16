// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import type {
  ICommandSettings,
  IContentPage,
  Content,
  FormContent,
  CommandResult,
  Details,
  ContextItem,
  IconInfo,
  OptionalColor,
} from '../types'

/**
 * A toggle (boolean) setting.
 */
export class ToggleSetting {
  readonly key: string
  label: string
  description?: string
  value: boolean
  isRequired?: boolean

  constructor(key: string, label: string, defaultValue: boolean = false, description?: string) {
    this.key = key
    this.label = label
    this.value = defaultValue
    this.description = description
  }
}

/**
 * A text input setting.
 */
export class TextSetting {
  readonly key: string
  label: string
  description?: string
  value: string
  placeholder?: string
  multiline?: boolean
  isRequired?: boolean

  constructor(key: string, label: string, defaultValue: string = '', description?: string) {
    this.key = key
    this.label = label
    this.value = defaultValue
    this.description = description
  }
}

/**
 * A choice set (dropdown) setting.
 */
export class ChoiceSetSetting {
  readonly key: string
  label: string
  description?: string
  value: string
  choices: Array<{ title: string; value: string }>
  isRequired?: boolean

  constructor(
    key: string,
    label: string,
    choices: Array<{ title: string; value: string }>,
    defaultValue?: string,
    description?: string
  ) {
    this.key = key
    this.label = label
    this.choices = choices
    this.value = defaultValue ?? (choices.length > 0 ? choices[0].value : '')
    this.description = description
  }
}

type AnySetting = ToggleSetting | TextSetting | ChoiceSetSetting

/**
 * Container for extension settings. Generates an Adaptive Card form for the settings page.
 */
export class Settings implements ICommandSettings {
  private readonly _settings: AnySetting[] = []
  private _settingsPage?: SettingsPage

  get settingsPage(): IContentPage {
    if (!this._settingsPage) {
      this._settingsPage = new SettingsPage(this)
    }
    return this._settingsPage
  }

  add(setting: AnySetting): this {
    this._settings.push(setting)
    return this
  }

  getSetting<T extends AnySetting>(key: string): T | undefined {
    return this._settings.find(s => s.key === key) as T | undefined
  }

  getAllSettings(): AnySetting[] {
    return [...this._settings]
  }

  /**
   * Update settings from a form submission (key-value map from Adaptive Card inputs).
   */
  update(inputs: Record<string, string>): void {
    for (const setting of this._settings) {
      if (inputs[setting.key] !== undefined) {
        if (setting instanceof ToggleSetting) {
          setting.value = inputs[setting.key] === 'true'
        } else {
          setting.value = inputs[setting.key]
        }
      }
    }
  }

  /**
   * Generate the Adaptive Card template JSON for the settings form.
   */
  toTemplateJson(): string {
    const body: unknown[] = []

    for (const setting of this._settings) {
      if (setting.label) {
        body.push({
          type: 'TextBlock',
          text: setting.label,
          weight: 'bolder',
          spacing: 'medium',
        })
      }
      if (setting.description) {
        body.push({
          type: 'TextBlock',
          text: setting.description,
          isSubtle: true,
          wrap: true,
          spacing: 'none',
        })
      }

      if (setting instanceof ToggleSetting) {
        body.push({
          type: 'Input.Toggle',
          id: setting.key,
          title: '',
          value: String(setting.value),
          valueOn: 'true',
          valueOff: 'false',
        })
      } else if (setting instanceof TextSetting) {
        body.push({
          type: 'Input.Text',
          id: setting.key,
          placeholder: setting.placeholder ?? '',
          value: setting.value,
          isMultiline: setting.multiline ?? false,
        })
      } else if (setting instanceof ChoiceSetSetting) {
        body.push({
          type: 'Input.ChoiceSet',
          id: setting.key,
          value: setting.value,
          choices: setting.choices.map(c => ({ title: c.title, value: c.value })),
        })
      }
    }

    body.push({
      type: 'ActionSet',
      actions: [{ type: 'Action.Submit', title: 'Save' }],
    })

    return JSON.stringify({ type: 'AdaptiveCard', version: '1.5', body })
  }

  /**
   * Generate the data JSON (current values).
   */
  toDataJson(): string {
    const data: Record<string, string> = {}
    for (const setting of this._settings) {
      data[setting.key] = String(setting.value)
    }
    return JSON.stringify(data)
  }
}

/**
 * Internal settings page that renders the settings form.
 */
class SettingsPage implements IContentPage {
  id = '__settings__'
  name = 'Settings'
  title = 'Extension Settings'
  icon?: IconInfo | null = null
  isLoading?: boolean = false
  accentColor?: OptionalColor | null = null
  details?: Details | null = null
  commands?: ContextItem[] = []

  private readonly settings: Settings

  constructor(settings: Settings) {
    this.settings = settings
  }

  getContent(): Content[] {
    const form: FormContent = {
      type: 'form',
      templateJson: this.settings.toTemplateJson(),
      dataJson: this.settings.toDataJson(),
      submitForm: (inputs: string, _data: string): CommandResult => {
        let parsed: Record<string, string> = {}
        try {
          const p = JSON.parse(inputs)
          if (p && typeof p === 'object') {
            parsed = p as Record<string, string>
          }
        } catch {
          // inputs was not valid JSON; keep empty
        }
        this.settings.update(parsed)
        return { kind: 'showToast', args: { message: 'Settings saved' } }
      },
    }
    return [form]
  }
}
