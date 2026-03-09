"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
Object.defineProperty(exports, "__esModule", { value: true });
exports.RaycastContentPage = exports.RaycastMarkdownContent = exports.RaycastDynamicListPage = exports.RaycastListItem = void 0;
exports.translateVNode = translateVNode;
// ══════════════════════════════════════════════════════════════════════════
// Concrete CmdPal-compatible classes
// ══════════════════════════════════════════════════════════════════════════
/**
 * ListItem produced by the translator.
 * Implements the IListItem shape from CmdPal SDK.
 */
class RaycastListItem {
    title = '';
    subtitle = '';
    icon;
    command;
    moreCommands = [];
    tags = [];
    details;
    section = '';
    textToSuggest = '';
}
exports.RaycastListItem = RaycastListItem;
/**
 * DynamicListPage produced by translating a Raycast List VNode.
 * Implements the IDynamicListPage shape from CmdPal SDK.
 */
class RaycastDynamicListPage {
    _type = 'dynamicListPage';
    id = '';
    name = '';
    icon;
    placeholderText = '';
    searchText = '';
    showDetails = false;
    hasMoreItems = false;
    PropChanged;
    ItemsChanged;
    _items;
    _onSearchTextChange;
    constructor(items = [], onSearchTextChange) {
        this._items = items;
        this._onSearchTextChange = onSearchTextChange;
    }
    getItems() {
        return this._items;
    }
    updateSearchText(oldSearch, newSearch) {
        this._onSearchTextChange?.(newSearch);
    }
    setSearchText(searchText) {
        const oldSearch = this.searchText;
        this.searchText = searchText;
        this.updateSearchText(oldSearch, searchText);
    }
    loadMore() { }
}
exports.RaycastDynamicListPage = RaycastDynamicListPage;
/**
 * MarkdownContent produced by translating Raycast Detail markdown.
 * Implements the IMarkdownContent shape from CmdPal SDK.
 */
class RaycastMarkdownContent {
    type = 'markdown';
    id = '';
    body = '';
    PropChanged;
    constructor(body) {
        if (body !== undefined)
            this.body = body;
    }
}
exports.RaycastMarkdownContent = RaycastMarkdownContent;
/**
 * ContentPage produced by translating Raycast Detail or Form VNodes.
 * Implements the IContentPage shape from CmdPal SDK.
 */
