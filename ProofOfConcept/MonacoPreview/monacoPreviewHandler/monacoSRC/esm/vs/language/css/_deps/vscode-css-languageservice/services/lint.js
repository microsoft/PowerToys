/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
'use strict';
import * as nls from './../../../fillers/vscode-nls.js';
import * as languageFacts from '../languageFacts/facts.js';
import * as nodes from '../parser/cssNodes.js';
import { union } from '../utils/arrays.js';
import { Rules, Settings } from './lintRules.js';
import calculateBoxModel, { Element } from './lintUtil.js';
var localize = nls.loadMessageBundle();
var NodesByRootMap = /** @class */ (function () {
    function NodesByRootMap() {
        this.data = {};
    }
    NodesByRootMap.prototype.add = function (root, name, node) {
        var entry = this.data[root];
        if (!entry) {
            entry = { nodes: [], names: [] };
            this.data[root] = entry;
        }
        entry.names.push(name);
        if (node) {
            entry.nodes.push(node);
        }
    };
    return NodesByRootMap;
}());
var LintVisitor = /** @class */ (function () {
    function LintVisitor(document, settings, cssDataManager) {
        var _this = this;
        this.cssDataManager = cssDataManager;
        this.warnings = [];
        this.settings = settings;
        this.documentText = document.getText();
        this.keyframes = new NodesByRootMap();
        this.validProperties = {};
        var properties = settings.getSetting(Settings.ValidProperties);
        if (Array.isArray(properties)) {
            properties.forEach(function (p) {
                if (typeof p === 'string') {
                    var name = p.trim().toLowerCase();
                    if (name.length) {
                        _this.validProperties[name] = true;
                    }
                }
            });
        }
    }
    LintVisitor.entries = function (node, document, settings, cssDataManager, entryFilter) {
        var visitor = new LintVisitor(document, settings, cssDataManager);
        node.acceptVisitor(visitor);
        visitor.completeValidations();
        return visitor.getEntries(entryFilter);
    };
    LintVisitor.prototype.isValidPropertyDeclaration = function (element) {
        var propertyName = element.fullPropertyName;
        return this.validProperties[propertyName];
    };
    LintVisitor.prototype.fetch = function (input, s) {
        var elements = [];
        for (var _i = 0, input_1 = input; _i < input_1.length; _i++) {
            var curr = input_1[_i];
            if (curr.fullPropertyName === s) {
                elements.push(curr);
            }
        }
        return elements;
    };
    LintVisitor.prototype.fetchWithValue = function (input, s, v) {
        var elements = [];
        for (var _i = 0, input_2 = input; _i < input_2.length; _i++) {
            var inputElement = input_2[_i];
            if (inputElement.fullPropertyName === s) {
                var expression = inputElement.node.getValue();
                if (expression && this.findValueInExpression(expression, v)) {
                    elements.push(inputElement);
                }
            }
        }
        return elements;
    };
    LintVisitor.prototype.findValueInExpression = function (expression, v) {
        var found = false;
        expression.accept(function (node) {
            if (node.type === nodes.NodeType.Identifier && node.matches(v)) {
                found = true;
            }
            return !found;
        });
        return found;
    };
    LintVisitor.prototype.getEntries = function (filter) {
        if (filter === void 0) { filter = (nodes.Level.Warning | nodes.Level.Error); }
        return this.warnings.filter(function (entry) {
            return (entry.getLevel() & filter) !== 0;
        });
    };
    LintVisitor.prototype.addEntry = function (node, rule, details) {
        var entry = new nodes.Marker(node, rule, this.settings.getRule(rule), details);
        this.warnings.push(entry);
    };
    LintVisitor.prototype.getMissingNames = function (expected, actual) {
        var expectedClone = expected.slice(0); // clone
        for (var i = 0; i < actual.length; i++) {
            var k = expectedClone.indexOf(actual[i]);
            if (k !== -1) {
                expectedClone[k] = null;
            }
        }
        var result = null;
        for (var i = 0; i < expectedClone.length; i++) {
            var curr = expectedClone[i];
            if (curr) {
                if (result === null) {
                    result = localize('namelist.single', "'{0}'", curr);
                }
                else {
                    result = localize('namelist.concatenated', "{0}, '{1}'", result, curr);
                }
            }
        }
        return result;
    };
    LintVisitor.prototype.visitNode = function (node) {
        switch (node.type) {
            case nodes.NodeType.UnknownAtRule:
                return this.visitUnknownAtRule(node);
            case nodes.NodeType.Keyframe:
                return this.visitKeyframe(node);
            case nodes.NodeType.FontFace:
                return this.visitFontFace(node);
            case nodes.NodeType.Ruleset:
                return this.visitRuleSet(node);
            case nodes.NodeType.SimpleSelector:
                return this.visitSimpleSelector(node);
            case nodes.NodeType.Function:
                return this.visitFunction(node);
            case nodes.NodeType.NumericValue:
                return this.visitNumericValue(node);
            case nodes.NodeType.Import:
                return this.visitImport(node);
            case nodes.NodeType.HexColorValue:
                return this.visitHexColorValue(node);
            case nodes.NodeType.Prio:
                return this.visitPrio(node);
            case nodes.NodeType.IdentifierSelector:
                return this.visitIdentifierSelector(node);
        }
        return true;
    };
    LintVisitor.prototype.completeValidations = function () {
        this.validateKeyframes();
    };
    LintVisitor.prototype.visitUnknownAtRule = function (node) {
        var atRuleName = node.getChild(0);
        if (!atRuleName) {
            return false;
        }
        var atDirective = this.cssDataManager.getAtDirective(atRuleName.getText());
        if (atDirective) {
            return false;
        }
        this.addEntry(atRuleName, Rules.UnknownAtRules, "Unknown at rule " + atRuleName.getText());
        return true;
    };
    LintVisitor.prototype.visitKeyframe = function (node) {
        var keyword = node.getKeyword();
        if (!keyword) {
            return false;
        }
        var text = keyword.getText();
        this.keyframes.add(node.getName(), text, (text !== '@keyframes') ? keyword : null);
        return true;
    };
    LintVisitor.prototype.validateKeyframes = function () {
        // @keyframe and it's vendor specific alternatives
        // @keyframe should be included
        var expected = ['@-webkit-keyframes', '@-moz-keyframes', '@-o-keyframes'];
        for (var name in this.keyframes.data) {
            var actual = this.keyframes.data[name].names;
            var needsStandard = (actual.indexOf('@keyframes') === -1);
            if (!needsStandard && actual.length === 1) {
                continue; // only the non-vendor specific keyword is used, that's fine, no warning
            }
            var missingVendorSpecific = this.getMissingNames(expected, actual);
            if (missingVendorSpecific || needsStandard) {
                for (var _i = 0, _a = this.keyframes.data[name].nodes; _i < _a.length; _i++) {
                    var node = _a[_i];
                    if (needsStandard) {
                        var message = localize('keyframes.standardrule.missing', "Always define standard rule '@keyframes' when defining keyframes.");
                        this.addEntry(node, Rules.IncludeStandardPropertyWhenUsingVendorPrefix, message);
                    }
                    if (missingVendorSpecific) {
                        var message = localize('keyframes.vendorspecific.missing', "Always include all vendor specific rules: Missing: {0}", missingVendorSpecific);
                        this.addEntry(node, Rules.AllVendorPrefixes, message);
                    }
                }
            }
        }
        return true;
    };
    LintVisitor.prototype.visitSimpleSelector = function (node) {
        /////////////////////////////////////////////////////////////
        //	Lint - The universal selector (*) is known to be slow.
        /////////////////////////////////////////////////////////////
        var firstChar = this.documentText.charAt(node.offset);
        if (node.length === 1 && firstChar === '*') {
            this.addEntry(node, Rules.UniversalSelector);
        }
        return true;
    };
    LintVisitor.prototype.visitIdentifierSelector = function (node) {
        /////////////////////////////////////////////////////////////
        //	Lint - Avoid id selectors
        /////////////////////////////////////////////////////////////
        this.addEntry(node, Rules.AvoidIdSelector);
        return true;
    };
    LintVisitor.prototype.visitImport = function (node) {
        /////////////////////////////////////////////////////////////
        //	Lint - Import statements shouldn't be used, because they aren't offering parallel downloads.
        /////////////////////////////////////////////////////////////
        this.addEntry(node, Rules.ImportStatemement);
        return true;
    };
    LintVisitor.prototype.visitRuleSet = function (node) {
        /////////////////////////////////////////////////////////////
        //	Lint - Don't use empty rulesets.
        /////////////////////////////////////////////////////////////
        var declarations = node.getDeclarations();
        if (!declarations) {
            // syntax error
            return false;
        }
        if (!declarations.hasChildren()) {
            this.addEntry(node.getSelectors(), Rules.EmptyRuleSet);
        }
        var propertyTable = [];
        for (var _i = 0, _a = declarations.getChildren(); _i < _a.length; _i++) {
            var element = _a[_i];
            if (element instanceof nodes.Declaration) {
                propertyTable.push(new Element(element));
            }
        }
        /////////////////////////////////////////////////////////////
        // the rule warns when it finds:
        // width being used with border, border-left, border-right, padding, padding-left, or padding-right
        // height being used with border, border-top, border-bottom, padding, padding-top, or padding-bottom
        // No error when box-sizing property is specified, as it assumes the user knows what he's doing.
        // see https://github.com/CSSLint/csslint/wiki/Beware-of-box-model-size
        /////////////////////////////////////////////////////////////
        var boxModel = calculateBoxModel(propertyTable);
        if (boxModel.width) {
            var properties = [];
            if (boxModel.right.value) {
                properties = union(properties, boxModel.right.properties);
            }
            if (boxModel.left.value) {
                properties = union(properties, boxModel.left.properties);
            }
            if (properties.length !== 0) {
                for (var _b = 0, properties_1 = properties; _b < properties_1.length; _b++) {
                    var item = properties_1[_b];
                    this.addEntry(item.node, Rules.BewareOfBoxModelSize);
                }
                this.addEntry(boxModel.width.node, Rules.BewareOfBoxModelSize);
            }
        }
        if (boxModel.height) {
            var properties = [];
            if (boxModel.top.value) {
                properties = union(properties, boxModel.top.properties);
            }
            if (boxModel.bottom.value) {
                properties = union(properties, boxModel.bottom.properties);
            }
            if (properties.length !== 0) {
                for (var _c = 0, properties_2 = properties; _c < properties_2.length; _c++) {
                    var item = properties_2[_c];
                    this.addEntry(item.node, Rules.BewareOfBoxModelSize);
                }
                this.addEntry(boxModel.height.node, Rules.BewareOfBoxModelSize);
            }
        }
        /////////////////////////////////////////////////////////////
        //	Properties ignored due to display
        /////////////////////////////////////////////////////////////
        // With 'display: inline-block', 'float' has no effect
        var displayElems = this.fetchWithValue(propertyTable, 'display', 'inline-block');
        if (displayElems.length > 0) {
            var elem = this.fetch(propertyTable, 'float');
            for (var index = 0; index < elem.length; index++) {
                var node_1 = elem[index].node;
                var value = node_1.getValue();
                if (value && !value.matches('none')) {
                    this.addEntry(node_1, Rules.PropertyIgnoredDueToDisplay, localize('rule.propertyIgnoredDueToDisplayInlineBlock', "inline-block is ignored due to the float. If 'float' has a value other than 'none', the box is floated and 'display' is treated as 'block'"));
                }
            }
        }
        // With 'display: block', 'vertical-align' has no effect
        displayElems = this.fetchWithValue(propertyTable, 'display', 'block');
        if (displayElems.length > 0) {
            var elem = this.fetch(propertyTable, 'vertical-align');
            for (var index = 0; index < elem.length; index++) {
                this.addEntry(elem[index].node, Rules.PropertyIgnoredDueToDisplay, localize('rule.propertyIgnoredDueToDisplayBlock', "Property is ignored due to the display. With 'display: block', vertical-align should not be used."));
            }
        }
        /////////////////////////////////////////////////////////////
        //	Avoid 'float'
        /////////////////////////////////////////////////////////////
        var elements = this.fetch(propertyTable, 'float');
        for (var index = 0; index < elements.length; index++) {
            var element = elements[index];
            if (!this.isValidPropertyDeclaration(element)) {
                this.addEntry(element.node, Rules.AvoidFloat);
            }
        }
        /////////////////////////////////////////////////////////////
        //	Don't use duplicate declarations.
        /////////////////////////////////////////////////////////////
        for (var i = 0; i < propertyTable.length; i++) {
            var element = propertyTable[i];
            if (element.fullPropertyName !== 'background' && !this.validProperties[element.fullPropertyName]) {
                var value = element.node.getValue();
                if (value && this.documentText.charAt(value.offset) !== '-') {
                    var elements_1 = this.fetch(propertyTable, element.fullPropertyName);
                    if (elements_1.length > 1) {
                        for (var k = 0; k < elements_1.length; k++) {
                            var value_1 = elements_1[k].node.getValue();
                            if (value_1 && this.documentText.charAt(value_1.offset) !== '-' && elements_1[k] !== element) {
                                this.addEntry(element.node, Rules.DuplicateDeclarations);
                            }
                        }
                    }
                }
            }
        }
        /////////////////////////////////////////////////////////////
        //	Unknown propery & When using a vendor-prefixed gradient, make sure to use them all.
        /////////////////////////////////////////////////////////////
        var isExportBlock = node.getSelectors().matches(":export");
        if (!isExportBlock) {
            var propertiesBySuffix = new NodesByRootMap();
            var containsUnknowns = false;
            for (var _d = 0, propertyTable_1 = propertyTable; _d < propertyTable_1.length; _d++) {
                var element = propertyTable_1[_d];
                var decl = element.node;
                if (this.isCSSDeclaration(decl)) {
                    var name = element.fullPropertyName;
                    var firstChar = name.charAt(0);
                    if (firstChar === '-') {
                        if (name.charAt(1) !== '-') { // avoid css variables
                            if (!this.cssDataManager.isKnownProperty(name) && !this.validProperties[name]) {
                                this.addEntry(decl.getProperty(), Rules.UnknownVendorSpecificProperty);
                            }
                            var nonPrefixedName = decl.getNonPrefixedPropertyName();
                            propertiesBySuffix.add(nonPrefixedName, name, decl.getProperty());
                        }
                    }
                    else {
                        var fullName = name;
                        if (firstChar === '*' || firstChar === '_') {
                            this.addEntry(decl.getProperty(), Rules.IEStarHack);
                            name = name.substr(1);
                        }
                        // _property and *property might be contributed via custom data
                        if (!this.cssDataManager.isKnownProperty(fullName) && !this.cssDataManager.isKnownProperty(name)) {
                            if (!this.validProperties[name]) {
                                this.addEntry(decl.getProperty(), Rules.UnknownProperty, localize('property.unknownproperty.detailed', "Unknown property: '{0}'", decl.getFullPropertyName()));
                            }
                        }
                        propertiesBySuffix.add(name, name, null); // don't pass the node as we don't show errors on the standard
                    }
                }
                else {
                    containsUnknowns = true;
                }
            }
            if (!containsUnknowns) { // don't perform this test if there are
                for (var suffix in propertiesBySuffix.data) {
                    var entry = propertiesBySuffix.data[suffix];
                    var actual = entry.names;
                    var needsStandard = this.cssDataManager.isStandardProperty(suffix) && (actual.indexOf(suffix) === -1);
                    if (!needsStandard && actual.length === 1) {
                        continue; // only the non-vendor specific rule is used, that's fine, no warning
                    }
                    var expected = [];
                    for (var i = 0, len = LintVisitor.prefixes.length; i < len; i++) {
                        var prefix = LintVisitor.prefixes[i];
                        if (this.cssDataManager.isStandardProperty(prefix + suffix)) {
                            expected.push(prefix + suffix);
                        }
                    }
                    var missingVendorSpecific = this.getMissingNames(expected, actual);
                    if (missingVendorSpecific || needsStandard) {
                        for (var _e = 0, _f = entry.nodes; _e < _f.length; _e++) {
                            var node_2 = _f[_e];
                            if (needsStandard) {
                                var message = localize('property.standard.missing', "Also define the standard property '{0}' for compatibility", suffix);
                                this.addEntry(node_2, Rules.IncludeStandardPropertyWhenUsingVendorPrefix, message);
                            }
                            if (missingVendorSpecific) {
                                var message = localize('property.vendorspecific.missing', "Always include all vendor specific properties: Missing: {0}", missingVendorSpecific);
                                this.addEntry(node_2, Rules.AllVendorPrefixes, message);
                            }
                        }
                    }
                }
            }
        }
        return true;
    };
    LintVisitor.prototype.visitPrio = function (node) {
        /////////////////////////////////////////////////////////////
        //	Don't use !important
        /////////////////////////////////////////////////////////////
        this.addEntry(node, Rules.AvoidImportant);
        return true;
    };
    LintVisitor.prototype.visitNumericValue = function (node) {
        /////////////////////////////////////////////////////////////
        //	0 has no following unit
        /////////////////////////////////////////////////////////////
        var funcDecl = node.findParent(nodes.NodeType.Function);
        if (funcDecl && funcDecl.getName() === 'calc') {
            return true;
        }
        var decl = node.findParent(nodes.NodeType.Declaration);
        if (decl) {
            var declValue = decl.getValue();
            if (declValue) {
                var value = node.getValue();
                if (!value.unit || languageFacts.units.length.indexOf(value.unit.toLowerCase()) === -1) {
                    return true;
                }
                if (parseFloat(value.value) === 0.0 && !!value.unit && !this.validProperties[decl.getFullPropertyName()]) {
                    this.addEntry(node, Rules.ZeroWithUnit);
                }
            }
        }
        return true;
    };
    LintVisitor.prototype.visitFontFace = function (node) {
        var declarations = node.getDeclarations();
        if (!declarations) {
            // syntax error
            return false;
        }
        var definesSrc = false, definesFontFamily = false;
        var containsUnknowns = false;
        for (var _i = 0, _a = declarations.getChildren(); _i < _a.length; _i++) {
            var node_3 = _a[_i];
            if (this.isCSSDeclaration(node_3)) {
                var name = node_3.getProperty().getName().toLowerCase();
                if (name === 'src') {
                    definesSrc = true;
                }
                if (name === 'font-family') {
                    definesFontFamily = true;
                }
            }
            else {
                containsUnknowns = true;
            }
        }
        if (!containsUnknowns && (!definesSrc || !definesFontFamily)) {
            this.addEntry(node, Rules.RequiredPropertiesForFontFace);
        }
        return true;
    };
    LintVisitor.prototype.isCSSDeclaration = function (node) {
        if (node instanceof nodes.Declaration) {
            if (!node.getValue()) {
                return false;
            }
            var property = node.getProperty();
            if (!property) {
                return false;
            }
            var identifier = property.getIdentifier();
            if (!identifier || identifier.containsInterpolation()) {
                return false;
            }
            return true;
        }
        return false;
    };
    LintVisitor.prototype.visitHexColorValue = function (node) {
        // Rule: #eeff0011 or #eeff00 or #ef01 or #ef0
        var length = node.length;
        if (length !== 9 && length !== 7 && length !== 5 && length !== 4) {
            this.addEntry(node, Rules.HexColorLength);
        }
        return false;
    };
    LintVisitor.prototype.visitFunction = function (node) {
        var fnName = node.getName().toLowerCase();
        var expectedAttrCount = -1;
        var actualAttrCount = 0;
        switch (fnName) {
            case 'rgb(':
            case 'hsl(':
                expectedAttrCount = 3;
                break;
            case 'rgba(':
            case 'hsla(':
                expectedAttrCount = 4;
                break;
        }
        if (expectedAttrCount !== -1) {
            node.getArguments().accept(function (n) {
                if (n instanceof nodes.BinaryExpression) {
                    actualAttrCount += 1;
                    return false;
                }
                return true;
            });
            if (actualAttrCount !== expectedAttrCount) {
                this.addEntry(node, Rules.ArgsInColorFunction);
            }
        }
        return true;
    };
    LintVisitor.prefixes = [
        '-ms-', '-moz-', '-o-', '-webkit-',
    ];
    return LintVisitor;
}());
export { LintVisitor };
