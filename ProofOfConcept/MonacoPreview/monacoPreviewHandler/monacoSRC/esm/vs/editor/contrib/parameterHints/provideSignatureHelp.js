/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    function adopt(value) { return value instanceof P ? value : new P(function (resolve) { resolve(value); }); }
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : adopt(result.value).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};
import { onUnexpectedExternalError } from '../../../base/common/errors.js';
import { Position } from '../../common/core/position.js';
import * as modes from '../../common/modes.js';
import { RawContextKey } from '../../../platform/contextkey/common/contextkey.js';
import { CancellationToken } from '../../../base/common/cancellation.js';
import { CommandsRegistry } from '../../../platform/commands/common/commands.js';
import { URI } from '../../../base/common/uri.js';
import { assertType } from '../../../base/common/types.js';
import { ITextModelService } from '../../common/services/resolverService.js';
export const Context = {
    Visible: new RawContextKey('parameterHintsVisible', false),
    MultipleSignatures: new RawContextKey('parameterHintsMultipleSignatures', false),
};
export function provideSignatureHelp(model, position, context, token) {
    return __awaiter(this, void 0, void 0, function* () {
        const supports = modes.SignatureHelpProviderRegistry.ordered(model);
        for (const support of supports) {
            try {
                const result = yield support.provideSignatureHelp(model, position, token, context);
                if (result) {
                    return result;
                }
            }
            catch (err) {
                onUnexpectedExternalError(err);
            }
        }
        return undefined;
    });
}
CommandsRegistry.registerCommand('_executeSignatureHelpProvider', (accessor, ...args) => __awaiter(void 0, void 0, void 0, function* () {
    const [uri, position, triggerCharacter] = args;
    assertType(URI.isUri(uri));
    assertType(Position.isIPosition(position));
    assertType(typeof triggerCharacter === 'string' || !triggerCharacter);
    const ref = yield accessor.get(ITextModelService).createModelReference(uri);
    try {
        const result = yield provideSignatureHelp(ref.object.textEditorModel, Position.lift(position), {
            triggerKind: modes.SignatureHelpTriggerKind.Invoke,
            isRetrigger: false,
            triggerCharacter,
        }, CancellationToken.None);
        if (!result) {
            return undefined;
        }
        setTimeout(() => result.dispose(), 0);
        return result.value;
    }
    finally {
        ref.dispose();
    }
}));
