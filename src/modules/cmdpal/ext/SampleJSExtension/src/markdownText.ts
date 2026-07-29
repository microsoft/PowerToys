// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * The rendered Markdown guide shared by the markdown sample pages. This mirrors
 * `SampleMarkdownPage.SampleMarkdownText` from the C# SamplePagesExtension.
 */
export const sampleMarkdownText = `
# Markdown Guide

Markdown is a lightweight markup language with plain text formatting syntax. It's often used to format readme files, for writing messages in online forums, and to create rich text using a simple, plain text editor.

## Basic Markdown Formatting

### Headings

    # This is an <h1> tag
    ## This is an <h2> tag
    ### This is an <h3> tag
    #### This is an <h4> tag
    ##### This is an <h5> tag
    ###### This is an <h6> tag

### Emphasis

    *This text will be italic*
    _This will also be italic_

    **This text will be bold**
    __This will also be bold__

    _You **can** combine them_

Result:

*This text will be italic*

_This will also be italic_

**This text will be bold**

__This will also be bold__

_You **can** combine them_

### Lists

**Unordered:**

    * Milk
    * Bread
        * Whole grain
    * Butter

Result:

* Milk
* Bread
    * Whole grain
* Butter

**Ordered:**

    1. Tidy the kitchen
    2. Prepare ingredients
    3. Cook delicious things

Result:

1. Tidy the kitchen
2. Prepare ingredients
3. Cook delicious things

### Links

    [example](http://example.com)

Result:

[example](http://example.com)

### Blockquotes

    As Albert Einstein said:

    > If we knew what it was we were doing,
    > it would not be called research, would it?

Result:

As Albert Einstein said:

> If we knew what it was we were doing,
> it would not be called research, would it?

### Horizontal Rules

\`\`\`markdown
    ---
\`\`\`

Result:

---

### Code Snippets

    Indenting by 4 spaces will turn an entire paragraph into a code-block.

Result:

    .my-link {
        text-decoration: underline;
    }

## Tables

### Pipe table

| Right | Left | Default | Center |
|------:|:-----|---------|:------:|
|   12  |  12  |    12   |    12  |
|  123  |  123 |   123   |   123  |
|    1  |    1 |     1   |     1  |

## Advanced Markdown

Note: Some syntax which is not standard to native Markdown. They're extensions of the language.

### Strike-throughs

    ~~deleted words~~

Result:

~~deleted words~~
`;
