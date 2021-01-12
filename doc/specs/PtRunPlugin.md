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
- Ability to add a weight multiplier to increase weighting for individual plugins
- Ability to disable a plugin

#### 1.4.2: Stretch Goals

- Ability to launch a single plugin via key shortcut
- Have configurations happen without restarting PT Run

#### 1.4.3: Non-goals

- Settings outside what is listed above
- 3rd party plugin spec

## 2: Scenarios

### 2.1: Golden path

1. Disable a built-in plugin
2. Change the weight of a plugin
3. Change the action keyword of a plugin
4. Trigger PT Run
5. New configuration respected

## 3: Key definitions

| Term | Definition |
|------|------------|
| Action word | This is the concept inside PowerToys Run where you can directly invoke only that plugin(s) that use that.  An example is doing `=2+2` would only call the calculator plugin |
| weight | A multiplier to increase the result value |
| PT Run / Run | Shorthand for PowerToys Run launcher |

## 4: Requirements

### 4.1: Core requirements

| # | Definition | Priority |
|---|------------|----------|
| 1 | List of plugins with toggle On / Off | P0 |
| 2 | When clicked, inline options are displayed | P0 |
| 3 | When weight multiplier is added, that target plugin's value should be increased | P0 |
| 4 | When new settings are applied, PT Run does not have to restart | P2 |

### 4.2: Inline Setting requirements

| # | Definition | Priority |
|---|------------|----------|
| 1 | Checkbox to "include results in default list" (adding in * to action keyword)  | P0 |
| 2 | Text field for additional action word. | P0 |
| 2 | Text field for additional action word can be blank. | P0 |
| 3 | Do not allow action word of * | P1 |
| 4 | Weight multiplier number box field, 0 to 10 with ability to add in a decimal.  This will need to be tweaked once the multiplier is added in after real world testing. | P1 |
| 5 | Direct shortcut to execute a single plugin | P2 |
| 5 | No plugin can have the same direct shortcut | P2 |

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

TODO: 

| # | Requirement | Implication | Pri |
| - | ------------|-------------|-----|
| 1 | TEXT | TEXT | P0 |
| 1 | | | |

## 7: Future considerations

- Additional per-plugin setting
- 3rd party plugins

## 8. Appendix

[WinSettingLanguage]: ./images/PtRunPlugin/preferredLanguages.png
[WinSettingSysIcon]: ./images/PtRunPlugin/systemIcons.png
[uxMock]: ./images/PtRunPlugin/pluginSettingMockUx.png