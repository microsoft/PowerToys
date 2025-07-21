# Winget Manifest Keyboard Shortcuts schema

## 1 What this spec is about

This spec provides an extension to the existing [WinGet manifest schema](https://github.com/microsoft/winget-pkgs/blob/master/doc/manifest/README.md) in form of an additional yaml file, that describes keyboard shortcuts the application provides.

These yaml files are saved on a per-user base and so called manifest interpreters can then display these manifests in a human-friendly version.

### 1.1 What this spec is not about

This spec does not provide a way to back up or save user-defined keyboard shortcuts.

## 2 Save location of manifests

### 2.1 WinGet 

These files are saved online along with the other manifest files in the [WinGet Package repository](https://github.com/microsoft/winget-pkgs).

### 2.2 Locally

All manifests and one index file are saved locally under `%LocalAppData%/Microsoft/WinGet/KeyboardShortcuts`. All apps are allowed to add their manifest files there. In addition Package Managers (like WinGet) and manifest interpreters (like PowerToys Shortcut Guide) can control and add other manifests themselves.

#### 2.2.1 Downloading manifests

When WinGet or other package managers download a package, they should also download the corresponding keyboard shortcuts manifest file and save it in the local directory, given such a file exists in the WinGet repository.

The downloader is also responsible for updating the local `index.yaml` file, which contains all the information about the different manifest files that are saved in the same directory.

#### 2.2.2 Updating manifests

When a manifest interpreter starts, it should download the latest version of the manifests from the WinGet repository and save them in the local directory. If a manifest interpreter is not able to download the manifests or they do not exist, it should use the locally saved manifests.

The updater is also responsible for updating the local `index.yaml` file, which contains all the information about the different manifest files that are saved in the same directory.

> Note: Winget must provide a way to update the keyboard shortcuts manifests given a package id.

### 2.3 File names

The file name of a keyboard shortcuts file is the WinGet package identifier, plus the locale of the strings of the file and at last the `.KBSC.yaml` file extension.

For example the package "test.bar" saves its manifest with `en-US` strings in `test.bar.en-US.KBSC.yaml`.

#### 2.3.1 No winget package available

If an application has no corresponding WinGet package its name starts with a plus (`+`) symbol.

### 2.4 Reserved namespaces

Every name starting with `+WindowsNT` is reserved for the Windows OS and its components.

## 3 File syntax

All relevant files are written in [YAML](https://yaml.org/spec).

> Note: A JSON schema will be provided as soon as the spec reaches a further step

### 3.1 Manifest Schema vNext Keyboard Shortcuts File

```
PackageName:                # The package unique identifier
WindowFilter:               # The filter of window processes to which the shortcuts apply to
BackgroundProcess:          # Optionally allows applying WindowFilter to background processes
Shortcuts:                  # List of sections with keyboard shortcuts
  - SectionName:            # Name of the category of shortcuts
    Properties:             # List of shortcuts in the category
      - Name:               # Name of the shortcut
        Description:        # Optional description of the shortcut
        AdditionalInfo:     # Optional additional information about the shortcut
        Recommended:        # Optionally determines if the shortcut is displayed in a designated recommended area
        Shortcut:           # An array of shortcuts that need to be pressed
          - Win:            # Determines if the Windows Key is part of the shortcut
            Ctrl:           # Determines if the Ctrl Key is part of the shortcut
            Shift:          # Determines if the Shift Key is part of the shortcut
            Alt:            # Determines if the Alt Key is part of the shortcut
            Keys:           # Array of keys that need to be pressed
```

Per Application/Package one or more Keyboard manifests can be declared. Every manifest must have a different locale and the same `PackageName`, `WindowFilter` and `BackgroundProcess` fields.

<details>
 <summary><b>PackageName</b> - The package unique identifier</summary>

 Package identifier (see 2.1 for more information on the package identifier).

</details>

<details>
 <summary><b>WindowFilter</b> - The filter of window processes to which the shortcuts apply to</summary>

 This field declares for which process name the shortcuts should be showed (To rephrase: For which processes the shortcut will have an effect if pressed). You can use an asterisk to leave out a certain part. For example `*.PowerToys.*.exe` targets all PowerToys processes and `*` apply to any process.

</details>

<details>
 <summary><b>BackgroundProcess</b> - Optionally allows applying WindowFilter to background processes.</summary>

 **Optional field**

 Defaults to `False`. Determines if WindowFilter should apply to background processes as well (Rephrased: When the process is running, the shortcuts will apply).

</details>

<details>
 <summary><b>Shortcuts</b> - List of sections with keyboard shortcuts</summary>
 
 List of different section (also called categories) of shortcuts.
</details>

<details>
 <summary><b>SectionName</b> - Name of the category of shortcuts</summary>

 Name of the section of shortcuts. 

**Special sections**:

Special sections start with an identifier enclosed between `<` and `>`. This declares the category as a special display. If the interpreter of the manifest file can't understand the content this section should be left out.

</details>

<details>
 <summary><b>Properties</b> - List of shortcuts in the category</summary>
</details>

<details>
 <summary><b>Name</b> - Name of the shortcut</summary>

 Name of the shortcut. This is the name that will be displayed in the interpreter.

</details>


<details>
 <summary><b>Description</b> - Optional description of the shortcut</summary>

 Optional description of the shortcut. This is the description that will be displayed by the interpreter.
</details>

<details>
 <summary><b>AdditionalInfo</b> - Optional additional information about the shortcut</summary>

 Array of additional information about the shortcut. This is the additional information that will be displayed by the interpreter and are not part of this manifest.

 **Example**:

 For example, if the shortcut is only available on a certain Windows version, this information could be added here.
 ```yaml
  AdditionalInfo:
    - MinWindowsVersion: "10.0.19041.0"
  ```
</details>

<details>
 <summary><b>Shortcut</b> - An array of shortcuts that need to be pressed</summary>

  An array of shortcuts that need to be pressed. This allows defining sequential shortcuts that need to be pressed in order to trigger the action.

</details>

<details>
 <summary><b>Win</b> - Determines if the Windows Key is part of the shortcut</summary>

 Refers to the left Windows Key on the keyboard.
</details>

<details>
 <summary><b>Ctrl</b> - Determines if the Ctrl Key is part of the shortcut</summary>

 Refers to the left Ctrl Key on the keyboard.
</details>

<details>
 <summary><b>Shift</b> - Determines if the Shift Key is part of the shortcut</summary>

 Refers to the left Shift Key on the keyboard.
</details>

<details>
 <summary><b>Alt</b> - Determines if the Alt Key is part of the shortcut</summary>

  Refers to the left Alt Key on the keyboard.
</details>


<details>
 <summary><b>Recommended</b> - Optionally determines if the shortcut is displayed in a designated recommended area</summary>

 **Optional field**

 Defaults to `False`. Determines if the shortcut should be displayed in a designated recommended area. This is a visual hint for the user that this shortcut is important.

</details>

<details>
 <summary><b>Keys</b> - Array of keys that need to be pressed</summary>

 A string array of all the keys that need to be pressed. If a number is supplied, it should be read as a [KeyCode](https://learn.microsoft.com/windows/win32/inputdev/virtual-key-codes) and displayed accordingly (based on the Keyboard Layout of the user).

**Special keys**:

Special keys are enclosed between `<` and `>` and correspond to a key that should be displayed in a certain way. If the interpreter of the manifest file can't understand the content, the brackets should be left out.

|Name|Description|
|----|-----------|
|`<Office>`| Corresponds to the Office key on some Windows keyboards |
|`<Copilot>`| Corresponds to the Copilot key on some Windows keyboards |
|`<Left>`| Corresponds to the left arrow key |
|`<Right>`| Corresponds to the right arrow key |
|`<Up>`| Corresponds to the up arrow key |
|`<Down>`| Corresponds to the down arrow key |
|`<Enter>`| Corresponds to the Enter key |
|`<Space>`| Corresponds to the Space key |
|`<Tab>`| Corresponds to the Tab key |
|`<Backspace>`| Corresponds to the Backspace key |
|`<Delete>`| Corresponds to the Delete key |
|`<Insert>`| Corresponds to the Insert key |
|`<Home>`| Corresponds to the Home key |
|`<End>`| Corresponds to the End key |
|`<PrtSc>`| Corresponds to the Print Screen key |
|`<Pause>`| Corresponds to the pause key |
|`<PageUp>`| Corresponds to the Page Up key |
|`<PageDown>`| Corresponds to the Page Down key |
|`<Escape>`| Corresponds to the Escape key |
|`<Arrow>`| Corresponds to either the left, right, up or down arrow key |
|`<ArrowLR>`| Corresponds to either the left or right arrow key |
|`<ArrowUD>`| Corresponds to either the up or down arrow key |
|`<Underlined letter>`| Corresponds to any letter that is _underlined_ in the UI |

</details>

#### 3.2.2 Example

```yaml
PackageName: Microsoft.PowerToys
WindowFilter: "*"
BackgroundProcess: True
Shortcuts:
  - SectionName: General
    Properties:
      - Name: Advanced Paste
        Shortcut:
          - Win: True
            Ctrl: False
            Alt: False
            Shift: False
            Keys:
              - 86
        Description: Open Advanced Paste window
      - Name: Advanced Paste
        Shortcut:
          - Win: True
            Ctrl: True
            Alt: True
            Shift: False
            Keys:
              - 86
        Description: Paste as plain text directly

```


### 3.2 `index.yaml` file

The `index.yaml` file is a file that contains all the information about the different manifest files that are saved in the same directory. This file is only available locally and is not saved in the WinGet repository as it is specific to the user.

```yaml
DefaultShellName:           # The package identifier of the default shell used in Windows
Index:                      # List of all manifest files
  - WindowFilter:           # The filter of window processes to which the shortcuts apply to
    BackgroundProcess:      # Optionally allows applying WindowFilter to background processes
    Apps:                   # List of all manifest files for the filter
```

<details>
 <summary><b>DefaultShellName</b> - The package identifier of the default shell used in Windows</summary>
  
 This declares the package identifier of the default shell used in Windows. Most commonly it is `+WindowsNT.Shell`. Although not enforced, only the shell declared in the registry key `HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon\Shell` should be used here.

</details>

<details>
 <summary><b>Index</b> - List of all manifest files</summary>
</details>

<details>
 <summary><b>WindowFilter</b> - The filter of window processes to which the shortcuts apply to</summary>

 See the `WindowFilter` field in the manifest file for more information.

</details>

<details>
 <summary><b>BackgroundProcess</b> - Optionally allows applying WindowFilter to background processes</summary>
 
 **Optional field**

 See the `BackgroundProcess` field in the manifest file for more information.

</details>

<details>
 <summary><b>Apps</b> - List of all the package identifiers applying for the filter</summary>
</details>

#### 3.2.1 Example

```yaml
DefaultShellName: "+WindowsNT.Shell"
Index:
  - Filter: "*"
    BackgroundProcess: True
    Apps: ["+WindowsNT.Shell", "Microsoft.PowerToys"]
  - Filter: "explorer.exe"
    Apps: ["+WindowsNT.WindowsExplorer"]
  - Filter: "taskmgr.exe"
    Apps: ["+WindowsNT.TaskManager"]
  - Filter: "msedge.exe"
    Apps: ["+WindowsNT.Edge"]
```
