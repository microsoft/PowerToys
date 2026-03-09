"use strict";
var __create = Object.create;
var __defProp = Object.defineProperty;
var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
var __getOwnPropNames = Object.getOwnPropertyNames;
var __getProtoOf = Object.getPrototypeOf;
var __hasOwnProp = Object.prototype.hasOwnProperty;
var __esm = (fn, res) => function __init() {
  return fn && (res = (0, fn[__getOwnPropNames(fn)[0]])(fn = 0)), res;
};
var __export = (target, all) => {
  for (var name in all)
    __defProp(target, name, { get: all[name], enumerable: true });
};
var __copyProps = (to, from, except, desc) => {
  if (from && typeof from === "object" || typeof from === "function") {
    for (let key of __getOwnPropNames(from))
      if (!__hasOwnProp.call(to, key) && key !== except)
        __defProp(to, key, { get: () => from[key], enumerable: !(desc = __getOwnPropDesc(from, key)) || desc.enumerable });
  }
  return to;
};
var __toESM = (mod, isNodeMode, target) => (target = mod != null ? __create(__getProtoOf(mod)) : {}, __copyProps(
  // If the importer is in node compatibility mode or this is not an ESM
  // file that has been converted to a CommonJS file using a Babel-
  // compatible transform (i.e. "__esModule" has not been set), then set
  // "default" to the CommonJS "module.exports" for node compatibility.
  isNodeMode || !mod || !mod.__esModule ? __defProp(target, "default", { value: mod, enumerable: true }) : target,
  mod
));
var __toCommonJS = (mod) => __copyProps(__defProp({}, "__esModule", { value: true }), mod);

// ../../../../src/api-stubs/toast.ts
var init_toast = __esm({
  "../../../../src/api-stubs/toast.ts"() {
    "use strict";
  }
});

// ../../../../src/api-stubs/clipboard.ts
var init_clipboard = __esm({
  "../../../../src/api-stubs/clipboard.ts"() {
    "use strict";
  }
});

// ../../../../src/api-stubs/navigation.ts
var init_navigation = __esm({
  "../../../../src/api-stubs/navigation.ts"() {
    "use strict";
    init_toast();
  }
});

// src/index.tsx
var index_exports = {};
__export(index_exports, {
  default: () => Command
});
module.exports = __toCommonJS(index_exports);

