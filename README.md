WoX
===

[![Build status](https://ci.appveyor.com/api/projects/status/bfktntbivg32e103/branch/master?svg=true)](https://ci.appveyor.com/project/happlebao/wox/branch/master)
[![Github All Releases](https://img.shields.io/github/downloads/Wox-launcher/Wox/total.svg)](https://github.com/Wox-launcher/Wox/releases)
[![RamenBless](https://cdn.rawgit.com/LunaGao/BlessYourCodeTag/master/tags/ramen.svg)](https://github.com/LunaGao/BlessYourCodeTag)

**WoX** is a launcher for Windows that simply works. It's an alternative to [Alfred](https://www.alfredapp.com/) and [Launchy](http://www.launchy.net/). You can call it Windows omni-eXecutor if you want a long name.

![demo](http://i.imgur.com/DtxNBJi.gif)

Features
--------

- Search for everything—applications, **uwp**, folders, files and more.
- Use *pinyin* to search for programs / 支持用 **拼音** 搜索程序
  - wyy / wangyiyun → 网易云音乐
- Keyword plugin search 
  - search google with `g search_term`
- Build custom themes at http://www.wox.one/theme/builder
- Install plugins from http://www.wox.one/plugin


Installation
------------

Download `Wox-xxx.exe` from [releases](https://github.com/Wox-launcher/Wox/releases). Latest as of now is [`1.3.524`](https://github.com/Wox-launcher/Wox/releases/download/v1.3.524/Wox-1.3.524.exe) ([`1.3.578`](https://github.com/Wox-launcher/Wox/releases/download/v1.3.578/Wox-1.3.578.exe) for preview channel)

Windows may complain about security due to code not being signed. This will be fixed later. 

Versions marked as **pre-release** are unstable pre-release versions.

- Requirements:
  - .net >= 4.5.2
  - [everything](https://www.voidtools.com/): `.exe` installer + use x64 if your windows is x64 + everything service is running
  - [python3](https://www.python.org/downloads/): `.exe` installer + add it to `%PATH%` or set it in WoX settings

Usage
-----

- Launch: <kbd>Alt</kbd>+<kbd>Space</kbd>
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

1. Install Visual Studio 2015 and tick all Windows 10 sdk options
2. Open powershell with admin permission and `Set-ExecutionPolicy Unrestricted -Scope CurrentUser`

Documentation
-------------
- [Wiki](https://github.com/Wox-launcher/Wox/wiki)
- Outdated doc: [WoX doc](http://doc.wox.one).
- Just ask questions in [issues](https://github.com/Wox-launcher/Wox/issues) for now.
