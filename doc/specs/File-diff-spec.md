# New PowerToys utility: File comparison tool

<img align="right" height="200px" src="https://github.com/microsoft/PowerToys/blob/user/aaron-junker/diff-spec/doc/images/icons/PowerToys%20icon/PNG/1440x2160.png" />

- **What is it:** File comparison tool
- **Authors:** [Aaron Junker](https://github.com/aaron-junker)
- **Spec Status:** Draft
- **GitHub issue:** [#14950](https://github.com/microsoft/PowerToys/issues/14950)

## 1 Overview

This utility will add a tool to view differences between two files.

### 1.1 Technical implementation

This utility will use [Microsoft Monaco Render engine](https://microsoft.github.io/monaco-editor/) which is currently implemented in the source code previewer. We will have to move the Monaco Editor source code and the corresponding helper classes to a new centralised 

### 1.2 Use cases

## 2 Goals and non-Goals

### 2.1 Goals



### 2.2 Non-goals

* Shipping a crashing product

## 3. Priorities

|Name|Description|Priority|
|----|-----------|--------|
|Working diff editor|An editor where you're able to compare two different files|P0|
|Open file from context menu|Let people open files from Explorer context menu|P0|

## 4. Open questions

* Will this be a standalone utility or under another PowerToys utility?
* Can you launch this as standalone application?
  * If yes Should we add this function also to other utilities?
