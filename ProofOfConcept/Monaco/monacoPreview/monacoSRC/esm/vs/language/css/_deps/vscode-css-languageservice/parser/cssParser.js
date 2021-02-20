/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
'use strict';
import { TokenType, Scanner } from './cssScanner.js';
import * as nodes from './cssNodes.js';
import { ParseError } from './cssErrors.js';
import * as languageFacts from '../languageFacts/facts.js';
import { isDefined } from '../utils/objects.js';
/// <summary>
/// A parser for the css core specification. See for reference:
/// https://www.w3.org/TR/CSS21/grammar.html
/// http://www.w3.org/TR/CSS21/syndata.html#tokenization
/// </summary>
var Parser = /** @class */ (function () {
    function Parser(scnr) {
        if (scnr === void 0) { scnr = new Scanner(); }
        this.keyframeRegex = /^@(\-(webkit|ms|moz|o)\-)?keyframes$/i;
        this.scanner = scnr;
        this.token = { type: TokenType.EOF, offset: -1, len: 0, text: '' };
        this.prevToken = undefined;
    }
    Parser.prototype.peekIdent = function (text) {
        return TokenType.Ident === this.token.type && text.length === this.token.text.length && text === this.token.text.toLowerCase();
    };
    Parser.prototype.peekKeyword = function (text) {
        return TokenType.AtKeyword === this.token.type && text.length === this.token.text.length && text === this.token.text.toLowerCase();
    };
    Parser.prototype.peekDelim = function (text) {
        return TokenType.Delim === this.token.type && text === this.token.text;
    };
    Parser.prototype.peek = function (type) {
        return type === this.token.type;
    };
    Parser.prototype.peekOne = function (types) {
        return types.indexOf(this.token.type) !== -1;
    };
    Parser.prototype.peekRegExp = function (type, regEx) {
        if (type !== this.token.type) {
            return false;
        }
        return regEx.test(this.token.text);
    };
    Parser.prototype.hasWhitespace = function () {
        return !!this.prevToken && (this.prevToken.offset + this.prevToken.len !== this.token.offset);
    };
    Parser.prototype.consumeToken = function () {
        this.prevToken = this.token;
        this.token = this.scanner.scan();
    };
    Parser.prototype.mark = function () {
        return {
            prev: this.prevToken,
            curr: this.token,
            pos: this.scanner.pos()
        };
    };
    Parser.prototype.restoreAtMark = function (mark) {
        this.prevToken = mark.prev;
        this.token = mark.curr;
        this.scanner.goBackTo(mark.pos);
    };
    Parser.prototype.try = function (func) {
        var pos = this.mark();
        var node = func();
        if (!node) {
            this.restoreAtMark(pos);
            return null;
        }
        return node;
    };
    Parser.prototype.acceptOneKeyword = function (keywords) {
        if (TokenType.AtKeyword === this.token.type) {
            for (var _i = 0, keywords_1 = keywords; _i < keywords_1.length; _i++) {
                var keyword = keywords_1[_i];
                if (keyword.length === this.token.text.length && keyword === this.token.text.toLowerCase()) {
                    this.consumeToken();
                    return true;
                }
            }
        }
        return false;
    };
    Parser.prototype.accept = function (type) {
        if (type === this.token.type) {
            this.consumeToken();
            return true;
        }
        return false;
    };
    Parser.prototype.acceptIdent = function (text) {
        if (this.peekIdent(text)) {
            this.consumeToken();
            return true;
        }
        return false;
    };
    Parser.prototype.acceptKeyword = function (text) {
        if (this.peekKeyword(text)) {
            this.consumeToken();
            return true;
        }
        return false;
    };
    Parser.prototype.acceptDelim = function (text) {
        if (this.peekDelim(text)) {
            this.consumeToken();
            return true;
        }
        return false;
    };
    Parser.prototype.acceptRegexp = function (regEx) {
        if (regEx.test(this.token.text)) {
            this.consumeToken();
            return true;
        }
        return false;
    };
    Parser.prototype._parseRegexp = function (regEx) {
        var node = this.createNode(nodes.NodeType.Identifier);
        do { } while (this.acceptRegexp(regEx));
        return this.finish(node);
    };
    Parser.prototype.acceptUnquotedString = function () {
        var pos = this.scanner.pos();
        this.scanner.goBackTo(this.token.offset);
        var unquoted = this.scanner.scanUnquotedString();
        if (unquoted) {
            this.token = unquoted;
            this.consumeToken();
            return true;
        }
        this.scanner.goBackTo(pos);
        return false;
    };
    Parser.prototype.resync = function (resyncTokens, resyncStopTokens) {
        while (true) {
            if (resyncTokens && resyncTokens.indexOf(this.token.type) !== -1) {
                this.consumeToken();
                return true;
            }
            else if (resyncStopTokens && resyncStopTokens.indexOf(this.token.type) !== -1) {
                return true;
            }
            else {
                if (this.token.type === TokenType.EOF) {
                    return false;
                }
                this.token = this.scanner.scan();
            }
        }
    };
    Parser.prototype.createNode = function (nodeType) {
        return new nodes.Node(this.token.offset, this.token.len, nodeType);
    };
    Parser.prototype.create = function (ctor) {
        return new ctor(this.token.offset, this.token.len);
    };
    Parser.prototype.finish = function (node, error, resyncTokens, resyncStopTokens) {
        // parseNumeric misuses error for boolean flagging (however the real error mustn't be a false)
        // + nodelist offsets mustn't be modified, because there is a offset hack in rulesets for smartselection
        if (!(node instanceof nodes.Nodelist)) {
            if (error) {
                this.markError(node, error, resyncTokens, resyncStopTokens);
            }
            // set the node end position
            if (this.prevToken) {
                // length with more elements belonging together
                var prevEnd = this.prevToken.offset + this.prevToken.len;
                node.length = prevEnd > node.offset ? prevEnd - node.offset : 0; // offset is taken from current token, end from previous: Use 0 for empty nodes
            }
        }
        return node;
    };
    Parser.prototype.markError = function (node, error, resyncTokens, resyncStopTokens) {
        if (this.token !== this.lastErrorToken) { // do not report twice on the same token
            node.addIssue(new nodes.Marker(node, error, nodes.Level.Error, undefined, this.token.offset, this.token.len));
            this.lastErrorToken = this.token;
        }
        if (resyncTokens || resyncStopTokens) {
            this.resync(resyncTokens, resyncStopTokens);
        }
    };
    Parser.prototype.parseStylesheet = function (textDocument) {
        var versionId = textDocument.version;
        var text = textDocument.getText();
        var textProvider = function (offset, length) {
            if (textDocument.version !== versionId) {
                throw new Error('Underlying model has changed, AST is no longer valid');
            }
            return text.substr(offset, length);
        };
        return this.internalParse(text, this._parseStylesheet, textProvider);
    };
    Parser.prototype.internalParse = function (input, parseFunc, textProvider) {
        this.scanner.setSource(input);
        this.token = this.scanner.scan();
        var node = parseFunc.bind(this)();
        if (node) {
            if (textProvider) {
                node.textProvider = textProvider;
            }
            else {
                node.textProvider = function (offset, length) { return input.substr(offset, length); };
            }
        }
        return node;
    };
    Parser.prototype._parseStylesheet = function () {
        var node = this.create(nodes.Stylesheet);
        while (node.addChild(this._parseStylesheetStart())) {
            // Parse statements only valid at the beginning of stylesheets.
        }
        var inRecovery = false;
        do {
            var hasMatch = false;
            do {
                hasMatch = false;
                var statement = this._parseStylesheetStatement();
                if (statement) {
                    node.addChild(statement);
                    hasMatch = true;
                    inRecovery = false;
                    if (!this.peek(TokenType.EOF) && this._needsSemicolonAfter(statement) && !this.accept(TokenType.SemiColon)) {
                        this.markError(node, ParseError.SemiColonExpected);
                    }
                }
                while (this.accept(TokenType.SemiColon) || this.accept(TokenType.CDO) || this.accept(TokenType.CDC)) {
                    // accept empty statements
                    hasMatch = true;
                    inRecovery = false;
                }
            } while (hasMatch);
            if (this.peek(TokenType.EOF)) {
                break;
            }
            if (!inRecovery) {
                if (this.peek(TokenType.AtKeyword)) {
                    this.markError(node, ParseError.UnknownAtRule);
                }
                else {
                    this.markError(node, ParseError.RuleOrSelectorExpected);
                }
                inRecovery = true;
            }
            this.consumeToken();
        } while (!this.peek(TokenType.EOF));
        return this.finish(node);
    };
    Parser.prototype._parseStylesheetStart = function () {
        return this._parseCharset();
    };
    Parser.prototype._parseStylesheetStatement = function (isNested) {
        if (isNested === void 0) { isNested = false; }
        if (this.peek(TokenType.AtKeyword)) {
            return this._parseStylesheetAtStatement(isNested);
        }
        return this._parseRuleset(isNested);
    };
    Parser.prototype._parseStylesheetAtStatement = function (isNested) {
        if (isNested === void 0) { isNested = false; }
        return this._parseImport()
            || this._parseMedia(isNested)
            || this._parsePage()
            || this._parseFontFace()
            || this._parseKeyframe()
            || this._parseSupports(isNested)
            || this._parseViewPort()
            || this._parseNamespace()
            || this._parseDocument()
            || this._parseUnknownAtRule();
    };
    Parser.prototype._tryParseRuleset = function (isNested) {
        var mark = this.mark();
        if (this._parseSelector(isNested)) {
            while (this.accept(TokenType.Comma) && this._parseSelector(isNested)) {
                // loop
            }
            if (this.accept(TokenType.CurlyL)) {
                this.restoreAtMark(mark);
                return this._parseRuleset(isNested);
            }
        }
        this.restoreAtMark(mark);
        return null;
    };
    Parser.prototype._parseRuleset = function (isNested) {
        if (isNested === void 0) { isNested = false; }
        var node = this.create(nodes.RuleSet);
        var selectors = node.getSelectors();
        if (!selectors.addChild(this._parseSelector(isNested))) {
            return null;
        }
        while (this.accept(TokenType.Comma)) {
            if (!selectors.addChild(this._parseSelector(isNested))) {
                return this.finish(node, ParseError.SelectorExpected);
            }
        }
        return this._parseBody(node, this._parseRuleSetDeclaration.bind(this));
    };
    Parser.prototype._parseRuleSetDeclarationAtStatement = function () {
        return this._parseAtApply()
            || this._parseUnknownAtRule();
    };
    Parser.prototype._parseRuleSetDeclaration = function () {
        // https://www.w3.org/TR/css-syntax-3/#consume-a-list-of-declarations0
        if (this.peek(TokenType.AtKeyword)) {
            return this._parseRuleSetDeclarationAtStatement();
        }
        return this._parseDeclaration();
    };
    /**
     * Parses declarations like:
     *   @apply --my-theme;
     *
     * Follows https://tabatkins.github.io/specs/css-apply-rule/#using
     */
    Parser.prototype._parseAtApply = function () {
        if (!this.peekKeyword('@apply')) {
            return null;
        }
        var node = this.create(nodes.AtApplyRule);
        this.consumeToken();
        if (!node.setIdentifier(this._parseIdent([nodes.ReferenceType.Variable]))) {
            return this.finish(node, ParseError.IdentifierExpected);
        }
        return this.finish(node);
    };
    Parser.prototype._needsSemicolonAfter = function (node) {
        switch (node.type) {
            case nodes.NodeType.Keyframe:
            case nodes.NodeType.ViewPort:
            case nodes.NodeType.Media:
            case nodes.NodeType.Ruleset:
            case nodes.NodeType.Namespace:
            case nodes.NodeType.If:
            case nodes.NodeType.For:
            case nodes.NodeType.Each:
            case nodes.NodeType.While:
            case nodes.NodeType.MixinDeclaration:
            case nodes.NodeType.FunctionDeclaration:
            case nodes.NodeType.MixinContentDeclaration:
                return false;
            case nodes.NodeType.ExtendsReference:
            case nodes.NodeType.MixinContentReference:
            case nodes.NodeType.ReturnStatement:
            case nodes.NodeType.MediaQuery:
            case nodes.NodeType.Debug:
            case nodes.NodeType.Import:
            case nodes.NodeType.AtApplyRule:
            case nodes.NodeType.CustomPropertyDeclaration:
                return true;
            case nodes.NodeType.VariableDeclaration:
                return node.needsSemicolon;
            case nodes.NodeType.MixinReference:
                return !node.getContent();
            case nodes.NodeType.Declaration:
                return !node.getNestedProperties();
        }
        return false;
    };
    Parser.prototype._parseDeclarations = function (parseDeclaration) {
        var node = this.create(nodes.Declarations);
        if (!this.accept(TokenType.CurlyL)) {
            return null;
        }
        var decl = parseDeclaration();
        while (node.addChild(decl)) {
            if (this.peek(TokenType.CurlyR)) {
                break;
            }
            if (this._needsSemicolonAfter(decl) && !this.accept(TokenType.SemiColon)) {
                return this.finish(node, ParseError.SemiColonExpected, [TokenType.SemiColon, TokenType.CurlyR]);
            }
            // We accepted semicolon token. Link it to declaration.
            if (decl && this.prevToken && this.prevToken.type === TokenType.SemiColon) {
                decl.semicolonPosition = this.prevToken.offset;
            }
            while (this.accept(TokenType.SemiColon)) {
                // accept empty statements
            }
            decl = parseDeclaration();
        }
        if (!this.accept(TokenType.CurlyR)) {
            return this.finish(node, ParseError.RightCurlyExpected, [TokenType.CurlyR, TokenType.SemiColon]);
        }
        return this.finish(node);
    };
    Parser.prototype._parseBody = function (node, parseDeclaration) {
        if (!node.setDeclarations(this._parseDeclarations(parseDeclaration))) {
            return this.finish(node, ParseError.LeftCurlyExpected, [TokenType.CurlyR, TokenType.SemiColon]);
        }
        return this.finish(node);
    };
    Parser.prototype._parseSelector = function (isNested) {
        var node = this.create(nodes.Selector);
        var hasContent = false;
        if (isNested) {
            // nested selectors can start with a combinator
            hasContent = node.addChild(this._parseCombinator());
        }
        while (node.addChild(this._parseSimpleSelector())) {
            hasContent = true;
            node.addChild(this._parseCombinator()); // optional
        }
        return hasContent ? this.finish(node) : null;
    };
    Parser.prototype._parseDeclaration = function (stopTokens) {
        var custonProperty = this._tryParseCustomPropertyDeclaration(stopTokens);
        if (custonProperty) {
            return custonProperty;
        }
        var node = this.create(nodes.Declaration);
        if (!node.setProperty(this._parseProperty())) {
            return null;
        }
        if (!this.accept(TokenType.Colon)) {
            return this.finish(node, ParseError.ColonExpected, [TokenType.Colon], stopTokens || [TokenType.SemiColon]);
        }
        if (this.prevToken) {
            node.colonPosition = this.prevToken.offset;
        }
        if (!node.setValue(this._parseExpr())) {
            return this.finish(node, ParseError.PropertyValueExpected);
        }
        node.addChild(this._parsePrio());
        if (this.peek(TokenType.SemiColon)) {
            node.semicolonPosition = this.token.offset; // not part of the declaration, but useful information for code assist
        }
        return this.finish(node);
    };
    Parser.prototype._tryParseCustomPropertyDeclaration = function (stopTokens) {
        if (!this.peekRegExp(TokenType.Ident, /^--/)) {
            return null;
        }
        var node = this.create(nodes.CustomPropertyDeclaration);
        if (!node.setProperty(this._parseProperty())) {
            return null;
        }
        if (!this.accept(TokenType.Colon)) {
            return this.finish(node, ParseError.ColonExpected, [TokenType.Colon]);
        }
        if (this.prevToken) {
            node.colonPosition = this.prevToken.offset;
        }
        var mark = this.mark();
        if (this.peek(TokenType.CurlyL)) {
            // try to parse it as nested declaration
            var propertySet = this.create(nodes.CustomPropertySet);
            var declarations = this._parseDeclarations(this._parseRuleSetDeclaration.bind(this));
            if (propertySet.setDeclarations(declarations) && !declarations.isErroneous(true)) {
                propertySet.addChild(this._parsePrio());
                if (this.peek(TokenType.SemiColon)) {
                    this.finish(propertySet);
                    node.setPropertySet(propertySet);
                    node.semicolonPosition = this.token.offset; // not part of the declaration, but useful information for code assist
                    return this.finish(node);
                }
            }
            this.restoreAtMark(mark);
        }
        // try tp parse as expression
        var expression = this._parseExpr();
        if (expression && !expression.isErroneous(true)) {
            this._parsePrio();
            if (this.peekOne(stopTokens || [TokenType.SemiColon])) {
                node.setValue(expression);
                node.semicolonPosition = this.token.offset; // not part of the declaration, but useful information for code assist
                return this.finish(node);
            }
        }
        this.restoreAtMark(mark);
        node.addChild(this._parseCustomPropertyValue(stopTokens));
        node.addChild(this._parsePrio());
        if (isDefined(node.colonPosition) && this.token.offset === node.colonPosition + 1) {
            return this.finish(node, ParseError.PropertyValueExpected);
        }
        return this.finish(node);
    };
    /**
     * Parse custom property values.
     *
     * Based on https://www.w3.org/TR/css-variables/#syntax
     *
     * This code is somewhat unusual, as the allowed syntax is incredibly broad,
     * parsing almost any sequence of tokens, save for a small set of exceptions.
     * Unbalanced delimitors, invalid tokens, and declaration
     * terminators like semicolons and !important directives (when not inside
     * of delimitors).
     */
    Parser.prototype._parseCustomPropertyValue = function (stopTokens) {
        var _this = this;
        if (stopTokens === void 0) { stopTokens = [TokenType.CurlyR]; }
        var node = this.create(nodes.Node);
        var isTopLevel = function () { return curlyDepth === 0 && parensDepth === 0 && bracketsDepth === 0; };
        var onStopToken = function () { return stopTokens.indexOf(_this.token.type) !== -1; };
        var curlyDepth = 0;
        var parensDepth = 0;
        var bracketsDepth = 0;
        done: while (true) {
            switch (this.token.type) {
                case TokenType.SemiColon:
                    // A semicolon only ends things if we're not inside a delimitor.
                    if (isTopLevel()) {
                        break done;
                    }
                    break;
                case TokenType.Exclamation:
                    // An exclamation ends the value if we're not inside delims.
                    if (isTopLevel()) {
                        break done;
                    }
                    break;
                case TokenType.CurlyL:
                    curlyDepth++;
                    break;
                case TokenType.CurlyR:
                    curlyDepth--;
                    if (curlyDepth < 0) {
                        // The property value has been terminated without a semicolon, and
                        // this is the last declaration in the ruleset.
                        if (onStopToken() && parensDepth === 0 && bracketsDepth === 0) {
                            break done;
                        }
                        return this.finish(node, ParseError.LeftCurlyExpected);
                    }
                    break;
                case TokenType.ParenthesisL:
                    parensDepth++;
                    break;
                case TokenType.ParenthesisR:
                    parensDepth--;
                    if (parensDepth < 0) {
                        if (onStopToken() && bracketsDepth === 0 && curlyDepth === 0) {
                            break done;
                        }
                        return this.finish(node, ParseError.LeftParenthesisExpected);
                    }
                    break;
                case TokenType.BracketL:
                    bracketsDepth++;
                    break;
                case TokenType.BracketR:
                    bracketsDepth--;
                    if (bracketsDepth < 0) {
                        return this.finish(node, ParseError.LeftSquareBracketExpected);
                    }
                    break;
                case TokenType.BadString: // fall through
                    break done;
                case TokenType.EOF:
                    // We shouldn't have reached the end of input, something is
                    // unterminated.
                    var error = ParseError.RightCurlyExpected;
                    if (bracketsDepth > 0) {
                        error = ParseError.RightSquareBracketExpected;
                    }
                    else if (parensDepth > 0) {
                        error = ParseError.RightParenthesisExpected;
                    }
                    return this.finish(node, error);
            }
            this.consumeToken();
        }
        return this.finish(node);
    };
    Parser.prototype._tryToParseDeclaration = function (stopTokens) {
        var mark = this.mark();
        if (this._parseProperty() && this.accept(TokenType.Colon)) {
            // looks like a declaration, go ahead
            this.restoreAtMark(mark);
            return this._parseDeclaration(stopTokens);
        }
        this.restoreAtMark(mark);
        return null;
    };
    Parser.prototype._parseProperty = function () {
        var node = this.create(nodes.Property);
        var mark = this.mark();
        if (this.acceptDelim('*') || this.acceptDelim('_')) {
            // support for  IE 5.x, 6 and 7 star hack: see http://en.wikipedia.org/wiki/CSS_filter#Star_hack
            if (this.hasWhitespace()) {
                this.restoreAtMark(mark);
                return null;
            }
        }
        if (node.setIdentifier(this._parsePropertyIdentifier())) {
            return this.finish(node);
        }
        return null;
    };
    Parser.prototype._parsePropertyIdentifier = function () {
        return this._parseIdent();
    };
    Parser.prototype._parseCharset = function () {
        if (!this.peek(TokenType.Charset)) {
            return null;
        }
        var node = this.create(nodes.Node);
        this.consumeToken(); // charset
        if (!this.accept(TokenType.String)) {
            return this.finish(node, ParseError.IdentifierExpected);
        }
        if (!this.accept(TokenType.SemiColon)) {
            return this.finish(node, ParseError.SemiColonExpected);
        }
        return this.finish(node);
    };
    Parser.prototype._parseImport = function () {
        if (!this.peekKeyword('@import')) {
            return null;
        }
        var node = this.create(nodes.Import);
        this.consumeToken(); // @import
        if (!node.addChild(this._parseURILiteral()) && !node.addChild(this._parseStringLiteral())) {
            return this.finish(node, ParseError.URIOrStringExpected);
        }
        if (!this.peek(TokenType.SemiColon) && !this.peek(TokenType.EOF)) {
            node.setMedialist(this._parseMediaQueryList());
        }
        return this.finish(node);
    };
    Parser.prototype._parseNamespace = function () {
        // http://www.w3.org/TR/css3-namespace/
        // namespace  : NAMESPACE_SYM S* [IDENT S*]? [STRING|URI] S* ';' S*
        if (!this.peekKeyword('@namespace')) {
            return null;
        }
        var node = this.create(nodes.Namespace);
        this.consumeToken(); // @namespace
        if (!node.addChild(this._parseURILiteral())) { // url literal also starts with ident
            node.addChild(this._parseIdent()); // optional prefix
            if (!node.addChild(this._parseURILiteral()) && !node.addChild(this._parseStringLiteral())) {
                return this.finish(node, ParseError.URIExpected, [TokenType.SemiColon]);
            }
        }
        if (!this.accept(TokenType.SemiColon)) {
            return this.finish(node, ParseError.SemiColonExpected);
        }
        return this.finish(node);
    };
    Parser.prototype._parseFontFace = function () {
        if (!this.peekKeyword('@font-face')) {
            return null;
        }
        var node = this.create(nodes.FontFace);
        this.consumeToken(); // @font-face
        return this._parseBody(node, this._parseRuleSetDeclaration.bind(this));
    };
    Parser.prototype._parseViewPort = function () {
        if (!this.peekKeyword('@-ms-viewport') &&
            !this.peekKeyword('@-o-viewport') &&
            !this.peekKeyword('@viewport')) {
            return null;
        }
        var node = this.create(nodes.ViewPort);
        this.consumeToken(); // @-ms-viewport
        return this._parseBody(node, this._parseRuleSetDeclaration.bind(this));
    };
    Parser.prototype._parseKeyframe = function () {
        if (!this.peekRegExp(TokenType.AtKeyword, this.keyframeRegex)) {
            return null;
        }
        var node = this.create(nodes.Keyframe);
        var atNode = this.create(nodes.Node);
        this.consumeToken(); // atkeyword
        node.setKeyword(this.finish(atNode));
        if (atNode.matches('@-ms-keyframes')) { // -ms-keyframes never existed
            this.markError(atNode, ParseError.UnknownKeyword);
        }
        if (!node.setIdentifier(this._parseKeyframeIdent())) {
            return this.finish(node, ParseError.IdentifierExpected, [TokenType.CurlyR]);
        }
        return this._parseBody(node, this._parseKeyframeSelector.bind(this));
    };
    Parser.prototype._parseKeyframeIdent = function () {
        return this._parseIdent([nodes.ReferenceType.Keyframe]);
    };
    Parser.prototype._parseKeyframeSelector = function () {
        var node = this.create(nodes.KeyframeSelector);
        if (!node.addChild(this._parseIdent()) && !this.accept(TokenType.Percentage)) {
            return null;
        }
        while (this.accept(TokenType.Comma)) {
            if (!node.addChild(this._parseIdent()) && !this.accept(TokenType.Percentage)) {
                return this.finish(node, ParseError.PercentageExpected);
            }
        }
        return this._parseBody(node, this._parseRuleSetDeclaration.bind(this));
    };
    Parser.prototype._tryParseKeyframeSelector = function () {
        var node = this.create(nodes.KeyframeSelector);
        var pos = this.mark();
        if (!node.addChild(this._parseIdent()) && !this.accept(TokenType.Percentage)) {
            return null;
        }
        while (this.accept(TokenType.Comma)) {
            if (!node.addChild(this._parseIdent()) && !this.accept(TokenType.Percentage)) {
                this.restoreAtMark(pos);
                return null;
            }
        }
        if (!this.peek(TokenType.CurlyL)) {
            this.restoreAtMark(pos);
            return null;
        }
        return this._parseBody(node, this._parseRuleSetDeclaration.bind(this));
    };
    Parser.prototype._parseSupports = function (isNested) {
        if (isNested === void 0) { isNested = false; }
        // SUPPORTS_SYM S* supports_condition '{' S* ruleset* '}' S*
        if (!this.peekKeyword('@supports')) {
            return null;
        }
        var node = this.create(nodes.Supports);
        this.consumeToken(); // @supports
        node.addChild(this._parseSupportsCondition());
        return this._parseBody(node, this._parseSupportsDeclaration.bind(this, isNested));
    };
    Parser.prototype._parseSupportsDeclaration = function (isNested) {
        if (isNested === void 0) { isNested = false; }
        if (isNested) {
            // if nested, the body can contain rulesets, but also declarations
            return this._tryParseRuleset(true)
                || this._tryToParseDeclaration()
                || this._parseStylesheetStatement(true);
        }
        return this._parseStylesheetStatement(false);
    };
    Parser.prototype._parseSupportsCondition = function () {
        // supports_condition : supports_negation | supports_conjunction | supports_disjunction | supports_condition_in_parens ;
        // supports_condition_in_parens: ( '(' S* supports_condition S* ')' ) | supports_declaration_condition | general_enclosed ;
        // supports_negation: NOT S+ supports_condition_in_parens ;
        // supports_conjunction: supports_condition_in_parens ( S+ AND S+ supports_condition_in_parens )+;
        // supports_disjunction: supports_condition_in_parens ( S+ OR S+ supports_condition_in_parens )+;
        // supports_declaration_condition: '(' S* declaration ')';
        // general_enclosed: ( FUNCTION | '(' ) ( any | unused )* ')' ;
        var node = this.create(nodes.SupportsCondition);
        if (this.acceptIdent('not')) {
            node.addChild(this._parseSupportsConditionInParens());
        }
        else {
            node.addChild(this._parseSupportsConditionInParens());
            if (this.peekRegExp(TokenType.Ident, /^(and|or)$/i)) {
                var text = this.token.text.toLowerCase();
                while (this.acceptIdent(text)) {
                    node.addChild(this._parseSupportsConditionInParens());
                }
            }
        }
        return this.finish(node);
    };
    Parser.prototype._parseSupportsConditionInParens = function () {
        var node = this.create(nodes.SupportsCondition);
        if (this.accept(TokenType.ParenthesisL)) {
            if (this.prevToken) {
                node.lParent = this.prevToken.offset;
            }
            if (!node.addChild(this._tryToParseDeclaration([TokenType.ParenthesisR]))) {
                if (!this._parseSupportsCondition()) {
                    return this.finish(node, ParseError.ConditionExpected);
                }
            }
            if (!this.accept(TokenType.ParenthesisR)) {
                return this.finish(node, ParseError.RightParenthesisExpected, [TokenType.ParenthesisR], []);
            }
            if (this.prevToken) {
                node.rParent = this.prevToken.offset;
            }
            return this.finish(node);
        }
        else if (this.peek(TokenType.Ident)) {
            var pos = this.mark();
            this.consumeToken();
            if (!this.hasWhitespace() && this.accept(TokenType.ParenthesisL)) {
                var openParentCount = 1;
                while (this.token.type !== TokenType.EOF && openParentCount !== 0) {
                    if (this.token.type === TokenType.ParenthesisL) {
                        openParentCount++;
                    }
                    else if (this.token.type === TokenType.ParenthesisR) {
                        openParentCount--;
                    }
                    this.consumeToken();
                }
                return this.finish(node);
            }
            else {
                this.restoreAtMark(pos);
            }
        }
        return this.finish(node, ParseError.LeftParenthesisExpected, [], [TokenType.ParenthesisL]);
    };
    Parser.prototype._parseMediaDeclaration = function (isNested) {
        if (isNested === void 0) { isNested = false; }
        if (isNested) {
            // if nested, the body can contain rulesets, but also declarations
            return this._tryParseRuleset(true)
                || this._tryToParseDeclaration()
                || this._parseStylesheetStatement(true);
        }
        return this._parseStylesheetStatement(false);
    };
    Parser.prototype._parseMedia = function (isNested) {
        if (isNested === void 0) { isNested = false; }
        // MEDIA_SYM S* media_query_list '{' S* ruleset* '}' S*
        // media_query_list : S* [media_query [ ',' S* media_query ]* ]?
        if (!this.peekKeyword('@media')) {
            return null;
        }
        var node = this.create(nodes.Media);
        this.consumeToken(); // @media
        if (!node.addChild(this._parseMediaQueryList())) {
            return this.finish(node, ParseError.MediaQueryExpected);
        }
        return this._parseBody(node, this._parseMediaDeclaration.bind(this, isNested));
    };
    Parser.prototype._parseMediaQueryList = function () {
        var node = this.create(nodes.Medialist);
        if (!node.addChild(this._parseMediaQuery([TokenType.CurlyL]))) {
            return this.finish(node, ParseError.MediaQueryExpected);
        }
        while (this.accept(TokenType.Comma)) {
            if (!node.addChild(this._parseMediaQuery([TokenType.CurlyL]))) {
                return this.finish(node, ParseError.MediaQueryExpected);
            }
        }
        return this.finish(node);
    };
    Parser.prototype._parseMediaQuery = function (resyncStopToken) {
        // http://www.w3.org/TR/css3-mediaqueries/
        // media_query : [ONLY | NOT]? S* IDENT S* [ AND S* expression ]* | expression [ AND S* expression ]*
        // expression : '(' S* IDENT S* [ ':' S* expr ]? ')' S*
        var node = this.create(nodes.MediaQuery);
        var parseExpression = true;
        var hasContent = false;
        if (!this.peek(TokenType.ParenthesisL)) {
            if (this.acceptIdent('only') || this.acceptIdent('not')) {
                // optional
            }
            if (!node.addChild(this._parseIdent())) {
                return null;
            }
            hasContent = true;
            parseExpression = this.acceptIdent('and');
        }
        while (parseExpression) {
            // Allow short-circuting for other language constructs.
            if (node.addChild(this._parseMediaContentStart())) {
                parseExpression = this.acceptIdent('and');
                continue;
            }
            if (!this.accept(TokenType.ParenthesisL)) {
                if (hasContent) {
                    return this.finish(node, ParseError.LeftParenthesisExpected, [], resyncStopToken);
                }
                return null;
            }
            if (!node.addChild(this._parseMediaFeatureName())) {
                return this.finish(node, ParseError.IdentifierExpected, [], resyncStopToken);
            }
            if (this.accept(TokenType.Colon)) {
                if (!node.addChild(this._parseExpr())) {
                    return this.finish(node, ParseError.TermExpected, [], resyncStopToken);
                }
            }
            if (!this.accept(TokenType.ParenthesisR)) {
                return this.finish(node, ParseError.RightParenthesisExpected, [], resyncStopToken);
            }
            parseExpression = this.acceptIdent('and');
        }
        return this.finish(node);
    };
    Parser.prototype._parseMediaContentStart = function () {
        return null;
    };
    Parser.prototype._parseMediaFeatureName = function () {
        return this._parseIdent();
    };
    Parser.prototype._parseMedium = function () {
        var node = this.create(nodes.Node);
        if (node.addChild(this._parseIdent())) {
            return this.finish(node);
        }
        else {
            return null;
        }
    };
    Parser.prototype._parsePageDeclaration = function () {
        return this._parsePageMarginBox() || this._parseRuleSetDeclaration();
    };
    Parser.prototype._parsePage = function () {
        // http://www.w3.org/TR/css3-page/
        // page_rule : PAGE_SYM S* page_selector_list '{' S* page_body '}' S*
        // page_body :  /* Can be empty */ declaration? [ ';' S* page_body ]? | page_margin_box page_body
        if (!this.peekKeyword('@page')) {
            return null;
        }
        var node = this.create(nodes.Page);
        this.consumeToken();
        if (node.addChild(this._parsePageSelector())) {
            while (this.accept(TokenType.Comma)) {
                if (!node.addChild(this._parsePageSelector())) {
                    return this.finish(node, ParseError.IdentifierExpected);
                }
            }
        }
        return this._parseBody(node, this._parsePageDeclaration.bind(this));
    };
    Parser.prototype._parsePageMarginBox = function () {
        // page_margin_box :  margin_sym S* '{' S* declaration? [ ';' S* declaration? ]* '}' S*
        if (!this.peek(TokenType.AtKeyword)) {
            return null;
        }
        var node = this.create(nodes.PageBoxMarginBox);
        if (!this.acceptOneKeyword(languageFacts.pageBoxDirectives)) {
            this.markError(node, ParseError.UnknownAtRule, [], [TokenType.CurlyL]);
        }
        return this._parseBody(node, this._parseRuleSetDeclaration.bind(this));
    };
    Parser.prototype._parsePageSelector = function () {
        // page_selector : pseudo_page+ | IDENT pseudo_page*
        // pseudo_page :  ':' [ "left" | "right" | "first" | "blank" ];
        if (!this.peek(TokenType.Ident) && !this.peek(TokenType.Colon)) {
            return null;
        }
        var node = this.create(nodes.Node);
        node.addChild(this._parseIdent()); // optional ident
        if (this.accept(TokenType.Colon)) {
            if (!node.addChild(this._parseIdent())) { // optional ident
                return this.finish(node, ParseError.IdentifierExpected);
            }
        }
        return this.finish(node);
    };
    Parser.prototype._parseDocument = function () {
        // -moz-document is experimental but has been pushed to css4
        if (!this.peekKeyword('@-moz-document')) {
            return null;
        }
        var node = this.create(nodes.Document);
        this.consumeToken(); // @-moz-document
        this.resync([], [TokenType.CurlyL]); // ignore all the rules
        return this._parseBody(node, this._parseStylesheetStatement.bind(this));
    };
    // https://www.w3.org/TR/css-syntax-3/#consume-an-at-rule
    Parser.prototype._parseUnknownAtRule = function () {
        if (!this.peek(TokenType.AtKeyword)) {
            return null;
        }
        var node = this.create(nodes.UnknownAtRule);
        node.addChild(this._parseUnknownAtRuleName());
        var isTopLevel = function () { return curlyDepth === 0 && parensDepth === 0 && bracketsDepth === 0; };
        var curlyLCount = 0;
        var curlyDepth = 0;
        var parensDepth = 0;
        var bracketsDepth = 0;
        done: while (true) {
            switch (this.token.type) {
                case TokenType.SemiColon:
                    if (isTopLevel()) {
                        break done;
                    }
                    break;
                case TokenType.EOF:
                    if (curlyDepth > 0) {
                        return this.finish(node, ParseError.RightCurlyExpected);
                    }
                    else if (bracketsDepth > 0) {
                        return this.finish(node, ParseError.RightSquareBracketExpected);
                    }
                    else if (parensDepth > 0) {
                        return this.finish(node, ParseError.RightParenthesisExpected);
                    }
                    else {
                        return this.finish(node);
                    }
                case TokenType.CurlyL:
                    curlyLCount++;
                    curlyDepth++;
                    break;
                case TokenType.CurlyR:
                    curlyDepth--;
                    // End of at-rule, consume CurlyR and return node
                    if (curlyLCount > 0 && curlyDepth === 0) {
                        this.consumeToken();
                        if (bracketsDepth > 0) {
                            return this.finish(node, ParseError.RightSquareBracketExpected);
                        }
                        else if (parensDepth > 0) {
                            return this.finish(node, ParseError.RightParenthesisExpected);
                        }
                        break done;
                    }
                    if (curlyDepth < 0) {
                        // The property value has been terminated without a semicolon, and
                        // this is the last declaration in the ruleset.
                        if (parensDepth === 0 && bracketsDepth === 0) {
                            break done;
                        }
                        return this.finish(node, ParseError.LeftCurlyExpected);
                    }
                    break;
                case TokenType.ParenthesisL:
                    parensDepth++;
                    break;
                case TokenType.ParenthesisR:
                    parensDepth--;
                    if (parensDepth < 0) {
                        return this.finish(node, ParseError.LeftParenthesisExpected);
                    }
                    break;
                case TokenType.BracketL:
                    bracketsDepth++;
                    break;
                case TokenType.BracketR:
                    bracketsDepth--;
                    if (bracketsDepth < 0) {
                        return this.finish(node, ParseError.LeftSquareBracketExpected);
                    }
                    break;
            }
            this.consumeToken();
        }
        return node;
    };
    Parser.prototype._parseUnknownAtRuleName = function () {
        var node = this.create(nodes.Node);
        if (this.accept(TokenType.AtKeyword)) {
            return this.finish(node);
        }
        return node;
    };
    Parser.prototype._parseOperator = function () {
        // these are operators for binary expressions
        if (this.peekDelim('/') ||
            this.peekDelim('*') ||
            this.peekDelim('+') ||
            this.peekDelim('-') ||
            this.peek(TokenType.Dashmatch) ||
            this.peek(TokenType.Includes) ||
            this.peek(TokenType.SubstringOperator) ||
            this.peek(TokenType.PrefixOperator) ||
            this.peek(TokenType.SuffixOperator) ||
            this.peekDelim('=')) { // doesn't stick to the standard here
            var node = this.createNode(nodes.NodeType.Operator);
            this.consumeToken();
            return this.finish(node);
        }
        else {
            return null;
        }
    };
    Parser.prototype._parseUnaryOperator = function () {
        if (!this.peekDelim('+') && !this.peekDelim('-')) {
            return null;
        }
        var node = this.create(nodes.Node);
        this.consumeToken();
        return this.finish(node);
    };
    Parser.prototype._parseCombinator = function () {
        if (this.peekDelim('>')) {
            var node = this.create(nodes.Node);
            this.consumeToken();
            var mark = this.mark();
            if (!this.hasWhitespace() && this.acceptDelim('>')) {
                if (!this.hasWhitespace() && this.acceptDelim('>')) {
                    node.type = nodes.NodeType.SelectorCombinatorShadowPiercingDescendant;
                    return this.finish(node);
                }
                this.restoreAtMark(mark);
            }
            node.type = nodes.NodeType.SelectorCombinatorParent;
            return this.finish(node);
        }
        else if (this.peekDelim('+')) {
            var node = this.create(nodes.Node);
            this.consumeToken();
            node.type = nodes.NodeType.SelectorCombinatorSibling;
            return this.finish(node);
        }
        else if (this.peekDelim('~')) {
            var node = this.create(nodes.Node);
            this.consumeToken();
            node.type = nodes.NodeType.SelectorCombinatorAllSiblings;
            return this.finish(node);
        }
        else if (this.peekDelim('/')) {
            var node = this.create(nodes.Node);
            this.consumeToken();
            var mark = this.mark();
            if (!this.hasWhitespace() && this.acceptIdent('deep') && !this.hasWhitespace() && this.acceptDelim('/')) {
                node.type = nodes.NodeType.SelectorCombinatorShadowPiercingDescendant;
                return this.finish(node);
            }
            this.restoreAtMark(mark);
        }
        return null;
    };
    Parser.prototype._parseSimpleSelector = function () {
        // simple_selector
        //  : element_name [ HASH | class | attrib | pseudo ]* | [ HASH | class | attrib | pseudo ]+ ;
        var node = this.create(nodes.SimpleSelector);
        var c = 0;
        if (node.addChild(this._parseElementName())) {
            c++;
        }
        while ((c === 0 || !this.hasWhitespace()) && node.addChild(this._parseSimpleSelectorBody())) {
            c++;
        }
        return c > 0 ? this.finish(node) : null;
    };
    Parser.prototype._parseSimpleSelectorBody = function () {
        return this._parsePseudo() || this._parseHash() || this._parseClass() || this._parseAttrib();
    };
    Parser.prototype._parseSelectorIdent = function () {
        return this._parseIdent();
    };
    Parser.prototype._parseHash = function () {
        if (!this.peek(TokenType.Hash) && !this.peekDelim('#')) {
            return null;
        }
        var node = this.createNode(nodes.NodeType.IdentifierSelector);
        if (this.acceptDelim('#')) {
            if (this.hasWhitespace() || !node.addChild(this._parseSelectorIdent())) {
                return this.finish(node, ParseError.IdentifierExpected);
            }
        }
        else {
            this.consumeToken(); // TokenType.Hash
        }
        return this.finish(node);
    };
    Parser.prototype._parseClass = function () {
        // class: '.' IDENT ;
        if (!this.peekDelim('.')) {
            return null;
        }
        var node = this.createNode(nodes.NodeType.ClassSelector);
        this.consumeToken(); // '.'
        if (this.hasWhitespace() || !node.addChild(this._parseSelectorIdent())) {
            return this.finish(node, ParseError.IdentifierExpected);
        }
        return this.finish(node);
    };
    Parser.prototype._parseElementName = function () {
        // element_name: (ns? '|')? IDENT | '*';
        var pos = this.mark();
        var node = this.createNode(nodes.NodeType.ElementNameSelector);
        node.addChild(this._parseNamespacePrefix());
        if (!node.addChild(this._parseSelectorIdent()) && !this.acceptDelim('*')) {
            this.restoreAtMark(pos);
            return null;
        }
        return this.finish(node);
    };
    Parser.prototype._parseNamespacePrefix = function () {
        var pos = this.mark();
        var node = this.createNode(nodes.NodeType.NamespacePrefix);
        if (!node.addChild(this._parseIdent()) && !this.acceptDelim('*')) {
            // ns is optional
        }
        if (!this.acceptDelim('|')) {
            this.restoreAtMark(pos);
            return null;
        }
        return this.finish(node);
    };
    Parser.prototype._parseAttrib = function () {
        // attrib : '[' S* IDENT S* [ [ '=' | INCLUDES | DASHMATCH ] S*   [ IDENT | STRING ] S* ]? ']'
        if (!this.peek(TokenType.BracketL)) {
            return null;
        }
        var node = this.create(nodes.AttributeSelector);
        this.consumeToken(); // BracketL
        // Optional attrib namespace
        node.setNamespacePrefix(this._parseNamespacePrefix());
        if (!node.setIdentifier(this._parseIdent())) {
            return this.finish(node, ParseError.IdentifierExpected);
        }
        if (node.setOperator(this._parseOperator())) {
            node.setValue(this._parseBinaryExpr());
            this.acceptIdent('i'); // case insensitive matching
        }
        if (!this.accept(TokenType.BracketR)) {
            return this.finish(node, ParseError.RightSquareBracketExpected);
        }
        return this.finish(node);
    };
    Parser.prototype._parsePseudo = function () {
        var _this = this;
        // pseudo: ':' [ IDENT | FUNCTION S* [IDENT S*]? ')' ]
        var node = this._tryParsePseudoIdentifier();
        if (node) {
            if (!this.hasWhitespace() && this.accept(TokenType.ParenthesisL)) {
                var tryAsSelector = function () {
                    var selectors = _this.create(nodes.Node);
                    if (!selectors.addChild(_this._parseSelector(false))) {
                        return null;
                    }
                    while (_this.accept(TokenType.Comma) && selectors.addChild(_this._parseSelector(false))) {
                        // loop
                    }
                    if (_this.peek(TokenType.ParenthesisR)) {
                        return _this.finish(selectors);
                    }
                    return null;
                };
                node.addChild(this.try(tryAsSelector) || this._parseBinaryExpr());
                if (!this.accept(TokenType.ParenthesisR)) {
                    return this.finish(node, ParseError.RightParenthesisExpected);
                }
            }
            return this.finish(node);
        }
        return null;
    };
    Parser.prototype._tryParsePseudoIdentifier = function () {
        if (!this.peek(TokenType.Colon)) {
            return null;
        }
        var pos = this.mark();
        var node = this.createNode(nodes.NodeType.PseudoSelector);
        this.consumeToken(); // Colon
        if (this.hasWhitespace()) {
            this.restoreAtMark(pos);
            return null;
        }
        // optional, support ::
        this.accept(TokenType.Colon);
        if (this.hasWhitespace() || !node.addChild(this._parseIdent())) {
            return this.finish(node, ParseError.IdentifierExpected);
        }
        return this.finish(node);
    };
    Parser.prototype._tryParsePrio = function () {
        var mark = this.mark();
        var prio = this._parsePrio();
        if (prio) {
            return prio;
        }
        this.restoreAtMark(mark);
        return null;
    };
    Parser.prototype._parsePrio = function () {
        if (!this.peek(TokenType.Exclamation)) {
            return null;
        }
        var node = this.createNode(nodes.NodeType.Prio);
        if (this.accept(TokenType.Exclamation) && this.acceptIdent('important')) {
            return this.finish(node);
        }
        return null;
    };
    Parser.prototype._parseExpr = function (stopOnComma) {
        if (stopOnComma === void 0) { stopOnComma = false; }
        var node = this.create(nodes.Expression);
        if (!node.addChild(this._parseBinaryExpr())) {
            return null;
        }
        while (true) {
            if (this.peek(TokenType.Comma)) { // optional
                if (stopOnComma) {
                    return this.finish(node);
                }
                this.consumeToken();
            }
            if (!node.addChild(this._parseBinaryExpr())) {
                break;
            }
        }
        return this.finish(node);
    };
    Parser.prototype._parseNamedLine = function () {
        // https://www.w3.org/TR/css-grid-1/#named-lines
        if (!this.peek(TokenType.BracketL)) {
            return null;
        }
        var node = this.createNode(nodes.NodeType.GridLine);
        this.consumeToken();
        while (node.addChild(this._parseIdent())) {
            // repeat
        }
        if (!this.accept(TokenType.BracketR)) {
            return this.finish(node, ParseError.RightSquareBracketExpected);
        }
        return this.finish(node);
    };
    Parser.prototype._parseBinaryExpr = function (preparsedLeft, preparsedOper) {
        var node = this.create(nodes.BinaryExpression);
        if (!node.setLeft((preparsedLeft || this._parseTerm()))) {
            return null;
        }
        if (!node.setOperator(preparsedOper || this._parseOperator())) {
            return this.finish(node);
        }
        if (!node.setRight(this._parseTerm())) {
            return this.finish(node, ParseError.TermExpected);
        }
        // things needed for multiple binary expressions
        node = this.finish(node);
        var operator = this._parseOperator();
        if (operator) {
            node = this._parseBinaryExpr(node, operator);
        }
        return this.finish(node);
    };
    Parser.prototype._parseTerm = function () {
        var node = this.create(nodes.Term);
        node.setOperator(this._parseUnaryOperator()); // optional
        if (node.setExpression(this._parseTermExpression())) {
            return this.finish(node);
        }
        return null;
    };
    Parser.prototype._parseTermExpression = function () {
        return this._parseURILiteral() || // url before function
            this._parseFunction() || // function before ident
            this._parseIdent() ||
            this._parseStringLiteral() ||
            this._parseNumeric() ||
            this._parseHexColor() ||
            this._parseOperation() ||
            this._parseNamedLine();
    };
    Parser.prototype._parseOperation = function () {
        if (!this.peek(TokenType.ParenthesisL)) {
            return null;
        }
        var node = this.create(nodes.Node);
        this.consumeToken(); // ParenthesisL
        node.addChild(this._parseExpr());
        if (!this.accept(TokenType.ParenthesisR)) {
            return this.finish(node, ParseError.RightParenthesisExpected);
        }
        return this.finish(node);
    };
    Parser.prototype._parseNumeric = function () {
        if (this.peek(TokenType.Num) ||
            this.peek(TokenType.Percentage) ||
            this.peek(TokenType.Resolution) ||
            this.peek(TokenType.Length) ||
            this.peek(TokenType.EMS) ||
            this.peek(TokenType.EXS) ||
            this.peek(TokenType.Angle) ||
            this.peek(TokenType.Time) ||
            this.peek(TokenType.Dimension) ||
            this.peek(TokenType.Freq)) {
            var node = this.create(nodes.NumericValue);
            this.consumeToken();
            return this.finish(node);
        }
        return null;
    };
    Parser.prototype._parseStringLiteral = function () {
        if (!this.peek(TokenType.String) && !this.peek(TokenType.BadString)) {
            return null;
        }
        var node = this.createNode(nodes.NodeType.StringLiteral);
        this.consumeToken();
        return this.finish(node);
    };
    Parser.prototype._parseURILiteral = function () {
        if (!this.peekRegExp(TokenType.Ident, /^url(-prefix)?$/i)) {
            return null;
        }
        var pos = this.mark();
        var node = this.createNode(nodes.NodeType.URILiteral);
        this.accept(TokenType.Ident);
        if (this.hasWhitespace() || !this.peek(TokenType.ParenthesisL)) {
            this.restoreAtMark(pos);
            return null;
        }
        this.scanner.inURL = true;
        this.consumeToken(); // consume ()
        node.addChild(this._parseURLArgument()); // argument is optional
        this.scanner.inURL = false;
        if (!this.accept(TokenType.ParenthesisR)) {
            return this.finish(node, ParseError.RightParenthesisExpected);
        }
        return this.finish(node);
    };
    Parser.prototype._parseURLArgument = function () {
        var node = this.create(nodes.Node);
        if (!this.accept(TokenType.String) && !this.accept(TokenType.BadString) && !this.acceptUnquotedString()) {
            return null;
        }
        return this.finish(node);
    };
    Parser.prototype._parseIdent = function (referenceTypes) {
        if (!this.peek(TokenType.Ident)) {
            return null;
        }
        var node = this.create(nodes.Identifier);
        if (referenceTypes) {
            node.referenceTypes = referenceTypes;
        }
        node.isCustomProperty = this.peekRegExp(TokenType.Ident, /^--/);
        this.consumeToken();
        return this.finish(node);
    };
    Parser.prototype._parseFunction = function () {
        var pos = this.mark();
        var node = this.create(nodes.Function);
        if (!node.setIdentifier(this._parseFunctionIdentifier())) {
            return null;
        }
        if (this.hasWhitespace() || !this.accept(TokenType.ParenthesisL)) {
            this.restoreAtMark(pos);
            return null;
        }
        if (node.getArguments().addChild(this._parseFunctionArgument())) {
            while (this.accept(TokenType.Comma)) {
                if (this.peek(TokenType.ParenthesisR)) {
                    break;
                }
                if (!node.getArguments().addChild(this._parseFunctionArgument())) {
                    this.markError(node, ParseError.ExpressionExpected);
                }
            }
        }
        if (!this.accept(TokenType.ParenthesisR)) {
            return this.finish(node, ParseError.RightParenthesisExpected);
        }
        return this.finish(node);
    };
    Parser.prototype._parseFunctionIdentifier = function () {
        if (!this.peek(TokenType.Ident)) {
            return null;
        }
        var node = this.create(nodes.Identifier);
        node.referenceTypes = [nodes.ReferenceType.Function];
        if (this.acceptIdent('progid')) {
            // support for IE7 specific filters: 'progid:DXImageTransform.Microsoft.MotionBlur(strength=13, direction=310)'
            if (this.accept(TokenType.Colon)) {
                while (this.accept(TokenType.Ident) && this.acceptDelim('.')) {
                    // loop
                }
            }
            return this.finish(node);
        }
        this.consumeToken();
        return this.finish(node);
    };
    Parser.prototype._parseFunctionArgument = function () {
        var node = this.create(nodes.FunctionArgument);
        if (node.setValue(this._parseExpr(true))) {
            return this.finish(node);
        }
        return null;
    };
    Parser.prototype._parseHexColor = function () {
        if (this.peekRegExp(TokenType.Hash, /^#([A-Fa-f0-9]{3}|[A-Fa-f0-9]{4}|[A-Fa-f0-9]{6}|[A-Fa-f0-9]{8})$/g)) {
            var node = this.create(nodes.HexColorValue);
            this.consumeToken();
            return this.finish(node);
        }
        else {
            return null;
        }
    };
    return Parser;
}());
export { Parser };
