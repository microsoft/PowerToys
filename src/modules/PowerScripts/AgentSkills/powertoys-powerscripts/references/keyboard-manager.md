# Manual shortcut setup

Only for requested Keyboard Manager actions; not a separate skill.
No binding API is assumed. Do not edit KBM JSON or claim creation assigns a hotkey.

1. Provide title, opaque ID, package path, behavior, and trust/runtime checks.
   Prefer `none` input/output; review code in the app before exact-content trust.
2. Enable PowerScripts and KBM in PowerToys Settings; enable the integration in
   PowerScripts app > Consumer modules, if policy permits.
3. Check this build's shortcut UI. If it offers a PowerScript picker, select the
   package, record the shortcut, and save normally. Required options need supported
   saved arguments/defaults; do not invent a parameter prompt.
4. If only Run Program exists, use installed `PowerScripts.Host.exe` with
   `run <id> --consumer keyboard-manager`, subject to UI argument support.
   Quote arguments; no credentials. If neither path exists, report the limitation.
5. Warn about conflicts/reserved shortcuts; never overwrite mappings or claim
   unchecked conflict detection. Test only with consent and verify actual effects.

Report creation/discovery, trust/execution, and shortcut setup independently.
