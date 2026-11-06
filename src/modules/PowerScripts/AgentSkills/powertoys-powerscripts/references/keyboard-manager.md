# Manual Keyboard Manager handoff

Read this only when the requested consumer is Keyboard Manager. It is optional
module-specific setup guidance within the PowerScripts skill, not another skill
to install. Clipboard and selected-file workflows do not need a hotkey binding.

There is no binding-management API assumed by this skill. Do not edit KBM JSON
or pretend that creation of a script also assigns a shortcut.

1. Give the user the script title, actual catalogue ID, package location, intended
   behavior, and outstanding trust/runtime checks. Recommend reviewing the code
   in the standalone PowerScripts editor before approving its exact contents.
   Prefer `none` input and `none` output for ordinary action hotkeys. Do not
   assume a shortcut supplies clipboard content or displays transform results.
2. Ask the user to open **PowerToys Settings > PowerScripts** and enable
   PowerScripts and its Keyboard Manager integration if allowed by policy.
   Keyboard Manager itself must also be enabled in its own Settings page.
3. Guide the user into Keyboard Manager's shortcut mapping UI. If this build
   offers a PowerScript action/picker, choose the generated script, record the
   desired shortcut (for example Ctrl+Shift+3), and save through the normal UI.
   Labels vary by build; do not claim a control exists without checking it.
4. If only **Run Program** is available, a user can point it at the installed
   `PowerScripts.Host.exe` and supply `run <actual-id> --consumer keyboard-manager`
   as arguments, subject to that UI's support. Quote arguments as needed; never
   embed credentials. This does not bypass Host trust or supply required options.
   If the current build has neither path, report the limitation.
5. Warn about existing shortcut conflicts and reserved/system/app shortcuts.
   Do not overwrite a mapping or claim a conflict check you did not perform.
6. Let the user authorize a test after reviewing its effects. Verify the actual
   desired result (such as the correct VPN profile's connection state), not just
   that a process launched. A failed or unsupported VPN operation is not success.

If the user has not performed the steps, finish with **Script created and
discovery validated; trust/test and Keyboard Manager binding still require
user setup**. Execution and binding status must be reported independently.
