/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
export const NO_KEY_MODS = { ctrlCmd: false, alt: false };
export var ItemActivation;
(function (ItemActivation) {
    ItemActivation[ItemActivation["NONE"] = 0] = "NONE";
    ItemActivation[ItemActivation["FIRST"] = 1] = "FIRST";
    ItemActivation[ItemActivation["SECOND"] = 2] = "SECOND";
    ItemActivation[ItemActivation["LAST"] = 3] = "LAST";
})(ItemActivation || (ItemActivation = {}));
//#endregion
