/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { CancellationToken } from '../../../base/common/cancellation.js';
import { illegalArgument } from '../../../base/common/errors.js';
import { URI } from '../../../base/common/uri.js';
import { Range } from '../../common/core/range.js';
import { ColorProviderRegistry } from '../../common/modes.js';
import { IModelService } from '../../common/services/modelService.js';
import { CommandsRegistry } from '../../../platform/commands/common/commands.js';
export function getColors(model, token) {
    const colors = [];
    const providers = ColorProviderRegistry.ordered(model).reverse();
    const promises = providers.map(provider => Promise.resolve(provider.provideDocumentColors(model, token)).then(result => {
        if (Array.isArray(result)) {
            for (let colorInfo of result) {
                colors.push({ colorInfo, provider });
            }
        }
    }));
    return Promise.all(promises).then(() => colors);
}
export function getColorPresentations(model, colorInfo, provider, token) {
    return Promise.resolve(provider.provideColorPresentations(model, colorInfo, token));
}
CommandsRegistry.registerCommand('_executeDocumentColorProvider', function (accessor, ...args) {
    const [resource] = args;
    if (!(resource instanceof URI)) {
        throw illegalArgument();
    }
    const model = accessor.get(IModelService).getModel(resource);
    if (!model) {
        throw illegalArgument();
    }
    const rawCIs = [];
    const providers = ColorProviderRegistry.ordered(model).reverse();
    const promises = providers.map(provider => Promise.resolve(provider.provideDocumentColors(model, CancellationToken.None)).then(result => {
        if (Array.isArray(result)) {
            for (let ci of result) {
                rawCIs.push({ range: ci.range, color: [ci.color.red, ci.color.green, ci.color.blue, ci.color.alpha] });
            }
        }
    }));
    return Promise.all(promises).then(() => rawCIs);
});
CommandsRegistry.registerCommand('_executeColorPresentationProvider', function (accessor, ...args) {
    const [color, context] = args;
    const { uri, range } = context;
    if (!(uri instanceof URI) || !Array.isArray(color) || color.length !== 4 || !Range.isIRange(range)) {
        throw illegalArgument();
    }
    const [red, green, blue, alpha] = color;
    const model = accessor.get(IModelService).getModel(uri);
    if (!model) {
        throw illegalArgument();
    }
    const colorInfo = {
        range,
        color: { red, green, blue, alpha }
    };
    const presentations = [];
    const providers = ColorProviderRegistry.ordered(model).reverse();
    const promises = providers.map(provider => Promise.resolve(provider.provideColorPresentations(model, colorInfo, CancellationToken.None)).then(result => {
        if (Array.isArray(result)) {
            presentations.push(...result);
        }
    }));
    return Promise.all(promises).then(() => presentations);
});
