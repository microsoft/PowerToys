# DRAFT

# PowerToy Run Settings for Plugin configuration

<img align="right" src="./images/Logo.png" />

- **What is it:** Settings UX for configuration PowerToys Run plugins
- **Authors:** Clint Rutkas
- **Spec Status:** Draft
- **GitHub issue:** [#5273](https://github.com/microsoft/PowerToys/issues/5273)

## 1: Overview

### 1.1: Executive overview (max 150 words)

PowerToys Run's is powered by plugins. Each plugin has a set of behaviors that are currently hardcoded but could be overridden or disabled. This provides the configuration an end user wants.  Making it work as they want it to.

### 1.2: Core Scenarios

1. Disable a built-in plugin
2. Change the weight of a plugin
3. Change the action keyword of a plugin

### 1.3: Existing solutions and expectations 

#### 1.3.1: Windows Settings

Lots of instances inside the settings dialog for how something like this could be enacted.  

Here the options are both inline and allow for an on/off.

![alt Windows 10 language inline options][WinSettingLanguage]

![alt Windows 10 OS systray dialog][WinSettingSysIcon]

### 1.4: Goals/Non-goals

#### 1.4.1: Goals

- Ability to customize action words
- Ability to disable a plugin
- ~~Ability to add a weight multiplier to increase weighting for individual plugins.~~
   - We fixed a bug that reintroduces weighting based on usage and want to validate based on user feedback if this is needed.

#### 1.4.2: Stretch Goals

- Ability to launch a single plugin via key shortcut
- Have configurations happen without restarting PT Run

#### 1.4.3: Non-goals

- Settings outside what is listed above
- 3rd party plugin spec


## 2: Scenarios

### 2.1: Golden path

1. Disable a built-in plugin
2. Change the action keyword of a plugin
3. Trigger PT Run
4. New configuration respected

## 3: Key definitions

| Term | Definition |
|------|------------|
| Action word | This is the concept inside PowerToys Run where you can directly invoke only that plugin(s) that use that.  An example is doing `=2+2` would only call the calculator plugin.  The UX Screenshot is calling this "Direct activation" below |
| Weight | A multiplier to increase the result value |
| PT Run / Run | Shorthand for PowerToys Run launcher |

## 4: Requirements

### 4.1: Core requirements

Most plugin information comes from `PowerToys\modules\launcher\Plugins\PLUGIN_FOLDER\plugin.json` where `PLUGIN_FOLDER` is the name of the plugin.

| # | Definition | Priority |
|---|------------|----------|
| 1 | List of plugins with toggle On / Off | P0 |
| 2 | When clicked, inline options are displayed | P0 |
| 3 | ~~When weight multiplier is added, that target plugin's value should be increased~~ Non-goal, waiting for feedback | P0 |
| 4 | When new settings are applied, PT Run does not have to restart | P2 |
| 5 | When an error state is hit, the warning will be displayed to user below title for plugin | P0 |
| 6 | Detect error state when there is no direct activation, no global result and no action word | P0 |
| 7 | Disable file drive warning checkbox if Search plugin is disabled  | P1 |
| 8 | Do not have warning appear if Disable file drive warning is enabled and the Search plugin is disabled  | P1 |
| 9 | List author of plugin (from json file) | P1 |

### 4.2: Inline Setting requirements

| # | Definition | Priority |
|---|------------|----------|
| 1 | Checkbox to "include results in default list" (adding in * to action keyword)  | P0 |
| 2 | Text field for additional action word. | P0 |
| 3 | Action word do not have to be unique (This already is supported). | P0 |
| 4 | Text field for additional action word can be blank. | P0 |
| 5 | Do not allow action word of * as under the hood this is the global keyword | P1 |
| 6 | ~~Weight multiplier number box field, 0 to 10 with ability to add in a decimal.  This will need to be tweaked once the multiplier is added in after real world testing.~~ Non-goal, waiting for feedback | P1 |
| 7 | Direct shortcut to execute a single plugin | P2 |
| 8 | No plugin can have the same direct shortcut | P2 |
| 9 | Description of plugin is shown when expanded | P1 | 

### 4.3: Accessibility 

| # | Definition | Priority |
|---|------------|----------|
| 1 | Tabbing between options once selected are in order | P0 |

## 5: Functional design

### 5.1: Core design and screenshots

Quick mock of what it could look like in a first attempt.

![alt Mock of the UX with existing settings][uxMock]

### 5.2: Existing consideration

Right now with Wox, each plugin defines their action keywords in a json file.  We could extend this and include the hotkey there.

## 6: Measurements

| # | Requirement | Implication | Pri |
| - | ------------|-------------|-----|
| 1 | The enable / disable state | Validate if a plugin should be off by default based on population | P0 |
| 2 | Action word for plug | Validate if we should shift the Action word | P1 |
| 3 | Include in global result list for plug | Validate if we should shift the Action word from the global list | P1 |
| 4 | Action word for plug | Validate if we should shift the default Action word | P1 |
| 5 | Is a direct shortcut used for a plugin | Validate if we should make certain plugins have direct shortcuts.  If so, we should add in one more bit of telemetry, what shortcut was used to see if there are commonalities  | P1 |

## 7: Future considerations

- Per-plugin setting
   - This would move the setting for ignoring the file drive warning into Search plugin section
- Adding in weight per plugin
- Adding in a 'reset' ability
- 3rd party plugins

## 8. Appendix

[WinSettingLanguage]: ./images/PtRunPlugin/preferredLanguages.png
[WinSettingSysIcon]: ./images/PtRunPlugin/systemIcons.png
[uxMock]: ./images/PtRunPlugin/pluginSettingMockUx.png