/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
export const allCharCodes = (() => {
    const v = [];
    for (let i = 32 /* START_CH_CODE */; i <= 126 /* END_CH_CODE */; i++) {
        v.push(i);
    }
    v.push(65533 /* UNKNOWN_CODE */);
    return v;
})();
export const getCharIndex = (chCode, fontScale) => {
    chCode -= 32 /* START_CH_CODE */;
    if (chCode < 0 || chCode > 96 /* CHAR_COUNT */) {
        if (fontScale <= 2) {
            // for smaller scales, we can get away with using any ASCII character...
            return (chCode + 96 /* CHAR_COUNT */) % 96 /* CHAR_COUNT */;
        }
        return 96 /* CHAR_COUNT */ - 1; // unknown symbol
    }
    return chCode;
};