class RaycastContentPage {
    _type = 'contentPage';
    id = '';
    name = '';
    icon;
    details;
    commands = [];
    PropChanged;
    ItemsChanged;
    _content;
    constructor(content = []) {
        this._content = content;
    }
    getContent() {
        return this._content;
    }
}
exports.RaycastContentPage = RaycastContentPage;
// ══════════════════════════════════════════════════════════════════════════
// Internal helpers
// ══════════════════════════════════════════════════════════════════════════
/** Type guard: is this child a valid VNode (not null, not text)? */
function isVNode(node) {
    return (node != null &&
        typeof node === 'object' &&
        'type' in node &&
        typeof node.type === 'string' &&
        node.type !== '#text');
}
/** Convert a Raycast icon prop to CmdPal IIconInfo format. */
function toIconInfo(raw) {
    if (raw == null)
        return undefined;
    if (typeof raw === 'string') {
        const data = { icon: raw };
        return { light: data, dark: data };
    }
    if (typeof raw === 'object') {
        const obj = raw;
        // Already IIconInfo-shaped
        if ('light' in obj || 'dark' in obj)
            return raw;
        // Raycast Icon enum object with source property
        if ('source' in obj && typeof obj.source === 'string') {
            const data = { icon: obj.source };
            return { light: data, dark: data };
        }
    }
    return undefined;
}
/** Convert Raycast accessories to CmdPal ITag array. */
function accessoriesToTags(accessories) {
    const tags = [];
    for (const acc of accessories) {
        if (acc.text) {
            tags.push({
                text: acc.text,
                toolTip: acc.tooltip,
                icon: toIconInfo(acc.icon),
            });
        }
        else if (acc.tag) {
            const text = typeof acc.tag === 'string' ? acc.tag : acc.tag.value;
            tags.push({ text });
        }
        else if (acc.icon) {
            tags.push({ text: '', icon: toIconInfo(acc.icon) });
        }
        else if (acc.date) {
            tags.push({ text: acc.date.toLocaleDateString() });
        }
    }
    return tags;
}
/** Convert a single Action VNode to a CmdPal IContextItem. */
function vnodeToContextItem(node) {
    return {
        title: node.props.title ?? node.type,
        icon: toIconInfo(node.props.icon),
    };
}
/** Walk an ActionPanel subtree and extract all actions as IContextItem[]. */
function extractActions(node) {
    const actions = [];
    for (const child of node.children) {
        if (!isVNode(child))
            continue;
        if (child.type === 'ActionPanel') {
            for (const actionChild of child.children) {
                if (!isVNode(actionChild))
                    continue;
                if (actionChild.type === 'ActionPanel.Section') {
                    for (const sectionChild of actionChild.children) {
                        if (isVNode(sectionChild)) {
                            actions.push(vnodeToContextItem(sectionChild));
                        }
                    }
                }
                else {
                    actions.push(vnodeToContextItem(actionChild));
                }
            }
        }
    }
    return actions;
}
// ══════════════════════════════════════════════════════════════════════════
// List translation
// ══════════════════════════════════════════════════════════════════════════
function translateListItem(node, section) {
    const item = new RaycastListItem();
    item.title = node.props.title ?? '';
    item.subtitle = node.props.subtitle ?? '';
    item.icon = toIconInfo(node.props.icon);
    item.section = section ?? '';
    const accessories = node.props.accessories;
    if (accessories?.length) {
        item.tags = accessoriesToTags(accessories);
    }
    const keywords = node.props.keywords;
    if (keywords?.length) {
        item.textToSuggest = keywords.join(' ');
    }
    item.moreCommands = extractActions(node);
    return item;
}
function translateList(node) {
    const items = [];
    for (const child of node.children) {
        if (!isVNode(child))
            continue;
        if (child.type === 'List.Item') {
            items.push(translateListItem(child));
        }
        else if (child.type === 'List.Section') {
            const sectionTitle = child.props.title ?? '';
            for (const sectionChild of child.children) {
                if (isVNode(sectionChild) && sectionChild.type === 'List.Item') {
                    items.push(translateListItem(sectionChild, sectionTitle));
                }
            }
        }
        // Unknown child types silently skipped
    }
    const page = new RaycastDynamicListPage(items, node.props.onSearchTextChange);
    page.name = node.props.navigationTitle ?? '';
    page.placeholderText = node.props.searchBarPlaceholder ?? '';
    return page;
}
// ══════════════════════════════════════════════════════════════════════════
// Detail translation
// ══════════════════════════════════════════════════════════════════════════
function translateDetailMetadata(node) {
    const elements = [];
    for (const child of node.children) {
        if (!isVNode(child))
            continue;
        switch (child.type) {
            case 'Detail.Metadata.Label':
                elements.push({
                    key: child.props.title ?? '',
                    data: { text: child.props.text ?? '' },
                    icon: toIconInfo(child.props.icon),
                });
                break;
            case 'Detail.Metadata.Link':
                elements.push({
                    key: child.props.title ?? '',
                    data: {
                        _type: 'link',
                        text: child.props.text ?? '',
                        url: child.props.target ?? '',
                    },
                });
                break;
            case 'Detail.Metadata.Separator':
                elements.push({
                    key: '__separator__',
                    data: { _type: 'separator' },
                });
                break;
            case 'Detail.Metadata.TagList': {
                const tags = [];
                for (const tagChild of child.children) {
                    if (isVNode(tagChild) && tagChild.type === 'Detail.Metadata.TagList.Item') {
                        tags.push({ text: tagChild.props.text ?? '' });
                    }
                }
                elements.push({
                    key: child.props.title ?? '',
                    data: { _type: 'tags', tags },
                });
                break;
            }
            default:
                // Unknown metadata type — skip gracefully
                break;
        }
    }
    return elements;
}
function translateDetail(node) {
    const content = [];
    let detailsMetadata;
    for (const child of node.children) {
        if (!isVNode(child))
            continue;
        if (child.type === 'Detail.Markdown') {
            content.push(new RaycastMarkdownContent(child.props.content ?? ''));
        }
        else if (child.type === 'Detail.Metadata') {
            detailsMetadata = translateDetailMetadata(child);
        }
    }
    // Fallback: check props.markdown (older Raycast pattern)
    if (content.length === 0 && typeof node.props.markdown === 'string') {
        content.push(new RaycastMarkdownContent(node.props.markdown));
    }
    const page = new RaycastContentPage(content);
    page.name = node.props.navigationTitle ?? '';
    if (detailsMetadata?.length) {
        page.details = { metadata: detailsMetadata };
    }
    return page;
}
// ══════════════════════════════════════════════════════════════════════════
// Form translation (stub — form fields as markdown description)
// ══════════════════════════════════════════════════════════════════════════
function translateForm(node) {
    // Represent form fields as a markdown description for now.
    // Full FormContent integration will land with the bridge layer.
    const fields = [];
    for (const child of node.children) {
        if (!isVNode(child))
            continue;
        const fieldTitle = child.props.title ??
            child.props.id ??
            child.type;
        fields.push(`- **${fieldTitle}** (${child.type})`);
    }
    const md = fields.length > 0 ? `# Form\n\n${fields.join('\n')}` : '# Form';
    const page = new RaycastContentPage([new RaycastMarkdownContent(md)]);
    page.name = node.props.navigationTitle ?? '';
    return page;
}
// ══════════════════════════════════════════════════════════════════════════
// Public API
// ══════════════════════════════════════════════════════════════════════════
/**
 * Translate a single VNode into a CmdPal-compatible SDK object.
 *
 * @param vnode - The root VNode from the reconciler tree
 * @returns A CmdPal page object, or null for unknown/unsupported types
 */
function translateVNode(vnode) {
    if (!vnode?.type)
        return null;
    switch (vnode.type) {
        case 'List':
            return translateList(vnode);
        case 'Detail':
            return translateDetail(vnode);
        case 'Form':
            return translateForm(vnode);
        default:
            return null;
    }
}
//# sourceMappingURL=translate-vnode.js.map