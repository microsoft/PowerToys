/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { SyncDescriptor } from './descriptors.js';
const _registry = [];
export function registerSingleton(id, ctor, supportsDelayedInstantiation) {
    _registry.push([id, new SyncDescriptor(ctor, [], supportsDelayedInstantiation)]);
}
export function getSingletonServiceDescriptors() {
    return _registry;
}
