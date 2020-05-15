# Keyboard Manager 

The Keyboard Manager (KBM) is a keyboard remapper that allows a user to
redefine keys on their keyboard (ex. swapping the letter A and D) as
well as shortcuts (Ctrl+C to Ctrl+A). Users can use these remappings as
long as KBM is enabled and PowerToys is running in the background.

![alt text][example]

# 1\. Get Started  

## General Settings
To create mappings with Keyboard Manager, you have the option of launching either the Remap Keyboard UI by clicking the \<Remap a Key\> button or Remap Shortcuts UI by clicking the \<Redefine a shortcut\> button.


## Remap Key to Key
To remap a key to another key, click the \<Remap a Key\> button to
launch the Remap Keyboard UI. When first launched, you are met
with no predefined mappings and must click the \<+\> button to add
a new remap. From there, the user selects the key whose output
they want to ***change*** as the “Original Key” and then keys new
output as the “New Key”. For example, if you want to press A and
get B, Key A would be your “Original Key” and Key B would be your
“New Key”. If you want to swap keys, add another remapping with
B as your Original and Key A as your New.

![alt text][remapkey]

## Remap Shortcuts (OS-only)
Currently you are only able to remap OS-level shortcuts,
app-specific shortcuts are coming soon\! To change how you invoke
a particular shortcut, click the \<Remap Shortcut\> button to
launch the Edit Shortcuts UI. When first launched, you are met
with no predefined mappings and must click the \<+\> button to add
a new remap. The Original shortcut is the shortcut you want to
change and the New Shortcut is the shortcut you want to change it
to. Ex. If you want Ctrl+C to paste, Ctrl+C is the Original
Shortcut and Ctrl+V is the New Shortcut. There are a few rules to shortcuts:
   
- Shortcuts must begin with a modifier key (Ctrl, Shift, Alt, and Winkey)
- Shortcuts must end with an action key (all non-modifier keys and non Fn keys)\* 
- Shortcuts cannot be longer than 3 keys  

![alt text][remapshort]

### System Reserved shortcuts 
- Winkey + L (Locking your computer) and Ctrl + Alt + Del cannot be remapped (as Original Shortcut) as they are reserved by the Windows OS.

## Selecting the keys: Drop down + Type Key feature  
To select a key in the remap or shortcut UI, you can use either the
\<Type Key\> button or the drop downs. For the drop downs you select a key from the list and other drop downs will appear o Once you click the \<type key\> button a dialogue will pop up. From here, type the key/shortcut using your keyboard. Once you’re satisfied with the output, hold enter to continue. If you’d like to leave the dialogue, hold the ESC button.

![alt text][dropdowntypekey]

## Frequently Asked Questions

- **Question**: *Can I use Keyboard Manager at my log-in screen?*
  - No, Keyboard Manager is only available when PowerToys is running and doesn’t work on any password screen, including Run As Admin.

- **Question**: *Do I have to turn off my computer for the remapping to take effect?*
  - No, as of now, all you need to do is press apply.

- **Question**: *Can I remap a shortcut to a single key?*
  - No, we hope to support this soon.

- **Question**: Why can’t I remap the Fn key?
  - Fn keys are often hardware-based and are not available to the OS, meaning we aren’t able to intercept and remap the key

- **Question**: *Where is the Mac/Linux profiles?*
  - This is the beta release; we will have these features in our P1

- **Question**: *Why can’t I remap a shortcut for a specific app?*
  - This is the beta release; we will have these features in our P1

- **Question**: *Will this work on video games?*
  - It depends on how the game accesses your keys. Many games use low-level* keyboard hooks and as a result any remappings would not work.

## Trouble shooting common problems:

  - *My remapping’s are not working on a specific app / window*
    
      - *Could be one of two issues:*
        
          - Remappings will not work on an app / window if that window
            is running as an admin (elevated) and PowerToys itself is
            not running as admin. Try running PowerToys as an
            administrator.
        
          - KBR intercepts keyboard hooks to remap your keys. Some apps
            that also do this can interfere with Keyboard Remapper, to
            fix this go to the Settings

[example]: ../../../doc/images/keyboardmanager/example-cp.gif "Feature"
[remapkey]: ../../../doc/images/keyboardmanager/remapkeyboard_both.gif "Feature"
[remapshort]: .../../../doc/images/keyboardmanager/remapshort_both.gif "Feature"
[dropdowntypekey]: ../../../doc/images/keyboardmanager/dropdowntypekey.gif "Feature"
