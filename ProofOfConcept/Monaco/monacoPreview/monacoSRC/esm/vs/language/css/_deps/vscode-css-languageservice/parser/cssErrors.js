/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
'use strict';
import * as nls from './../../../fillers/vscode-nls.js';
var localize = nls.loadMessageBundle();
var CSSIssueType = /** @class */ (function () {
    function CSSIssueType(id, message) {
        this.id = id;
        this.message = message;
    }
    return CSSIssueType;
}());
export { CSSIssueType };
export var ParseError = {
    NumberExpected: new CSSIssueType('css-numberexpected', localize('expected.number', "number expected")),
    ConditionExpected: new CSSIssueType('css-conditionexpected', localize('expected.condt', "condition expected")),
    RuleOrSelectorExpected: new CSSIssueType('css-ruleorselectorexpected', localize('expected.ruleorselector', "at-rule or selector expected")),
    DotExpected: new CSSIssueType('css-dotexpected', localize('expected.dot', "dot expected")),
    ColonExpected: new CSSIssueType('css-colonexpected', localize('expected.colon', "colon expected")),
    SemiColonExpected: new CSSIssueType('css-semicolonexpected', localize('expected.semicolon', "semi-colon expected")),
    TermExpected: new CSSIssueType('css-termexpected', localize('expected.term', "term expected")),
    ExpressionExpected: new CSSIssueType('css-expressionexpected', localize('expected.expression', "expression expected")),
    OperatorExpected: new CSSIssueType('css-operatorexpected', localize('expected.operator', "operator expected")),
    IdentifierExpected: new CSSIssueType('css-identifierexpected', localize('expected.ident', "identifier expected")),
    PercentageExpected: new CSSIssueType('css-percentageexpected', localize('expected.percentage', "percentage expected")),
    URIOrStringExpected: new CSSIssueType('css-uriorstringexpected', localize('expected.uriorstring', "uri or string expected")),
    URIExpected: new CSSIssueType('css-uriexpected', localize('expected.uri', "URI expected")),
    VariableNameExpected: new CSSIssueType('css-varnameexpected', localize('expected.varname', "variable name expected")),
    VariableValueExpected: new CSSIssueType('css-varvalueexpected', localize('expected.varvalue', "variable value expected")),
    PropertyValueExpected: new CSSIssueType('css-propertyvalueexpected', localize('expected.propvalue', "property value expected")),
    LeftCurlyExpected: new CSSIssueType('css-lcurlyexpected', localize('expected.lcurly', "{ expected")),
    RightCurlyExpected: new CSSIssueType('css-rcurlyexpected', localize('expected.rcurly', "} expected")),
    LeftSquareBracketExpected: new CSSIssueType('css-rbracketexpected', localize('expected.lsquare', "[ expected")),
    RightSquareBracketExpected: new CSSIssueType('css-lbracketexpected', localize('expected.rsquare', "] expected")),
    LeftParenthesisExpected: new CSSIssueType('css-lparentexpected', localize('expected.lparen', "( expected")),
    RightParenthesisExpected: new CSSIssueType('css-rparentexpected', localize('expected.rparent', ") expected")),
    CommaExpected: new CSSIssueType('css-commaexpected', localize('expected.comma', "comma expected")),
    PageDirectiveOrDeclarationExpected: new CSSIssueType('css-pagedirordeclexpected', localize('expected.pagedirordecl', "page directive or declaraton expected")),
    UnknownAtRule: new CSSIssueType('css-unknownatrule', localize('unknown.atrule', "at-rule unknown")),
    UnknownKeyword: new CSSIssueType('css-unknownkeyword', localize('unknown.keyword', "unknown keyword")),
    SelectorExpected: new CSSIssueType('css-selectorexpected', localize('expected.selector', "selector expected")),
    StringLiteralExpected: new CSSIssueType('css-stringliteralexpected', localize('expected.stringliteral', "string literal expected")),
    WhitespaceExpected: new CSSIssueType('css-whitespaceexpected', localize('expected.whitespace', "whitespace expected")),
    MediaQueryExpected: new CSSIssueType('css-mediaqueryexpected', localize('expected.mediaquery', "media query expected")),
    IdentifierOrWildcardExpected: new CSSIssueType('css-idorwildcardexpected', localize('expected.idorwildcard', "identifier or wildcard expected")),
    WildcardExpected: new CSSIssueType('css-wildcardexpected', localize('expected.wildcard', "wildcard expected")),
    IdentifierOrVariableExpected: new CSSIssueType('css-idorvarexpected', localize('expected.idorvar', "identifier or variable expected")),
};
