/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
'use strict';
var __extends = (this && this.__extends) || (function () {
    var extendStatics = function (d, b) {
        extendStatics = Object.setPrototypeOf ||
            ({ __proto__: [] } instanceof Array && function (d, b) { d.__proto__ = b; }) ||
            function (d, b) { for (var p in b) if (Object.prototype.hasOwnProperty.call(b, p)) d[p] = b[p]; };
        return extendStatics(d, b);
    };
    return function (d, b) {
        extendStatics(d, b);
        function __() { this.constructor = d; }
        d.prototype = b === null ? Object.create(b) : (__.prototype = b.prototype, new __());
    };
})();
import * as lessScanner from './lessScanner.js';
import { TokenType } from './cssScanner.js';
import * as cssParser from './cssParser.js';
import * as nodes from './cssNodes.js';
import { ParseError } from './cssErrors.js';
/// <summary>
/// A parser for LESS
/// http://lesscss.org/
/// </summary>
var LESSParser = /** @class */ (function (_super) {
    __extends(LESSParser, _super);
    function LESSParser() {
        return _super.call(this, new lessScanner.LESSScanner()) || this;
    }
    LESSParser.prototype._parseStylesheetStatement = function (isNested) {
        if (isNested === void 0) { isNested = false; }
        if (this.peek(TokenType.AtKeyword)) {
            return this._parseVariableDeclaration()
                || this._parsePlugin()
                || _super.prototype._parseStylesheetAtStatement.call(this, isNested);
        }
        return this._tryParseMixinDeclaration()
            || this._tryParseMixinReference()
            || this._parseFunction()
            || this._parseRuleset(true);
    };
    LESSParser.prototype._parseImport = function () {
        if (!this.peekKeyword('@import') && !this.peekKeyword('@import-once') /* deprecated in less 1.4.1 */) {
            return null;
        }
        var node = this.create(nodes.Import);
        this.consumeToken();
        // less 1.4.1: @import (css) "lib"
        if (this.accept(TokenType.ParenthesisL)) {
            if (!this.accept(TokenType.Ident)) {
                return this.finish(node, ParseError.IdentifierExpected, [TokenType.SemiColon]);
            }
            do {
                if (!this.accept(TokenType.Comma)) {
                    break;
                }
            } while (this.accept(TokenType.Ident));
            if (!this.accept(TokenType.ParenthesisR)) {
                return this.finish(node, ParseError.RightParenthesisExpected, [TokenType.SemiColon]);
            }
        }
        if (!node.addChild(this._parseURILiteral()) && !node.addChild(this._parseStringLiteral())) {
            return this.finish(node, ParseError.URIOrStringExpected, [TokenType.SemiColon]);
        }
        if (!this.peek(TokenType.SemiColon) && !this.peek(TokenType.EOF)) {
            node.setMedialist(this._parseMediaQueryList());
        }
        return this.finish(node);
    };
    LESSParser.prototype._parsePlugin = function () {
        if (!this.peekKeyword('@plugin')) {
            return null;
        }
        var node = this.createNode(nodes.NodeType.Plugin);
        this.consumeToken(); // @import
        if (!node.addChild(this._parseStringLiteral())) {
            return this.finish(node, ParseError.StringLiteralExpected);
        }
        if (!this.accept(TokenType.SemiColon)) {
            return this.finish(node, ParseError.SemiColonExpected);
        }
        return this.finish(node);
    };
    LESSParser.prototype._parseMediaQuery = function (resyncStopToken) {
        var node = _super.prototype._parseMediaQuery.call(this, resyncStopToken);
        if (!node) {
            var node_1 = this.create(nodes.MediaQuery);
            if (node_1.addChild(this._parseVariable())) {
                return this.finish(node_1);
            }
            return null;
        }
        return node;
    };
    LESSParser.prototype._parseMediaDeclaration = function (isNested) {
        if (isNested === void 0) { isNested = false; }
        return this._tryParseRuleset(isNested)
            || this._tryToParseDeclaration()
            || this._tryParseMixinDeclaration()
            || this._tryParseMixinReference()
            || this._parseDetachedRuleSetMixin()
            || this._parseStylesheetStatement(isNested);
    };
    LESSParser.prototype._parseMediaFeatureName = function () {
        return this._parseIdent() || this._parseVariable();
    };
    LESSParser.prototype._parseVariableDeclaration = function (panic) {
        if (panic === void 0) { panic = []; }
        var node = this.create(nodes.VariableDeclaration);
        var mark = this.mark();
        if (!node.setVariable(this._parseVariable(true))) {
            return null;
        }
        if (this.accept(TokenType.Colon)) {
            if (this.prevToken) {
                node.colonPosition = this.prevToken.offset;
            }
            if (node.setValue(this._parseDetachedRuleSet())) {
                node.needsSemicolon = false;
            }
            else if (!node.setValue(this._parseExpr())) {
                return this.finish(node, ParseError.VariableValueExpected, [], panic);
            }
            node.addChild(this._parsePrio());
        }
        else {
            this.restoreAtMark(mark);
            return null; // at keyword, but no ':', not a variable declaration but some at keyword
        }
        if (this.peek(TokenType.SemiColon)) {
            node.semicolonPosition = this.token.offset; // not part of the declaration, but useful information for code assist
        }
        return this.finish(node);
    };
    LESSParser.prototype._parseDetachedRuleSet = function () {
        var mark = this.mark();
        // "Anonymous mixin" used in each() and possibly a generic type in the future
        if (this.peekDelim('#') || this.peekDelim('.')) {
            this.consumeToken();
            if (!this.hasWhitespace() && this.accept(TokenType.ParenthesisL)) {
                var node = this.create(nodes.MixinDeclaration);
                if (node.getParameters().addChild(this._parseMixinParameter())) {
                    while (this.accept(TokenType.Comma) || this.accept(TokenType.SemiColon)) {
                        if (this.peek(TokenType.ParenthesisR)) {
                            break;
                        }
                        if (!node.getParameters().addChild(this._parseMixinParameter())) {
                            this.markError(node, ParseError.IdentifierExpected, [], [TokenType.ParenthesisR]);
                        }
                    }
                }
                if (!this.accept(TokenType.ParenthesisR)) {
                    this.restoreAtMark(mark);
                    return null;
                }
            }
            else {
                this.restoreAtMark(mark);
                return null;
            }
        }
        if (!this.peek(TokenType.CurlyL)) {
            return null;
        }
        var content = this.create(nodes.BodyDeclaration);
        this._parseBody(content, this._parseDetachedRuleSetBody.bind(this));
        return this.finish(content);
    };
    LESSParser.prototype._parseDetachedRuleSetBody = function () {
        return this._tryParseKeyframeSelector() || this._parseRuleSetDeclaration();
    };
    LESSParser.prototype._addLookupChildren = function (node) {
        if (!node.addChild(this._parseLookupValue())) {
            return false;
        }
        var expectsValue = false;
        while (true) {
            if (this.peek(TokenType.BracketL)) {
                expectsValue = true;
            }
            if (!node.addChild(this._parseLookupValue())) {
                break;
            }
            expectsValue = false;
        }
        return !expectsValue;
    };
    LESSParser.prototype._parseLookupValue = function () {
        var node = this.create(nodes.Node);
        var mark = this.mark();
        if (!this.accept(TokenType.BracketL)) {
            this.restoreAtMark(mark);
            return null;
        }
        if (((node.addChild(this._parseVariable(false, true)) ||
            node.addChild(this._parsePropertyIdentifier())) &&
            this.accept(TokenType.BracketR)) || this.accept(TokenType.BracketR)) {
            return node;
        }
        this.restoreAtMark(mark);
        return null;
    };
    LESSParser.prototype._parseVariable = function (declaration, insideLookup) {
        if (declaration === void 0) { declaration = false; }
        if (insideLookup === void 0) { insideLookup = false; }
        var isPropertyReference = !declaration && this.peekDelim('$');
        if (!this.peekDelim('@') && !isPropertyReference && !this.peek(TokenType.AtKeyword)) {
            return null;
        }
        var node = this.create(nodes.Variable);
        var mark = this.mark();
        while (this.acceptDelim('@') || (!declaration && this.acceptDelim('$'))) {
            if (this.hasWhitespace()) {
                this.restoreAtMark(mark);
                return null;
            }
        }
        if (!this.accept(TokenType.AtKeyword) && !this.accept(TokenType.Ident)) {
            this.restoreAtMark(mark);
            return null;
        }
        if (!insideLookup && this.peek(TokenType.BracketL)) {
            if (!this._addLookupChildren(node)) {
                this.restoreAtMark(mark);
                return null;
            }
        }
        return node;
    };
    LESSParser.prototype._parseTermExpression = function () {
        return this._parseVariable() ||
            this._parseEscaped() ||
            _super.prototype._parseTermExpression.call(this) || // preference for colors before mixin references
            this._tryParseMixinReference(false);
    };
    LESSParser.prototype._parseEscaped = function () {
        if (this.peek(TokenType.EscapedJavaScript) ||
            this.peek(TokenType.BadEscapedJavaScript)) {
            var node = this.createNode(nodes.NodeType.EscapedValue);
            this.consumeToken();
            return this.finish(node);
        }
        if (this.peekDelim('~')) {
            var node = this.createNode(nodes.NodeType.EscapedValue);
            this.consumeToken();
            if (this.accept(TokenType.String) || this.accept(TokenType.EscapedJavaScript)) {
                return this.finish(node);
            }
            else {
                return this.finish(node, ParseError.TermExpected);
            }
        }
        return null;
    };
    LESSParser.prototype._parseOperator = function () {
        var node = this._parseGuardOperator();
        if (node) {
            return node;
        }
        else {
            return _super.prototype._parseOperator.call(this);
        }
    };
    LESSParser.prototype._parseGuardOperator = function () {
        if (this.peekDelim('>')) {
            var node = this.createNode(nodes.NodeType.Operator);
            this.consumeToken();
            this.acceptDelim('=');
            return node;
        }
        else if (this.peekDelim('=')) {
            var node = this.createNode(nodes.NodeType.Operator);
            this.consumeToken();
            this.acceptDelim('<');
            return node;
        }
        else if (this.peekDelim('<')) {
            var node = this.createNode(nodes.NodeType.Operator);
            this.consumeToken();
            this.acceptDelim('=');
            return node;
        }
        return null;
    };
    LESSParser.prototype._parseRuleSetDeclaration = function () {
        if (this.peek(TokenType.AtKeyword)) {
            return this._parseKeyframe()
                || this._parseMedia(true)
                || this._parseImport()
                || this._parseSupports(true) // @supports
                || this._parseDetachedRuleSetMixin() // less detached ruleset mixin
                || this._parseVariableDeclaration() // Variable declarations
                || _super.prototype._parseRuleSetDeclarationAtStatement.call(this);
        }
        return this._tryParseMixinDeclaration()
            || this._tryParseRuleset(true) // nested ruleset
            || this._tryParseMixinReference() // less mixin reference
            || this._parseFunction()
            || this._parseExtend() // less extend declaration
            || _super.prototype._parseRuleSetDeclaration.call(this); // try css ruleset declaration as the last option
    };
    LESSParser.prototype._parseKeyframeIdent = function () {
        return this._parseIdent([nodes.ReferenceType.Keyframe]) || this._parseVariable();
    };
    LESSParser.prototype._parseKeyframeSelector = function () {
        return this._parseDetachedRuleSetMixin() // less detached ruleset mixin
            || _super.prototype._parseKeyframeSelector.call(this);
    };
    LESSParser.prototype._parseSimpleSelectorBody = function () {
        return this._parseSelectorCombinator() || _super.prototype._parseSimpleSelectorBody.call(this);
    };
    LESSParser.prototype._parseSelector = function (isNested) {
        // CSS Guards
        var node = this.create(nodes.Selector);
        var hasContent = false;
        if (isNested) {
            // nested selectors can start with a combinator
            hasContent = node.addChild(this._parseCombinator());
        }
        while (node.addChild(this._parseSimpleSelector())) {
            hasContent = true;
            var mark = this.mark();
            if (node.addChild(this._parseGuard()) && this.peek(TokenType.CurlyL)) {
                break;
            }
            this.restoreAtMark(mark);
            node.addChild(this._parseCombinator()); // optional
        }
        return hasContent ? this.finish(node) : null;
    };
    LESSParser.prototype._parseSelectorCombinator = function () {
        if (this.peekDelim('&')) {
            var node = this.createNode(nodes.NodeType.SelectorCombinator);
            this.consumeToken();
            while (!this.hasWhitespace() && (this.acceptDelim('-') || this.accept(TokenType.Num) || this.accept(TokenType.Dimension) || node.addChild(this._parseIdent()) || this.acceptDelim('&'))) {
                //  support &-foo
            }
            return this.finish(node);
        }
        return null;
    };
    LESSParser.prototype._parseSelectorIdent = function () {
        if (!this.peekInterpolatedIdent()) {
            return null;
        }
        var node = this.createNode(nodes.NodeType.SelectorInterpolation);
        var hasContent = this._acceptInterpolatedIdent(node);
        return hasContent ? this.finish(node) : null;
    };
    LESSParser.prototype._parsePropertyIdentifier = function (inLookup) {
        if (inLookup === void 0) { inLookup = false; }
        var propertyRegex = /^[\w-]+/;
        if (!this.peekInterpolatedIdent() && !this.peekRegExp(this.token.type, propertyRegex)) {
            return null;
        }
        var mark = this.mark();
        var node = this.create(nodes.Identifier);
        node.isCustomProperty = this.acceptDelim('-') && this.acceptDelim('-');
        var childAdded = false;
        if (!inLookup) {
            if (node.isCustomProperty) {
                childAdded = this._acceptInterpolatedIdent(node);
            }
            else {
                childAdded = this._acceptInterpolatedIdent(node, propertyRegex);
            }
        }
        else {
            if (node.isCustomProperty) {
                childAdded = node.addChild(this._parseIdent());
            }
            else {
                childAdded = node.addChild(this._parseRegexp(propertyRegex));
            }
        }
        if (!childAdded) {
            this.restoreAtMark(mark);
            return null;
        }
        if (!inLookup && !this.hasWhitespace()) {
            this.acceptDelim('+');
            if (!this.hasWhitespace()) {
                this.acceptIdent('_');
            }
        }
        return this.finish(node);
    };
    LESSParser.prototype.peekInterpolatedIdent = function () {
        return this.peek(TokenType.Ident) ||
            this.peekDelim('@') ||
            this.peekDelim('$') ||
            this.peekDelim('-');
    };
    LESSParser.prototype._acceptInterpolatedIdent = function (node, identRegex) {
        var _this = this;
        var hasContent = false;
        var indentInterpolation = function () {
            var pos = _this.mark();
            if (_this.acceptDelim('-')) {
                if (!_this.hasWhitespace()) {
                    _this.acceptDelim('-');
                }
                if (_this.hasWhitespace()) {
                    _this.restoreAtMark(pos);
                    return null;
                }
            }
            return _this._parseInterpolation();
        };
        var accept = identRegex ?
            function () { return _this.acceptRegexp(identRegex); } :
            function () { return _this.accept(TokenType.Ident); };
        while (accept() ||
            node.addChild(this._parseInterpolation() ||
                this.try(indentInterpolation))) {
            hasContent = true;
            if (this.hasWhitespace()) {
                break;
            }
        }
        return hasContent;
    };
    LESSParser.prototype._parseInterpolation = function () {
        // @{name} Variable or
        // ${name} Property
        var mark = this.mark();
        if (this.peekDelim('@') || this.peekDelim('$')) {
            var node = this.createNode(nodes.NodeType.Interpolation);
            this.consumeToken();
            if (this.hasWhitespace() || !this.accept(TokenType.CurlyL)) {
                this.restoreAtMark(mark);
                return null;
            }
            if (!node.addChild(this._parseIdent())) {
                return this.finish(node, ParseError.IdentifierExpected);
            }
            if (!this.accept(TokenType.CurlyR)) {
                return this.finish(node, ParseError.RightCurlyExpected);
            }
            return this.finish(node);
        }
        return null;
    };
    LESSParser.prototype._tryParseMixinDeclaration = function () {
        var mark = this.mark();
        var node = this.create(nodes.MixinDeclaration);
        if (!node.setIdentifier(this._parseMixinDeclarationIdentifier()) || !this.accept(TokenType.ParenthesisL)) {
            this.restoreAtMark(mark);
            return null;
        }
        if (node.getParameters().addChild(this._parseMixinParameter())) {
            while (this.accept(TokenType.Comma) || this.accept(TokenType.SemiColon)) {
                if (this.peek(TokenType.ParenthesisR)) {
                    break;
                }
                if (!node.getParameters().addChild(this._parseMixinParameter())) {
                    this.markError(node, ParseError.IdentifierExpected, [], [TokenType.ParenthesisR]);
                }
            }
        }
        if (!this.accept(TokenType.ParenthesisR)) {
            this.restoreAtMark(mark);
            return null;
        }
        node.setGuard(this._parseGuard());
        if (!this.peek(TokenType.CurlyL)) {
            this.restoreAtMark(mark);
            return null;
        }
        return this._parseBody(node, this._parseMixInBodyDeclaration.bind(this));
    };
    LESSParser.prototype._parseMixInBodyDeclaration = function () {
        return this._parseFontFace() || this._parseRuleSetDeclaration();
    };
    LESSParser.prototype._parseMixinDeclarationIdentifier = function () {
        var identifier;
        if (this.peekDelim('#') || this.peekDelim('.')) {
            identifier = this.create(nodes.Identifier);
            this.consumeToken(); // # or .
            if (this.hasWhitespace() || !identifier.addChild(this._parseIdent())) {
                return null;
            }
        }
        else if (this.peek(TokenType.Hash)) {
            identifier = this.create(nodes.Identifier);
            this.consumeToken(); // TokenType.Hash
        }
        else {
            return null;
        }
        identifier.referenceTypes = [nodes.ReferenceType.Mixin];
        return this.finish(identifier);
    };
    LESSParser.prototype._parsePseudo = function () {
        if (!this.peek(TokenType.Colon)) {
            return null;
        }
        var mark = this.mark();
        var node = this.create(nodes.ExtendsReference);
        this.consumeToken(); // :
        if (this.acceptIdent('extend')) {
            return this._completeExtends(node);
        }
        this.restoreAtMark(mark);
        return _super.prototype._parsePseudo.call(this);
    };
    LESSParser.prototype._parseExtend = function () {
        if (!this.peekDelim('&')) {
            return null;
        }
        var mark = this.mark();
        var node = this.create(nodes.ExtendsReference);
        this.consumeToken(); // &
        if (this.hasWhitespace() || !this.accept(TokenType.Colon) || !this.acceptIdent('extend')) {
            this.restoreAtMark(mark);
            return null;
        }
        return this._completeExtends(node);
    };
    LESSParser.prototype._completeExtends = function (node) {
        if (!this.accept(TokenType.ParenthesisL)) {
            return this.finish(node, ParseError.LeftParenthesisExpected);
        }
        var selectors = node.getSelectors();
        if (!selectors.addChild(this._parseSelector(true))) {
            return this.finish(node, ParseError.SelectorExpected);
        }
        while (this.accept(TokenType.Comma)) {
            if (!selectors.addChild(this._parseSelector(true))) {
                return this.finish(node, ParseError.SelectorExpected);
            }
        }
        if (!this.accept(TokenType.ParenthesisR)) {
            return this.finish(node, ParseError.RightParenthesisExpected);
        }
        return this.finish(node);
    };
    LESSParser.prototype._parseDetachedRuleSetMixin = function () {
        if (!this.peek(TokenType.AtKeyword)) {
            return null;
        }
        var mark = this.mark();
        var node = this.create(nodes.MixinReference);
        if (node.addChild(this._parseVariable(true)) && (this.hasWhitespace() || !this.accept(TokenType.ParenthesisL))) {
            this.restoreAtMark(mark);
            return null;
        }
        if (!this.accept(TokenType.ParenthesisR)) {
            return this.finish(node, ParseError.RightParenthesisExpected);
        }
        return this.finish(node);
    };
    LESSParser.prototype._tryParseMixinReference = function (atRoot) {
        if (atRoot === void 0) { atRoot = true; }
        var mark = this.mark();
        var node = this.create(nodes.MixinReference);
        var identifier = this._parseMixinDeclarationIdentifier();
        while (identifier) {
            this.acceptDelim('>');
            var nextId = this._parseMixinDeclarationIdentifier();
            if (nextId) {
                node.getNamespaces().addChild(identifier);
                identifier = nextId;
            }
            else {
                break;
            }
        }
        if (!node.setIdentifier(identifier)) {
            this.restoreAtMark(mark);
            return null;
        }
        var hasArguments = false;
        if (this.accept(TokenType.ParenthesisL)) {
            hasArguments = true;
            if (node.getArguments().addChild(this._parseMixinArgument())) {
                while (this.accept(TokenType.Comma) || this.accept(TokenType.SemiColon)) {
                    if (this.peek(TokenType.ParenthesisR)) {
                        break;
                    }
                    if (!node.getArguments().addChild(this._parseMixinArgument())) {
                        return this.finish(node, ParseError.ExpressionExpected);
                    }
                }
            }
            if (!this.accept(TokenType.ParenthesisR)) {
                return this.finish(node, ParseError.RightParenthesisExpected);
            }
            identifier.referenceTypes = [nodes.ReferenceType.Mixin];
        }
        else {
            identifier.referenceTypes = [nodes.ReferenceType.Mixin, nodes.ReferenceType.Rule];
        }
        if (this.peek(TokenType.BracketL)) {
            if (!atRoot) {
                this._addLookupChildren(node);
            }
        }
        else {
            node.addChild(this._parsePrio());
        }
        if (!hasArguments && !this.peek(TokenType.SemiColon) && !this.peek(TokenType.CurlyR) && !this.peek(TokenType.EOF)) {
            this.restoreAtMark(mark);
            return null;
        }
        return this.finish(node);
    };
    LESSParser.prototype._parseMixinArgument = function () {
        // [variableName ':'] expression | variableName '...'
        var node = this.create(nodes.FunctionArgument);
        var pos = this.mark();
        var argument = this._parseVariable();
        if (argument) {
            if (!this.accept(TokenType.Colon)) {
                this.restoreAtMark(pos);
            }
            else {
                node.setIdentifier(argument);
            }
        }
        if (node.setValue(this._parseDetachedRuleSet() || this._parseExpr(true))) {
            return this.finish(node);
        }
        this.restoreAtMark(pos);
        return null;
    };
    LESSParser.prototype._parseMixinParameter = function () {
        var node = this.create(nodes.FunctionParameter);
        // special rest variable: @rest...
        if (this.peekKeyword('@rest')) {
            var restNode = this.create(nodes.Node);
            this.consumeToken();
            if (!this.accept(lessScanner.Ellipsis)) {
                return this.finish(node, ParseError.DotExpected, [], [TokenType.Comma, TokenType.ParenthesisR]);
            }
            node.setIdentifier(this.finish(restNode));
            return this.finish(node);
        }
        // special const args: ...
        if (this.peek(lessScanner.Ellipsis)) {
            var varargsNode = this.create(nodes.Node);
            this.consumeToken();
            node.setIdentifier(this.finish(varargsNode));
            return this.finish(node);
        }
        var hasContent = false;
        // default variable declaration: @param: 12 or @name
        if (node.setIdentifier(this._parseVariable())) {
            this.accept(TokenType.Colon);
            hasContent = true;
        }
        if (!node.setDefaultValue(this._parseDetachedRuleSet() || this._parseExpr(true)) && !hasContent) {
            return null;
        }
        return this.finish(node);
    };
    LESSParser.prototype._parseGuard = function () {
        if (!this.peekIdent('when')) {
            return null;
        }
        var node = this.create(nodes.LessGuard);
        this.consumeToken(); // when
        node.isNegated = this.acceptIdent('not');
        if (!node.getConditions().addChild(this._parseGuardCondition())) {
            return this.finish(node, ParseError.ConditionExpected);
        }
        while (this.acceptIdent('and') || this.accept(TokenType.Comma)) {
            if (!node.getConditions().addChild(this._parseGuardCondition())) {
                return this.finish(node, ParseError.ConditionExpected);
            }
        }
        return this.finish(node);
    };
    LESSParser.prototype._parseGuardCondition = function () {
        if (!this.peek(TokenType.ParenthesisL)) {
            return null;
        }
        var node = this.create(nodes.GuardCondition);
        this.consumeToken(); // ParenthesisL
        if (!node.addChild(this._parseExpr())) {
            // empty (?)
        }
        if (!this.accept(TokenType.ParenthesisR)) {
            return this.finish(node, ParseError.RightParenthesisExpected);
        }
        return this.finish(node);
    };
    LESSParser.prototype._parseFunction = function () {
        var pos = this.mark();
        var node = this.create(nodes.Function);
        if (!node.setIdentifier(this._parseFunctionIdentifier())) {
            return null;
        }
        if (this.hasWhitespace() || !this.accept(TokenType.ParenthesisL)) {
            this.restoreAtMark(pos);
            return null;
        }
        if (node.getArguments().addChild(this._parseMixinArgument())) {
            while (this.accept(TokenType.Comma) || this.accept(TokenType.SemiColon)) {
                if (this.peek(TokenType.ParenthesisR)) {
                    break;
                }
                if (!node.getArguments().addChild(this._parseMixinArgument())) {
                    return this.finish(node, ParseError.ExpressionExpected);
                }
            }
        }
        if (!this.accept(TokenType.ParenthesisR)) {
            return this.finish(node, ParseError.RightParenthesisExpected);
        }
        return this.finish(node);
    };
    LESSParser.prototype._parseFunctionIdentifier = function () {
        if (this.peekDelim('%')) {
            var node = this.create(nodes.Identifier);
            node.referenceTypes = [nodes.ReferenceType.Function];
            this.consumeToken();
            return this.finish(node);
        }
        return _super.prototype._parseFunctionIdentifier.call(this);
    };
    LESSParser.prototype._parseURLArgument = function () {
        var pos = this.mark();
        var node = _super.prototype._parseURLArgument.call(this);
        if (!node || !this.peek(TokenType.ParenthesisR)) {
            this.restoreAtMark(pos);
            var node_2 = this.create(nodes.Node);
            node_2.addChild(this._parseBinaryExpr());
            return this.finish(node_2);
        }
        return node;
    };
    return LESSParser;
}(cssParser.Parser));
export { LESSParser };
