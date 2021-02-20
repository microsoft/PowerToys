/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import * as nodes from '../parser/cssNodes.js';
import * as nls from './../../../fillers/vscode-nls.js';
var localize = nls.loadMessageBundle();
export var colorFunctions = [
    { func: 'rgb($red, $green, $blue)', desc: localize('css.builtin.rgb', 'Creates a Color from red, green, and blue values.') },
    { func: 'rgba($red, $green, $blue, $alpha)', desc: localize('css.builtin.rgba', 'Creates a Color from red, green, blue, and alpha values.') },
    { func: 'hsl($hue, $saturation, $lightness)', desc: localize('css.builtin.hsl', 'Creates a Color from hue, saturation, and lightness values.') },
    { func: 'hsla($hue, $saturation, $lightness, $alpha)', desc: localize('css.builtin.hsla', 'Creates a Color from hue, saturation, lightness, and alpha values.') }
];
export var colors = {
    aliceblue: '#f0f8ff',
    antiquewhite: '#faebd7',
    aqua: '#00ffff',
    aquamarine: '#7fffd4',
    azure: '#f0ffff',
    beige: '#f5f5dc',
    bisque: '#ffe4c4',
    black: '#000000',
    blanchedalmond: '#ffebcd',
    blue: '#0000ff',
    blueviolet: '#8a2be2',
    brown: '#a52a2a',
    burlywood: '#deb887',
    cadetblue: '#5f9ea0',
    chartreuse: '#7fff00',
    chocolate: '#d2691e',
    coral: '#ff7f50',
    cornflowerblue: '#6495ed',
    cornsilk: '#fff8dc',
    crimson: '#dc143c',
    cyan: '#00ffff',
    darkblue: '#00008b',
    darkcyan: '#008b8b',
    darkgoldenrod: '#b8860b',
    darkgray: '#a9a9a9',
    darkgrey: '#a9a9a9',
    darkgreen: '#006400',
    darkkhaki: '#bdb76b',
    darkmagenta: '#8b008b',
    darkolivegreen: '#556b2f',
    darkorange: '#ff8c00',
    darkorchid: '#9932cc',
    darkred: '#8b0000',
    darksalmon: '#e9967a',
    darkseagreen: '#8fbc8f',
    darkslateblue: '#483d8b',
    darkslategray: '#2f4f4f',
    darkslategrey: '#2f4f4f',
    darkturquoise: '#00ced1',
    darkviolet: '#9400d3',
    deeppink: '#ff1493',
    deepskyblue: '#00bfff',
    dimgray: '#696969',
    dimgrey: '#696969',
    dodgerblue: '#1e90ff',
    firebrick: '#b22222',
    floralwhite: '#fffaf0',
    forestgreen: '#228b22',
    fuchsia: '#ff00ff',
    gainsboro: '#dcdcdc',
    ghostwhite: '#f8f8ff',
    gold: '#ffd700',
    goldenrod: '#daa520',
    gray: '#808080',
    grey: '#808080',
    green: '#008000',
    greenyellow: '#adff2f',
    honeydew: '#f0fff0',
    hotpink: '#ff69b4',
    indianred: '#cd5c5c',
    indigo: '#4b0082',
    ivory: '#fffff0',
    khaki: '#f0e68c',
    lavender: '#e6e6fa',
    lavenderblush: '#fff0f5',
    lawngreen: '#7cfc00',
    lemonchiffon: '#fffacd',
    lightblue: '#add8e6',
    lightcoral: '#f08080',
    lightcyan: '#e0ffff',
    lightgoldenrodyellow: '#fafad2',
    lightgray: '#d3d3d3',
    lightgrey: '#d3d3d3',
    lightgreen: '#90ee90',
    lightpink: '#ffb6c1',
    lightsalmon: '#ffa07a',
    lightseagreen: '#20b2aa',
    lightskyblue: '#87cefa',
    lightslategray: '#778899',
    lightslategrey: '#778899',
    lightsteelblue: '#b0c4de',
    lightyellow: '#ffffe0',
    lime: '#00ff00',
    limegreen: '#32cd32',
    linen: '#faf0e6',
    magenta: '#ff00ff',
    maroon: '#800000',
    mediumaquamarine: '#66cdaa',
    mediumblue: '#0000cd',
    mediumorchid: '#ba55d3',
    mediumpurple: '#9370d8',
    mediumseagreen: '#3cb371',
    mediumslateblue: '#7b68ee',
    mediumspringgreen: '#00fa9a',
    mediumturquoise: '#48d1cc',
    mediumvioletred: '#c71585',
    midnightblue: '#191970',
    mintcream: '#f5fffa',
    mistyrose: '#ffe4e1',
    moccasin: '#ffe4b5',
    navajowhite: '#ffdead',
    navy: '#000080',
    oldlace: '#fdf5e6',
    olive: '#808000',
    olivedrab: '#6b8e23',
    orange: '#ffa500',
    orangered: '#ff4500',
    orchid: '#da70d6',
    palegoldenrod: '#eee8aa',
    palegreen: '#98fb98',
    paleturquoise: '#afeeee',
    palevioletred: '#d87093',
    papayawhip: '#ffefd5',
    peachpuff: '#ffdab9',
    peru: '#cd853f',
    pink: '#ffc0cb',
    plum: '#dda0dd',
    powderblue: '#b0e0e6',
    purple: '#800080',
    red: '#ff0000',
    rebeccapurple: '#663399',
    rosybrown: '#bc8f8f',
    royalblue: '#4169e1',
    saddlebrown: '#8b4513',
    salmon: '#fa8072',
    sandybrown: '#f4a460',
    seagreen: '#2e8b57',
    seashell: '#fff5ee',
    sienna: '#a0522d',
    silver: '#c0c0c0',
    skyblue: '#87ceeb',
    slateblue: '#6a5acd',
    slategray: '#708090',
    slategrey: '#708090',
    snow: '#fffafa',
    springgreen: '#00ff7f',
    steelblue: '#4682b4',
    tan: '#d2b48c',
    teal: '#008080',
    thistle: '#d8bfd8',
    tomato: '#ff6347',
    turquoise: '#40e0d0',
    violet: '#ee82ee',
    wheat: '#f5deb3',
    white: '#ffffff',
    whitesmoke: '#f5f5f5',
    yellow: '#ffff00',
    yellowgreen: '#9acd32'
};
export var colorKeywords = {
    'currentColor': 'The value of the \'color\' property. The computed value of the \'currentColor\' keyword is the computed value of the \'color\' property. If the \'currentColor\' keyword is set on the \'color\' property itself, it is treated as \'color:inherit\' at parse time.',
    'transparent': 'Fully transparent. This keyword can be considered a shorthand for rgba(0,0,0,0) which is its computed value.',
};
function getNumericValue(node, factor) {
    var val = node.getText();
    var m = val.match(/^([-+]?[0-9]*\.?[0-9]+)(%?)$/);
    if (m) {
        if (m[2]) {
            factor = 100.0;
        }
        var result = parseFloat(m[1]) / factor;
        if (result >= 0 && result <= 1) {
            return result;
        }
    }
    throw new Error();
}
function getAngle(node) {
    var val = node.getText();
    var m = val.match(/^([-+]?[0-9]*\.?[0-9]+)(deg)?$/);
    if (m) {
        return parseFloat(val) % 360;
    }
    throw new Error();
}
export function isColorConstructor(node) {
    var name = node.getName();
    if (!name) {
        return false;
    }
    return /^(rgb|rgba|hsl|hsla)$/gi.test(name);
}
/**
 * Returns true if the node is a color value - either
 * defined a hex number, as rgb or rgba function, or
 * as color name.
 */
