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
import React from 'react';
type MarkerProps = Record<string, unknown> & {
    children?: React.ReactNode;
};
export declare const List: React.FC<MarkerProps> & {
    Item: React.FC<MarkerProps>;
    Section: React.FC<MarkerProps>;
    EmptyView: React.FC<MarkerProps>;
    Dropdown: React.FC<MarkerProps> & {
        Item: React.FC<MarkerProps>;
        Section: React.FC<MarkerProps>;
    };
};
export declare const Detail: React.FC<MarkerProps> & {
    Metadata: React.FC<MarkerProps> & {
        Label: React.FC<MarkerProps>;
        Link: React.FC<MarkerProps>;
        Separator: React.FC<MarkerProps>;
        TagList: React.FC<MarkerProps> & {
            Item: React.FC<MarkerProps>;
        };
    };
};
export declare const ActionPanel: React.FC<MarkerProps> & {
    Section: React.FC<MarkerProps>;
    Submenu: React.FC<MarkerProps>;
};
export declare const Action: React.FC<MarkerProps> & {
    CopyToClipboard: React.FC<MarkerProps>;
    Open: React.FC<MarkerProps>;
    OpenInBrowser: React.FC<MarkerProps>;
    Paste: React.FC<MarkerProps>;
    Push: React.FC<MarkerProps>;
    SubmitForm: React.FC<MarkerProps>;
};
export declare const Form: React.FC<MarkerProps> & {
    TextField: React.FC<MarkerProps>;
    TextArea: React.FC<MarkerProps>;
    Checkbox: React.FC<MarkerProps>;
    DatePicker: React.FC<MarkerProps>;
    Dropdown: React.FC<MarkerProps> & {
        Item: React.FC<MarkerProps>;
        Section: React.FC<MarkerProps>;
    };
    TagPicker: React.FC<MarkerProps> & {
        Item: React.FC<MarkerProps>;
    };
    Separator: React.FC<MarkerProps>;
    Description: React.FC<MarkerProps>;
    FilePicker: React.FC<MarkerProps>;
    PasswordField: React.FC<MarkerProps>;
};
export declare const Grid: React.FC<MarkerProps> & {
    Item: React.FC<MarkerProps>;
    Section: React.FC<MarkerProps>;
    EmptyView: React.FC<MarkerProps>;
    Dropdown: React.FC<MarkerProps> & {
        Item: React.FC<MarkerProps>;
        Section: React.FC<MarkerProps>;
    };
};
export {};
//# sourceMappingURL=markers.d.ts.map