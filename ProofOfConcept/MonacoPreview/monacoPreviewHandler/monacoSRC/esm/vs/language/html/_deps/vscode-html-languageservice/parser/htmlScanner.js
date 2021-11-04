/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import * as nls from './../../../fillers/vscode-nls.js';
import { TokenType, ScannerState } from '../htmlLanguageTypes.js';
var localize = nls.loadMessageBundle();
var MultiLineStream = /** @class */ (function () {
    function MultiLineStream(source, position) {
        this.source = source;
        this.len = source.length;
        this.position = position;
    }
    MultiLineStream.prototype.eos = function () {
        return this.len <= this.position;
    };
    MultiLineStream.prototype.getSource = function () {
        return this.source;
    };
    MultiLineStream.prototype.pos = function () {
        return this.position;
    };
    MultiLineStream.prototype.goBackTo = function (pos) {
        this.position = pos;
    };
    MultiLineStream.prototype.goBack = function (n) {
        this.position -= n;
    };
    MultiLineStream.prototype.advance = function (n) {
        this.position += n;
    };
    MultiLineStream.prototype.goToEnd = function () {
        this.position = this.source.length;
    };
    MultiLineStream.prototype.nextChar = function () {
        return this.source.charCodeAt(this.position++) || 0;
    };
    MultiLineStream.prototype.peekChar = function (n) {
        if (n === void 0) { n = 0; }
        return this.source.charCodeAt(this.position + n) || 0;
    };
    MultiLineStream.prototype.advanceIfChar = function (ch) {
        if (ch === this.source.charCodeAt(this.position)) {
            this.position++;
            return true;
        }
        return false;
    };
    MultiLineStream.prototype.advanceIfChars = function (ch) {
        var i;
        if (this.position + ch.length > this.source.length) {
            return false;
        }
        for (i = 0; i < ch.length; i++) {
            if (this.source.charCodeAt(this.position + i) !== ch[i]) {
                return false;
            }
        }
        this.advance(i);
        return true;
    };
    MultiLineStream.prototype.advanceIfRegExp = function (regex) {
        var str = this.source.substr(this.position);
        var match = str.match(regex);
        if (match) {
            this.position = this.position + match.index + match[0].length;
            return match[0];
        }
        return '';
    };
    MultiLineStream.prototype.advanceUntilRegExp = function (regex) {
        var str = this.source.substr(this.position);
        var match = str.match(regex);
        if (match) {
            this.position = this.position + match.index;
            return match[0];
        }
        else {
            this.goToEnd();
        }
        return '';
    };
    MultiLineStream.prototype.advanceUntilChar = function (ch) {
        while (this.position < this.source.length) {
            if (this.source.charCodeAt(this.position) === ch) {
                return true;
            }
            this.advance(1);
        }
        return false;
    };
    MultiLineStream.prototype.advanceUntilChars = function (ch) {
        while (this.position + ch.length <= this.source.length) {
            var i = 0;
            for (; i < ch.length && this.source.charCodeAt(this.position + i) === ch[i]; i++) {
            }
            if (i === ch.length) {
                return true;
            }
            this.advance(1);
        }
        this.goToEnd();
        return false;
    };
    MultiLineStream.prototype.skipWhitespace = function () {
        var n = this.advanceWhileChar(function (ch) {
            return ch === _WSP || ch === _TAB || ch === _NWL || ch === _LFD || ch === _CAR;
        });
        return n > 0;
    };
    MultiLineStream.prototype.advanceWhileChar = function (condition) {
        var posNow = this.position;
        while (this.position < this.len && condition(this.source.charCodeAt(this.position))) {
            this.position++;
        }
        return this.position - posNow;
    };
    return MultiLineStream;
}());
var _BNG = '!'.charCodeAt(0);
var _MIN = '-'.charCodeAt(0);
var _LAN = '<'.charCodeAt(0);
var _RAN = '>'.charCodeAt(0);
var _FSL = '/'.charCodeAt(0);
var _EQS = '='.charCodeAt(0);
var _DQO = '"'.charCodeAt(0);
var _SQO = '\''.charCodeAt(0);
var _NWL = '\n'.charCodeAt(0);
var _CAR = '\r'.charCodeAt(0);
var _LFD = '\f'.charCodeAt(0);
var _WSP = ' '.charCodeAt(0);
var _TAB = '\t'.charCodeAt(0);
var htmlScriptContents = {
    'text/x-handlebars-template': true
};
export function createScanner(input, initialOffset, initialState, emitPseudoCloseTags) {
    if (initialOffset === void 0) { initialOffset = 0; }
    if (initialState === void 0) { initialState = ScannerState.WithinContent; }
    if (emitPseudoCloseTags === void 0) { emitPseudoCloseTags = false; }
    var stream = new MultiLineStream(input, initialOffset);
    var state = initialState;
    var tokenOffset = 0;
    var tokenType = TokenType.Unknown;
    var tokenError;
    var hasSpaceAfterTag;
    var lastTag;
    var lastAttributeName;
    var lastTypeValue;
    function nextElementName() {
        return stream.advanceIfRegExp(/^[_:\w][_:\w-.\d]*/).toLowerCase();
    }
    function nextAttributeName() {
        return stream.advanceIfRegExp(/^[^\s"'></=\x00-\x0F\x7F\x80-\x9F]*/).toLowerCase();
    }
    function finishToken(offset, type, errorMessage) {
        tokenType = type;
        tokenOffset = offset;
        tokenError = errorMessage;
        return type;
    }
    function scan() {
        var offset = stream.pos();
        var oldState = state;
        var token = internalScan();
        if (token !== TokenType.EOS && offset === stream.pos() && !(emitPseudoCloseTags && (token === TokenType.StartTagClose || token === TokenType.EndTagClose))) {
            console.log('Scanner.scan has not advanced at offset ' + offset + ', state before: ' + oldState + ' after: ' + state);
            stream.advance(1);
            return finishToken(offset, TokenType.Unknown);
        }
        return token;
    }
    function internalScan() {
        var offset = stream.pos();
        if (stream.eos()) {
            return finishToken(offset, TokenType.EOS);
        }
        var errorMessage;
        switch (state) {
            case ScannerState.WithinComment:
                if (stream.advanceIfChars([_MIN, _MIN, _RAN])) { // -->
                    state = ScannerState.WithinContent;
                    return finishToken(offset, TokenType.EndCommentTag);
                }
                stream.advanceUntilChars([_MIN, _MIN, _RAN]); // -->
                return finishToken(offset, TokenType.Comment);
            case ScannerState.WithinDoctype:
                if (stream.advanceIfChar(_RAN)) {
                    state = ScannerState.WithinContent;
                    return finishToken(offset, TokenType.EndDoctypeTag);
                }
                stream.advanceUntilChar(_RAN); // >
                return finishToken(offset, TokenType.Doctype);
            case ScannerState.WithinContent:
                if (stream.advanceIfChar(_LAN)) { // <
                    if (!stream.eos() && stream.peekChar() === _BNG) { // !
                        if (stream.advanceIfChars([_BNG, _MIN, _MIN])) { // <!--
                            state = ScannerState.WithinComment;
                            return finishToken(offset, TokenType.StartCommentTag);
                        }
                        if (stream.advanceIfRegExp(/^!doctype/i)) {
                            state = ScannerState.WithinDoctype;
                            return finishToken(offset, TokenType.StartDoctypeTag);
                        }
                    }
                    if (stream.advanceIfChar(_FSL)) { // /
                        state = ScannerState.AfterOpeningEndTag;
                        return finishToken(offset, TokenType.EndTagOpen);
                    }
                    state = ScannerState.AfterOpeningStartTag;
                    return finishToken(offset, TokenType.StartTagOpen);
                }
                stream.advanceUntilChar(_LAN);
                return finishToken(offset, TokenType.Content);
            case ScannerState.AfterOpeningEndTag:
                var tagName = nextElementName();
                if (tagName.length > 0) {
                    state = ScannerState.WithinEndTag;
                    return finishToken(offset, TokenType.EndTag);
                }
                if (stream.skipWhitespace()) { // white space is not valid here
                    return finishToken(offset, TokenType.Whitespace, localize('error.unexpectedWhitespace', 'Tag name must directly follow the open bracket.'));
                }
                state = ScannerState.WithinEndTag;
                stream.advanceUntilChar(_RAN);
                if (offset < stream.pos()) {
                    return finishToken(offset, TokenType.Unknown, localize('error.endTagNameExpected', 'End tag name expected.'));
                }
                return internalScan();
            case ScannerState.WithinEndTag:
                if (stream.skipWhitespace()) { // white space is valid here
                    return finishToken(offset, TokenType.Whitespace);
                }
                if (stream.advanceIfChar(_RAN)) { // >
                    state = ScannerState.WithinContent;
                    return finishToken(offset, TokenType.EndTagClose);
                }
                if (emitPseudoCloseTags && stream.peekChar() === _LAN) { // <
                    state = ScannerState.WithinContent;
                    return finishToken(offset, TokenType.EndTagClose, localize('error.closingBracketMissing', 'Closing bracket missing.'));
                }
                errorMessage = localize('error.closingBracketExpected', 'Closing bracket expected.');
                break;
            case ScannerState.AfterOpeningStartTag:
                lastTag = nextElementName();
                lastTypeValue = void 0;
                lastAttributeName = void 0;
                if (lastTag.length > 0) {
                    hasSpaceAfterTag = false;
                    state = ScannerState.WithinTag;
                    return finishToken(offset, TokenType.StartTag);
                }
                if (stream.skipWhitespace()) { // white space is not valid here
                    return finishToken(offset, TokenType.Whitespace, localize('error.unexpectedWhitespace', 'Tag name must directly follow the open bracket.'));
                }
                state = ScannerState.WithinTag;
                stream.advanceUntilChar(_RAN);
                if (offset < stream.pos()) {
                    return finishToken(offset, TokenType.Unknown, localize('error.startTagNameExpected', 'Start tag name expected.'));
                }
                return internalScan();
            case ScannerState.WithinTag:
                if (stream.skipWhitespace()) {
                    hasSpaceAfterTag = true; // remember that we have seen a whitespace
                    return finishToken(offset, TokenType.Whitespace);
                }
                if (hasSpaceAfterTag) {
                    lastAttributeName = nextAttributeName();
                    if (lastAttributeName.length > 0) {
                        state = ScannerState.AfterAttributeName;
                        hasSpaceAfterTag = false;
                        return finishToken(offset, TokenType.AttributeName);
                    }
                }
                if (stream.advanceIfChars([_FSL, _RAN])) { // />
                    state = ScannerState.WithinContent;
                    return finishToken(offset, TokenType.StartTagSelfClose);
                }
                if (stream.advanceIfChar(_RAN)) { // >
                    if (lastTag === 'script') {
                        if (lastTypeValue && htmlScriptContents[lastTypeValue]) {
                            // stay in html
                            state = ScannerState.WithinContent;
                        }
                        else {
                            state = ScannerState.WithinScriptContent;
                        }
                    }
                    else if (lastTag === 'style') {
                        state = ScannerState.WithinStyleContent;
                    }
                    else {
                        state = ScannerState.WithinContent;
                    }
                    return finishToken(offset, TokenType.StartTagClose);
                }
                if (emitPseudoCloseTags && stream.peekChar() === _LAN) { // <
                    state = ScannerState.WithinContent;
                    return finishToken(offset, TokenType.StartTagClose, localize('error.closingBracketMissing', 'Closing bracket missing.'));
                }
                stream.advance(1);
                return finishToken(offset, TokenType.Unknown, localize('error.unexpectedCharacterInTag', 'Unexpected character in tag.'));
            case ScannerState.AfterAttributeName:
                if (stream.skipWhitespace()) {
                    hasSpaceAfterTag = true;
                    return finishToken(offset, TokenType.Whitespace);
                }
                if (stream.advanceIfChar(_EQS)) {
                    state = ScannerState.BeforeAttributeValue;
                    return finishToken(offset, TokenType.DelimiterAssign);
                }
                state = ScannerState.WithinTag;
                return internalScan(); // no advance yet - jump to WithinTag
            case ScannerState.BeforeAttributeValue:
                if (stream.skipWhitespace()) {
                    return finishToken(offset, TokenType.Whitespace);
                }
                var attributeValue = stream.advanceIfRegExp(/^[^\s"'`=<>]+/);
                if (attributeValue.length > 0) {
                    if (stream.peekChar() === _RAN && stream.peekChar(-1) === _FSL) { // <foo bar=http://foo/>
                        stream.goBack(1);
                        attributeValue = attributeValue.substr(0, attributeValue.length - 1);
                    }
                    if (lastAttributeName === 'type') {
                        lastTypeValue = attributeValue;
                    }
                    state = ScannerState.WithinTag;
                    hasSpaceAfterTag = false;
                    return finishToken(offset, TokenType.AttributeValue);
                }
                var ch = stream.peekChar();
                if (ch === _SQO || ch === _DQO) {
                    stream.advance(1); // consume quote
                    if (stream.advanceUntilChar(ch)) {
                        stream.advance(1); // consume quote
                    }
                    if (lastAttributeName === 'type') {
                        lastTypeValue = stream.getSource().substring(offset + 1, stream.pos() - 1);
                    }
                    state = ScannerState.WithinTag;
                    hasSpaceAfterTag = false;
                    return finishToken(offset, TokenType.AttributeValue);
                }
                state = ScannerState.WithinTag;
                hasSpaceAfterTag = false;
                return internalScan(); // no advance yet - jump to WithinTag
            case ScannerState.WithinScriptContent:
                // see http://stackoverflow.com/questions/14574471/how-do-browsers-parse-a-script-tag-exactly
                var sciptState = 1;
                while (!stream.eos()) {
                    var match = stream.advanceIfRegExp(/<!--|-->|<\/?script\s*\/?>?/i);
                    if (match.length === 0) {
                        stream.goToEnd();
                        return finishToken(offset, TokenType.Script);
                    }
                    else if (match === '<!--') {
                        if (sciptState === 1) {
                            sciptState = 2;
                        }
                    }
                    else if (match === '-->') {
                        sciptState = 1;
                    }
                    else if (match[1] !== '/') { // <script
                        if (sciptState === 2) {
                            sciptState = 3;
                        }
                    }
                    else { // </script
                        if (sciptState === 3) {
                            sciptState = 2;
                        }
                        else {
                            stream.goBack(match.length); // to the beginning of the closing tag
                            break;
                        }
                    }
                }
                state = ScannerState.WithinContent;
                if (offset < stream.pos()) {
                    return finishToken(offset, TokenType.Script);
                }
                return internalScan(); // no advance yet - jump to content
            case ScannerState.WithinStyleContent:
                stream.advanceUntilRegExp(/<\/style/i);
                state = ScannerState.WithinContent;
                if (offset < stream.pos()) {
                    return finishToken(offset, TokenType.Styles);
                }
                return internalScan(); // no advance yet - jump to content
        }
        stream.advance(1);
        state = ScannerState.WithinContent;
        return finishToken(offset, TokenType.Unknown, errorMessage);
    }
    return {
        scan: scan,
        getTokenType: function () { return tokenType; },
        getTokenOffset: function () { return tokenOffset; },
        getTokenLength: function () { return stream.pos() - tokenOffset; },
        getTokenEnd: function () { return stream.pos(); },
        getTokenText: function () { return stream.getSource().substring(tokenOffset, stream.pos()); },
        getScannerState: function () { return state; },
        getTokenError: function () { return tokenError; }
    };
}
