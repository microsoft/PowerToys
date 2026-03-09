"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
Object.defineProperty(exports, "__esModule", { value: true });
exports.Icon = void 0;
exports.resolveIcon = resolveIcon;
/**
 * Raycast Icon enum compatibility stub.
 *
 * Maps Raycast's named icons to CmdPal's icon system. CmdPal uses
 * Segoe MDL2 Assets glyphs (Windows icon font) and emoji. We map
 * Raycast's icon enum values to the closest Segoe MDL2 glyph or emoji.
 *
 * Unknown icons fall back to a generic glyph (\uE8A5 = ActionCenter).
 */
/**
 * Raycast Icon enum — commonly used icon identifiers.
 * Values are Segoe MDL2 glyph codes or emoji that approximate the Raycast icon.
 */
exports.Icon = {
    // ── General ──────────────────────────────────────────────────────────
    AddPerson: '\uE8FA', // AddFriend
    Airplane: '✈️',
    AirplaneFilled: '✈️',
    AirplaneLanding: '✈️',
    AirplaneTakeoff: '✈️',
    Alarm: '\uE787', // AlarmClock
    AppWindow: '\uE737', // ChromeRestore
    AppWindowGrid2x2: '\uE8A0', // AllApps
    AppWindowGrid3x3: '\uE8A0',
    ArrowClockwise: '\uE72C', // Refresh
    ArrowCounterClockwise: '\uE7A7', // Undo
    ArrowDown: '\uE74B',
    ArrowLeft: '\uE72B',
    ArrowRight: '\uE72A',
    ArrowUp: '\uE74A',
    ArrowNe: '\uE740',
    ArrowRightCircle: '\uE72A',
    AtSymbol: '\uE910',
    BankNote: '💵',
    BarChart: '\uE9D2',
    BathTub: '🛁',
    BatteryCharging: '\uEA93',
    BatteryDisabled: '\uEA94',
    Bell: '\uEA8F',
    BellDisabled: '\uE7ED',
    Binoculars: '🔭',
    Bird: '🐦',
    BlankDocument: '\uE8A5', // Page
    Bluetooth: '\uE702',
    Boat: '⛵',
    Bold: '\uE8DD',
    Bolt: '\uE945', // Brightness
    BoltDisabled: '\uEC8A',
    Book: '\uE82D', // BookLegacy
    Bookmark: '\uE8A4',
    Box: '\uE7B8', // Package
    Brush: '\uE771',
    Bug: '🐛',
    Building: '\uEC06',
    BulletPoints: '\uE8FD', // BulletedList
    BullsEye: '🎯',
    Calendar: '\uE787',
    Camera: '\uE722',
    Car: '🚗',
    Cart: '\uE7BF', // Shop
    Cd: '💿',
    Center: '\uE8E3', // AlignCenter
    Check: '\uE73E', // CheckMark
    Checkmark: '\uE73E',
    ChevronDown: '\uE70D',
    ChevronUp: '\uE70E',
    ChevronLeft: '\uE70E',
    ChevronRight: '\uE70D',
    Circle: '\uEA3A',
    CircleEllipsis: '\uE712', // More
    CircleFilled: '\uEA3B',
    CircleProgress: '\uE916',
    CircleProgress100: '\uE73E',
    CircleProgress25: '\uE916',
    CircleProgress50: '\uE916',
    CircleProgress75: '\uE916',
    Clipboard: '\uE8C8', // Paste
    Clock: '\uE823',
    Cloud: '\uE753',
    CloudLightning: '⛈️',
    CloudRain: '🌧️',
    CloudSnow: '❄️',
    CloudSun: '⛅',
    Code: '\uE943',
    CodeBlock: '\uE943',
    Cog: '\uE713', // Settings
    Coin: '🪙',
    Coins: '🪙',
    CommandSymbol: '⌘',
    Compass: '🧭',
    ComputerChip: '\uE950',
    Contrast: '\uE793',
    CopyClipboard: '\uE8C8',
    CreditCard: '💳',
    Crop: '\uE7A8',
    Crown: '👑',
    Crypto: '₿',
    DeleteDocument: '\uE74D',
    Desktop: '\uE7F4', // TVMonitor
    Diamond: '💎',
    Dna: '🧬',
    Document: '\uE8A5',
    Dot: '•',
    Download: '\uE896',
    Duplicate: '\uE8C8',
    EditShape: '\uE70F', // Edit
    Eject: '\uE7E8',
    Ellipsis: '\uE712',
    Emoji: '😀',
    EmptyView: '\uE8A5',
    Envelope: '\uE715',
    Eraser: '\uE75C',
    ExclamationMark: '\uE783', // Warning
    Eye: '\uE7B3',
    EyeDisabled: '\uE7B4',
    EyeDropper: '\uEF3C',
    Female: '♀️',
    FilmStrip: '🎞️',
    Filter: '\uE71C',
    Finder: '\uE8B7',
    Fingerprint: '\uE928',
    Flag: '🏳️',
    Folder: '\uE8B7',
    Forward: '\uE72A',
    FullSignal: '\uEC3B',
    GameController: '\uE7FC',
    Gauge: '🔥',
    Gear: '\uE713',
    Gift: '🎁',
    Glasses: '👓',
    Globe: '\uE774',
    Goal: '🎯',
    Hammer: '🔨',
    HardDrive: '\uEDA2',
    Hashtag: '#',
    Headphones: '\uE7F6',
    Heart: '\uEB51',
    HeartDisabled: '\uEB52',
    Heartbeat: '💓',
    Highlight: '\uE7E6',
    Hourglass: '⏳',
    House: '\uE80F', // Home
    Image: '\uEB9F',
    Important: '\uE734', // FavoriteStar
    Info: '\uE946',
    Italic: '\uE8DB',
    Key: '\uE8D7',
    Keyboard: '\uE765',
    Layers: '\uE81E',
    Leaderboard: '🏆',
    LevelMeter: '📊',
    LightBulb: '\uEA80',
    LightBulbOff: '\uEA80',
    LineChart: '\uE9D2',
    Link: '\uE71B',
    List: '\uE8FD',
    Lock: '\uE72E',
    LockDisabled: '\uE785',
    LockUnlocked: '\uE785',
    Logout: '\uE7E8',
    Lowercase: '\uE8AC',
    MagnifyingGlass: '\uE721', // Search
    Male: '♂️',
    Map: '🗺️',
    Maximize: '\uE923',
    Megaphone: '📢',
    MemoryChip: '\uE950',
    MemoryStick: '\uE950',
    Message: '\uE8BD',
    Microphone: '\uE720',
    MicrophoneDisabled: '\uE720',
    Minimize: '\uE921',
    Minus: '\uE738',
    MinusCircle: '\uE738',
    Mobile: '\uE8EA',
    Monitor: '\uE7F4',
    Moon: '🌙',
    Mountain: '⛰️',
    Mouse: '\uE962',
    Multiply: '\uE711', // Cancel
    Music: '\uE8D6',
    Network: '\uE968',
    NewDocument: '\uE8A5',
    NewFolder: '\uE8F4',
    Paperclip: '\uE723',
    Paragraph: '\uE8ED',
    Patch: '\uEBD2',
    Pause: '\uE769',
    PauseCircle: '\uE769',
    Pencil: '\uE70F',
    People: '\uE716',
    Person: '\uE77B',
    PersonCircle: '\uE77B',
    PersonLines: '\uE77B',
    Phone: '\uE717',
    PhoneRinging: '\uE717',
    Pin: '\uE718',
    PinDisabled: '\uE77A',
    Play: '\uE768',
    PlayCircle: '\uE768',
    Plug: '\uE83A',
    Plus: '\uE710', // Add
    PlusCircle: '\uE710',
    PlusMinusDivideMultiply: '\uE94A',
    PlusSquare: '\uE710',
    PlusTopRightSquare: '\uE710',
    Power: '\uE7E8',
    Print: '\uE749',
    QuestionMark: '\uE897',
    QuestionMarkCircle: '\uE897',
    QuoteBlock: '\uE8B2',
    Raindrop: '💧',
    Redo: '\uE7A6',
    RemovePerson: '\uE8FB',
    Repeat: '\uE8EE',
    Reply: '\uE97A',
    Rewind: '\uEB9D',
    RotateAntiClockwise: '\uE7A7',
    RotateClockwise: '\uE7AD',
    Rss: '📡',
    Ruler: '📏',
    SaveDocument: '\uE74E',
    Shield: '\uE83D',
    Shuffle: '\uE8B1',
    Sidebar: '\uE89F',
    Signal0: '\uEC37',
    Signal1: '\uEC38',
    Signal2: '\uEC39',
    Signal3: '\uEC3B',
    Snippets: '\uE943',
    Snowflake: '❄️',
    SoccerBall: '⚽',
    Speaker: '\uE767',
    SpeakerOff: '\uE74F',
    SpeechBubble: '\uE8BD',
    SpeechBubbleActive: '\uE8BD',
    SpeechBubbleImportant: '\uE8BD',
    Star: '\uE734',
    StarCircle: '\uE734',
    StarDisabled: '\uE735',
    Stop: '\uE71A',
    StopFilled: '\uE71A',
    Stopwatch: '⏱️',
    Store: '\uE719',
    Strikethrough: '\uE8DE',
    Sun: '☀️',
    Sunrise: '🌅',
    Swatch: '\uE790',
    Switch: '\uE8AB',
    Syringe: '💉',
    Tag: '\uE8EC',
    Temperature: '🌡️',
    Terminal: '\uE756',
    Text: '\uE8D2',
    TextCursor: '\uE7C3',
    TextInput: '\uE8D2',
    Thermometer: '🌡️',
    Torch: '🔦',
    Train: '🚂',
    Trash: '\uE74D', // Delete
    Tray: '\uE7C8',
    Tree: '🌲',
    Trophy: '🏆',
    TwoPeople: '\uE716',
    Umbrella: '☂️',
    Underline: '\uE8DC',
    Undo: '\uE7A7',
    Upload: '\uE898',
    Uppercase: '\uE8AB',
    Video: '\uE714',
    Wallet: '\uE8C7',
    Wand: '\uE790',
    Warning: '\uE7BA',
    Weights: '🏋️',
    Wifi: '\uE701',
    WifiDisabled: '\uE871',
    Window: '\uE737',
    Wrench: '\uE90F',
    XMarkCircle: '\uE711',
    XMarkTopRightSquare: '\uE711',
};
/**
 * Resolve a Raycast icon reference to a CmdPal-compatible icon string.
 * Handles both Icon enum values and custom image paths.
 */
function resolveIcon(icon) {
    if (typeof icon === 'string') {
        // Direct string — could be an enum value or a custom path
        return icon;
    }
    if (icon && typeof icon === 'object' && 'source' in icon) {
        // Raycast Image.source pattern: { source: Icon.Star } or { source: "path.png" }
        return resolveIcon(icon.source);
    }
    // Fallback: generic document icon
    return '\uE8A5';
}
//# sourceMappingURL=icons.js.map