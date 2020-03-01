WoX
===

![Maintenance](https://img.shields.io/maintenance/yes/2020)
[![GitHub release (latest by date)](https://img.shields.io/github/v/release/jjw24/wox)](https://github.com/jjw24/Wox/releases/latest)
![GitHub Release Date](https://img.shields.io/github/release-date/jjw24/wox)
![GitHub commits since latest release](https://img.shields.io/github/commits-since/jjw24/wox/v1.3.524)
[![Build Status](https://dev.azure.com/Wox-Launcher/Wox/_apis/build/status/jjw24.Wox?branchName=master)](https://dev.azure.com/Wox-Launcher/Wox/_build/latest?definitionId=1&branchName=master)
[![Github All Releases](https://img.shields.io/github/downloads/jjw24/Wox/total.svg)](https://github.com/jjw24/Wox/releases)
[![RamenBless](https://cdn.rawgit.com/LunaGao/BlessYourCodeTag/master/tags/ramen.svg)](https://github.com/LunaGao/BlessYourCodeTag)

**WoX** is a launcher for Windows that simply works. It's an alternative to [Alfred](https://www.alfredapp.com/) and [Launchy](http://www.launchy.net/). You can call it Windows omni-eXecutor if you want a long name.

![demo](http://i.imgur.com/DtxNBJi.gif)

Features
--------

- Search for everything—applications, **uwp**, folders, files and more.
- Use *pinyin* to search for programs / 支持用 **拼音** 搜索程序
  - wyy / wangyiyun → 网易云音乐
- Keyword plugin search `g search_term`
- Search youtube, google, twitter and many more
- Build custom themes at http://www.wox.one/theme/builder
- Install plugins from http://www.wox.one/plugin

**New from this fork:**
- Portable mode
- Drastically improved search experience
- Search all subfolders and files
- Option to always run CMD or Powershell as administrator
- Run CMD, Powershell and programs as a different user
- Manage what programs should be loaded
- Highlighting of how results are matched during query search
- Open web search result as a tab or a new window
- Automatic update
- Reload/update plugin data

Installation
------------

View new features released from this fork since Wox v1.3.524: [new releases](https://github.com/jjw24/Wox/releases)

To install this fork's version of Wox, you can **download** it [here](https://github.com/jjw24/Wox/releases/latest).

To install the upstream version:

Download `Wox-xxx.exe` from [releases](https://github.com/Wox-launcher/Wox/releases). Latest as of now is [`1.3.524`](https://github.com/Wox-launcher/Wox/releases/download/v1.3.524/Wox-1.3.524.exe) ([`1.3.578`](https://github.com/Wox-launcher/Wox/releases/download/v1.3.578/Wox-1.3.578.exe) for preview channel)

Windows may complain about security due to code not being signed. This will be fixed later. 

Versions marked as **pre-release** are unstable pre-release versions.

- Requirements:
  - .net >= 4.5.2
  - If you want to integrate with [everything](https://www.voidtools.com/): `.exe` installer + use x64 if your windows is x64 + everything service is running. Supported version is 1.3.4.686
  - If you use python plugins, install [python3](https://www.python.org/downloads/): `.exe` installer + add it to `%PATH%` or set it in WoX settings

Usage
-----

- Launch: <kbd>Alt</kbd>+<kbd>Space</kbd>
- Context Menu: <kbd>Ctrl</kbd>+<kbd>O</kbd>
- Cancel/Return: <kbd>Esc</kbd>
- Install/Uninstall plugin: type `wpm install/uninstall`
- Reset: delete `%APPDATA%\Wox`
- Log: `%APPDATA%\Wox\Logs`

Contribution
------------

- First and most importantly, star it!
- Read [Coding Style](https://github.com/Wox-launcher/Wox/wiki/Coding-Style)
- Send PR to **dev** branch
- I'd appreciate if you could solve [help_needed](https://github.com/Wox-launcher/Wox/issues?q=is%3Aopen+is%3Aissue+label%3Ahelp_needed) labeled issue
- Don't hesitate to ask questions in the [issues](https://github.com/Wox-launcher/Wox/issues)

Build
-----

Install Visual Studio 2015/2017/2019

This project requires Windows 10 SDK:

  VS 2015:
  - Tick all Windows 10 sdk options

  VS 2017/2019 and later:  
  - Last Windows 10 SDK which [supported](https://github.com/Wox-launcher/Wox/pull/1827#commitcomment-26475392) UwpDesktop is version 10.0.14393.795. It is needed to compile "Programs" Plugin (UWP.cs), you will see the "References" of Plugin.Programs as broken if you use a later SDK version.
  - This SDK cannot be installed via VS 2019 installer.
  - Download and install [Windows 10 SDK version 10.0.14393.795](https://go.microsoft.com/fwlink/p/?LinkId=838916).

Documentation
-------------
- [Wiki](https://github.com/Wox-launcher/Wox/wiki)
- Outdated doc: [WoX doc](http://doc.wox.one).
- Just ask questions in [issues](https://github.com/Wox-launcher/Wox/issues) for now.
