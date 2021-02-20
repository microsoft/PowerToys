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
import { trim } from "../utils/strings.js";
/// <summary>
/// Nodes for the css 2.1 specification. See for reference:
/// http://www.w3.org/TR/CSS21/grammar.html#grammar
/// </summary>
export var NodeType;
(function (NodeType) {
    NodeType[NodeType["Undefined"] = 0] = "Undefined";
    NodeType[NodeType["Identifier"] = 1] = "Identifier";
    NodeType[NodeType["Stylesheet"] = 2] = "Stylesheet";
    NodeType[NodeType["Ruleset"] = 3] = "Ruleset";
    NodeType[NodeType["Selector"] = 4] = "Selector";
    NodeType[NodeType["SimpleSelector"] = 5] = "SimpleSelector";
    NodeType[NodeType["SelectorInterpolation"] = 6] = "SelectorInterpolation";
    NodeType[NodeType["SelectorCombinator"] = 7] = "SelectorCombinator";
    NodeType[NodeType["SelectorCombinatorParent"] = 8] = "SelectorCombinatorParent";
    NodeType[NodeType["SelectorCombinatorSibling"] = 9] = "SelectorCombinatorSibling";
    NodeType[NodeType["SelectorCombinatorAllSiblings"] = 10] = "SelectorCombinatorAllSiblings";
    NodeType[NodeType["SelectorCombinatorShadowPiercingDescendant"] = 11] = "SelectorCombinatorShadowPiercingDescendant";
    NodeType[NodeType["Page"] = 12] = "Page";
    NodeType[NodeType["PageBoxMarginBox"] = 13] = "PageBoxMarginBox";
    NodeType[NodeType["ClassSelector"] = 14] = "ClassSelector";
    NodeType[NodeType["IdentifierSelector"] = 15] = "IdentifierSelector";
    NodeType[NodeType["ElementNameSelector"] = 16] = "ElementNameSelector";
    NodeType[NodeType["PseudoSelector"] = 17] = "PseudoSelector";
    NodeType[NodeType["AttributeSelector"] = 18] = "AttributeSelector";
    NodeType[NodeType["Declaration"] = 19] = "Declaration";
    NodeType[NodeType["Declarations"] = 20] = "Declarations";
    NodeType[NodeType["Property"] = 21] = "Property";
    NodeType[NodeType["Expression"] = 22] = "Expression";
    NodeType[NodeType["BinaryExpression"] = 23] = "BinaryExpression";
    NodeType[NodeType["Term"] = 24] = "Term";
    NodeType[NodeType["Operator"] = 25] = "Operator";
    NodeType[NodeType["Value"] = 26] = "Value";
    NodeType[NodeType["StringLiteral"] = 27] = "StringLiteral";
    NodeType[NodeType["URILiteral"] = 28] = "URILiteral";
    NodeType[NodeType["EscapedValue"] = 29] = "EscapedValue";
    NodeType[NodeType["Function"] = 30] = "Function";
    NodeType[NodeType["NumericValue"] = 31] = "NumericValue";
    NodeType[NodeType["HexColorValue"] = 32] = "HexColorValue";
    NodeType[NodeType["MixinDeclaration"] = 33] = "MixinDeclaration";
    NodeType[NodeType["MixinReference"] = 34] = "MixinReference";
    NodeType[NodeType["VariableName"] = 35] = "VariableName";
    NodeType[NodeType["VariableDeclaration"] = 36] = "VariableDeclaration";
    NodeType[NodeType["Prio"] = 37] = "Prio";
    NodeType[NodeType["Interpolation"] = 38] = "Interpolation";
    NodeType[NodeType["NestedProperties"] = 39] = "NestedProperties";
    NodeType[NodeType["ExtendsReference"] = 40] = "ExtendsReference";
    NodeType[NodeType["SelectorPlaceholder"] = 41] = "SelectorPlaceholder";
    NodeType[NodeType["Debug"] = 42] = "Debug";
    NodeType[NodeType["If"] = 43] = "If";
    NodeType[NodeType["Else"] = 44] = "Else";
    NodeType[NodeType["For"] = 45] = "For";
    NodeType[NodeType["Each"] = 46] = "Each";
    NodeType[NodeType["While"] = 47] = "While";
    NodeType[NodeType["MixinContentReference"] = 48] = "MixinContentReference";
    NodeType[NodeType["MixinContentDeclaration"] = 49] = "MixinContentDeclaration";
    NodeType[NodeType["Media"] = 50] = "Media";
    NodeType[NodeType["Keyframe"] = 51] = "Keyframe";
    NodeType[NodeType["FontFace"] = 52] = "FontFace";
    NodeType[NodeType["Import"] = 53] = "Import";
    NodeType[NodeType["Namespace"] = 54] = "Namespace";
    NodeType[NodeType["Invocation"] = 55] = "Invocation";
    NodeType[NodeType["FunctionDeclaration"] = 56] = "FunctionDeclaration";
    NodeType[NodeType["ReturnStatement"] = 57] = "ReturnStatement";
    NodeType[NodeType["MediaQuery"] = 58] = "MediaQuery";
    NodeType[NodeType["FunctionParameter"] = 59] = "FunctionParameter";
    NodeType[NodeType["FunctionArgument"] = 60] = "FunctionArgument";
    NodeType[NodeType["KeyframeSelector"] = 61] = "KeyframeSelector";
    NodeType[NodeType["ViewPort"] = 62] = "ViewPort";
    NodeType[NodeType["Document"] = 63] = "Document";
    NodeType[NodeType["AtApplyRule"] = 64] = "AtApplyRule";
    NodeType[NodeType["CustomPropertyDeclaration"] = 65] = "CustomPropertyDeclaration";
    NodeType[NodeType["CustomPropertySet"] = 66] = "CustomPropertySet";
    NodeType[NodeType["ListEntry"] = 67] = "ListEntry";
    NodeType[NodeType["Supports"] = 68] = "Supports";
    NodeType[NodeType["SupportsCondition"] = 69] = "SupportsCondition";
    NodeType[NodeType["NamespacePrefix"] = 70] = "NamespacePrefix";
    NodeType[NodeType["GridLine"] = 71] = "GridLine";
    NodeType[NodeType["Plugin"] = 72] = "Plugin";
    NodeType[NodeType["UnknownAtRule"] = 73] = "UnknownAtRule";
    NodeType[NodeType["Use"] = 74] = "Use";
    NodeType[NodeType["ModuleConfiguration"] = 75] = "ModuleConfiguration";
    NodeType[NodeType["Forward"] = 76] = "Forward";
    NodeType[NodeType["ForwardVisibility"] = 77] = "ForwardVisibility";
    NodeType[NodeType["Module"] = 78] = "Module";
})(NodeType || (NodeType = {}));
export var ReferenceType;
(function (ReferenceType) {
    ReferenceType[ReferenceType["Mixin"] = 0] = "Mixin";
    ReferenceType[ReferenceType["Rule"] = 1] = "Rule";
    ReferenceType[ReferenceType["Variable"] = 2] = "Variable";
    ReferenceType[ReferenceType["Function"] = 3] = "Function";
    ReferenceType[ReferenceType["Keyframe"] = 4] = "Keyframe";
    ReferenceType[ReferenceType["Unknown"] = 5] = "Unknown";
    ReferenceType[ReferenceType["Module"] = 6] = "Module";
    ReferenceType[ReferenceType["Forward"] = 7] = "Forward";
    ReferenceType[ReferenceType["ForwardVisibility"] = 8] = "ForwardVisibility";
})(ReferenceType || (ReferenceType = {}));
export function getNodeAtOffset(node, offset) {
    var candidate = null;
    if (!node || offset < node.offset || offset > node.end) {
        return null;
    }
    // Find the shortest node at the position
    node.accept(function (node) {
        if (node.offset === -1 && node.length === -1) {
            return true;
        }
        if (node.offset <= offset && node.end >= offset) {
            if (!candidate) {
                candidate = node;
            }
            else if (node.length <= candidate.length) {
                candidate = node;
            }
            return true;
        }
        return false;
    });
    return candidate;
}
export function getNodePath(node, offset) {
    var candidate = getNodeAtOffset(node, offset);
    var path = [];
    while (candidate) {
        path.unshift(candidate);
        candidate = candidate.parent;
    }
    return path;
}
export function getParentDeclaration(node) {
    var decl = node.findParent(NodeType.Declaration);
    var value = decl && decl.getValue();
    if (value && value.encloses(node)) {
        return decl;
    }
    return null;
}
var Node = /** @class */ (function () {
    function Node(offset, len, nodeType) {
        if (offset === void 0) { offset = -1; }
        if (len === void 0) { len = -1; }
        this.parent = null;
        this.offset = offset;
        this.length = len;
        if (nodeType) {
            this.nodeType = nodeType;
        }
    }
    Object.defineProperty(Node.prototype, "end", {
        get: function () { return this.offset + this.length; },
        enumerable: false,
        configurable: true
    });
    Object.defineProperty(Node.prototype, "type", {
        get: function () {
            return this.nodeType || NodeType.Undefined;
        },
        set: function (type) {
            this.nodeType = type;
        },
        enumerable: false,
        configurable: true
    });
    Node.prototype.getTextProvider = function () {
        var node = this;
        while (node && !node.textProvider) {
            node = node.parent;
        }
        if (node) {
            return node.textProvider;
        }
        return function () { return 'unknown'; };
    };
    Node.prototype.getText = function () {
        return this.getTextProvider()(this.offset, this.length);
    };
    Node.prototype.matches = function (str) {
        return this.length === str.length && this.getTextProvider()(this.offset, this.length) === str;
    };
    Node.prototype.startsWith = function (str) {
        return this.length >= str.length && this.getTextProvider()(this.offset, str.length) === str;
    };
    Node.prototype.endsWith = function (str) {
        return this.length >= str.length && this.getTextProvider()(this.end - str.length, str.length) === str;
    };
    Node.prototype.accept = function (visitor) {
        if (visitor(this) && this.children) {
            for (var _i = 0, _a = this.children; _i < _a.length; _i++) {
                var child = _a[_i];
                child.accept(visitor);
            }
        }
    };
    Node.prototype.acceptVisitor = function (visitor) {
        this.accept(visitor.visitNode.bind(visitor));
    };
    Node.prototype.adoptChild = function (node, index) {
        if (index === void 0) { index = -1; }
        if (node.parent && node.parent.children) {
            var idx = node.parent.children.indexOf(node);
            if (idx >= 0) {
                node.parent.children.splice(idx, 1);
            }
        }
        node.parent = this;
        var children = this.children;
        if (!children) {
            children = this.children = [];
        }
        if (index !== -1) {
            children.splice(index, 0, node);
        }
        else {
            children.push(node);
        }
        return node;
    };
    Node.prototype.attachTo = function (parent, index) {
        if (index === void 0) { index = -1; }
        if (parent) {
            parent.adoptChild(this, index);
        }
        return this;
    };
    Node.prototype.collectIssues = function (results) {
        if (this.issues) {
            results.push.apply(results, this.issues);
        }
    };
    Node.prototype.addIssue = function (issue) {
        if (!this.issues) {
            this.issues = [];
        }
        this.issues.push(issue);
    };
    Node.prototype.hasIssue = function (rule) {
        return Array.isArray(this.issues) && this.issues.some(function (i) { return i.getRule() === rule; });
    };
    Node.prototype.isErroneous = function (recursive) {
        if (recursive === void 0) { recursive = false; }
        if (this.issues && this.issues.length > 0) {
            return true;
        }
        return recursive && Array.isArray(this.children) && this.children.some(function (c) { return c.isErroneous(true); });
    };
    Node.prototype.setNode = function (field, node, index) {
        if (index === void 0) { index = -1; }
        if (node) {
            node.attachTo(this, index);
            this[field] = node;
            return true;
        }
        return false;
    };
    Node.prototype.addChild = function (node) {
        if (node) {
            if (!this.children) {
                this.children = [];
            }
            node.attachTo(this);
            this.updateOffsetAndLength(node);
            return true;
        }
        return false;
    };
    Node.prototype.updateOffsetAndLength = function (node) {
        if (node.offset < this.offset || this.offset === -1) {
            this.offset = node.offset;
        }
        var nodeEnd = node.end;
        if ((nodeEnd > this.end) || this.length === -1) {
            this.length = nodeEnd - this.offset;
        }
    };
    Node.prototype.hasChildren = function () {
        return !!this.children && this.children.length > 0;
    };
    Node.prototype.getChildren = function () {
        return this.children ? this.children.slice(0) : [];
    };
    Node.prototype.getChild = function (index) {
        if (this.children && index < this.children.length) {
            return this.children[index];
        }
        return null;
    };
    Node.prototype.addChildren = function (nodes) {
        for (var _i = 0, nodes_1 = nodes; _i < nodes_1.length; _i++) {
            var node = nodes_1[_i];
            this.addChild(node);
        }
    };
    Node.prototype.findFirstChildBeforeOffset = function (offset) {
        if (this.children) {
            var current = null;
            for (var i = this.children.length - 1; i >= 0; i--) {
                // iterate until we find a child that has a start offset smaller than the input offset
                current = this.children[i];
                if (current.offset <= offset) {
                    return current;
                }
            }
        }
        return null;
    };
    Node.prototype.findChildAtOffset = function (offset, goDeep) {
        var current = this.findFirstChildBeforeOffset(offset);
        if (current && current.end >= offset) {
            if (goDeep) {
                return current.findChildAtOffset(offset, true) || current;
            }
            return current;
        }
        return null;
    };
    Node.prototype.encloses = function (candidate) {
        return this.offset <= candidate.offset && this.offset + this.length >= candidate.offset + candidate.length;
    };
    Node.prototype.getParent = function () {
        var result = this.parent;
        while (result instanceof Nodelist) {
            result = result.parent;
        }
        return result;
    };
    Node.prototype.findParent = function (type) {
        var result = this;
        while (result && result.type !== type) {
            result = result.parent;
        }
        return result;
    };
    Node.prototype.findAParent = function () {
        var types = [];
        for (var _i = 0; _i < arguments.length; _i++) {
            types[_i] = arguments[_i];
        }
        var result = this;
        while (result && !types.some(function (t) { return result.type === t; })) {
            result = result.parent;
        }
        return result;
    };
    Node.prototype.setData = function (key, value) {
        if (!this.options) {
            this.options = {};
        }
        this.options[key] = value;
    };
    Node.prototype.getData = function (key) {
        if (!this.options || !this.options.hasOwnProperty(key)) {
            return null;
        }
        return this.options[key];
    };
    return Node;
}());
export { Node };
var Nodelist = /** @class */ (function (_super) {
    __extends(Nodelist, _super);
    function Nodelist(parent, index) {
        if (index === void 0) { index = -1; }
        var _this = _super.call(this, -1, -1) || this;
        _this.attachTo(parent, index);
        _this.offset = -1;
        _this.length = -1;
        return _this;
    }
    return Nodelist;
}(Node));
export { Nodelist };
var Identifier = /** @class */ (function (_super) {
    __extends(Identifier, _super);
    function Identifier(offset, length) {
        var _this = _super.call(this, offset, length) || this;
        _this.isCustomProperty = false;
        return _this;
    }
    Object.defineProperty(Identifier.prototype, "type", {
        get: function () {
            return NodeType.Identifier;
        },
        enumerable: false,
        configurable: true
    });
    Identifier.prototype.containsInterpolation = function () {
        return this.hasChildren();
    };
    return Identifier;
}(Node));
export { Identifier };
var Stylesheet = /** @class */ (function (_super) {
    __extends(Stylesheet, _super);
    function Stylesheet(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(Stylesheet.prototype, "type", {
        get: function () {
            return NodeType.Stylesheet;
        },
        enumerable: false,
        configurable: true
    });
    return Stylesheet;
}(Node));
export { Stylesheet };
var Declarations = /** @class */ (function (_super) {
    __extends(Declarations, _super);
    function Declarations(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(Declarations.prototype, "type", {
        get: function () {
            return NodeType.Declarations;
        },
        enumerable: false,
        configurable: true
    });
    return Declarations;
}(Node));
export { Declarations };
var BodyDeclaration = /** @class */ (function (_super) {
    __extends(BodyDeclaration, _super);
    function BodyDeclaration(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    BodyDeclaration.prototype.getDeclarations = function () {
        return this.declarations;
    };
    BodyDeclaration.prototype.setDeclarations = function (decls) {
        return this.setNode('declarations', decls);
    };
    return BodyDeclaration;
}(Node));
export { BodyDeclaration };
var RuleSet = /** @class */ (function (_super) {
    __extends(RuleSet, _super);
    function RuleSet(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(RuleSet.prototype, "type", {
        get: function () {
            return NodeType.Ruleset;
        },
        enumerable: false,
        configurable: true
    });
    RuleSet.prototype.getSelectors = function () {
        if (!this.selectors) {
            this.selectors = new Nodelist(this);
        }
        return this.selectors;
    };
    RuleSet.prototype.isNested = function () {
        return !!this.parent && this.parent.findParent(NodeType.Declarations) !== null;
    };
    return RuleSet;
}(BodyDeclaration));
export { RuleSet };
var Selector = /** @class */ (function (_super) {
    __extends(Selector, _super);
    function Selector(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(Selector.prototype, "type", {
        get: function () {
            return NodeType.Selector;
        },
        enumerable: false,
        configurable: true
    });
    return Selector;
}(Node));
export { Selector };
var SimpleSelector = /** @class */ (function (_super) {
    __extends(SimpleSelector, _super);
    function SimpleSelector(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(SimpleSelector.prototype, "type", {
        get: function () {
            return NodeType.SimpleSelector;
        },
        enumerable: false,
        configurable: true
    });
    return SimpleSelector;
}(Node));
export { SimpleSelector };
var AtApplyRule = /** @class */ (function (_super) {
    __extends(AtApplyRule, _super);
    function AtApplyRule(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(AtApplyRule.prototype, "type", {
        get: function () {
            return NodeType.AtApplyRule;
        },
        enumerable: false,
        configurable: true
    });
    AtApplyRule.prototype.setIdentifier = function (node) {
        return this.setNode('identifier', node, 0);
    };
    AtApplyRule.prototype.getIdentifier = function () {
        return this.identifier;
    };
    AtApplyRule.prototype.getName = function () {
        return this.identifier ? this.identifier.getText() : '';
    };
    return AtApplyRule;
}(Node));
export { AtApplyRule };
var AbstractDeclaration = /** @class */ (function (_super) {
    __extends(AbstractDeclaration, _super);
    function AbstractDeclaration(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    return AbstractDeclaration;
}(Node));
export { AbstractDeclaration };
var CustomPropertySet = /** @class */ (function (_super) {
    __extends(CustomPropertySet, _super);
    function CustomPropertySet(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(CustomPropertySet.prototype, "type", {
        get: function () {
            return NodeType.CustomPropertySet;
        },
        enumerable: false,
        configurable: true
    });
    return CustomPropertySet;
}(BodyDeclaration));
export { CustomPropertySet };
var Declaration = /** @class */ (function (_super) {
    __extends(Declaration, _super);
    function Declaration(offset, length) {
        var _this = _super.call(this, offset, length) || this;
        _this.property = null;
        return _this;
    }
    Object.defineProperty(Declaration.prototype, "type", {
        get: function () {
            return NodeType.Declaration;
        },
        enumerable: false,
        configurable: true
    });
    Declaration.prototype.setProperty = function (node) {
        return this.setNode('property', node);
    };
    Declaration.prototype.getProperty = function () {
        return this.property;
    };
    Declaration.prototype.getFullPropertyName = function () {
        var propertyName = this.property ? this.property.getName() : 'unknown';
        if (this.parent instanceof Declarations && this.parent.getParent() instanceof NestedProperties) {
            var parentDecl = this.parent.getParent().getParent();
            if (parentDecl instanceof Declaration) {
                return parentDecl.getFullPropertyName() + propertyName;
            }
        }
        return propertyName;
    };
    Declaration.prototype.getNonPrefixedPropertyName = function () {
        var propertyName = this.getFullPropertyName();
        if (propertyName && propertyName.charAt(0) === '-') {
            var vendorPrefixEnd = propertyName.indexOf('-', 1);
            if (vendorPrefixEnd !== -1) {
                return propertyName.substring(vendorPrefixEnd + 1);
            }
        }
        return propertyName;
    };
    Declaration.prototype.setValue = function (value) {
        return this.setNode('value', value);
    };
    Declaration.prototype.getValue = function () {
        return this.value;
    };
    Declaration.prototype.setNestedProperties = function (value) {
        return this.setNode('nestedProperties', value);
    };
    Declaration.prototype.getNestedProperties = function () {
        return this.nestedProperties;
    };
    return Declaration;
}(AbstractDeclaration));
export { Declaration };
var CustomPropertyDeclaration = /** @class */ (function (_super) {
    __extends(CustomPropertyDeclaration, _super);
    function CustomPropertyDeclaration(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(CustomPropertyDeclaration.prototype, "type", {
        get: function () {
            return NodeType.CustomPropertyDeclaration;
        },
        enumerable: false,
        configurable: true
    });
    CustomPropertyDeclaration.prototype.setPropertySet = function (value) {
        return this.setNode('propertySet', value);
    };
    CustomPropertyDeclaration.prototype.getPropertySet = function () {
        return this.propertySet;
    };
    return CustomPropertyDeclaration;
}(Declaration));
export { CustomPropertyDeclaration };
var Property = /** @class */ (function (_super) {
    __extends(Property, _super);
    function Property(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(Property.prototype, "type", {
        get: function () {
            return NodeType.Property;
        },
        enumerable: false,
        configurable: true
    });
    Property.prototype.setIdentifier = function (value) {
        return this.setNode('identifier', value);
    };
    Property.prototype.getIdentifier = function () {
        return this.identifier;
    };
    Property.prototype.getName = function () {
        return trim(this.getText(), /[_\+]+$/); /* +_: less merge */
    };
    Property.prototype.isCustomProperty = function () {
        return !!this.identifier && this.identifier.isCustomProperty;
    };
    return Property;
}(Node));
export { Property };
var Invocation = /** @class */ (function (_super) {
    __extends(Invocation, _super);
    function Invocation(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(Invocation.prototype, "type", {
        get: function () {
            return NodeType.Invocation;
        },
        enumerable: false,
        configurable: true
    });
    Invocation.prototype.getArguments = function () {
        if (!this.arguments) {
            this.arguments = new Nodelist(this);
        }
        return this.arguments;
    };
    return Invocation;
}(Node));
export { Invocation };
var Function = /** @class */ (function (_super) {
    __extends(Function, _super);
    function Function(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(Function.prototype, "type", {
        get: function () {
            return NodeType.Function;
        },
        enumerable: false,
        configurable: true
    });
    Function.prototype.setIdentifier = function (node) {
        return this.setNode('identifier', node, 0);
    };
    Function.prototype.getIdentifier = function () {
        return this.identifier;
    };
    Function.prototype.getName = function () {
        return this.identifier ? this.identifier.getText() : '';
    };
    return Function;
}(Invocation));
export { Function };
var FunctionParameter = /** @class */ (function (_super) {
    __extends(FunctionParameter, _super);
    function FunctionParameter(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(FunctionParameter.prototype, "type", {
        get: function () {
            return NodeType.FunctionParameter;
        },
        enumerable: false,
        configurable: true
    });
    FunctionParameter.prototype.setIdentifier = function (node) {
        return this.setNode('identifier', node, 0);
    };
    FunctionParameter.prototype.getIdentifier = function () {
        return this.identifier;
    };
    FunctionParameter.prototype.getName = function () {
        return this.identifier ? this.identifier.getText() : '';
    };
    FunctionParameter.prototype.setDefaultValue = function (node) {
        return this.setNode('defaultValue', node, 0);
    };
    FunctionParameter.prototype.getDefaultValue = function () {
        return this.defaultValue;
    };
    return FunctionParameter;
}(Node));
export { FunctionParameter };
var FunctionArgument = /** @class */ (function (_super) {
    __extends(FunctionArgument, _super);
    function FunctionArgument(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(FunctionArgument.prototype, "type", {
        get: function () {
            return NodeType.FunctionArgument;
        },
        enumerable: false,
        configurable: true
    });
    FunctionArgument.prototype.setIdentifier = function (node) {
        return this.setNode('identifier', node, 0);
    };
    FunctionArgument.prototype.getIdentifier = function () {
        return this.identifier;
    };
    FunctionArgument.prototype.getName = function () {
        return this.identifier ? this.identifier.getText() : '';
    };
    FunctionArgument.prototype.setValue = function (node) {
        return this.setNode('value', node, 0);
    };
    FunctionArgument.prototype.getValue = function () {
        return this.value;
    };
    return FunctionArgument;
}(Node));
export { FunctionArgument };
var IfStatement = /** @class */ (function (_super) {
    __extends(IfStatement, _super);
    function IfStatement(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(IfStatement.prototype, "type", {
        get: function () {
            return NodeType.If;
        },
        enumerable: false,
        configurable: true
    });
    IfStatement.prototype.setExpression = function (node) {
        return this.setNode('expression', node, 0);
    };
    IfStatement.prototype.setElseClause = function (elseClause) {
        return this.setNode('elseClause', elseClause);
    };
    return IfStatement;
}(BodyDeclaration));
export { IfStatement };
var ForStatement = /** @class */ (function (_super) {
    __extends(ForStatement, _super);
    function ForStatement(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(ForStatement.prototype, "type", {
        get: function () {
            return NodeType.For;
        },
        enumerable: false,
        configurable: true
    });
    ForStatement.prototype.setVariable = function (node) {
        return this.setNode('variable', node, 0);
    };
    return ForStatement;
}(BodyDeclaration));
export { ForStatement };
var EachStatement = /** @class */ (function (_super) {
    __extends(EachStatement, _super);
    function EachStatement(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(EachStatement.prototype, "type", {
        get: function () {
            return NodeType.Each;
        },
        enumerable: false,
        configurable: true
    });
    EachStatement.prototype.getVariables = function () {
        if (!this.variables) {
            this.variables = new Nodelist(this);
        }
        return this.variables;
    };
    return EachStatement;
}(BodyDeclaration));
export { EachStatement };
var WhileStatement = /** @class */ (function (_super) {
    __extends(WhileStatement, _super);
    function WhileStatement(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(WhileStatement.prototype, "type", {
        get: function () {
            return NodeType.While;
        },
        enumerable: false,
        configurable: true
    });
    return WhileStatement;
}(BodyDeclaration));
export { WhileStatement };
var ElseStatement = /** @class */ (function (_super) {
    __extends(ElseStatement, _super);
    function ElseStatement(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(ElseStatement.prototype, "type", {
        get: function () {
            return NodeType.Else;
        },
        enumerable: false,
        configurable: true
    });
    return ElseStatement;
}(BodyDeclaration));
export { ElseStatement };
var FunctionDeclaration = /** @class */ (function (_super) {
    __extends(FunctionDeclaration, _super);
    function FunctionDeclaration(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(FunctionDeclaration.prototype, "type", {
        get: function () {
            return NodeType.FunctionDeclaration;
        },
        enumerable: false,
        configurable: true
    });
    FunctionDeclaration.prototype.setIdentifier = function (node) {
        return this.setNode('identifier', node, 0);
    };
    FunctionDeclaration.prototype.getIdentifier = function () {
        return this.identifier;
    };
    FunctionDeclaration.prototype.getName = function () {
        return this.identifier ? this.identifier.getText() : '';
    };
    FunctionDeclaration.prototype.getParameters = function () {
        if (!this.parameters) {
            this.parameters = new Nodelist(this);
        }
        return this.parameters;
    };
    return FunctionDeclaration;
}(BodyDeclaration));
export { FunctionDeclaration };
var ViewPort = /** @class */ (function (_super) {
    __extends(ViewPort, _super);
    function ViewPort(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(ViewPort.prototype, "type", {
        get: function () {
            return NodeType.ViewPort;
        },
        enumerable: false,
        configurable: true
    });
    return ViewPort;
}(BodyDeclaration));
export { ViewPort };
var FontFace = /** @class */ (function (_super) {
    __extends(FontFace, _super);
    function FontFace(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(FontFace.prototype, "type", {
        get: function () {
            return NodeType.FontFace;
        },
        enumerable: false,
        configurable: true
    });
    return FontFace;
}(BodyDeclaration));
export { FontFace };
var NestedProperties = /** @class */ (function (_super) {
    __extends(NestedProperties, _super);
    function NestedProperties(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(NestedProperties.prototype, "type", {
        get: function () {
            return NodeType.NestedProperties;
        },
        enumerable: false,
        configurable: true
    });
    return NestedProperties;
}(BodyDeclaration));
export { NestedProperties };
var Keyframe = /** @class */ (function (_super) {
    __extends(Keyframe, _super);
    function Keyframe(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(Keyframe.prototype, "type", {
        get: function () {
            return NodeType.Keyframe;
        },
        enumerable: false,
        configurable: true
    });
    Keyframe.prototype.setKeyword = function (keyword) {
        return this.setNode('keyword', keyword, 0);
    };
    Keyframe.prototype.getKeyword = function () {
        return this.keyword;
    };
    Keyframe.prototype.setIdentifier = function (node) {
        return this.setNode('identifier', node, 0);
    };
    Keyframe.prototype.getIdentifier = function () {
        return this.identifier;
    };
    Keyframe.prototype.getName = function () {
        return this.identifier ? this.identifier.getText() : '';
    };
    return Keyframe;
}(BodyDeclaration));
export { Keyframe };
var KeyframeSelector = /** @class */ (function (_super) {
    __extends(KeyframeSelector, _super);
    function KeyframeSelector(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(KeyframeSelector.prototype, "type", {
        get: function () {
            return NodeType.KeyframeSelector;
        },
        enumerable: false,
        configurable: true
    });
    return KeyframeSelector;
}(BodyDeclaration));
export { KeyframeSelector };
var Import = /** @class */ (function (_super) {
    __extends(Import, _super);
    function Import(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(Import.prototype, "type", {
        get: function () {
            return NodeType.Import;
        },
        enumerable: false,
        configurable: true
    });
    Import.prototype.setMedialist = function (node) {
        if (node) {
            node.attachTo(this);
            return true;
        }
        return false;
    };
    return Import;
}(Node));
export { Import };
var Use = /** @class */ (function (_super) {
    __extends(Use, _super);
    function Use() {
        return _super !== null && _super.apply(this, arguments) || this;
    }
    Object.defineProperty(Use.prototype, "type", {
        get: function () {
            return NodeType.Use;
        },
        enumerable: false,
        configurable: true
    });
    Use.prototype.getParameters = function () {
        if (!this.parameters) {
            this.parameters = new Nodelist(this);
        }
        return this.parameters;
    };
    Use.prototype.setIdentifier = function (node) {
        return this.setNode('identifier', node, 0);
    };
    Use.prototype.getIdentifier = function () {
        return this.identifier;
    };
    return Use;
}(Node));
export { Use };
var ModuleConfiguration = /** @class */ (function (_super) {
    __extends(ModuleConfiguration, _super);
    function ModuleConfiguration() {
        return _super !== null && _super.apply(this, arguments) || this;
    }
    Object.defineProperty(ModuleConfiguration.prototype, "type", {
        get: function () {
            return NodeType.ModuleConfiguration;
        },
        enumerable: false,
        configurable: true
    });
    ModuleConfiguration.prototype.setIdentifier = function (node) {
        return this.setNode('identifier', node, 0);
    };
    ModuleConfiguration.prototype.getIdentifier = function () {
        return this.identifier;
    };
    ModuleConfiguration.prototype.getName = function () {
        return this.identifier ? this.identifier.getText() : '';
    };
    ModuleConfiguration.prototype.setValue = function (node) {
        return this.setNode('value', node, 0);
    };
    ModuleConfiguration.prototype.getValue = function () {
        return this.value;
    };
    return ModuleConfiguration;
}(Node));
export { ModuleConfiguration };
var Forward = /** @class */ (function (_super) {
    __extends(Forward, _super);
    function Forward() {
        return _super !== null && _super.apply(this, arguments) || this;
    }
    Object.defineProperty(Forward.prototype, "type", {
        get: function () {
            return NodeType.Forward;
        },
        enumerable: false,
        configurable: true
    });
    Forward.prototype.setIdentifier = function (node) {
        return this.setNode('identifier', node, 0);
    };
    Forward.prototype.getIdentifier = function () {
        return this.identifier;
    };
    Forward.prototype.getMembers = function () {
        if (!this.members) {
            this.members = new Nodelist(this);
        }
        return this.members;
    };
    Forward.prototype.getParameters = function () {
        if (!this.parameters) {
            this.parameters = new Nodelist(this);
        }
        return this.parameters;
    };
    return Forward;
}(Node));
export { Forward };
var ForwardVisibility = /** @class */ (function (_super) {
    __extends(ForwardVisibility, _super);
    function ForwardVisibility() {
        return _super !== null && _super.apply(this, arguments) || this;
    }
    Object.defineProperty(ForwardVisibility.prototype, "type", {
        get: function () {
            return NodeType.ForwardVisibility;
        },
        enumerable: false,
        configurable: true
    });
    ForwardVisibility.prototype.setIdentifier = function (node) {
        return this.setNode('identifier', node, 0);
    };
    ForwardVisibility.prototype.getIdentifier = function () {
        return this.identifier;
    };
    return ForwardVisibility;
}(Node));
export { ForwardVisibility };
var Namespace = /** @class */ (function (_super) {
    __extends(Namespace, _super);
    function Namespace(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(Namespace.prototype, "type", {
        get: function () {
            return NodeType.Namespace;
        },
        enumerable: false,
        configurable: true
    });
    return Namespace;
}(Node));
export { Namespace };
var Media = /** @class */ (function (_super) {
    __extends(Media, _super);
    function Media(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(Media.prototype, "type", {
        get: function () {
            return NodeType.Media;
        },
        enumerable: false,
        configurable: true
    });
    return Media;
}(BodyDeclaration));
export { Media };
var Supports = /** @class */ (function (_super) {
    __extends(Supports, _super);
    function Supports(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(Supports.prototype, "type", {
        get: function () {
            return NodeType.Supports;
        },
        enumerable: false,
        configurable: true
    });
    return Supports;
}(BodyDeclaration));
export { Supports };
var Document = /** @class */ (function (_super) {
    __extends(Document, _super);
    function Document(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(Document.prototype, "type", {
        get: function () {
            return NodeType.Document;
        },
        enumerable: false,
        configurable: true
    });
    return Document;
}(BodyDeclaration));
export { Document };
var Medialist = /** @class */ (function (_super) {
    __extends(Medialist, _super);
    function Medialist(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Medialist.prototype.getMediums = function () {
        if (!this.mediums) {
            this.mediums = new Nodelist(this);
        }
        return this.mediums;
    };
    return Medialist;
}(Node));
export { Medialist };
var MediaQuery = /** @class */ (function (_super) {
    __extends(MediaQuery, _super);
    function MediaQuery(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(MediaQuery.prototype, "type", {
        get: function () {
            return NodeType.MediaQuery;
        },
        enumerable: false,
        configurable: true
    });
    return MediaQuery;
}(Node));
export { MediaQuery };
var SupportsCondition = /** @class */ (function (_super) {
    __extends(SupportsCondition, _super);
    function SupportsCondition(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(SupportsCondition.prototype, "type", {
        get: function () {
            return NodeType.SupportsCondition;
        },
        enumerable: false,
        configurable: true
    });
    return SupportsCondition;
}(Node));
export { SupportsCondition };
var Page = /** @class */ (function (_super) {
    __extends(Page, _super);
    function Page(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(Page.prototype, "type", {
        get: function () {
            return NodeType.Page;
        },
        enumerable: false,
        configurable: true
    });
    return Page;
}(BodyDeclaration));
export { Page };
var PageBoxMarginBox = /** @class */ (function (_super) {
    __extends(PageBoxMarginBox, _super);
    function PageBoxMarginBox(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(PageBoxMarginBox.prototype, "type", {
        get: function () {
            return NodeType.PageBoxMarginBox;
        },
        enumerable: false,
        configurable: true
    });
    return PageBoxMarginBox;
}(BodyDeclaration));
export { PageBoxMarginBox };
var Expression = /** @class */ (function (_super) {
    __extends(Expression, _super);
    function Expression(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(Expression.prototype, "type", {
        get: function () {
            return NodeType.Expression;
        },
        enumerable: false,
        configurable: true
    });
    return Expression;
}(Node));
export { Expression };
var BinaryExpression = /** @class */ (function (_super) {
    __extends(BinaryExpression, _super);
    function BinaryExpression(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(BinaryExpression.prototype, "type", {
        get: function () {
            return NodeType.BinaryExpression;
        },
        enumerable: false,
        configurable: true
    });
    BinaryExpression.prototype.setLeft = function (left) {
        return this.setNode('left', left);
    };
    BinaryExpression.prototype.getLeft = function () {
        return this.left;
    };
    BinaryExpression.prototype.setRight = function (right) {
        return this.setNode('right', right);
    };
    BinaryExpression.prototype.getRight = function () {
        return this.right;
    };
    BinaryExpression.prototype.setOperator = function (value) {
        return this.setNode('operator', value);
    };
    BinaryExpression.prototype.getOperator = function () {
        return this.operator;
    };
    return BinaryExpression;
}(Node));
export { BinaryExpression };
var Term = /** @class */ (function (_super) {
    __extends(Term, _super);
    function Term(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(Term.prototype, "type", {
        get: function () {
            return NodeType.Term;
        },
        enumerable: false,
        configurable: true
    });
    Term.prototype.setOperator = function (value) {
        return this.setNode('operator', value);
    };
    Term.prototype.getOperator = function () {
        return this.operator;
    };
    Term.prototype.setExpression = function (value) {
        return this.setNode('expression', value);
    };
    Term.prototype.getExpression = function () {
        return this.expression;
    };
    return Term;
}(Node));
export { Term };
var AttributeSelector = /** @class */ (function (_super) {
    __extends(AttributeSelector, _super);
    function AttributeSelector(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(AttributeSelector.prototype, "type", {
        get: function () {
            return NodeType.AttributeSelector;
        },
        enumerable: false,
        configurable: true
    });
    AttributeSelector.prototype.setNamespacePrefix = function (value) {
        return this.setNode('namespacePrefix', value);
    };
    AttributeSelector.prototype.getNamespacePrefix = function () {
        return this.namespacePrefix;
    };
    AttributeSelector.prototype.setIdentifier = function (value) {
        return this.setNode('identifier', value);
    };
    AttributeSelector.prototype.getIdentifier = function () {
        return this.identifier;
    };
    AttributeSelector.prototype.setOperator = function (operator) {
        return this.setNode('operator', operator);
    };
    AttributeSelector.prototype.getOperator = function () {
        return this.operator;
    };
    AttributeSelector.prototype.setValue = function (value) {
        return this.setNode('value', value);
    };
    AttributeSelector.prototype.getValue = function () {
        return this.value;
    };
    return AttributeSelector;
}(Node));
export { AttributeSelector };
var Operator = /** @class */ (function (_super) {
    __extends(Operator, _super);
    function Operator(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(Operator.prototype, "type", {
        get: function () {
            return NodeType.Operator;
        },
        enumerable: false,
        configurable: true
    });
    return Operator;
}(Node));
export { Operator };
var HexColorValue = /** @class */ (function (_super) {
    __extends(HexColorValue, _super);
    function HexColorValue(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(HexColorValue.prototype, "type", {
        get: function () {
            return NodeType.HexColorValue;
        },
        enumerable: false,
        configurable: true
    });
    return HexColorValue;
}(Node));
export { HexColorValue };
var _dot = '.'.charCodeAt(0), _0 = '0'.charCodeAt(0), _9 = '9'.charCodeAt(0);
var NumericValue = /** @class */ (function (_super) {
    __extends(NumericValue, _super);
    function NumericValue(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(NumericValue.prototype, "type", {
        get: function () {
            return NodeType.NumericValue;
        },
        enumerable: false,
        configurable: true
    });
    NumericValue.prototype.getValue = function () {
        var raw = this.getText();
        var unitIdx = 0;
        var code;
        for (var i = 0, len = raw.length; i < len; i++) {
            code = raw.charCodeAt(i);
            if (!(_0 <= code && code <= _9 || code === _dot)) {
                break;
            }
            unitIdx += 1;
        }
        return {
            value: raw.substring(0, unitIdx),
            unit: unitIdx < raw.length ? raw.substring(unitIdx) : undefined
        };
    };
    return NumericValue;
}(Node));
export { NumericValue };
var VariableDeclaration = /** @class */ (function (_super) {
    __extends(VariableDeclaration, _super);
    function VariableDeclaration(offset, length) {
        var _this = _super.call(this, offset, length) || this;
        _this.variable = null;
        _this.value = null;
        _this.needsSemicolon = true;
        return _this;
    }
    Object.defineProperty(VariableDeclaration.prototype, "type", {
        get: function () {
            return NodeType.VariableDeclaration;
        },
        enumerable: false,
        configurable: true
    });
    VariableDeclaration.prototype.setVariable = function (node) {
        if (node) {
            node.attachTo(this);
            this.variable = node;
            return true;
        }
        return false;
    };
    VariableDeclaration.prototype.getVariable = function () {
        return this.variable;
    };
    VariableDeclaration.prototype.getName = function () {
        return this.variable ? this.variable.getName() : '';
    };
    VariableDeclaration.prototype.setValue = function (node) {
        if (node) {
            node.attachTo(this);
            this.value = node;
            return true;
        }
        return false;
    };
    VariableDeclaration.prototype.getValue = function () {
        return this.value;
    };
    return VariableDeclaration;
}(AbstractDeclaration));
export { VariableDeclaration };
var Interpolation = /** @class */ (function (_super) {
    __extends(Interpolation, _super);
    // private _interpolations: void; // workaround for https://github.com/Microsoft/TypeScript/issues/18276
    function Interpolation(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(Interpolation.prototype, "type", {
        get: function () {
            return NodeType.Interpolation;
        },
        enumerable: false,
        configurable: true
    });
    return Interpolation;
}(Node));
export { Interpolation };
var Variable = /** @class */ (function (_super) {
    __extends(Variable, _super);
    function Variable(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(Variable.prototype, "type", {
        get: function () {
            return NodeType.VariableName;
        },
        enumerable: false,
        configurable: true
    });
    Variable.prototype.getName = function () {
        return this.getText();
    };
    return Variable;
}(Node));
export { Variable };
var ExtendsReference = /** @class */ (function (_super) {
    __extends(ExtendsReference, _super);
    function ExtendsReference(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(ExtendsReference.prototype, "type", {
        get: function () {
            return NodeType.ExtendsReference;
        },
        enumerable: false,
        configurable: true
    });
    ExtendsReference.prototype.getSelectors = function () {
        if (!this.selectors) {
            this.selectors = new Nodelist(this);
        }
        return this.selectors;
    };
    return ExtendsReference;
}(Node));
export { ExtendsReference };
var MixinContentReference = /** @class */ (function (_super) {
    __extends(MixinContentReference, _super);
    function MixinContentReference(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(MixinContentReference.prototype, "type", {
        get: function () {
            return NodeType.MixinContentReference;
        },
        enumerable: false,
        configurable: true
    });
    MixinContentReference.prototype.getArguments = function () {
        if (!this.arguments) {
            this.arguments = new Nodelist(this);
        }
        return this.arguments;
    };
    return MixinContentReference;
}(Node));
export { MixinContentReference };
var MixinContentDeclaration = /** @class */ (function (_super) {
    __extends(MixinContentDeclaration, _super);
    function MixinContentDeclaration(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(MixinContentDeclaration.prototype, "type", {
        get: function () {
            return NodeType.MixinContentReference;
        },
        enumerable: false,
        configurable: true
    });
    MixinContentDeclaration.prototype.getParameters = function () {
        if (!this.parameters) {
            this.parameters = new Nodelist(this);
        }
        return this.parameters;
    };
    return MixinContentDeclaration;
}(BodyDeclaration));
export { MixinContentDeclaration };
var MixinReference = /** @class */ (function (_super) {
    __extends(MixinReference, _super);
    function MixinReference(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(MixinReference.prototype, "type", {
        get: function () {
            return NodeType.MixinReference;
        },
        enumerable: false,
        configurable: true
    });
    MixinReference.prototype.getNamespaces = function () {
        if (!this.namespaces) {
            this.namespaces = new Nodelist(this);
        }
        return this.namespaces;
    };
    MixinReference.prototype.setIdentifier = function (node) {
        return this.setNode('identifier', node, 0);
    };
    MixinReference.prototype.getIdentifier = function () {
        return this.identifier;
    };
    MixinReference.prototype.getName = function () {
        return this.identifier ? this.identifier.getText() : '';
    };
    MixinReference.prototype.getArguments = function () {
        if (!this.arguments) {
            this.arguments = new Nodelist(this);
        }
        return this.arguments;
    };
    MixinReference.prototype.setContent = function (node) {
        return this.setNode('content', node);
    };
    MixinReference.prototype.getContent = function () {
        return this.content;
    };
    return MixinReference;
}(Node));
export { MixinReference };
var MixinDeclaration = /** @class */ (function (_super) {
    __extends(MixinDeclaration, _super);
    function MixinDeclaration(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(MixinDeclaration.prototype, "type", {
        get: function () {
            return NodeType.MixinDeclaration;
        },
        enumerable: false,
        configurable: true
    });
    MixinDeclaration.prototype.setIdentifier = function (node) {
        return this.setNode('identifier', node, 0);
    };
    MixinDeclaration.prototype.getIdentifier = function () {
        return this.identifier;
    };
    MixinDeclaration.prototype.getName = function () {
        return this.identifier ? this.identifier.getText() : '';
    };
    MixinDeclaration.prototype.getParameters = function () {
        if (!this.parameters) {
            this.parameters = new Nodelist(this);
        }
        return this.parameters;
    };
    MixinDeclaration.prototype.setGuard = function (node) {
        if (node) {
            node.attachTo(this);
            this.guard = node;
        }
        return false;
    };
    return MixinDeclaration;
}(BodyDeclaration));
export { MixinDeclaration };
var UnknownAtRule = /** @class */ (function (_super) {
    __extends(UnknownAtRule, _super);
    function UnknownAtRule(offset, length) {
        return _super.call(this, offset, length) || this;
    }
    Object.defineProperty(UnknownAtRule.prototype, "type", {
        get: function () {
            return NodeType.UnknownAtRule;
        },
        enumerable: false,
        configurable: true
    });
    UnknownAtRule.prototype.setAtRuleName = function (atRuleName) {
        this.atRuleName = atRuleName;
    };
    UnknownAtRule.prototype.getAtRuleName = function () {
        return this.atRuleName;
    };
    return UnknownAtRule;
}(BodyDeclaration));
export { UnknownAtRule };
var ListEntry = /** @class */ (function (_super) {
    __extends(ListEntry, _super);
    function ListEntry() {
        return _super !== null && _super.apply(this, arguments) || this;
    }
    Object.defineProperty(ListEntry.prototype, "type", {
        get: function () {
            return NodeType.ListEntry;
        },
        enumerable: false,
        configurable: true
    });
    ListEntry.prototype.setKey = function (node) {
        return this.setNode('key', node, 0);
    };
    ListEntry.prototype.setValue = function (node) {
        return this.setNode('value', node, 1);
    };
    return ListEntry;
}(Node));
export { ListEntry };
var LessGuard = /** @class */ (function (_super) {
    __extends(LessGuard, _super);
    function LessGuard() {
        return _super !== null && _super.apply(this, arguments) || this;
    }
    LessGuard.prototype.getConditions = function () {
        if (!this.conditions) {
            this.conditions = new Nodelist(this);
        }
        return this.conditions;
    };
    return LessGuard;
}(Node));
export { LessGuard };
var GuardCondition = /** @class */ (function (_super) {
    __extends(GuardCondition, _super);
    function GuardCondition() {
        return _super !== null && _super.apply(this, arguments) || this;
    }
    GuardCondition.prototype.setVariable = function (node) {
        return this.setNode('variable', node);
    };
    return GuardCondition;
}(Node));
export { GuardCondition };
var Module = /** @class */ (function (_super) {
    __extends(Module, _super);
    function Module() {
        return _super !== null && _super.apply(this, arguments) || this;
    }
    Object.defineProperty(Module.prototype, "type", {
        get: function () {
            return NodeType.Module;
        },
        enumerable: false,
        configurable: true
    });
    Module.prototype.setIdentifier = function (node) {
        return this.setNode('identifier', node, 0);
    };
    Module.prototype.getIdentifier = function () {
        return this.identifier;
    };
    return Module;
}(Node));
export { Module };
export var Level;
(function (Level) {
    Level[Level["Ignore"] = 1] = "Ignore";
    Level[Level["Warning"] = 2] = "Warning";
    Level[Level["Error"] = 4] = "Error";
})(Level || (Level = {}));
var Marker = /** @class */ (function () {
    function Marker(node, rule, level, message, offset, length) {
        if (offset === void 0) { offset = node.offset; }
        if (length === void 0) { length = node.length; }
        this.node = node;
        this.rule = rule;
        this.level = level;
        this.message = message || rule.message;
        this.offset = offset;
        this.length = length;
    }
    Marker.prototype.getRule = function () {
        return this.rule;
    };
    Marker.prototype.getLevel = function () {
        return this.level;
    };
    Marker.prototype.getOffset = function () {
        return this.offset;
    };
    Marker.prototype.getLength = function () {
        return this.length;
    };
    Marker.prototype.getNode = function () {
        return this.node;
    };
    Marker.prototype.getMessage = function () {
        return this.message;
    };
    return Marker;
}());
export { Marker };
/*
export class DefaultVisitor implements IVisitor {

    public visitNode(node:Node):boolean {
        switch (node.type) {
            case NodeType.Stylesheet:
                return this.visitStylesheet(<Stylesheet> node);
            case NodeType.FontFace:
                return this.visitFontFace(<FontFace> node);
            case NodeType.Ruleset:
                return this.visitRuleSet(<RuleSet> node);
            case NodeType.Selector:
                return this.visitSelector(<Selector> node);
            case NodeType.SimpleSelector:
                return this.visitSimpleSelector(<SimpleSelector> node);
            case NodeType.Declaration:
                return this.visitDeclaration(<Declaration> node);
            case NodeType.Function:
                return this.visitFunction(<Function> node);
            case NodeType.FunctionDeclaration:
                return this.visitFunctionDeclaration(<FunctionDeclaration> node);
            case NodeType.FunctionParameter:
                return this.visitFunctionParameter(<FunctionParameter> node);
            case NodeType.FunctionArgument:
                return this.visitFunctionArgument(<FunctionArgument> node);
            case NodeType.Term:
                return this.visitTerm(<Term> node);
            case NodeType.Declaration:
                return this.visitExpression(<Expression> node);
            case NodeType.NumericValue:
                return this.visitNumericValue(<NumericValue> node);
            case NodeType.Page:
                return this.visitPage(<Page> node);
            case NodeType.PageBoxMarginBox:
                return this.visitPageBoxMarginBox(<PageBoxMarginBox> node);
            case NodeType.Property:
                return this.visitProperty(<Property> node);
            case NodeType.NumericValue:
                return this.visitNodelist(<Nodelist> node);
            case NodeType.Import:
                return this.visitImport(<Import> node);
            case NodeType.Namespace:
                return this.visitNamespace(<Namespace> node);
            case NodeType.Keyframe:
                return this.visitKeyframe(<Keyframe> node);
            case NodeType.KeyframeSelector:
                return this.visitKeyframeSelector(<KeyframeSelector> node);
            case NodeType.MixinDeclaration:
                return this.visitMixinDeclaration(<MixinDeclaration> node);
            case NodeType.MixinReference:
                return this.visitMixinReference(<MixinReference> node);
            case NodeType.Variable:
                return this.visitVariable(<Variable> node);
            case NodeType.VariableDeclaration:
                return this.visitVariableDeclaration(<VariableDeclaration> node);
        }
        return this.visitUnknownNode(node);
    }

    public visitFontFace(node:FontFace):boolean {
        return true;
    }

    public visitKeyframe(node:Keyframe):boolean {
        return true;
    }

    public visitKeyframeSelector(node:KeyframeSelector):boolean {
        return true;
    }

    public visitStylesheet(node:Stylesheet):boolean {
        return true;
    }

    public visitProperty(Node:Property):boolean {
        return true;
    }

    public visitRuleSet(node:RuleSet):boolean {
        return true;
    }

    public visitSelector(node:Selector):boolean {
        return true;
    }

    public visitSimpleSelector(node:SimpleSelector):boolean {
        return true;
    }

    public visitDeclaration(node:Declaration):boolean {
        return true;
    }

    public visitFunction(node:Function):boolean {
        return true;
    }

    public visitFunctionDeclaration(node:FunctionDeclaration):boolean {
        return true;
    }

    public visitInvocation(node:Invocation):boolean {
        return true;
    }

    public visitTerm(node:Term):boolean {
        return true;
    }

    public visitImport(node:Import):boolean {
        return true;
    }

    public visitNamespace(node:Namespace):boolean {
        return true;
    }

    public visitExpression(node:Expression):boolean {
        return true;
    }

    public visitNumericValue(node:NumericValue):boolean {
        return true;
    }

    public visitPage(node:Page):boolean {
        return true;
    }

    public visitPageBoxMarginBox(node:PageBoxMarginBox):boolean {
        return true;
    }

    public visitNodelist(node:Nodelist):boolean {
        return true;
    }

    public visitVariableDeclaration(node:VariableDeclaration):boolean {
        return true;
    }

    public visitVariable(node:Variable):boolean {
        return true;
    }

    public visitMixinDeclaration(node:MixinDeclaration):boolean {
        return true;
    }

    public visitMixinReference(node:MixinReference):boolean {
        return true;
    }

    public visitUnknownNode(node:Node):boolean {
        return true;
    }
}
*/
var ParseErrorCollector = /** @class */ (function () {
    function ParseErrorCollector() {
        this.entries = [];
    }
    ParseErrorCollector.entries = function (node) {
        var visitor = new ParseErrorCollector();
        node.acceptVisitor(visitor);
        return visitor.entries;
    };
    ParseErrorCollector.prototype.visitNode = function (node) {
        if (node.isErroneous()) {
            node.collectIssues(this.entries);
        }
        return true;
    };
    return ParseErrorCollector;
}());
export { ParseErrorCollector };