// ../../../../src/reconciler/host-config.ts
function sanitizeProps(rawProps) {
  const { children, key, ref, ...rest } = rawProps;
  return rest;
}
var hostConfig = {
  // ── Feature flags ───────────────────────────────────────────────────
  supportsMutation: true,
  supportsPersistence: false,
  supportsHydration: false,
  isPrimaryRenderer: true,
  // ── Instance creation ───────────────────────────────────────────────
  createInstance(type, props) {
    return {
      type,
      props: sanitizeProps(props),
      children: []
    };
  },
  createTextInstance(text) {
    return { type: "#text", text };
  },
  // ── Tree building (initial render) ─────────────────────────────────
  appendInitialChild(parent, child) {
    parent.children.push(child);
  },
  finalizeInitialChildren() {
    return false;
  },
  // ── Tree mutations (updates) ───────────────────────────────────────
  appendChild(parent, child) {
    parent.children.push(child);
  },
  appendChildToContainer(container, child) {
    container.children.push(child);
  },
  removeChild(parent, child) {
    const idx = parent.children.indexOf(child);
    if (idx !== -1) {
      parent.children.splice(idx, 1);
    }
  },
  removeChildFromContainer(container, child) {
    const idx = container.children.indexOf(child);
    if (idx !== -1) {
      container.children.splice(idx, 1);
    }
  },
  insertBefore(parent, child, beforeChild) {
    const idx = parent.children.indexOf(beforeChild);
    if (idx !== -1) {
      parent.children.splice(idx, 0, child);
    } else {
      parent.children.push(child);
    }
  },
  insertInContainerBefore(container, child, beforeChild) {
    const idx = container.children.indexOf(beforeChild);
    if (idx !== -1) {
      container.children.splice(idx, 0, child);
    } else {
      container.children.push(child);
    }
  },
  // ── Prop updates ───────────────────────────────────────────────────
  prepareUpdate(_instance, _type, _oldProps, newProps) {
    return sanitizeProps(newProps);
  },
  // react-reconciler 0.32 (React 19) changed the commitUpdate signature:
  //   Old (≤0.31): commitUpdate(instance, updatePayload, type, oldProps, newProps)
  //   New (0.32+):  commitUpdate(instance, type, oldProps, newProps, internalHandle)
  // We accept the new signature and diff newProps directly.
  commitUpdate(instance, _type, _oldProps, newProps) {
    instance.props = sanitizeProps(newProps);
  },
  commitTextUpdate(_textInstance, _oldText, newText) {
    _textInstance.text = newText;
  },
  // ── Container lifecycle ────────────────────────────────────────────
  prepareForCommit(_container) {
    return null;
  },
  resetAfterCommit(container) {
    if (container.onCommit) {
      container.onCommit();
    }
  },
  clearContainer(container) {
    container.children.length = 0;
  },
  // ── Context ────────────────────────────────────────────────────────
  getRootHostContext() {
    return {};
  },
  getChildHostContext(parentContext) {
    return parentContext;
  },
  // ── Misc required methods ──────────────────────────────────────────
  shouldSetTextContent() {
    return false;
  },
  getPublicInstance(instance) {
    return instance;
  },
  preparePortalMount() {
  },
  scheduleTimeout: setTimeout,
  cancelTimeout: clearTimeout,
  noTimeout: -1,
  getCurrentEventPriority() {
    return 32;
  },
  getInstanceFromNode() {
    return null;
  },
  beforeActiveInstanceBlur() {
  },
  afterActiveInstanceBlur() {
  },
  prepareScopeUpdate() {
  },
  getInstanceFromScope() {
    return null;
  },
  detachDeletedInstance() {
  },
  requestPostPaintCallback() {
  },
  maySuspendCommit() {
    return false;
  },
  preloadInstance() {
    return true;
  },
  startSuspendingCommit() {
  },
  suspendInstance() {
  },
  waitForCommitToBeReady() {
    return null;
  },
  NotPendingTransition: null,
  resetFormInstance() {
  },
  setCurrentUpdatePriority() {
  },
  getCurrentUpdatePriority() {
    return 32;
  },
  resolveUpdatePriority() {
    return 32;
  },
  shouldAttemptEagerTransition() {
    return false;
  },
  trackSchedulerEvent() {
  }
};

// ../../../../src/reconciler/reconciler.ts
var import_react_reconciler = __toESM(require("react-reconciler"));
var reconciler = (0, import_react_reconciler.default)(hostConfig);
reconciler.injectIntoDevTools({
  bundleType: 0,
  // production
  version: "0.1.0",
  rendererPackageName: "@cmdpal/raycast-compat"
});