export function isColorValue(node) {
    if (node.type === nodes.NodeType.HexColorValue) {
        return true;
    }
    else if (node.type === nodes.NodeType.Function) {
        return isColorConstructor(node);
    }
    else if (node.type === nodes.NodeType.Identifier) {
        if (node.parent && node.parent.type !== nodes.NodeType.Term) {
            return false;
        }
        var candidateColor = node.getText().toLowerCase();
        if (candidateColor === 'none') {
            return false;
        }
        if (colors[candidateColor]) {
            return true;
        }
    }
    return false;
}
var Digit0 = 48;
var Digit9 = 57;
var A = 65;
var F = 70;
var a = 97;
var f = 102;
export function hexDigit(charCode) {
    if (charCode < Digit0) {
        return 0;
    }
    if (charCode <= Digit9) {
        return charCode - Digit0;
    }
    if (charCode < a) {
        charCode += (a - A);
    }
    if (charCode >= a && charCode <= f) {
        return charCode - a + 10;
    }
    return 0;
}
export function colorFromHex(text) {
    if (text[0] !== '#') {
        return null;
    }
    switch (text.length) {
        case 4:
            return {
                red: (hexDigit(text.charCodeAt(1)) * 0x11) / 255.0,
                green: (hexDigit(text.charCodeAt(2)) * 0x11) / 255.0,
                blue: (hexDigit(text.charCodeAt(3)) * 0x11) / 255.0,
                alpha: 1
            };
        case 5:
            return {
                red: (hexDigit(text.charCodeAt(1)) * 0x11) / 255.0,
                green: (hexDigit(text.charCodeAt(2)) * 0x11) / 255.0,
                blue: (hexDigit(text.charCodeAt(3)) * 0x11) / 255.0,
                alpha: (hexDigit(text.charCodeAt(4)) * 0x11) / 255.0,
            };
        case 7:
            return {
                red: (hexDigit(text.charCodeAt(1)) * 0x10 + hexDigit(text.charCodeAt(2))) / 255.0,
                green: (hexDigit(text.charCodeAt(3)) * 0x10 + hexDigit(text.charCodeAt(4))) / 255.0,
                blue: (hexDigit(text.charCodeAt(5)) * 0x10 + hexDigit(text.charCodeAt(6))) / 255.0,
                alpha: 1
            };
        case 9:
            return {
                red: (hexDigit(text.charCodeAt(1)) * 0x10 + hexDigit(text.charCodeAt(2))) / 255.0,
                green: (hexDigit(text.charCodeAt(3)) * 0x10 + hexDigit(text.charCodeAt(4))) / 255.0,
                blue: (hexDigit(text.charCodeAt(5)) * 0x10 + hexDigit(text.charCodeAt(6))) / 255.0,
                alpha: (hexDigit(text.charCodeAt(7)) * 0x10 + hexDigit(text.charCodeAt(8))) / 255.0
            };
    }
    return null;
}
export function colorFrom256RGB(red, green, blue, alpha) {
    if (alpha === void 0) { alpha = 1.0; }
    return {
        red: red / 255.0,
        green: green / 255.0,
        blue: blue / 255.0,
        alpha: alpha
    };
}
export function colorFromHSL(hue, sat, light, alpha) {
    if (alpha === void 0) { alpha = 1.0; }
    hue = hue / 60.0;
    if (sat === 0) {
        return { red: light, green: light, blue: light, alpha: alpha };
    }
    else {
        var hueToRgb = function (t1, t2, hue) {
            while (hue < 0) {
                hue += 6;
            }
            while (hue >= 6) {
                hue -= 6;
            }
            if (hue < 1) {
                return (t2 - t1) * hue + t1;
            }
            if (hue < 3) {
                return t2;
            }
            if (hue < 4) {
                return (t2 - t1) * (4 - hue) + t1;
            }
            return t1;
        };
        var t2 = light <= 0.5 ? (light * (sat + 1)) : (light + sat - (light * sat));
        var t1 = light * 2 - t2;
        return { red: hueToRgb(t1, t2, hue + 2), green: hueToRgb(t1, t2, hue), blue: hueToRgb(t1, t2, hue - 2), alpha: alpha };
    }
}
export function hslFromColor(rgba) {
    var r = rgba.red;
    var g = rgba.green;
    var b = rgba.blue;
    var a = rgba.alpha;
    var max = Math.max(r, g, b);
    var min = Math.min(r, g, b);
    var h = 0;
    var s = 0;
    var l = (min + max) / 2;
    var chroma = max - min;
    if (chroma > 0) {
        s = Math.min((l <= 0.5 ? chroma / (2 * l) : chroma / (2 - (2 * l))), 1);
        switch (max) {
            case r:
                h = (g - b) / chroma + (g < b ? 6 : 0);
                break;
            case g:
                h = (b - r) / chroma + 2;
                break;
            case b:
                h = (r - g) / chroma + 4;
                break;
        }
        h *= 60;
        h = Math.round(h);
    }
    return { h: h, s: s, l: l, a: a };
}
export function getColorValue(node) {
    if (node.type === nodes.NodeType.HexColorValue) {
        var text = node.getText();
        return colorFromHex(text);
    }
    else if (node.type === nodes.NodeType.Function) {
        var functionNode = node;
        var name = functionNode.getName();
        var colorValues = functionNode.getArguments().getChildren();
        if (!name || colorValues.length < 3 || colorValues.length > 4) {
            return null;
        }
        try {
            var alpha = colorValues.length === 4 ? getNumericValue(colorValues[3], 1) : 1;
            if (name === 'rgb' || name === 'rgba') {
                return {
                    red: getNumericValue(colorValues[0], 255.0),
                    green: getNumericValue(colorValues[1], 255.0),
                    blue: getNumericValue(colorValues[2], 255.0),
                    alpha: alpha
                };
            }
            else if (name === 'hsl' || name === 'hsla') {
                var h = getAngle(colorValues[0]);
                var s = getNumericValue(colorValues[1], 100.0);
                var l = getNumericValue(colorValues[2], 100.0);
                return colorFromHSL(h, s, l, alpha);
            }
        }
        catch (e) {
            // parse error on numeric value
            return null;
        }
    }
    else if (node.type === nodes.NodeType.Identifier) {
        if (node.parent && node.parent.type !== nodes.NodeType.Term) {
            return null;
        }
        var term = node.parent;
        if (term && term.parent && term.parent.type === nodes.NodeType.BinaryExpression) {
            var expression = term.parent;
            if (expression.parent && expression.parent.type === nodes.NodeType.ListEntry && expression.parent.key === expression) {
                return null;
            }
        }
        var candidateColor = node.getText().toLowerCase();
        if (candidateColor === 'none') {
            return null;
        }
        var colorHex = colors[candidateColor];
        if (colorHex) {
            return colorFromHex(colorHex);
        }
    }
    return null;
}
