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

// ../../../../src/api-stubs/icons.ts
var Icon = {
  // ── General ──────────────────────────────────────────────────────────
  AddPerson: "\uE8FA",
  // AddFriend
  Airplane: "\u2708\uFE0F",
  AirplaneFilled: "\u2708\uFE0F",
  AirplaneLanding: "\u2708\uFE0F",
  AirplaneTakeoff: "\u2708\uFE0F",
  Alarm: "\uE787",
  // AlarmClock
  AppWindow: "\uE737",
  // ChromeRestore
  AppWindowGrid2x2: "\uE8A0",
  // AllApps
  AppWindowGrid3x3: "\uE8A0",
  ArrowClockwise: "\uE72C",
  // Refresh
  ArrowCounterClockwise: "\uE7A7",
  // Undo
  ArrowDown: "\uE74B",
  ArrowLeft: "\uE72B",
  ArrowRight: "\uE72A",
  ArrowUp: "\uE74A",
  ArrowNe: "\uE740",
  ArrowRightCircle: "\uE72A",
  AtSymbol: "\uE910",
  BankNote: "\u{1F4B5}",
  BarChart: "\uE9D2",
  BathTub: "\u{1F6C1}",
  BatteryCharging: "\uEA93",
  BatteryDisabled: "\uEA94",
  Bell: "\uEA8F",
  BellDisabled: "\uE7ED",
  Binoculars: "\u{1F52D}",
  Bird: "\u{1F426}",
  BlankDocument: "\uE8A5",
  // Page
  Bluetooth: "\uE702",
  Boat: "\u26F5",
  Bold: "\uE8DD",
  Bolt: "\uE945",
  // Brightness
  BoltDisabled: "\uEC8A",
  Book: "\uE82D",
  // BookLegacy
  Bookmark: "\uE8A4",
  Box: "\uE7B8",
  // Package
  Brush: "\uE771",
  Bug: "\u{1F41B}",
  Building: "\uEC06",
  BulletPoints: "\uE8FD",
  // BulletedList
  BullsEye: "\u{1F3AF}",
  Calendar: "\uE787",
  Camera: "\uE722",
  Car: "\u{1F697}",
  Cart: "\uE7BF",
  // Shop
  Cd: "\u{1F4BF}",
  Center: "\uE8E3",
  // AlignCenter
  Check: "\uE73E",
  // CheckMark
  Checkmark: "\uE73E",
  ChevronDown: "\uE70D",
  ChevronUp: "\uE70E",
  ChevronLeft: "\uE70E",
  ChevronRight: "\uE70D",
  Circle: "\uEA3A",
  CircleEllipsis: "\uE712",
  // More
  CircleFilled: "\uEA3B",
  CircleProgress: "\uE916",
  CircleProgress100: "\uE73E",
  CircleProgress25: "\uE916",
  CircleProgress50: "\uE916",
  CircleProgress75: "\uE916",
  Clipboard: "\uE8C8",
  // Paste
  Clock: "\uE823",
  Cloud: "\uE753",
  CloudLightning: "\u26C8\uFE0F",
  CloudRain: "\u{1F327}\uFE0F",
  CloudSnow: "\u2744\uFE0F",
  CloudSun: "\u26C5",
  Code: "\uE943",
  CodeBlock: "\uE943",
  Cog: "\uE713",
  // Settings
  Coin: "\u{1FA99}",
  Coins: "\u{1FA99}",
  CommandSymbol: "\u2318",
  Compass: "\u{1F9ED}",
  ComputerChip: "\uE950",
  Contrast: "\uE793",
  CopyClipboard: "\uE8C8",
  CreditCard: "\u{1F4B3}",
  Crop: "\uE7A8",
  Crown: "\u{1F451}",
  Crypto: "\u20BF",
  DeleteDocument: "\uE74D",
  Desktop: "\uE7F4",
  // TVMonitor
  Diamond: "\u{1F48E}",
  Dna: "\u{1F9EC}",
  Document: "\uE8A5",
  Dot: "\u2022",
  Download: "\uE896",
  Duplicate: "\uE8C8",
  EditShape: "\uE70F",
  // Edit
  Eject: "\uE7E8",
  Ellipsis: "\uE712",
  Emoji: "\u{1F600}",
  EmptyView: "\uE8A5",
  Envelope: "\uE715",
  Eraser: "\uE75C",
  ExclamationMark: "\uE783",
  // Warning
  Eye: "\uE7B3",
  EyeDisabled: "\uE7B4",
  EyeDropper: "\uEF3C",
  Female: "\u2640\uFE0F",
  FilmStrip: "\u{1F39E}\uFE0F",
  Filter: "\uE71C",
  Finder: "\uE8B7",
  Fingerprint: "\uE928",
  Flag: "\u{1F3F3}\uFE0F",
  Folder: "\uE8B7",
  Forward: "\uE72A",
  FullSignal: "\uEC3B",
  GameController: "\uE7FC",
  Gauge: "\u{1F525}",
  Gear: "\uE713",
  Gift: "\u{1F381}",
  Glasses: "\u{1F453}",
  Globe: "\uE774",
  Goal: "\u{1F3AF}",
  Hammer: "\u{1F528}",
  HardDrive: "\uEDA2",
  Hashtag: "#",
  Headphones: "\uE7F6",
  Heart: "\uEB51",
  HeartDisabled: "\uEB52",
  Heartbeat: "\u{1F493}",
  Highlight: "\uE7E6",
  Hourglass: "\u23F3",
  House: "\uE80F",
  // Home
  Image: "\uEB9F",
  Important: "\uE734",
  // FavoriteStar
  Info: "\uE946",
  Italic: "\uE8DB",
  Key: "\uE8D7",
  Keyboard: "\uE765",
  Layers: "\uE81E",
  Leaderboard: "\u{1F3C6}",
  LevelMeter: "\u{1F4CA}",
  LightBulb: "\uEA80",
  LightBulbOff: "\uEA80",
  LineChart: "\uE9D2",
  Link: "\uE71B",
  List: "\uE8FD",
  Lock: "\uE72E",
  LockDisabled: "\uE785",
  LockUnlocked: "\uE785",
  Logout: "\uE7E8",
  Lowercase: "\uE8AC",
  MagnifyingGlass: "\uE721",
  // Search
  Male: "\u2642\uFE0F",
  Map: "\u{1F5FA}\uFE0F",
  Maximize: "\uE923",
  Megaphone: "\u{1F4E2}",
  MemoryChip: "\uE950",
  MemoryStick: "\uE950",
  Message: "\uE8BD",
  Microphone: "\uE720",
  MicrophoneDisabled: "\uE720",
  Minimize: "\uE921",
  Minus: "\uE738",
  MinusCircle: "\uE738",
  Mobile: "\uE8EA",
  Monitor: "\uE7F4",
  Moon: "\u{1F319}",
  Mountain: "\u26F0\uFE0F",
  Mouse: "\uE962",
  Multiply: "\uE711",
  // Cancel
  Music: "\uE8D6",
  Network: "\uE968",
  NewDocument: "\uE8A5",
  NewFolder: "\uE8F4",
  Paperclip: "\uE723",
  Paragraph: "\uE8ED",
  Patch: "\uEBD2",
  Pause: "\uE769",
  PauseCircle: "\uE769",
  Pencil: "\uE70F",
  People: "\uE716",
  Person: "\uE77B",
  PersonCircle: "\uE77B",
  PersonLines: "\uE77B",
  Phone: "\uE717",
  PhoneRinging: "\uE717",
  Pin: "\uE718",
  PinDisabled: "\uE77A",
  Play: "\uE768",
  PlayCircle: "\uE768",
  Plug: "\uE83A",
  Plus: "\uE710",
  // Add
  PlusCircle: "\uE710",
  PlusMinusDivideMultiply: "\uE94A",
  PlusSquare: "\uE710",
  PlusTopRightSquare: "\uE710",
  Power: "\uE7E8",
  Print: "\uE749",
  QuestionMark: "\uE897",
  QuestionMarkCircle: "\uE897",
  QuoteBlock: "\uE8B2",
  Raindrop: "\u{1F4A7}",
  Redo: "\uE7A6",
  RemovePerson: "\uE8FB",
  Repeat: "\uE8EE",
  Reply: "\uE97A",
  Rewind: "\uEB9D",
  RotateAntiClockwise: "\uE7A7",
  RotateClockwise: "\uE7AD",
  Rss: "\u{1F4E1}",
  Ruler: "\u{1F4CF}",
  SaveDocument: "\uE74E",
  Shield: "\uE83D",
  Shuffle: "\uE8B1",
  Sidebar: "\uE89F",
  Signal0: "\uEC37",
  Signal1: "\uEC38",
  Signal2: "\uEC39",
  Signal3: "\uEC3B",
  Snippets: "\uE943",
  Snowflake: "\u2744\uFE0F",
  SoccerBall: "\u26BD",
  Speaker: "\uE767",
  SpeakerOff: "\uE74F",
  SpeechBubble: "\uE8BD",
  SpeechBubbleActive: "\uE8BD",
  SpeechBubbleImportant: "\uE8BD",
  Star: "\uE734",
  StarCircle: "\uE734",
  StarDisabled: "\uE735",
  Stop: "\uE71A",
  StopFilled: "\uE71A",
  Stopwatch: "\u23F1\uFE0F",
  Store: "\uE719",
  Strikethrough: "\uE8DE",
  Sun: "\u2600\uFE0F",
  Sunrise: "\u{1F305}",
  Swatch: "\uE790",
  Switch: "\uE8AB",
  Syringe: "\u{1F489}",
  Tag: "\uE8EC",
  Temperature: "\u{1F321}\uFE0F",
  Terminal: "\uE756",
  Text: "\uE8D2",
  TextCursor: "\uE7C3",
  TextInput: "\uE8D2",
  Thermometer: "\u{1F321}\uFE0F",
  Torch: "\u{1F526}",
  Train: "\u{1F682}",
  Trash: "\uE74D",
  // Delete
  Tray: "\uE7C8",
  Tree: "\u{1F332}",
  Trophy: "\u{1F3C6}",
  TwoPeople: "\uE716",
  Umbrella: "\u2602\uFE0F",
  Underline: "\uE8DC",
  Undo: "\uE7A7",
  Upload: "\uE898",
  Uppercase: "\uE8AB",
  Video: "\uE714",
  Wallet: "\uE8C7",
  Wand: "\uE790",
  Warning: "\uE7BA",
  Weights: "\u{1F3CB}\uFE0F",
  Wifi: "\uE701",
  WifiDisabled: "\uE871",
  Window: "\uE737",
  Wrench: "\uE90F",
  XMarkCircle: "\uE711",
  XMarkTopRightSquare: "\uE711"
};

