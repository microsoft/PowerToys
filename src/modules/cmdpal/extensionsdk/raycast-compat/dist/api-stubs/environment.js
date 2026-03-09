"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || (function () {
    var ownKeys = function(o) {
        ownKeys = Object.getOwnPropertyNames || function (o) {
            var ar = [];
            for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k)) ar[ar.length] = k;
            return ar;
        };
        return ownKeys(o);
    };
    return function (mod) {
        if (mod && mod.__esModule) return mod;
        var result = {};
        if (mod != null) for (var k = ownKeys(mod), i = 0; i < k.length; i++) if (k[i] !== "default") __createBinding(result, mod, k[i]);
        __setModuleDefault(result, mod);
        return result;
    };
})();
Object.defineProperty(exports, "__esModule", { value: true });
exports.environment = exports.LaunchType = void 0;
exports._configureEnvironment = _configureEnvironment;
/**
 * Raycast environment compatibility stub.
 *
 * Provides runtime environment information that Raycast extensions
 * read via `import { environment } from "@raycast/api"`.
 *
 * Values are populated from the CmdPal manifest and runtime context.
 * Call `_configureEnvironment()` during extension bootstrap to set
 * actual values; until then, safe defaults are used.
 */
const path = __importStar(require("path"));
var LaunchType;
(function (LaunchType) {
    LaunchType["UserInitiated"] = "userInitiated";
    LaunchType["Background"] = "background";
})(LaunchType || (exports.LaunchType = LaunchType = {}));
const defaultBasePath = path.join(process.env.LOCALAPPDATA ?? process.env.TEMP ?? '.', 'Microsoft', 'PowerToys', 'CommandPalette', 'JSExtensions');
// Mutable config — populated by _configureEnvironment()
const config = {
    extensionName: 'unknown',
    commandName: 'default',
    assetsPath: path.join(defaultBasePath, '_raycast-compat', 'assets'),
    supportPath: path.join(defaultBasePath, '_raycast-compat', 'data'),
    extensionDir: path.join(defaultBasePath, '_raycast-compat'),
    launchType: LaunchType.UserInitiated,
    launchContext: {},
};
/**
 * The environment object exposed to Raycast extensions.
 * Getters ensure values reflect any runtime reconfiguration.
 */
exports.environment = {
    get extensionName() { return config.extensionName; },
    get commandName() { return config.commandName; },
    get assetsPath() { return config.assetsPath; },
    get supportPath() { return config.supportPath; },
    get extensionDir() { return config.extensionDir; },
    /** Always false in CmdPal — extensions run in production mode. */
    get isDevelopment() { return false; },
    /** Raycast API version compatibility — use latest known. */
    get raycastVersion() { return '1.80.0'; },
    get launchType() { return config.launchType; },
    get launchContext() { return config.launchContext; },
    /**
     * Feature detection. Raycast uses this to check API capabilities.
     * We report true for the subset we support.
     */
    canAccess(api) {
        // For now, all shimmed APIs are "accessible"
        if (api === undefined || api === null)
            return false;
        return true;
    },
    /** Raycast's textSize preference (we default to medium). */
    get textSize() { return 'medium'; },
    /** Raycast's appearance (we follow system). */
    get appearance() { return 'light'; },
    /** Raycast's theme string. */
    get theme() { return 'raycast-default'; },
};
/**
 * Bootstrap call — the compat runtime sets actual values from
 * the CmdPal manifest and runtime context before the extension runs.
 */
function _configureEnvironment(overrides) {
    if (overrides.extensionName !== undefined)
        config.extensionName = overrides.extensionName;
    if (overrides.commandName !== undefined)
        config.commandName = overrides.commandName;
    if (overrides.assetsPath !== undefined)
        config.assetsPath = overrides.assetsPath;
    if (overrides.supportPath !== undefined)
        config.supportPath = overrides.supportPath;
    if (overrides.extensionDir !== undefined)
        config.extensionDir = overrides.extensionDir;
    if (overrides.launchType !== undefined)
        config.launchType = overrides.launchType;
    if (overrides.launchContext !== undefined)
        config.launchContext = overrides.launchContext;
}
//# sourceMappingURL=environment.js.map