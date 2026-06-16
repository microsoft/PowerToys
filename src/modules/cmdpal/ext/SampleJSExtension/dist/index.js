"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
Object.defineProperty(exports, "__esModule", { value: true });
exports.activate = activate;
const cmdpal_sdk_1 = require("@microsoft/cmdpal-sdk");
const EXTENSION_ID = 'sample-js-extension';
const EXTENSION_DISPLAY_NAME = 'Sample JS Extension';
const README_PAGE_ID = 'markdown-page';
const DEFAULT_NAVIGATION_MODE = 'push';
const GALLERY_LAYOUT = 'gallery';
const MONOSPACE_FONT = 'monospace';
const SAMPLE_CONTENT_TYPE = 'markdown';
const SAMPLE_STATUS_CONTEXT = 'extension';
const PAGE_IDS = {
    main: 'main-index-page',
    staticList: 'static-list-page',
    detailsList: 'details-list-page',
    gridGallery: 'grid-gallery-page',
    filteredList: 'filtered-list-page',
    markdown: README_PAGE_ID,
    plainText: 'plain-text-page',
    image: 'image-page',
    tree: 'tree-page',
    form: 'form-page',
    multiContent: 'multi-content-page',
};
function icon(glyph) {
    const light = { icon: glyph };
    const dark = { icon: glyph };
    return { light, dark };
}
function rgba(r, g, b, a = 255) {
    return { r, g, b, a };
}
function optionalColor(color) {
    return { hasValue: true, color };
}
function svgIcon(label, background, foreground = '#FFFFFF') {
    const svg = encodeURIComponent(`<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 160 160"><rect width="160" height="160" rx="24" fill="${background}"/><text x="80" y="98" text-anchor="middle" font-family="Segoe UI,Arial" font-size="40" fill="${foreground}">${label}</text></svg>`);
    return {
        light: { data: `data:image/svg+xml;utf8,${svg}` },
        dark: { data: `data:image/svg+xml;utf8,${svg}` },
    };
}
function makeTag(text, foreground, background, glyph, toolTip) {
    return {
        text,
        foreground,
        background,
        icon: glyph ? icon(glyph) : undefined,
        toolTip,
    };
}
function makeStatus(message, state, progress) {
    return { message, state, progress };
}
function publishStatus(message, state, progress) {
    const status = makeStatus(message, state, progress);
    cmdpal_sdk_1.ExtensionHost.showStatus(status.message, status.state, status.progress);
    (0, cmdpal_sdk_1.sendNotification)('sample-js-extension/status', { context: SAMPLE_STATUS_CONTEXT, status });
}
function hidePublishedStatus(message) {
    cmdpal_sdk_1.ExtensionHost.hideStatus(message);
}
function showToastResult(message, result) {
    const args = { message, result };
    return { kind: 'showToast', args };
}
function goToPageResult(pageId, navigationMode = DEFAULT_NAVIGATION_MODE) {
    const args = { pageId, navigationMode };
    return { kind: 'goToPage', args };
}
function shortcut(modifiers, vkey, scanCode) {
    return { modifiers, vkey, scanCode };
}
function contextAction(command, title, glyph, subtitle, requestedShortcut, isCritical) {
    return {
        command,
        title,
        subtitle,
        icon: icon(glyph),
        requestedShortcut,
        isCritical,
    };
}
function containsQuery(item, query) {
    const haystack = `${item.title} ${item.subtitle ?? ''} ${item.section ?? ''}`.toLowerCase();
    return haystack.includes(query.toLowerCase());
}
function parseJsonObject(json) {
    if (!json) {
        return {};
    }
    try {
        const parsed = JSON.parse(json);
        return parsed && typeof parsed === 'object' ? parsed : {};
    }
    catch {
        return {};
    }
}
class CommandRegistry {
    commands = new Map();
    register(command) {
        this.commands.set(command.id, command);
        return command;
    }
    registerAll(commands) {
        for (const command of commands) {
            this.register(command);
        }
    }
    get(id) {
        return this.commands.get(id) ?? null;
    }
}
// === Utility Commands ===
class ShowToastDemoCommand extends cmdpal_sdk_1.InvokableCommandBase {
    settings;
    id = 'show-toast-command';
    name = 'Show Toast';
    icon = icon('\uE7F4');
    constructor(settings) {
        super();
        this.settings = settings;
    }
    invoke() {
        const greeting = this.settings.getSetting('greeting')?.value ?? 'Hello!';
        const darkMode = this.settings.getSetting('darkMode')?.value ?? false;
        const theme = this.settings.getSetting('theme')?.value ?? 'default';
        const progress = { isIndeterminate: false, progressPercent: 100 };
        publishStatus('Running toast showcase…', 'info', progress);
        cmdpal_sdk_1.ExtensionHost.log(`Toast showcase invoked with theme=${theme}; darkMode=${darkMode}`, 'info');
        hidePublishedStatus('Running toast showcase…');
        return showToastResult(`${greeting} from ${EXTENSION_DISPLAY_NAME} (${theme}${darkMode ? ', dark mode' : ''})`, {
            kind: 'keepOpen',
            args: { source: 'show-toast-demo' },
        });
    }
}
class GoToPageCommand extends cmdpal_sdk_1.InvokableCommandBase {
    id;
    name;
    pageId;
    navigationMode;
    icon;
    constructor(id, name, pageId, glyph, navigationMode = DEFAULT_NAVIGATION_MODE) {
        super();
        this.id = id;
        this.name = name;
        this.pageId = pageId;
        this.navigationMode = navigationMode;
        this.icon = icon(glyph);
    }
    invoke() {
        return goToPageResult(this.pageId, this.navigationMode);
    }
}
class FixedResultCommand extends cmdpal_sdk_1.InvokableCommandBase {
    id;
    name;
    kind;
    args;
    logState;
    icon;
    constructor(id, name, kind, glyph, args, logState = 'info') {
        super();
        this.id = id;
        this.name = name;
        this.kind = kind;
        this.args = args;
        this.logState = logState;
        this.icon = icon(glyph);
    }
    invoke() {
        cmdpal_sdk_1.ExtensionHost.log(`${this.name} invoked`, this.logState);
        return { kind: this.kind, args: this.args };
    }
}
class QueryAwareFallbackCommand extends cmdpal_sdk_1.InvokableCommandBase {
    id = 'fallback-query-command';
    name = 'Fallback Query';
    icon = icon('\uE721');
    query = '';
    setQuery(query) {
        this.query = query.trim();
    }
    invoke() {
        const message = this.query
            ? `Fallback command received query: “${this.query}”`
            : 'Fallback command invoked without any query text.';
        cmdpal_sdk_1.ExtensionHost.log(message, 'info');
        return showToastResult(message, { kind: 'keepOpen' });
    }
}
// === Demo Pages ===
//   - Main Index Page
//   - Static List Page
//   - Details List Page
//   - Grid Gallery Page
//   - Filtered List Page
//   - Markdown Content Page
//   - Plain Text Content Page
//   - Image Content Page
//   - Tree Content Page
//   - Form Content Page
//   - Multi-Content Page
class StaticListPage extends cmdpal_sdk_1.ListPageBase {
    moreCommands;
    noOp;
    readmePage;
    goHomeCommand;
    id = PAGE_IDS.staticList;
    name = 'Static List';
    title = 'Static List Page';
    icon = icon('\uE8FD');
    placeholderText = 'Static pages do not respond to search input';
    showDetails = true;
    accentColor = optionalColor(rgba(15, 108, 189));
    items;
    constructor(moreCommands, noOp, readmePage, goHomeCommand) {
        super();
        this.moreCommands = moreCommands;
        this.noOp = noOp;
        this.readmePage = readmePage;
        this.goHomeCommand = goHomeCommand;
        this.items = [
            new cmdpal_sdk_1.Separator('Pinned items'),
            new cmdpal_sdk_1.ListItemBase({
                command: this.readmePage,
                title: 'Open the README-style markdown page',
                subtitle: 'Static items can navigate directly to a page command',
                icon: icon('\uE8A5'),
                section: 'Pages',
                tags: [
                    makeTag('Static', optionalColor(rgba(0, 120, 212))),
                    makeTag('Page', optionalColor(rgba(255, 255, 255)), optionalColor(rgba(0, 120, 212))),
                ],
                details: {
                    title: 'Fixed list item',
                    body: 'This page returns a fixed `IListItem[]` and uses sections, tags, icons, separators, and context actions.',
                },
                moreCommands: [...this.moreCommands],
            }),
            new cmdpal_sdk_1.Separator('Built-in helper commands'),
            new cmdpal_sdk_1.ListItemBase({
                command: this.noOp,
                title: 'No-op item',
                subtitle: 'Uses `NoOpCommand` to keep the list open without side effects',
                icon: icon('\uE9CE'),
                section: 'Helpers',
                tags: [makeTag('NoOp', optionalColor(rgba(96, 94, 92)), optionalColor(rgba(243, 242, 241)))],
                textToSuggest: 'no-op helper',
                details: {
                    title: 'NoOpCommand sample',
                    body: 'Use `NoOpCommand` for placeholders, dividers, or list items whose value is purely descriptive.',
                },
                moreCommands: [contextAction(this.goHomeCommand, 'Go home', '\uE80F', 'Return to the top level')],
            }),
            new cmdpal_sdk_1.ListItemBase({
                command: this.goHomeCommand,
                title: 'Go home from a static page',
                subtitle: 'Returns a `goHome` command result',
                icon: icon('\uE80F'),
                section: 'Commands',
                tags: [makeTag('Result', optionalColor(rgba(0, 153, 188)))],
                details: {
                    title: 'Command result demo',
                    body: 'This item demonstrates `CommandResultKind = goHome` from a regular list item.',
                },
            }),
        ];
    }
    getItems() {
        return this.items;
    }
}
class DetailsListPage extends cmdpal_sdk_1.DynamicListPageBase {
    openRepoCommand;
    copyCommand;
    showToastCommand;
    hideCommand;
    imageIcon;
    id = PAGE_IDS.detailsList;
    name = 'Details List';
    title = 'Details List Page';
    icon = icon('\uE8A0');
    placeholderText = 'Search rich detail cards…';
    showDetails = true;
    query = '';
    items;
    constructor(openRepoCommand, copyCommand, showToastCommand, hideCommand, imageIcon) {
        super();
        this.openRepoCommand = openRepoCommand;
        this.copyCommand = copyCommand;
        this.showToastCommand = showToastCommand;
        this.hideCommand = hideCommand;
        this.imageIcon = imageIcon;
        const statusTags = {
            type: 'tags',
            tags: [
                makeTag('Active', optionalColor(rgba(255, 255, 255)), optionalColor(rgba(16, 124, 16))),
                makeTag('SDK', optionalColor(rgba(0, 120, 212)), undefined, '\uE943', 'Command Palette SDK'),
            ],
        };
        const linkData = {
            type: 'link',
            link: 'https://github.com/microsoft/PowerToys',
            text: 'PowerToys repository',
        };
        const separatorData = { type: 'separator' };
        const commandData = {
            type: 'commands',
            commands: [this.showToastCommand, this.copyCommand, this.hideCommand],
        };
        const metadata = [
            { key: 'Status', data: statusTags },
            { key: 'Link', data: linkData },
            { key: '', data: separatorData },
            { key: 'Actions', data: commandData },
        ];
        const detailPayload = {
            heroImage: this.imageIcon,
            title: 'Rich details panel',
            body: 'Details bodies support **Markdown**, hero images, and structured metadata containing tags, links, commands, and separators.',
            metadata,
        };
        this.items = [
            new cmdpal_sdk_1.ListItemBase({
                command: this.openRepoCommand,
                title: 'Rich details for repository links',
                subtitle: 'Shows every `Details` metadata shape in one place',
                icon: icon('\uE943'),
                section: 'Metadata',
                tags: [
                    makeTag('Details', optionalColor(rgba(0, 120, 212))),
                    makeTag('Hero', optionalColor(rgba(255, 255, 255)), optionalColor(rgba(136, 23, 152))),
                ],
                details: detailPayload,
                moreCommands: [
                    contextAction(this.copyCommand, 'Copy repo URL', '\uE8C8', 'Copy the sample URL', shortcut(2, 67, 46)),
                    contextAction(this.hideCommand, 'Hide palette', '\uE8BB', 'Return to the desktop'),
                ],
            }),
            new cmdpal_sdk_1.ListItemBase({
                command: this.showToastCommand,
                title: 'Action-oriented detail commands',
                subtitle: 'The details panel exposes contextual command buttons',
                icon: icon('\uE7F4'),
                section: 'Commands',
                tags: [makeTag('Context', optionalColor(rgba(16, 124, 16)))],
                details: {
                    title: 'Metadata commands',
                    body: 'Use the command strip inside details to surface contextual actions for the selected item.',
                    metadata: [{ key: 'Commands', data: commandData }],
                },
            }),
            new cmdpal_sdk_1.ListItemBase({
                command: this.hideCommand,
                title: 'Link-heavy metadata item',
                subtitle: 'Combines links, separators, and tags in the side pane',
                icon: icon('\uE8A7'),
                section: 'Links',
                tags: [makeTag('Link', optionalColor(rgba(0, 120, 212))), makeTag('Separator', optionalColor(rgba(96, 94, 92)))],
                details: {
                    title: 'Link metadata',
                    body: 'A details panel can mix simple text, links, tags, separators, and actionable commands.',
                    metadata: [
                        { key: 'Docs', data: linkData },
                        { key: '', data: separatorData },
                        { key: 'Status', data: statusTags },
                    ],
                },
            }),
        ];
    }
    setSearchText(text) {
        this.query = text.trim().toLowerCase();
        this.notifyItemsChanged();
    }
    getItems() {
        if (!this.query) {
            return this.items;
        }
        return this.items.filter((item) => containsQuery(item, this.query));
    }
}
class GridGalleryPage extends cmdpal_sdk_1.ListPageBase {
    imagePage;
    treePage;
    multiPage;
    id = PAGE_IDS.gridGallery;
    name = 'Grid Gallery';
    title = 'Grid & Gallery Page';
    icon = icon('\uECA5');
    gridProperties = { type: GALLERY_LAYOUT, showTitle: true, showSubtitle: true };
    items;
    constructor(imagePage, treePage, multiPage) {
        super();
        this.imagePage = imagePage;
        this.treePage = treePage;
        this.multiPage = multiPage;
        this.items = [
            new cmdpal_sdk_1.ListItemBase({
                command: this.imagePage,
                title: 'Image content',
                subtitle: 'Open the image showcase page',
                icon: svgIcon('IMG', '#0F6CBD'),
            }),
            new cmdpal_sdk_1.ListItemBase({
                command: this.treePage,
                title: 'Tree content',
                subtitle: 'Nested content threads',
                icon: svgIcon('TREE', '#107C10'),
            }),
            new cmdpal_sdk_1.ListItemBase({
                command: this.multiPage,
                title: 'Mixed content',
                subtitle: 'Markdown + form + image',
                icon: svgIcon('MIX', '#881798'),
            }),
            new cmdpal_sdk_1.ListItemBase({
                command: this.imagePage,
                title: 'Medium grid compatible',
                subtitle: 'Gallery pages can also use medium layout',
                icon: svgIcon('GRID', '#D83B01'),
            }),
        ];
    }
    getItems() {
        return this.items;
    }
}
class FilteredListPage extends cmdpal_sdk_1.DynamicListPageBase {
    moreCommands;
    id = PAGE_IDS.filteredList;
    name = 'Filtered List';
    title = 'Filtered List Page';
    icon = icon('\uE71C');
    placeholderText = 'Filter by group and search within the current selection…';
    showDetails = true;
    filters = {
        currentFilterId: 'all',
        filters: [
            { id: 'all', name: 'All', icon: icon('\uE8EF') },
            { id: 'recent', name: 'Recent', icon: icon('\uE823') },
            { id: 'favorite', name: 'Favorites', icon: icon('\uE735') },
        ],
    };
    query = '';
    allItems;
    constructor(moreCommands, readmePage, formPage, treePage) {
        super();
        this.moreCommands = moreCommands;
        this.allItems = [
            {
                id: 'recent-readme',
                title: 'README walkthrough',
                subtitle: 'Rich markdown content and details metadata',
                section: 'Recent',
                filter: 'recent',
                command: readmePage,
                icon: icon('\uE8A5'),
            },
            {
                id: 'recent-form',
                title: 'Form submission sample',
                subtitle: 'Adaptive Card form with submit handling',
                section: 'Recent',
                filter: 'recent',
                command: formPage,
                icon: icon('\uE70F'),
            },
            {
                id: 'favorite-tree',
                title: 'Tree comment thread',
                subtitle: 'Nested markdown content with child nodes',
                section: 'Favorites',
                filter: 'favorite',
                command: treePage,
                icon: icon('\uE8FD'),
            },
        ];
    }
    setSearchText(text) {
        this.query = text.trim().toLowerCase();
        this.notifyItemsChanged();
    }
    setFilter(filterId) {
        const available = this.filters.filters.filter((filter) => 'id' in filter);
        const nextFilterId = available.some((filter) => filter.id === filterId) ? filterId : 'all';
        this.filters = {
            ...this.filters,
            currentFilterId: nextFilterId,
        };
        this.notifyItemsChanged();
    }
    getItems() {
        return this.allItems
            .filter((item) => this.filters.currentFilterId === 'all' || item.filter === this.filters.currentFilterId)
            .filter((item) => !this.query || containsQuery(item, this.query))
            .map((item) => new cmdpal_sdk_1.ListItemBase({
            command: item.command,
            title: item.title,
            subtitle: item.subtitle,
            icon: item.icon,
            section: item.section,
            tags: [
                makeTag(item.filter === 'recent' ? 'Recent' : 'Favorite', optionalColor(rgba(0, 120, 212))),
                makeTag(this.filters.currentFilterId, optionalColor(rgba(96, 94, 92))),
            ],
            moreCommands: [...this.moreCommands],
            details: {
                title: `${item.title} (${item.id})`,
                body: `Current filter: **${this.filters.currentFilterId}**\n\nSearch text: \`${this.query || '(none)'}\`.`,
            },
        }));
    }
}
class MarkdownContentPage extends cmdpal_sdk_1.ContentPageBase {
    id = PAGE_IDS.markdown;
    name = 'Markdown';
    title = 'Markdown Content Page';
    icon = icon('\uE8A5');
    details;
    commands;
    constructor(openRepoCommand, copyCommand, goHomeCommand) {
        super();
        this.details = {
            title: 'README details',
            body: 'This content page mirrors a README-style experience with commands and metadata.',
            metadata: [
                {
                    key: 'Capabilities',
                    data: {
                        type: 'tags',
                        tags: [makeTag('Markdown', optionalColor(rgba(0, 120, 212))), makeTag('Commands', optionalColor(rgba(16, 124, 16)))],
                    },
                },
            ],
        };
        this.commands = [
            contextAction(openRepoCommand, 'Open repository', '\uE8A7', 'Show the PowerToys repo'),
            contextAction(copyCommand, 'Copy README heading', '\uE8C8', 'Copy sample text', shortcut(2, 67, 46)),
            contextAction(goHomeCommand, 'Go home', '\uE80F', 'Return to the main index'),
        ];
    }
    getContent() {
        const overview = {
            type: SAMPLE_CONTENT_TYPE,
            body: `# ${EXTENSION_DISPLAY_NAME}\n\nThis page demonstrates **MarkdownContent** in the TypeScript SDK.\n\n## Highlights\n\n- Searchable list pages\n- Grid and gallery layouts\n- Filters and dynamic updates\n- Images, trees, plain text, and forms\n- Settings, fallback commands, and confirmation dialogs\n\n> Every sample in this extension is reachable from the main index page.`,
        };
        return [overview];
    }
}
class PlainTextContentPage extends cmdpal_sdk_1.ContentPageBase {
    id = PAGE_IDS.plainText;
    name = 'Plain Text';
    title = 'Plain Text Content Page';
    icon = icon('\uE8A5');
    getContent() {
        const content = {
            type: 'plainText',
            fontFamily: MONOSPACE_FONT,
            wrapWords: true,
            text: [
                '// Sample SDK viewer',
                'const provider = new SampleJSProvider();',
                'provider.topLevelCommands();',
                '',
                'This plain text page behaves like a tiny code viewer.',
                'Use it to demonstrate monospace rendering and word wrapping.',
            ].join('\n'),
        };
        return [content];
    }
}
class ImageContentPage extends cmdpal_sdk_1.ContentPageBase {
    id = PAGE_IDS.image;
    name = 'Image';
    title = 'Image Content Page';
    icon = icon('\uE91B');
    getContent() {
        const imageContent = {
            type: 'image',
            image: icon('\uE91B'),
            maxWidth: 320,
            maxHeight: 220,
        };
        return [imageContent];
    }
}
function makeTreeNode(body, children) {
    return {
        type: 'tree',
        rootContent: { type: 'markdown', body },
        getChildren: () => children,
    };
}
class TreeContentPage extends cmdpal_sdk_1.ContentPageBase {
    id = PAGE_IDS.tree;
    name = 'Tree';
    title = 'Tree Content Page';
    icon = icon('\uE8FD');
    getContent() {
        const discussion = makeTreeNode('### @maintainer\nHow should a comprehensive sample page be organized?', [
            makeTreeNode('**@reviewer**\nStart with a searchable index and branch into focused demos.', [
                { type: 'markdown', body: '- Static list page\n- Dynamic list page\n- Rich details panel' },
            ]),
            makeTreeNode('**@designer**\nGroup content samples separately so forms and images are easy to compare.', [
                { type: 'markdown', body: 'Nested trees work well for comment-thread style UIs.' },
            ]),
        ]);
        return [
            {
                type: 'markdown',
                body: '## Nested content\nThe items below simulate a comment thread by mixing `TreeContent` and `MarkdownContent`.',
            },
            discussion,
        ];
    }
}
class FormContentPage extends cmdpal_sdk_1.ContentPageBase {
    settings;
    id = PAGE_IDS.form;
    name = 'Form';
    title = 'Form Content Page';
    icon = icon('\uE70F');
    details = {
        title: 'Adaptive Card form',
        body: 'Submit this sample card to see how `FormContent.submitForm(inputs, data)` works.',
    };
    constructor(settings) {
        super();
        this.settings = settings;
    }
    getContent() {
        const greeting = this.settings.getSetting('greeting')?.value ?? 'Hello!';
        const templateJson = JSON.stringify({
            type: 'AdaptiveCard',
            version: '1.5',
            body: [
                { type: 'TextBlock', size: 'Medium', weight: 'Bolder', text: 'SDK Form Demo' },
                { type: 'Input.Text', id: 'name', label: 'Name', placeholder: 'Enter your name' },
                {
                    type: 'Input.ChoiceSet',
                    id: 'favoritePage',
                    label: 'Favorite page',
                    choices: [
                        { title: 'Markdown', value: PAGE_IDS.markdown },
                        { title: 'Form', value: PAGE_IDS.form },
                        { title: 'Tree', value: PAGE_IDS.tree },
                    ],
                },
                { type: 'Input.Toggle', id: 'notify', title: 'Send a host notification', valueOn: 'true', valueOff: 'false' },
                { type: 'ActionSet', actions: [{ type: 'Action.Submit', title: 'Submit' }] },
            ],
        });
        const dataJson = JSON.stringify({ greeting, submittedFrom: PAGE_IDS.form });
        const form = {
            type: 'form',
            templateJson,
            dataJson,
            stateJson: JSON.stringify({ lastAction: 'idle' }),
            submitForm: (inputs, data) => {
                const parsedInputs = parseJsonObject(inputs);
                const parsedData = parseJsonObject(data);
                const person = parsedInputs.name || 'friend';
                const favoritePage = parsedInputs.favoritePage || 'unknown';
                if (parsedInputs.notify === 'true') {
                    (0, cmdpal_sdk_1.sendNotification)('sample-js-extension/formSubmitted', {
                        context: 'page',
                        inputs: parsedInputs,
                        data: parsedData,
                    });
                }
                cmdpal_sdk_1.ExtensionHost.log(`Form submitted for ${person} (${favoritePage})`, 'success');
                return showToastResult(`${parsedData.greeting ?? greeting} ${person}! Favorite page: ${favoritePage}.`, {
                    kind: 'keepOpen',
                    args: { source: PAGE_IDS.form },
                });
            },
        };
        return [form];
    }
}
class MultiContentPage extends cmdpal_sdk_1.ContentPageBase {
    id = PAGE_IDS.multiContent;
    name = 'Mixed Content';
    title = 'Multi-Content Page';
    icon = icon('\uECA5');
    getContent() {
        const markdown = {
            type: 'markdown',
            body: '## Multiple content blocks\nThis page mixes **markdown**, **plain text**, **images**, and a **form** in one experience.',
        };
        const plainText = {
            type: 'plainText',
            fontFamily: 'userInterface',
            wrapWords: true,
            text: 'This block sits between the markdown header and the image to show mixed content ordering.',
        };
        const image = {
            type: 'image',
            image: svgIcon('MIX', '#881798'),
            maxWidth: 220,
            maxHeight: 180,
        };
        const form = {
            type: 'form',
            templateJson: JSON.stringify({
                type: 'AdaptiveCard',
                version: '1.5',
                body: [
                    { type: 'TextBlock', text: 'Quick feedback', weight: 'Bolder' },
                    { type: 'Input.Text', id: 'feedback', isMultiline: true, placeholder: 'What did you like most?' },
                    { type: 'ActionSet', actions: [{ type: 'Action.Submit', title: 'Send' }] },
                ],
            }),
            dataJson: JSON.stringify({ source: PAGE_IDS.multiContent }),
            submitForm: (inputs, data) => {
                const parsedInputs = parseJsonObject(inputs);
                const parsedData = parseJsonObject(data);
                return showToastResult(`Feedback recorded from ${parsedData.source}: ${parsedInputs.feedback || 'No feedback entered.'}`);
            },
        };
        return [markdown, plainText, image, form];
    }
}
class MainIndexPage extends cmdpal_sdk_1.DynamicListPageBase {
    id = PAGE_IDS.main;
    name = 'Samples';
    title = EXTENSION_DISPLAY_NAME;
    icon = icon('\uE943');
    placeholderText = 'Search pages, commands, settings, and fallback demos…';
    showDetails = true;
    emptyContent;
    query = '';
    items;
    constructor(readmePage, sections) {
        super();
        this.items = [...sections];
        this.emptyContent = new cmdpal_sdk_1.CommandItemBase({
            command: readmePage,
            title: 'No matches found — open the README page',
            subtitle: 'The markdown page explains every available sample.',
            icon: icon('\uE8A5'),
        });
    }
    setSearchText(text) {
        this.query = text.trim().toLowerCase();
        this.notifyItemsChanged();
    }
    getItems() {
        if (!this.query) {
            return this.items;
        }
        return this.items.filter((item) => item instanceof cmdpal_sdk_1.Separator || containsQuery(item, this.query));
    }
}
// === Fallback Handler ===
class QueryAwareFallbackItem extends cmdpal_sdk_1.FallbackCommandItemBase {
    readmePage;
    title = 'Run fallback command';
    subtitle = 'Updates its title live as the user types';
    icon = icon('\uE721');
    command;
    displayTitle = 'Try typing to update this fallback suggestion';
    constructor(command, readmePage) {
        super();
        this.readmePage = readmePage;
        this.command = command;
        this.moreCommands = [contextAction(this.readmePage, 'Open markdown page', '\uE8A5', 'Jump to the README sample')];
    }
    updateQuery(query) {
        const trimmed = query.trim();
        this.command.setQuery(trimmed);
        this.displayTitle = trimmed
            ? `Fallback: act on “${trimmed}”`
            : 'Fallback: type anything to customize this suggestion';
    }
}
// === Provider ===
class SampleJSProvider extends cmdpal_sdk_1.CommandProviderBase {
    id = EXTENSION_ID;
    displayName = EXTENSION_DISPLAY_NAME;
    icon = icon('\uE943');
    registry = new CommandRegistry();
    _settings = new cmdpal_sdk_1.Settings()
        .add(new cmdpal_sdk_1.ToggleSetting('darkMode', 'Dark Mode', false, 'Enable dark mode'))
        .add(new cmdpal_sdk_1.TextSetting('greeting', 'Greeting', 'Hello!', 'Custom greeting message'))
        .add(new cmdpal_sdk_1.ChoiceSetSetting('theme', 'Theme', [
        { title: 'Default', value: 'default' },
        { title: 'Compact', value: 'compact' },
        { title: 'Comfortable', value: 'comfortable' },
    ], 'default', 'UI density'));
    settings = { settingsPage: this._settings.settingsPage };
    showToastCommand = this.registry.register(new ShowToastDemoCommand(this._settings));
    goToReadmeCommand = this.registry.register(new GoToPageCommand('goto-readme-command', 'Go To README Page', PAGE_IDS.markdown, '\uE8A5'));
    goHomeCommand = this.registry.register(new FixedResultCommand('go-home-command', 'Go Home', 'goHome', '\uE80F'));
    hideCommand = this.registry.register(new FixedResultCommand('hide-command', 'Hide Palette', 'hide', '\uE8BB'));
    confirmPrimaryCommand = this.registry.register(new FixedResultCommand('confirm-primary-command', 'Delete sample', 'showToast', '\uE74D', { message: 'Confirmed action executed.' }, 'warning'));
    confirmCommand = this.registry.register(new cmdpal_sdk_1.ConfirmableCommand({
        id: 'confirm-command',
        name: 'Confirm Before Action',
        title: 'Delete the sample item?',
        description: 'This demonstrates the confirmation dialog flow before an action runs.',
        primaryCommand: this.confirmPrimaryCommand,
        isCritical: true,
        icon: icon('\uE74D'),
    }));
    noOpCommand = this.registry.register(new cmdpal_sdk_1.NoOpCommand('noop-command', 'No operation'));
    openRepoCommand = this.registry.register(new cmdpal_sdk_1.OpenUrlCommand('https://github.com/microsoft/PowerToys', 'Open PowerToys'));
    copyRepoCommand = this.registry.register(new cmdpal_sdk_1.CopyTextCommand('https://github.com/microsoft/PowerToys', 'Copy PowerToys URL', 'Repository URL copied.'));
    fallbackCommand = this.registry.register(new QueryAwareFallbackCommand());
    markdownPage = this.registry.register(new MarkdownContentPage(this.openRepoCommand, this.copyRepoCommand, this.goHomeCommand));
    plainTextPage = this.registry.register(new PlainTextContentPage());
    imagePage = this.registry.register(new ImageContentPage());
    treePage = this.registry.register(new TreeContentPage());
    formPage = this.registry.register(new FormContentPage(this._settings));
    multiContentPage = this.registry.register(new MultiContentPage());
    staticListPage = this.registry.register(new StaticListPage([
        contextAction(this.copyRepoCommand, 'Copy link', '\uE8C8', 'Copy the repository URL', shortcut(2, 67, 46)),
        contextAction(this.openRepoCommand, 'Open repository', '\uE8A7', 'View the PowerToys repo'),
    ], this.noOpCommand, this.markdownPage, this.goHomeCommand));
    detailsListPage = this.registry.register(new DetailsListPage(this.openRepoCommand, this.copyRepoCommand, this.showToastCommand, this.hideCommand, svgIcon('PT', '#0F6CBD')));
    gridGalleryPage = this.registry.register(new GridGalleryPage(this.imagePage, this.treePage, this.multiContentPage));
    filteredListPage = this.registry.register(new FilteredListPage([
        contextAction(this.showToastCommand, 'Toast', '\uE7F4', 'Run the toast demo'),
        contextAction(this.goHomeCommand, 'Go home', '\uE80F', 'Return to the main index'),
    ], this.markdownPage, this.formPage, this.treePage));
    mainIndexPage = this.registry.register(new MainIndexPage(this.markdownPage, this.createMainIndexItems()));
    fallbackItem = new QueryAwareFallbackItem(this.fallbackCommand, this.markdownPage);
    pages = [
        this.mainIndexPage,
        this.staticListPage,
        this.detailsListPage,
        this.gridGalleryPage,
        this.filteredListPage,
        this.markdownPage,
        this.plainTextPage,
        this.imagePage,
        this.treePage,
        this.formPage,
        this.multiContentPage,
        this.settings.settingsPage,
    ];
    listPages = [
        this.mainIndexPage,
        this.staticListPage,
        this.detailsListPage,
        this.gridGalleryPage,
        this.filteredListPage,
    ];
    dynamicListPages = [this.mainIndexPage, this.detailsListPage, this.filteredListPage];
    contentPages = [
        this.markdownPage,
        this.plainTextPage,
        this.imagePage,
        this.treePage,
        this.formPage,
        this.multiContentPage,
        this.settings.settingsPage,
    ];
    constructor() {
        super();
        this.registry.register(this.settings.settingsPage);
        this.registry.registerAll(this.pages);
        this.registry.registerAll(this.listPages);
        this.registry.registerAll(this.dynamicListPages);
        this.registry.registerAll(this.contentPages);
    }
    initializeWithHost(host) {
        super.initializeWithHost(host);
        host.log(`${EXTENSION_DISPLAY_NAME} initialized.`, 'info');
    }
    topLevelCommands() {
        return [
            new cmdpal_sdk_1.CommandItemBase({
                command: this.mainIndexPage,
                title: EXTENSION_DISPLAY_NAME,
                subtitle: 'Comprehensive showcase of the TypeScript Command Palette SDK',
                icon: this.icon,
            }),
        ];
    }
    fallbackCommands() {
        return [this.fallbackItem];
    }
    getCommand(id) {
        return this.registry.get(id);
    }
    createMainIndexItems() {
        const commandItems = [
            new cmdpal_sdk_1.Separator('Commands'),
            new cmdpal_sdk_1.ListItemBase({
                command: this.showToastCommand,
                title: 'ShowToast command',
                subtitle: 'Shows a toast and writes to the host status channel',
                icon: icon('\uE7F4'),
                section: 'Commands',
                details: {
                    title: 'showToast',
                    body: 'Demonstrates `CommandResultKind.showToast`, `ExtensionHost`, `sendNotification`, and settings-backed text.',
                },
            }),
            new cmdpal_sdk_1.ListItemBase({
                command: this.goToReadmeCommand,
                title: 'GoToPage command',
                subtitle: 'Navigates to the markdown README page',
                icon: icon('\uE8A5'),
                section: 'Commands',
                details: { title: 'goToPage', body: 'Uses `GoToPageArgs` with `navigationMode = push`.' },
            }),
            new cmdpal_sdk_1.ListItemBase({
                command: this.confirmCommand,
                title: 'Confirm command',
                subtitle: 'Prompts before invoking a critical action',
                icon: icon('\uE74D'),
                section: 'Commands',
                details: {
                    title: 'confirm',
                    body: 'This item wraps a primary action with `ConfirmableCommand`.',
                    metadata: [
                        {
                            key: 'Primary action',
                            data: { type: 'commands', commands: [this.confirmPrimaryCommand] },
                        },
                    ],
                },
            }),
            new cmdpal_sdk_1.ListItemBase({
                command: this.goHomeCommand,
                title: 'GoHome command',
                subtitle: 'Returns to the top-level extension entry',
                icon: icon('\uE80F'),
                section: 'Commands',
            }),
            new cmdpal_sdk_1.ListItemBase({
                command: this.hideCommand,
                title: 'Hide command',
                subtitle: 'Closes the palette without dismissing the extension',
                icon: icon('\uE8BB'),
                section: 'Commands',
            }),
        ];
        const pageItems = [
            new cmdpal_sdk_1.Separator('List Pages'),
            this.makePageItem(this.staticListPage, 'Fixed items with sections, icons, tags, and separators', 'Pages'),
            this.makePageItem(this.detailsListPage, 'Dynamic items with rich details metadata and hero images', 'Pages'),
            this.makePageItem(this.gridGalleryPage, 'Gallery grid layout with image-backed tiles', 'Pages'),
            this.makePageItem(this.filteredListPage, 'Dynamic filters plus text search', 'Pages'),
            new cmdpal_sdk_1.Separator('Content Pages'),
            this.makePageItem(this.markdownPage, 'README-style markdown rendering with contextual commands', 'Content'),
            this.makePageItem(this.plainTextPage, 'Monospace plain text viewer', 'Content'),
            this.makePageItem(this.imagePage, 'Image content with max dimensions', 'Content'),
            this.makePageItem(this.treePage, 'Nested tree content for comment-thread style UI', 'Content'),
            this.makePageItem(this.formPage, 'Adaptive Card form with submit handling', 'Content'),
            this.makePageItem(this.multiContentPage, 'Multiple content types on a single page', 'Content'),
            new cmdpal_sdk_1.Separator('Settings'),
            new cmdpal_sdk_1.ListItemBase({
                command: this.settings.settingsPage,
                title: 'Settings page',
                subtitle: 'Toggle, text, and choice settings powered by the helper classes',
                icon: icon('\uE713'),
                section: 'Settings',
                details: {
                    title: 'Settings helpers',
                    body: 'The provider exposes `Settings`, `ToggleSetting`, `TextSetting`, and `ChoiceSetSetting` via `settings.settingsPage`.',
                },
            }),
        ];
        return [...commandItems, ...pageItems];
    }
    makePageItem(page, subtitle, section) {
        return new cmdpal_sdk_1.ListItemBase({
            command: page,
            title: page.title,
            subtitle,
            icon: page.icon ?? this.icon,
            section,
            tags: [makeTag(section, optionalColor(rgba(0, 120, 212)))],
            details: {
                title: page.title,
                body: subtitle,
            },
            moreCommands: [
                contextAction(this.showToastCommand, 'Toast', '\uE7F4', 'Run the toast demo'),
                contextAction(this.goHomeCommand, 'Go home', '\uE80F', 'Return to the sample index'),
            ],
        });
    }
}
// === Start Server ===
function activate(context) {
    return (0, cmdpal_sdk_1.activate)(context, () => new SampleJSProvider());
}
(0, cmdpal_sdk_1.startJsonRpcServer)(() => new SampleJSProvider());
