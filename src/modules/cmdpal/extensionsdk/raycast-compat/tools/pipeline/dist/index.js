"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
Object.defineProperty(exports, "__esModule", { value: true });
exports.getDefaultInstallDir = exports.uninstallExtension = exports.listInstalledExtensions = exports.installRaycastExtension = void 0;
/**
 * Public API barrel export for the pipeline library.
 */
var pipeline_1 = require("./pipeline");
Object.defineProperty(exports, "installRaycastExtension", { enumerable: true, get: function () { return pipeline_1.installRaycastExtension; } });
var manage_1 = require("./manage");
Object.defineProperty(exports, "listInstalledExtensions", { enumerable: true, get: function () { return manage_1.listInstalledExtensions; } });
Object.defineProperty(exports, "uninstallExtension", { enumerable: true, get: function () { return manage_1.uninstallExtension; } });
var stage_install_1 = require("./stage-install");
Object.defineProperty(exports, "getDefaultInstallDir", { enumerable: true, get: function () { return stage_install_1.getDefaultInstallDir; } });
//# sourceMappingURL=index.js.map