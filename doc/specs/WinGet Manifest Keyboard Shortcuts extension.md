# Winget Manifest Keyboard Shortcuts extension v1.0

## 1 What this spec is about

## 2 Save location of manifests

All manifests and one index file are saved under `%localappdata%/Microsoft/Windows/KeyboardShortcuts`. All apps are allowed to add their manifest files there. In addition Package Managers (like WinGet) and manifest interpreters (like PowerToys Shortcut Guide) can control and add other manifests themselves.

### 2.1 File names

The file name of a keyboard shortcuts file is the WinGet package identifier, plus the locale of the strings of the file and at last the `.KBSC.yml` file extension.

For example the package "test.bar" saves its manifest with `en-US` strings in `test.bar.en-US.KBSC.yml"

#### 2.1.1 No winget package available

If an application has no corresponding WinGet package its name starts with a plus symbol.

### 2.2 Reserved namespaces

Every name starting with `+WindowsNT` is reserved for the Windows OS and its components.

## 3 File syntax

All relevant files are written in the 

### 3.1 Keyboard shortcuts manifest

#### 3.1.1 `PackageName` field

#### 3.1.2 `Filter` field

#### 3.1.3 `Shortcuts` field

##### 3.1.3.1 `SectionName` field

**Special sections**:

Special sections start with an identifier enclosed between `<` and `>`. This declares the category as a special display. If the interpreter of the manifest file can't understand the content this section should be left out.

##### 3.1.3.2 `Properties` field

###### 3.1.3.2.1 `Name` field

###### 3.1.3.2.2 `Win` field

###### 3.1.3.2.3 `Ctrl` field

###### 3.1.3.2.4 `Shift` field

###### 3.1.3.2.5 `Alt` field

###### 3.1.3.2.6 `Description` field

###### 3.1.3.2.7 `Recommended` field

###### 3.1.3.2.8 `Keys` field

An string array of all the keys that need to be pressed.

**Special keys**:

Special keys are enclosed between `<` and `>` and correspond to a key that should be displayed in a certain way

|Name|Description|
|----|-----------|
|`<Office>`| Corresponds to the Office key on some Windows keyboards |
|`<Copilot>`| Corresponds to the Copilot key on some Windows keyboards |

### 3.2 `index.yml` file

#### 3.2.1 `DefaultShellName` field

This declares the package identifier of the default shell used in Windows. Most commenly it is `+WindowsNT.Shell`. Although not enforced, only the shell declared in the registry key `HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon\Shell` should be used here.

#### 3.2.2 `Index` field

##### 3.2.2.1 `Filter` field

##### 3.2.2.2 `Apps` field

