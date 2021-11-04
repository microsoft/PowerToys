/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
export class FrankensteinMode {
    constructor(languageIdentifier) {
        this._languageIdentifier = languageIdentifier;
    }
    getId() {
        return this._languageIdentifier.language;
    }
}
