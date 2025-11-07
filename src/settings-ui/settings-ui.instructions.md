---
applyTo: "**/*.cs,**/*.xaml"
---
# Settings UI – configuration app guidance

Scope
- WinUI/WPF UI, communicates with Runner over named pipes; manages persisted settings schema.

Guidelines
- Don’t break settings schema silently; add migration when shape changes.
- If IPC/JSON contracts change, align with `src/runner/**` implementation.
- Keep UI responsive: marshal to UI thread for UI-bound operations.
- Reuse existing styles/resources; avoid duplicate theme keys.
- Add/adjust migration or serialization tests when changing persisted settings.

Acceptance
- Schema integrity preserved, responsive UI, consistent contracts, no style duplication.