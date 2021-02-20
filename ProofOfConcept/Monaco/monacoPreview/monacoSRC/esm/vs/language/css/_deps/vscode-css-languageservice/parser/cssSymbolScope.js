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
import * as nodes from './cssNodes.js';
import { findFirst } from '../utils/arrays.js';
var Scope = /** @class */ (function () {
    function Scope(offset, length) {
        this.offset = offset;
        this.length = length;
        this.symbols = [];
        this.parent = null;
        this.children = [];
    }
    Scope.prototype.addChild = function (scope) {
        this.children.push(scope);
        scope.setParent(this);
    };
    Scope.prototype.setParent = function (scope) {
        this.parent = scope;
    };
    Scope.prototype.findScope = function (offset, length) {
        if (length === void 0) { length = 0; }
        if (this.offset <= offset && this.offset + this.length > offset + length || this.offset === offset && this.length === length) {
            return this.findInScope(offset, length);
        }
        return null;
    };
    Scope.prototype.findInScope = function (offset, length) {
        if (length === void 0) { length = 0; }
        // find the first scope child that has an offset larger than offset + length
        var end = offset + length;
        var idx = findFirst(this.children, function (s) { return s.offset > end; });
        if (idx === 0) {
            // all scopes have offsets larger than our end
            return this;
        }
        var res = this.children[idx - 1];
        if (res.offset <= offset && res.offset + res.length >= offset + length) {
            return res.findInScope(offset, length);
        }
        return this;
    };
    Scope.prototype.addSymbol = function (symbol) {
        this.symbols.push(symbol);
    };
    Scope.prototype.getSymbol = function (name, type) {
        for (var index = 0; index < this.symbols.length; index++) {
            var symbol = this.symbols[index];
            if (symbol.name === name && symbol.type === type) {
                return symbol;
            }
        }
        return null;
    };
    Scope.prototype.getSymbols = function () {
        return this.symbols;
    };
    return Scope;
}());
export { Scope };
var GlobalScope = /** @class */ (function (_super) {
    __extends(GlobalScope, _super);
    function GlobalScope() {
        return _super.call(this, 0, Number.MAX_VALUE) || this;
    }
    return GlobalScope;
}(Scope));
export { GlobalScope };
var Symbol = /** @class */ (function () {
    function Symbol(name, value, node, type) {
        this.name = name;
        this.value = value;
        this.node = node;
        this.type = type;
    }
    return Symbol;
}());
export { Symbol };
var ScopeBuilder = /** @class */ (function () {
    function ScopeBuilder(scope) {
        this.scope = scope;
    }
    ScopeBuilder.prototype.addSymbol = function (node, name, value, type) {
        if (node.offset !== -1) {
            var current = this.scope.findScope(node.offset, node.length);
            if (current) {
                current.addSymbol(new Symbol(name, value, node, type));
            }
        }
    };
    ScopeBuilder.prototype.addScope = function (node) {
        if (node.offset !== -1) {
            var current = this.scope.findScope(node.offset, node.length);
            if (current && (current.offset !== node.offset || current.length !== node.length)) { // scope already known?
                var newScope = new Scope(node.offset, node.length);
                current.addChild(newScope);
                return newScope;
            }
            return current;
        }
        return null;
    };
    ScopeBuilder.prototype.addSymbolToChildScope = function (scopeNode, node, name, value, type) {
        if (scopeNode && scopeNode.offset !== -1) {
            var current = this.addScope(scopeNode); // create the scope or gets the existing one
            if (current) {
                current.addSymbol(new Symbol(name, value, node, type));
            }
        }
    };
    ScopeBuilder.prototype.visitNode = function (node) {
        switch (node.type) {
            case nodes.NodeType.Keyframe:
                this.addSymbol(node, node.getName(), void 0, nodes.ReferenceType.Keyframe);
                return true;
            case nodes.NodeType.CustomPropertyDeclaration:
                return this.visitCustomPropertyDeclarationNode(node);
            case nodes.NodeType.VariableDeclaration:
                return this.visitVariableDeclarationNode(node);
            case nodes.NodeType.Ruleset:
                return this.visitRuleSet(node);
            case nodes.NodeType.MixinDeclaration:
                this.addSymbol(node, node.getName(), void 0, nodes.ReferenceType.Mixin);
                return true;
            case nodes.NodeType.FunctionDeclaration:
                this.addSymbol(node, node.getName(), void 0, nodes.ReferenceType.Function);
                return true;
            case nodes.NodeType.FunctionParameter: {
                return this.visitFunctionParameterNode(node);
            }
            case nodes.NodeType.Declarations:
                this.addScope(node);
                return true;
            case nodes.NodeType.For:
                var forNode = node;
                var scopeNode = forNode.getDeclarations();
                if (scopeNode && forNode.variable) {
                    this.addSymbolToChildScope(scopeNode, forNode.variable, forNode.variable.getName(), void 0, nodes.ReferenceType.Variable);
                }
                return true;
            case nodes.NodeType.Each: {
                var eachNode = node;
                var scopeNode_1 = eachNode.getDeclarations();
                if (scopeNode_1) {
                    var variables = eachNode.getVariables().getChildren();
                    for (var _i = 0, variables_1 = variables; _i < variables_1.length; _i++) {
                        var variable = variables_1[_i];
                        this.addSymbolToChildScope(scopeNode_1, variable, variable.getName(), void 0, nodes.ReferenceType.Variable);
                    }
                }
                return true;
            }
        }
        return true;
    };
    ScopeBuilder.prototype.visitRuleSet = function (node) {
        var current = this.scope.findScope(node.offset, node.length);
        if (current) {
            for (var _i = 0, _a = node.getSelectors().getChildren(); _i < _a.length; _i++) {
                var child = _a[_i];
                if (child instanceof nodes.Selector) {
                    if (child.getChildren().length === 1) { // only selectors with a single element can be extended
                        current.addSymbol(new Symbol(child.getChild(0).getText(), void 0, child, nodes.ReferenceType.Rule));
                    }
                }
            }
        }
        return true;
    };
    ScopeBuilder.prototype.visitVariableDeclarationNode = function (node) {
        var value = node.getValue() ? node.getValue().getText() : void 0;
        this.addSymbol(node, node.getName(), value, nodes.ReferenceType.Variable);
        return true;
    };
    ScopeBuilder.prototype.visitFunctionParameterNode = function (node) {
        // parameters are part of the body scope
        var scopeNode = node.getParent().getDeclarations();
        if (scopeNode) {
            var valueNode = node.getDefaultValue();
            var value = valueNode ? valueNode.getText() : void 0;
            this.addSymbolToChildScope(scopeNode, node, node.getName(), value, nodes.ReferenceType.Variable);
        }
        return true;
    };
    ScopeBuilder.prototype.visitCustomPropertyDeclarationNode = function (node) {
        var value = node.getValue() ? node.getValue().getText() : '';
        this.addCSSVariable(node.getProperty(), node.getProperty().getName(), value, nodes.ReferenceType.Variable);
        return true;
    };
    ScopeBuilder.prototype.addCSSVariable = function (node, name, value, type) {
        if (node.offset !== -1) {
            this.scope.addSymbol(new Symbol(name, value, node, type));
        }
    };
    return ScopeBuilder;
}());
export { ScopeBuilder };
var Symbols = /** @class */ (function () {
    function Symbols(node) {
        this.global = new GlobalScope();
        node.acceptVisitor(new ScopeBuilder(this.global));
    }
    Symbols.prototype.findSymbolsAtOffset = function (offset, referenceType) {
        var scope = this.global.findScope(offset, 0);
        var result = [];
        var names = {};
        while (scope) {
            var symbols = scope.getSymbols();
            for (var i = 0; i < symbols.length; i++) {
                var symbol = symbols[i];
                if (symbol.type === referenceType && !names[symbol.name]) {
                    result.push(symbol);
                    names[symbol.name] = true;
                }
            }
            scope = scope.parent;
        }
        return result;
    };
    Symbols.prototype.internalFindSymbol = function (node, referenceTypes) {
        var scopeNode = node;
        if (node.parent instanceof nodes.FunctionParameter && node.parent.getParent() instanceof nodes.BodyDeclaration) {
            scopeNode = node.parent.getParent().getDeclarations();
        }
        if (node.parent instanceof nodes.FunctionArgument && node.parent.getParent() instanceof nodes.Function) {
            var funcId = node.parent.getParent().getIdentifier();
            if (funcId) {
                var functionSymbol = this.internalFindSymbol(funcId, [nodes.ReferenceType.Function]);
                if (functionSymbol) {
                    scopeNode = functionSymbol.node.getDeclarations();
                }
            }
        }
        if (!scopeNode) {
            return null;
        }
        var name = node.getText();
        var scope = this.global.findScope(scopeNode.offset, scopeNode.length);
        while (scope) {
            for (var index = 0; index < referenceTypes.length; index++) {
                var type = referenceTypes[index];
                var symbol = scope.getSymbol(name, type);
                if (symbol) {
                    return symbol;
                }
            }
            scope = scope.parent;
        }
        return null;
    };
    Symbols.prototype.evaluateReferenceTypes = function (node) {
        if (node instanceof nodes.Identifier) {
            var referenceTypes = node.referenceTypes;
            if (referenceTypes) {
                return referenceTypes;
            }
            else {
                if (node.isCustomProperty) {
                    return [nodes.ReferenceType.Variable];
                }
                // are a reference to a keyframe?
                var decl = nodes.getParentDeclaration(node);
                if (decl) {
                    var propertyName = decl.getNonPrefixedPropertyName();
                    if ((propertyName === 'animation' || propertyName === 'animation-name')
                        && decl.getValue() && decl.getValue().offset === node.offset) {
                        return [nodes.ReferenceType.Keyframe];
                    }
                }
            }
        }
        else if (node instanceof nodes.Variable) {
            return [nodes.ReferenceType.Variable];
        }
        var selector = node.findAParent(nodes.NodeType.Selector, nodes.NodeType.ExtendsReference);
        if (selector) {
            return [nodes.ReferenceType.Rule];
        }
        return null;
    };
    Symbols.prototype.findSymbolFromNode = function (node) {
        if (!node) {
            return null;
        }
        while (node.type === nodes.NodeType.Interpolation) {
            node = node.getParent();
        }
        var referenceTypes = this.evaluateReferenceTypes(node);
        if (referenceTypes) {
            return this.internalFindSymbol(node, referenceTypes);
        }
        return null;
    };
    Symbols.prototype.matchesSymbol = function (node, symbol) {
        if (!node) {
            return false;
        }
        while (node.type === nodes.NodeType.Interpolation) {
            node = node.getParent();
        }
        if (!node.matches(symbol.name)) {
            return false;
        }
        var referenceTypes = this.evaluateReferenceTypes(node);
        if (!referenceTypes || referenceTypes.indexOf(symbol.type) === -1) {
            return false;
        }
        var nodeSymbol = this.internalFindSymbol(node, referenceTypes);
        return nodeSymbol === symbol;
    };
    Symbols.prototype.findSymbol = function (name, type, offset) {
        var scope = this.global.findScope(offset);
        while (scope) {
            var symbol = scope.getSymbol(name, type);
            if (symbol) {
                return symbol;
            }
            scope = scope.parent;
        }
        return null;
    };
    return Symbols;
}());
export { Symbols };
