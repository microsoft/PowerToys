# PowerToys Source Code

## Code organization
The PowerToys are split into DLLs for each PowerToy module ([`modules`](/src/modules) folder), and an executable ([`runner`](/src/runner) folder) that loads and manages those DLLs.

The settings window is a separate executable, contained in [`settings-ui`](/src/settings-ui) folder. It utilizes a WebView to display an HTML-based settings window.

The [`common`](/src/common) contains code for a static library with helper functions, used by both the runner and the PowerToys modules.
