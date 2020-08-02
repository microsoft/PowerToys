# What is a font subset and why should I use one?
As the name implies, a font subset contains a portion of the complete set of characters included in a font. This is useful for fonts like [Office UI Fabric's icon font](https://developer.microsoft.com/en-us/fabric#/styles/icons), which includes far more characters than what any single application will need or should serve at once (>1000 characters, which is ~150KB for the .woff). Using a subset ensures that an application is only using the glyphs it needs at any given time, and saves significant bytes over the wire.

If you're reading this, you've probably already generated a subset of Fabric's icon font. Follow the instructions below to understand its contents and learn how to integrate it into your project.

## Contents
1. [Get started](#get-started)
    - [Folder structure](#folder-structure)
2. [How to use icon subsets](#how-to-use-icon-subsets)
    - [Which subset should I use?](#which-subset-should-i-use)
      - [CSS and SCSS subsets](#css-and-scss-subsets)
      - [TypeScript subsets](#typescript-subsets)
3. [Maintaining a subset](#maintaining-a-subset)
    - [Font config options](#font-config-options)
      - [Subset chunk settings](#subset-chunk-settings)

# Get started
## Folder structure
Each subset package will include some variation the following files, assuming a font name of `fabric-icons` (which can be configured in the tool—see Font config options below). Note that if "Create subset chunks" was selected when generating a subset, there will be an additional HTML, JSON, CSS, SCSS, and TS file for each generated "chunked" subset.

```
fabric-icons
│   README.md: The docs you're reading now.
│   microsoft-ui-fabric-assets-license: License and usage terms for the fonts.
│   fabric-icons.html - Demo HTML for a given subset.
│
└─── config
│   │   fabric-icons.json - Configuration file for the subset package.
|        > Contains the list of icon names to be included as well as options for
|        > the subset itself. See the section below on "maintaining a subset"
|        > for more details.
│
└─── css
|   │   fabric-icons.css - @font-face definition and icon classes for the subset. Links to the subsetted font file.
|   │   fabric-icons-inline.css - Same as standard CSS, but includes the base64-encoded WOFF font file inline.
│
└─── scss
|   │   fabric-icons.scss - Same as standard CSS and adds a mixin for each icon.
|   │   fabric-icons-inline.scss - Same as standard SCSS, but includes the base64-encoded WOFF font file inline.
│
└─── fonts
|   │   fabric-icons.woff - The subsetted icon font.
│
└─── src
|   │   index.ts - Contains top-level exports for all subset initialization code.
|   │   fabric-icons.ts - TypeScript subset options and initialization code.
|   │   IconNames.ts - Contains const enum of all available icon names for Intellisense.
```

# How to use icon subsets
The icon subsets included here are based on the CSS and SCSS approaches of [office-ui-fabric-core](https://github.com/OfficeDev/office-ui-fabric-core/) (see the [icons page on the Fabric website](https://developer.microsoft.com/en-us/fabric#/styles/icons)), and TypeScript-based approach of [`@uifabric/icons`](https://www.npmjs.com/package/@uifabric/icons), which is what's used in [office-ui-fabric-react](https://github.com/OfficeDev/office-ui-fabric-react/). Each subset can be used independently of either of those projects, meaning your app doesn't need to have them installed in order to use the icon subsets in this package. The instructions here will help you get started quickly using each subset method, but you should refer to the full documentation for each for more detail.

## Which subset should I use?

### CSS and SCSS subsets
The CSS and SCSS methods are similar to what's used in [office-ui-fabric-core](https://github.com/OfficeDev/office-ui-fabric-core/), which is useful for quickly applying Microsoft's design language to an HTML and CSS-based web app. Both include class names you can use in plain HTML, and differ only in that the SCSS files require a SASS preprocessor to use its icon mixins and build its output into plain CSS.

**Use the CSS subset** if your app is relatively simple (e.g. no build process) or you aren't already using SCSS. Simply add a link to one of the icons CSS files to your page, or `@import` it into another CSS file, and add the icon classes to HTML elements like so:
```css
<i class="ms-Icon ms-Icon--Edit"></i>
```

**Use the SCSS subset** if your app already uses [SCSS](http://sass-lang.com/) in a build pipeline, or you wish to use the icon mixins to inject the icon code into your own class names.

For example, if you wish to use the same Edit icon as before but without using the standard `.ms-Icon--Edit` class, you can use the mixins to inject the icon code into a custom class like so:

```scss
.myClassName:before { @include ms-Icon--Edit }
```

This would result in the following code being generated:

```scss
.myClassName:before { content: "\E70F"; }
```

You may wish to use this approach if there may be multiple versions of Fabric on the page and you want to ensure there won't be any rendering conflicts. However, be sure to either add the `.ms-Icon` class to those elements *or* `@include ms-Icon` in your custom class name as this sets the `@font-family` to the font in your subset.

### TypeScript subsets
The TypeScript subset method included under `src` is similar to what's used in [`@uifabric/icons`](https://www.npmjs.com/package/@uifabric/icons) and will make the most sense in applications that use [office-ui-fabric-react](https://github.com/OfficeDev/office-ui-fabric-react/) controls.

As a prerequisite to using these subsets, ensure that your project is configured to build TypeScript. You may wish to use a tool like [create-react-app-typescript](https://github.com/wmonk/create-react-app-typescript) or Microsoft's own [TypeScript-React-Starter](https://github.com/Microsoft/TypeScript-React-Starter). This is a temporary limitation—future updates to the subsetter tool will include pre-compiled subsets that you'll be able to use with simpler configurations.

Once your project is configured, in your source code, import the `initializeIcons` function and call it on the page(s) you wish to use the icons:

```tsx
import { initializeIcons } from 'path-to-subset/src';

initializeIcons();
```

This defines an `@font-face` rule and registers a map of icon names for the subset. Once initialized, icons can be used through the `getIcon` API in `office-ui-fabric-react`, like below:

```tsx
import { Icon } from 'office-ui-fabric-react/lib/Icon';

<Icon iconName='Snow' />
```

CSS classnames can also be used directly on elements using the `getIconClassNames` API from `@uifabric/styling`:

```tsx
import { getIconClassName } from '@uifabric/styling';

return `<i class="${getIconClassName('Snow')}" />`;
```

More details on JavaScript-based icon usage can be found on [Office UI Fabric React's wiki](https://github.com/OfficeDev/office-ui-fabric-react/wiki/Using-icons), [`@uifabric/icons`](https://www.npmjs.com/package/@uifabric/icons), and [`@uifabric/styling`](https://www.npmjs.com/package/@uifabric/styling#using-fabric-core-classes).


# Maintaining a subset
Each subset package has a configuration JSON file that describes which icons are included in that subset and which options were selected for the subset, such as `chunkSubsets` or `excludeGlyphs`. This file is used to maintain and update the subset over time--it can be dragged and dropped on to the Fabric Icons tool to pre-populate icon selection and whichever options were chosen in the tool.

It is recommended to check this file in to a project's source control and update it each time you make changes to a subset.

Most options map to a text field or checkbox in the "Subset options" section of the details pane of Fabric Icons tool. This is represented by the "Tool label" column below.


## Font config options
| Option        | Default value | Tool label | Description  |
|:------------- |:--------------|:--- |:-----------------|
| `fontName` | `'fabric-icons'` | Font file name | The name given to each of the subset's HTML, CSS, SCSS, TS, and JSON files. |
| `fontFamilyName` | `'FabricMDL2Icons'` | Font-family name | The name of the font-family given in the @font-face definition for the subset. It is recommended to change this only if the icon subset will be used in conjunction with multiple, different versions of Fabric or other icon subsets on the same page. |
| `excludeGlyphs` | `false` | Exclude selection from subset | Produces a subset that excludes the selected glyphs from the full Fabric icon set. This is useful if you wish to create a subset that includes all of the Fabric icons EXCEPT for the selected icons.|
| `chunkSubsets` | `false` | Create subset chunks | Controls whether to produce additional subsets that can be loaded on-demand.|
| `hashFontFileName` | `false` | Hash font file name | Controls whether to add a unique hash to the .woff font file based on glyph selection and subset options. This is useful for [CDN cache busting](http://www.adopsinsider.com/ad-ops-basics/what-is-a-cache-buster-and-how-does-it-work//) if you plan on hosting font files on a CDN, which may serve old cached versions of a font without a busting mechanism. |
| `subsetChunkSettings` | { } | N/A | Additional configuration options for subset chunks. Options here only apply if `chunkSubsets` is `true`. See "Subset chunk settings" below for more details. |
| `glyphs` | [{ }] | N/A | The list of icons included in a subset, populated from selecting icons in the Fabric Icons tool. Each glyph is an object with a `name` and `unicode` property. |

### Subset chunk settings
Each option here is a property of `subsetChunkSettings`, and only apply if `chunkSubsets` is `true`.

| Option        | Default value | Tool label | Description  |
|:------------- |:--------------|:--- |:-----------------|
| `maxSubsetSize` | `100` | Max subset chunk size | The maximum number of icons to be included in a generated subset chunk. Larger chunks take longer to load as they will have more characters and larger fonts, but smaller chunks may incur more HTTP requests. |

