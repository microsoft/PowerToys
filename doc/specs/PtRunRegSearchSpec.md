# PowerToys Run Plugin - Registry Search

## Motivation

As an admin and a user it would be very handy to find a registry key via PowerToys Run (search window).

## Usability and Functions

* A user want to find a full registry key (`HKEY_CURRENT_USER\SOFTWARE\Microsoft\Terminal Server Client\Default`)
* A user want to find a registry key without type the full key (`HKEY_LOCAL_MACHINE\HARDWARE\`)
* A user want to find a registry key without type the full key and full sub-keys (`HKEY_L\MA\`)
* A user want to find a registry key without type all sub-keys  (`HKEY_L\\ACPI\`)
* A user like it to use shorthand keys (`HKLM`, `HKCU`, ...)
* A user don't like to be annoyed with case-sentive search
* A user need a easy way to direct open the found key in the Registry Editor ('regedit.exe');
* A user need a easy way to direct copy the full registry key
* A user want to see how many sub-keys a key have
* A user want to see how many values are inside key.
* A user want to direct see a value and his name and typ, when the key have no sub-keys and only one value
* A user want to see a information when he don't have access to a key

## ToDo list

* P1 = need
* P2 = most
* P3 = nice to have

| Priority | Functions                  | Example                                                               |
| -------- | -------------------------- | --------------------------------------------------------------------- |
| P1       | Find full key              | `HKEY_CURRENT_USER\SOFTWARE\Microsoft\Terminal Server Client\Default` |
| P1       | Find start key             | `HKEY_LOCAL_MACHINE\HARDWARE\`                                        |
| P1       | Shorthand support          | `HKLM`, `HKCU`, ...                                                   |
| P1       | Case insensitive           | `HKLM  == hklm == hKLm`                                               |
| P1       | Open                       | `Open this key in Registry Editor`                                    |
| P1       | Copy full key              | `Copy this key to clipboard`                                          |
| P1       | Show access denied         | `You have no access to this key`                                      |
| -------- | -------------------------- | --------------------------------------------------------------------- |
| P2       | Find with parts of keys    | `HKEY_L\MA\`                                                          |
| P2       | Show sub-key count of key  | `Contains 5 sub-keys and 0 values`                                    |
| P2       | Show value count of key    | `Contains 0 sub-keys and 5 values`                                    |
| P2       | Show value (when only one) | `Current - 0x00000001 (REG_DWORD)`                                    |
| P2       | Copy value (when only one) | `1`                                                                   |
| P2       | Open as admin              | `Open this key in Registry Editor as admin`                           |
| -------- | -------------------------- | --------------------------------------------------------------------- |
| P3       | Find without parts of keys | `HKEY_L\\ACPI\`                                                       |

## Possible commands

| Result line    | First Command | Second Command | Third Command      |
| -------------- |-------------- | -------------- | ------------------ |
| Any key        | Open          | Copy full key  |                    |
| Restricted key | Open as admin | Copy full key  |                    |
| Only one value | Open          | Copy full key  | Copy value         |

## Open questions

* Should we copy the complete key with the prefix `Computer\`?
* Any other helpfully things are missing in this document?
