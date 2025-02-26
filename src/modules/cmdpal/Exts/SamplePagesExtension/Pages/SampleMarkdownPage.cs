// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

internal sealed partial class SampleMarkdownPage : ContentPage
{
    public static readonly string SampleMarkdownText = @"
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

**Inordered:**

    * Milk
    * Bread
        * Wholegrain
    * Butter

Result:

* Milk
* Bread
    * Wholegrain
* Butter

**Ordered:**

    1. Tidy the kitchen
    2. Prepare ingredients
    3. Cook delicious things

Result:

1. Tidy the kitchen
2. Prepare ingredients
3. Cook delicious things

### Images

    ![Alt Text](url)

Result:

![painting](https://i.imgur.com/93XJSNh.png)

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

```markdown
    ---
```

Result:

---

### Code Snippets

    Indenting by 4 spaces will turn an entire paragraph into a code-block.

Result:

    .my-link {
        text-decoration: underline;
    }

### Reference Lists & Titles

    **The quick brown [fox][1], jumped over the lazy [dog][2].**

    [1]: https://en.wikipedia.org/wiki/Fox ""Wikipedia: Fox""
    [2]: https://en.wikipedia.org/wiki/Dog ""Wikipedia: Dog""

Result:

**The quick brown [fox][1], jumped over the lazy [dog][2].**

[1]: https://en.wikipedia.org/wiki/Fox ""Wikipedia: Fox""
[2]: https://en.wikipedia.org/wiki/Dog ""Wikipedia: Dog""

### Escaping

    \*literally\*

Result:

\*literally\*

## Advanced Markdown

Note: Some syntax which is not standard to native Markdown. They're extensions of the language.

### Strike-throughs

    ~~deleted words~~

Result:

~~deleted words~~


";

    public SampleMarkdownPage()
    {
        Icon = new IconInfo(string.Empty);
        Name = "Sample Markdown Page";
    }

    public override IContent[] GetContent() => [new MarkdownContent(SampleMarkdownText)];
}