// ../../../../src/components/markers.tsx
var import_react = __toESM(require("react"));
function createMarker(displayName) {
  const Marker = ({ children, ...props }) => {
    return import_react.default.createElement(displayName, props, children);
  };
  Marker.displayName = displayName;
  return Marker;
}
function createMarkerWithChildProps(displayName, childPropNames) {
  const Marker = ({ children, ...allProps }) => {
    const elemChildren = [];
    const cleanProps = {};
    for (const [key, value] of Object.entries(allProps)) {
      if (childPropNames.includes(key) && import_react.default.isValidElement(value)) {
        elemChildren.push(value);
      } else {
        cleanProps[key] = value;
      }
    }
    return import_react.default.createElement(displayName, cleanProps, children, ...elemChildren);
  };
  Marker.displayName = displayName;
  return Marker;
}
var List = Object.assign(createMarker("List"), {
  Item: createMarkerWithChildProps("List.Item", ["actions", "detail"]),
  Section: createMarker("List.Section"),
  EmptyView: createMarker("List.EmptyView"),
  Dropdown: Object.assign(createMarker("List.Dropdown"), {
    Item: createMarker("List.Dropdown.Item"),
    Section: createMarker("List.Dropdown.Section")
  })
});
var Detail = Object.assign(createMarker("Detail"), {
  Metadata: Object.assign(createMarker("Detail.Metadata"), {
    Label: createMarker("Detail.Metadata.Label"),
    Link: createMarker("Detail.Metadata.Link"),
    Separator: createMarker("Detail.Metadata.Separator"),
    TagList: Object.assign(createMarker("Detail.Metadata.TagList"), {
      Item: createMarker("Detail.Metadata.TagList.Item")
    })
  })
});
var ActionPanel = Object.assign(createMarker("ActionPanel"), {
  Section: createMarker("ActionPanel.Section"),
  Submenu: createMarker("ActionPanel.Submenu")
});
var Action = Object.assign(createMarker("Action"), {
  CopyToClipboard: createMarker("Action.CopyToClipboard"),
  Open: createMarker("Action.Open"),
  OpenInBrowser: createMarker("Action.OpenInBrowser"),
  Paste: createMarker("Action.Paste"),
  Push: createMarker("Action.Push"),
  SubmitForm: createMarker("Action.SubmitForm")
});
var Form = Object.assign(createMarker("Form"), {
  TextField: createMarker("Form.TextField"),
  TextArea: createMarker("Form.TextArea"),
  Checkbox: createMarker("Form.Checkbox"),
  DatePicker: createMarker("Form.DatePicker"),
  Dropdown: Object.assign(createMarker("Form.Dropdown"), {
    Item: createMarker("Form.Dropdown.Item"),
    Section: createMarker("Form.Dropdown.Section")
  }),
  TagPicker: Object.assign(createMarker("Form.TagPicker"), {
    Item: createMarker("Form.TagPicker.Item")
  }),
  Separator: createMarker("Form.Separator"),
  Description: createMarker("Form.Description"),
  FilePicker: createMarker("Form.FilePicker"),
  PasswordField: createMarker("Form.PasswordField")
});
var Grid = Object.assign(createMarker("Grid"), {
  Item: createMarkerWithChildProps("Grid.Item", ["content", "actions"]),
  Section: createMarker("Grid.Section"),
  EmptyView: createMarker("Grid.EmptyView"),
  Dropdown: Object.assign(createMarker("Grid.Dropdown"), {
    Item: createMarker("Grid.Dropdown.Item"),
    Section: createMarker("Grid.Dropdown.Section")
  })
});

// ../../../../src/bridge/bridge-provider.ts
var import_react2 = __toESM(require("react"));

// ../../../../src/api-stubs/index.ts
init_toast();
init_clipboard();

// ../../../../src/api-stubs/environment.ts
var path = __toESM(require("path"));
var defaultBasePath = path.join(
  process.env.LOCALAPPDATA ?? process.env.TEMP ?? ".",
  "Microsoft",
  "PowerToys",
  "CommandPalette",
  "JSExtensions"
);
var config = {
  extensionName: "unknown",
  commandName: "default",
  assetsPath: path.join(defaultBasePath, "_raycast-compat", "assets"),
  supportPath: path.join(defaultBasePath, "_raycast-compat", "data"),
  extensionDir: path.join(defaultBasePath, "_raycast-compat"),
  launchType: "userInitiated" /* UserInitiated */,
  launchContext: {}
};

// ../../../../src/api-stubs/index.ts
init_navigation();

// ../../../../src/api-stubs/system-utilities.ts
init_toast();

// ../../../../src/api-stubs/hooks.ts
var import_react3 = require("react");

// src/index.tsx
var import_jsx_runtime = require("react/jsx-runtime");
function Command() {
  return /* @__PURE__ */ (0, import_jsx_runtime.jsxs)(Grid, { children: [
    /* @__PURE__ */ (0, import_jsx_runtime.jsx)(Grid.Item, { title: "Image 1", content: "https://example.com/img1.png" }),
    /* @__PURE__ */ (0, import_jsx_runtime.jsx)(Grid.Item, { title: "Image 2", content: "https://example.com/img2.png" })
  ] });
}
//# sourceMappingURL=unsupported-ext.js.map
