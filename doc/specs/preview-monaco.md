# **Preview pane - Adding previews with monaco**

<img align="right" src="../images/overview/PT_small.png" />

- **What is it:** Implementing [#1527](https://github.com/microsoft/PowerToys/issues/1527)
- **Authors:** Aaron Junker ([@aaron-junker](https://github.com/aaron-junker)), Clint Rutkas ([@crutkas](https://github.com/crutkas))
- **Spec Status:** Waiting for review

## 1 Overview

Creating a better preview handler for all developer files. For this we use [Microsoft Monaco editor](https://github.com/microsoft/monaco-editor). It is the code editor which powers VS Code.

### 1.1 Technical implementation

A WebView2 window implements shows HTML file with Monaco integrated.

#### 1.1.1 New dependencies

The installer needs to install [WebView2](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)

#### 1.1.2 Why WebView2

Experiments with a POC showed that only WebView2 is displaying Monaco the right way. WebView and WebBrowser elements are using too old versions of IE/Edge who don't support JS functions like `require()`, which are needed for monaco to work. The other option would be to import many JS libraries, but they don't guarantee 100% coverage with JS and they take more space.

### 1.2 Why?

Many people asking us for supporting new file types in the preview panes. With implementing this we will support many developer files (over 125, for example .py, .php, .cs, and many more).

#### 1.2.1 Why Monaco?

* Monaco supports many different language styles. 
* It supports dark mode
* Clickable links
* Monaco has also the MIT-license
* Monaco powers VS Code. It already knows how to render most dev related files.

## 2 What does this implement

[(#1527) - Use Monaco to load developer based files for Preview Pane](https://github.com/microsoft/PowerToys/issues/1527).  
 
## 3 Goals and non-goals

### 3.1 Goals
 
* Create working implementation of Monaco in the preview pane
* Support many languages
 
### 3.2 Non-goals

* Replacing existing previewpanes (like .txt)
* Publishing a crashing system

## 4. Priorities

|Name|Description|Priority|
|----|-----------|--------|
| Working Preview pane | It's simply working. | P0 |
| Settings can install filetypes | When the user activates MonacoPreview in settings it registers the preview handlers. | P0 |
| Style code | Monaco recognizes the file extensions and highlights the syntax the right way. | P0 |
| User can choose file previews | Users can attach custom filetypes to preview. For example `.phptest` files to the `.php` handler. | P2 |
| OOBE | Description for the OOBE. | P1 |
| On/Off in settings | The user can turn it on and off. | P0 |
| Settings: selection for file extensions | User can choose in the settings which File extensions should get registered. | P1 |
| Preview pane Handler registrataion logic in app, not installer | Logic is migrated out so we deduplicate logic now we're adding 125 files.  The uninstaller needs to be run a script of some sort to remove the registration.  | P1 |

## 5. Open questions

- Does enabling / disabling preview pane handlers require admin rights?  If so, we need to warn user uninstalling needs admin priv else the handlers will stay.
