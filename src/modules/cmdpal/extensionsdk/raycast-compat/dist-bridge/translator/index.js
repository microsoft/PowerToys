"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
Object.defineProperty(exports, "__esModule", { value: true });
exports.RaycastMarkdownContent = exports.RaycastListItem = exports.RaycastContentPage = exports.RaycastDynamicListPage = exports.translateVNode = void 0;
exports.translateTree = translateTree;
const vnode_1 = require("../reconciler/vnode");
// Re-export the full translator and its types
var translate_vnode_1 = require("./translate-vnode");
Object.defineProperty(exports, "translateVNode", { enumerable: true, get: function () { return translate_vnode_1.translateVNode; } });
var translate_vnode_2 = require("./translate-vnode");
Object.defineProperty(exports, "RaycastDynamicListPage", { enumerable: true, get: function () { return translate_vnode_2.RaycastDynamicListPage; } });
Object.defineProperty(exports, "RaycastContentPage", { enumerable: true, get: function () { return translate_vnode_2.RaycastContentPage; } });
Object.defineProperty(exports, "RaycastListItem", { enumerable: true, get: function () { return translate_vnode_2.RaycastListItem; } });
Object.defineProperty(exports, "RaycastMarkdownContent", { enumerable: true, get: function () { return translate_vnode_2.RaycastMarkdownContent; } });
// ── Translator ────────────────────────────────────────────────────────
/**
 * Translate a VNode tree root into a CmdPal-compatible page description.
 */
function translateTree(roots) {
    const root = roots.find(vnode_1.isElementVNode);
    if (!root)
        return null;
    switch (root.type) {
        case 'List':
            return translateList(root);
        case 'Detail':
            return translateDetail(root);
        default:
            return null;
    }
}
function translateList(node) {
    const items = [];
    for (const child of node.children) {
        if (!(0, vnode_1.isElementVNode)(child))
            continue;
        if (child.type === 'List.Item') {
            items.push(translateListItem(child));
        }
        else if (child.type === 'List.Section') {
            const sectionTitle = child.props.title ?? '';
            for (const sectionChild of child.children) {
                if ((0, vnode_1.isElementVNode)(sectionChild) && sectionChild.type === 'List.Item') {
                    items.push(translateListItem(sectionChild, sectionTitle));
                }
            }
        }
    }
    return {
        type: 'list',
        isLoading: node.props.isLoading ?? false,
        searchBarPlaceholder: node.props.searchBarPlaceholder,
        items,
        onSearchTextChange: node.props.onSearchTextChange,
    };
}
function translateListItem(node, section) {
    const actions = extractActions(node);
    const accessories = node.props.accessories;
    const tags = accessories?.map((a) => ({ text: a.text ?? '' })).filter((t) => t.text);
    return {
        title: node.props.title ?? '',
        subtitle: node.props.subtitle,
        section,
        icon: node.props.icon,
        tags: tags?.length ? tags : undefined,
        keywords: node.props.keywords,
        actions,
    };
}
function extractActions(node) {
    const actions = [];
    for (const child of node.children) {
        if (!(0, vnode_1.isElementVNode)(child))
            continue;
        if (child.type === 'ActionPanel') {
            for (const actionChild of child.children) {
                if (!(0, vnode_1.isElementVNode)(actionChild))
                    continue;
                if (actionChild.type === 'ActionPanel.Section') {
                    for (const sectionChild of actionChild.children) {
                        if ((0, vnode_1.isElementVNode)(sectionChild)) {
                            actions.push(translateAction(sectionChild));
                        }
                    }
                }
                else {
                    actions.push(translateAction(actionChild));
                }
            }
        }
    }
    return actions;
}
function translateAction(node) {
    return {
        title: node.props.title ?? node.type,
        type: node.type,
        onAction: node.props.onAction,
        props: node.props,
    };
}
function translateDetail(node) {
    const metadata = [];
    // Detail's markdown is in props.markdown
    const markdown = node.props.markdown ?? '';
    // Walk Detail.Metadata children for metadata entries
    for (const child of node.children) {
        if (!(0, vnode_1.isElementVNode)(child))
            continue;
        if (child.type === 'Detail.Metadata') {
            for (const metaChild of child.children) {
                if (!(0, vnode_1.isElementVNode)(metaChild))
                    continue;
                if (metaChild.type === 'Detail.Metadata.Label') {
                    metadata.push({
                        key: metaChild.props.title ?? '',
                        value: metaChild.props.text ?? metaChild.props.icon,
                    });
                }
            }
        }
    }
    return {
        type: 'detail',
        markdown,
        isLoading: node.props.isLoading ?? false,
        metadata,
    };
}
//# sourceMappingURL=index.js.map