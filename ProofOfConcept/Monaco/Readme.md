# Proof of concept spot for the Preview Pane work powered by the Monaco Editor 

Belongs to [microsoft/powertoys#1527](https://github.com/microsoft/PowerToys/issues/1527). This is an addition to the File Explorer Preview PowerToy

## What's this?

This here is a proof of concept for loading developer files in the preview pane of the explorer

## Usage

1. Compile it

2. Currently you need to start `monacoPreview.exe` with the path to the displayed file as first argument. For example: `monacoPreview.exe c:\index.html`

## Currently limitations

* It's not working as preview pane
* You can't edit the files
* `index.html` and `/monacoSRC/*` need to get outputet

## Files / Folders
### `MainWindow.xaml` 
The main window that gets displayed. It contains the webView

### `MainWindow.xaml.cs`
Contains all main functions

### `FileHandler.cs`
Contains functions to handle the files

### `addHandler.xaml`
Just a dummy window

### `Settings.cs`
Contains settings

### `index.html`
The web file that get's included

Parameters:

`code`: Base64 encoded file content

`theme`: Theme `light` or `dark`

`lang`: Monaco language of the file (ex.: `javascript` or `php`)

### `/monacoSRC/`
Contains the monaco source code

## Installer

The installer needs to install webview2
https://developer.microsoft.com/de-de/microsoft-edge/webview2

And it needs to register all preview handlers:

abap aes cls bat cmd btm c h ligo clj cljs cljc edn coffee litcoffee cc cpp cxx c++ hh hpp hxx h++ cs csx css dart dockerfile fs fsi fsx fsscript go graphql html htm ini java class jar js cjs mjs json jl kt kts ktm less lua i3 m3 s sql m mm pp pas pl plx pm xs t pod php phtml php3 php4 php5 php7 phps php-s pht phar pq ps1 ps1xml psc1 psd1 psm1 pssc psrc cdxml py pyi pyc pyd pyo pyw pyz r rdata rds rda razor cshtml vbhtml rst rb rs sb smallbasic sc scala scm ss sass scss sh st stx swift sv svh tcl tbc ts tsx vb v vh xml yaml yml

## PowerToys Settings
* Set Dark or light mode or system settings
* Ability to add preview handlers

![](https://i.imgur.com/uOybyyd.png)
![](https://i.imgur.com/90qFvAl.png)
