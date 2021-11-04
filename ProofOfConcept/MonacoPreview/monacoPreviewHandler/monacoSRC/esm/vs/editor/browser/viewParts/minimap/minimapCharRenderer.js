/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { getCharIndex } from './minimapCharSheet.js';
import { toUint8 } from '../../../../base/common/uint.js';
export class MinimapCharRenderer {
    constructor(charData, scale) {
        this.scale = scale;
        this.charDataNormal = MinimapCharRenderer.soften(charData, 12 / 15);
        this.charDataLight = MinimapCharRenderer.soften(charData, 50 / 60);
    }
    static soften(input, ratio) {
        let result = new Uint8ClampedArray(input.length);
        for (let i = 0, len = input.length; i < len; i++) {
            result[i] = toUint8(input[i] * ratio);
        }
        return result;
    }
    renderChar(target, dx, dy, chCode, color, backgroundColor, fontScale, useLighterFont, force1pxHeight) {
        const charWidth = 1 /* BASE_CHAR_WIDTH */ * this.scale;
        const charHeight = 2 /* BASE_CHAR_HEIGHT */ * this.scale;
        const renderHeight = (force1pxHeight ? 1 : charHeight);
        if (dx + charWidth > target.width || dy + renderHeight > target.height) {
            console.warn('bad render request outside image data');
            return;
        }
        const charData = useLighterFont ? this.charDataLight : this.charDataNormal;
        const charIndex = getCharIndex(chCode, fontScale);
        const destWidth = target.width * 4 /* RGBA_CHANNELS_CNT */;
        const backgroundR = backgroundColor.r;
        const backgroundG = backgroundColor.g;
        const backgroundB = backgroundColor.b;
        const deltaR = color.r - backgroundR;
        const deltaG = color.g - backgroundG;
        const deltaB = color.b - backgroundB;
        const dest = target.data;
        let sourceOffset = charIndex * charWidth * charHeight;
        let row = dy * destWidth + dx * 4 /* RGBA_CHANNELS_CNT */;
        for (let y = 0; y < renderHeight; y++) {
            let column = row;
            for (let x = 0; x < charWidth; x++) {
                const c = charData[sourceOffset++] / 255;
                dest[column++] = backgroundR + deltaR * c;
                dest[column++] = backgroundG + deltaG * c;
                dest[column++] = backgroundB + deltaB * c;
                column++;
            }
            row += destWidth;
        }
    }
    blockRenderChar(target, dx, dy, color, backgroundColor, useLighterFont, force1pxHeight) {
        const charWidth = 1 /* BASE_CHAR_WIDTH */ * this.scale;
        const charHeight = 2 /* BASE_CHAR_HEIGHT */ * this.scale;
        const renderHeight = (force1pxHeight ? 1 : charHeight);
        if (dx + charWidth > target.width || dy + renderHeight > target.height) {
            console.warn('bad render request outside image data');
            return;
        }
        const destWidth = target.width * 4 /* RGBA_CHANNELS_CNT */;
        const c = 0.5;
        const backgroundR = backgroundColor.r;
        const backgroundG = backgroundColor.g;
        const backgroundB = backgroundColor.b;
        const deltaR = color.r - backgroundR;
        const deltaG = color.g - backgroundG;
        const deltaB = color.b - backgroundB;
        const colorR = backgroundR + deltaR * c;
        const colorG = backgroundG + deltaG * c;
        const colorB = backgroundB + deltaB * c;
        const dest = target.data;
        let row = dy * destWidth + dx * 4 /* RGBA_CHANNELS_CNT */;
        for (let y = 0; y < renderHeight; y++) {
            let column = row;
            for (let x = 0; x < charWidth; x++) {
                dest[column++] = colorR;
                dest[column++] = colorG;
                dest[column++] = colorB;
                column++;
            }
            row += destWidth;
        }
    }
}
