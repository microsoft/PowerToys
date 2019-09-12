# PowerToys Source Code

## Code organization
The PowerToys are split into DLLs for each PowerToy module ([`modules`](/src/modules) folder), and an executable ([`runner`](/src/runner) folder) that loads and manages those DLLs.

The settings window is a separate executable, contained in [`editor`](/src/editor) folder. It utilizes a WebView to display an HTML-based settings window (contained in [`settings-web`](/src/settings-web) folder).

The [`common`](/src/common) contains code for a static libary with helper functions, used by both the runner and the PowerToys modules.
