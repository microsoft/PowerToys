# Keyboard Manager 

The Keyboard Manager (KBM) is a keyboard remapper that allows a user to redefine keys on their keyboard (ex. swapping the letter <kbd>A</kbd> and <kbd>D</kbd>) as well as shortcuts (<kbd>Ctrl</kbd>+<kbd>C</kbd> to <kbd>![alt text][winlogo]</kbd>+<kbd>C</kbd>). You can use these remappings as long as KBM is enabled and PowerToys is running in the background. Below is an example of using keys and shortcuts that were remapped:

![alt text][example]

# 1. Get Started  

## 1.1 General Settings
To create mappings with Keyboard Manager, you have the option of launching either the Remap Keyboard UI by clicking the <kbd>Remap a Key</kbd> button or Remap Shortcuts UI by clicking the <kbd>Remap a shortcut</kbd> button.


## 1.2 Remap Keys
To remap a key to another key, click the <kbd>Remap a Key</kbd> button to launch the Remap Keyboard UI. When first launched, you are met with no predefined mappings and must click the <kbd>+</kbd> button to add a new remap. From there, select the key whose output you want to ***change*** as the “Key” and then keys new output as the “Mapped To”. For example, if you want to press <kbd>A</kbd> and have <kbd>B</kbd>  appear, Key <kbd>A</kbd> would be your “Key” and Key <kbd>B</kbd> would be your “Mapped To" key. If you want to swap keys, add another remapping with Key <kbd>B</kbd> as your "Key" and Key <kbd>A</kbd> as your "Mapped To".

![alt text][remapkey]

## 1.3 Remap Shortcuts (Global-only)
Currently you are only able to remap global level shortcuts (they apply to your whole OS), but **app-specific shortcuts are coming soon!**

To change how you invoke a particular shortcut, click the <kbd>Remap a shortcut</kbd> button to
launch the Remap Shortcuts UI. When first launched, you are met with no predefined mappings and must click the <kbd>+</kbd> button to add a new remap. The "Shortcut" is the shortcut you want to change and the "Mapped To" is the shortcut you want to change it
to. Ex. If you want <kbd>Ctrl</kbd>+<kbd>C</kbd> to paste, <kbd>Ctrl</kbd>+<kbd>C</kbd> is the "Shortcut" and <kbd>Ctrl</kbd>+<kbd>V</kbd> is the "Mapped To". Here are a few rules to shortcuts as you get started:
   
- Shortcuts must begin with a modifier key (<kbd>Ctrl</kbd>, <kbd>Shift</kbd>, <kbd>Alt</kbd>, <kbd>![alt text][winlogo]</kbd>)
- Shortcuts must end with an action key (all non-modifier keys) 
- Shortcuts cannot be longer than 3 keys  

![alt text][remapshort]

### 1.4 Keys that cannot be remapped:


- <kbd>![alt text][winlogo]</kbd>+<kbd>L</kbd> (Locking your computer) and <kbd>Ctrl</kbd>+<kbd>Alt</kbd>+ <kbd>Del</kbd> cannot be remapped as they are reserved by the Windows OS.
- The <kbd>Fn</kbd> key itself cannot be remapped (in most cases) but the F1-24 can be mapped.


## 1.5 Selecting the keys: Drop down + Type Key / Type Shortcut feature  
To select a key in the remap or shortcut UI, you can use either the <kbd>Type Key</kbd> button or the drop downs. Once you click the <kbd>Type Key / Shortcut</kbd> button a dialogue will pop up. From here, type the key/shortcut using your keyboard. Once you’re satisfied with the output, hold <kbd>Enter</kbd> to continue. If you’d like to leave the dialogue, hold the <kbd>Esc</kbd> button. For the drop downs, you can search with the key name and additional drop downs will appear as you progress. However, you can not use the type-key feature while on the drop down. 

![alt text][dropdowntypekey]

## 1.6 Orphaning Keys
Orphaning a key means that you mapped it to another key and no longer have anything mapped to it. (Ex. If I map A -> B, I no longer have a key on my keyboard that results in A) To fix this, create another remap with that key as the New Key. We have created a warning to ensure you don't do this by accident.

![alt text][orphaned]

## 2. Frequently Asked Questions

- **Question**: *I remapped the wrong keys, and I want to stop it quickly. How can I do that?*
  - You can simply disable KBM from the settings or you can close PowerToys. For the remappings to work PowerToys must be running in the background and KBM must be enabled.

- **Question**: *Can I use Keyboard Manager at my log-in screen?*
  - No, Keyboard Manager is only available when PowerToys is running and doesn’t work on any password screen, including Run As Admin.

- **Question**: *Do I have to turn off my computer for the remapping to take effect?*
  - No, as of now, all you need to do is press apply.

- **Question**: *Can I remap a shortcut to a single key?*
  - No, we hope to support this soon.

- **Question**: *Where are the Mac/Linux profiles?*
  - This is the beta release; we will have these features in our V1

- **Question**: *Why can’t I remap a shortcut for a specific app?*
  - This is the beta release; we will have these features in our V1

- **Question**: *Will this work on video games?*
  - It depends on how the game accesses your keys. Certain keyboard APIs do not work with Keyboard Manager.

- **Question**: *Does  this work if I change my input language? How?*
  - Yes it will. Right now if you remap <kbd>A</kbd> to <kbd>B</kbd> on English (US) keyboard and then the switch language to French, then typing <kbd>A</kbd> on the French keyboard (i.e. <kbd>Q</kbd> on the English US physical keyboard) would result in <kbd>B</kbd>, this is consistent with how Windows handles multilingual input. 

## 3. Trouble shooting if remappings are not working:

   - *Could be one of the following issues:*
     
     - **Run As Admin:** Remappings will not work on an app / window if that window is running as an admin (elevated) and PowerToys itself is not running as admin. Try running PowerToys as an administrator.
     - **Not Intercepting Keys:** KBM intercepts keyboard hooks to remap your keys. Some apps that also do this can interfere with Keyboard Manager, to fix this go to the Settings and Disable then Re-Enable Keyboard manager.

## 4. Known Issues
- [Caps light indicator not toggling correctly](https://github.com/microsoft/PowerToys/issues/1692)
- [Remaps not working for FancyZones and Shortcut Guide](https://github.com/microsoft/PowerToys/issues/3079)

For a list of all known issues/suggestions, check it out
[here](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+label%3A%22Product-Keyboard+Shortcut+Manager%22).

[example]: ../../../doc/images/keyboardmanager/example.gif "Example"
[remapkey]: ../../../doc/images/keyboardmanager/remapkeyboard_both.gif "Remap a Key"
[remapshort]: ../../../doc/images/keyboardmanager/remapshort_both.gif "Remap a Shortcut"
[dropdowntypekey]: ../../../doc/images/keyboardmanager/dropdownstypekey.gif "Drop-downs and Type Features"
[orphaned]: ../../../doc/images/keyboardmanager/orphanedkey.gif "Orphaned key warning"
[winlogo]: ../../../doc/images/keyboardmanager/winkey.png 
