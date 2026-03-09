"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.Grid = exports.Form = exports.Action = exports.ActionPanel = exports.Detail = exports.List = void 0;
/**
 * Marker components — fake @raycast/api components.
 *
 * These are NOT real React components that render to DOM or native views.
 * They are lightweight function components that act as "markers" — the
 * custom reconciler sees their type strings (e.g. "List", "List.Item")
 * and captures them as VNode objects in the tree.
 *
 * When a Raycast extension does:
 *   import { List } from "@raycast/api";
 *   <List><List.Item title="Hello" /></List>
 *
 * Our esbuild alias redirects the import to this module, so the extension
 * runs with our marker components instead. The reconciler captures the
 * tree structure, and the translator maps it to CmdPal SDK types.
 */
const react_1 = __importDefault(require("react"));
/**
 * Create a marker component with a specific display name.
 * The display name becomes the VNode.type in the reconciler tree.
 */
function createMarker(displayName) {
    const Marker = ({ children, ...props }) => {
        // The component itself is a thin wrapper. The reconciler's createInstance
        // uses the component's type name (displayName) to build the VNode.
        // We use createElement with a string type so the reconciler sees it
        // as a host element, not a composite component.
        return react_1.default.createElement(displayName, props, children);
    };
    Marker.displayName = displayName;
    return Marker;
}
/**
 * Create a marker that promotes React-element props to children.
 *
 * Raycast components like List.Item accept props (e.g. `actions`, `detail`)
 * that contain React elements. These must be rendered as children so the
 * reconciler captures them as VNodes in the tree. Without this, they stay
 * as opaque React element objects in VNode.props and the translator can't
 * walk them.
 */
function createMarkerWithChildProps(displayName, childPropNames) {
    const Marker = ({ children, ...allProps }) => {
        const elemChildren = [];
        const cleanProps = {};
        for (const [key, value] of Object.entries(allProps)) {
            if (childPropNames.includes(key) && react_1.default.isValidElement(value)) {
                elemChildren.push(value);
            }
            else {
                cleanProps[key] = value;
            }
        }
        return react_1.default.createElement(displayName, cleanProps, children, ...elemChildren);
    };
    Marker.displayName = displayName;
    return Marker;
}
// ── List components ────────────────────────────────────────────────────
exports.List = Object.assign(createMarker('List'), {
    Item: createMarkerWithChildProps('List.Item', ['actions', 'detail']),
    Section: createMarker('List.Section'),
    EmptyView: createMarker('List.EmptyView'),
    Dropdown: Object.assign(createMarker('List.Dropdown'), {
        Item: createMarker('List.Dropdown.Item'),
        Section: createMarker('List.Dropdown.Section'),
    }),
});
// ── Detail components ──────────────────────────────────────────────────
exports.Detail = Object.assign(createMarker('Detail'), {
    Metadata: Object.assign(createMarker('Detail.Metadata'), {
        Label: createMarker('Detail.Metadata.Label'),
        Link: createMarker('Detail.Metadata.Link'),
        Separator: createMarker('Detail.Metadata.Separator'),
        TagList: Object.assign(createMarker('Detail.Metadata.TagList'), {
            Item: createMarker('Detail.Metadata.TagList.Item'),
        }),
    }),
});
// ── ActionPanel components ─────────────────────────────────────────────
exports.ActionPanel = Object.assign(createMarker('ActionPanel'), {
    Section: createMarker('ActionPanel.Section'),
    Submenu: createMarker('ActionPanel.Submenu'),
});
exports.Action = Object.assign(createMarker('Action'), {
    CopyToClipboard: createMarker('Action.CopyToClipboard'),
    Open: createMarker('Action.Open'),
    OpenInBrowser: createMarker('Action.OpenInBrowser'),
    Paste: createMarker('Action.Paste'),
    Push: createMarker('Action.Push'),
    SubmitForm: createMarker('Action.SubmitForm'),
});
// ── Form components ────────────────────────────────────────────────────
exports.Form = Object.assign(createMarker('Form'), {
    TextField: createMarker('Form.TextField'),
    TextArea: createMarker('Form.TextArea'),
    Checkbox: createMarker('Form.Checkbox'),
    DatePicker: createMarker('Form.DatePicker'),
    Dropdown: Object.assign(createMarker('Form.Dropdown'), {
        Item: createMarker('Form.Dropdown.Item'),
        Section: createMarker('Form.Dropdown.Section'),
    }),
    TagPicker: Object.assign(createMarker('Form.TagPicker'), {
        Item: createMarker('Form.TagPicker.Item'),
    }),
    Separator: createMarker('Form.Separator'),
    Description: createMarker('Form.Description'),
    FilePicker: createMarker('Form.FilePicker'),
    PasswordField: createMarker('Form.PasswordField'),
});
// ── Grid components ────────────────────────────────────────────────────
exports.Grid = Object.assign(createMarker('Grid'), {
    Item: createMarkerWithChildProps('Grid.Item', ['content', 'actions']),
    Section: createMarker('Grid.Section'),
    EmptyView: createMarker('Grid.EmptyView'),
    Dropdown: Object.assign(createMarker('Grid.Dropdown'), {
        Item: createMarker('Grid.Dropdown.Item'),
        Section: createMarker('Grid.Dropdown.Section'),
    }),
});
//# sourceMappingURL=markers.js.map