// ../../../../src/api-stubs/hooks.ts
var import_react3 = require("react");

// src/index.tsx
var import_react4 = require("react");
var import_jsx_runtime = require("react/jsx-runtime");
function Command() {
  const [searchText, setSearchText] = (0, import_react4.useState)("");
  const [items] = (0, import_react4.useState)([
    { id: "1", title: "Hello World", subtitle: "First item", icon: Icon.Star },
    { id: "2", title: "Goodbye World", subtitle: "Second item", icon: Icon.Globe }
  ]);
  const filtered = items.filter(
    (i) => i.title.toLowerCase().includes(searchText.toLowerCase())
  );
  return /* @__PURE__ */ (0, import_jsx_runtime.jsx)(List, { onSearchTextChange: setSearchText, searchBarPlaceholder: "Search items...", children: filtered.map((item) => /* @__PURE__ */ (0, import_jsx_runtime.jsx)(
    List.Item,
    {
      title: item.title,
      subtitle: item.subtitle,
      icon: item.icon,
      actions: /* @__PURE__ */ (0, import_jsx_runtime.jsxs)(ActionPanel, { children: [
        /* @__PURE__ */ (0, import_jsx_runtime.jsx)(Action.CopyToClipboard, { content: item.title }),
        /* @__PURE__ */ (0, import_jsx_runtime.jsx)(Action.OpenInBrowser, { url: `https://example.com/${item.id}` })
      ] })
    },
    item.id
  )) });
}
//# sourceMappingURL=valid-ext.js.map
