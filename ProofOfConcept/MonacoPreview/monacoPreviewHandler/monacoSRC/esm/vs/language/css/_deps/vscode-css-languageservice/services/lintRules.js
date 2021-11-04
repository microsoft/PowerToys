/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
'use strict';
import * as nodes from '../parser/cssNodes.js';
import * as nls from './../../../fillers/vscode-nls.js';
var localize = nls.loadMessageBundle();
var Warning = nodes.Level.Warning;
var Error = nodes.Level.Error;
var Ignore = nodes.Level.Ignore;
var Rule = /** @class */ (function () {
    function Rule(id, message, defaultValue) {
        this.id = id;
        this.message = message;
        this.defaultValue = defaultValue;
        // nothing to do
    }
    return Rule;
}());
export { Rule };
var Setting = /** @class */ (function () {
    function Setting(id, message, defaultValue) {
        this.id = id;
        this.message = message;
        this.defaultValue = defaultValue;
        // nothing to do
    }
    return Setting;
}());
export { Setting };
export var Rules = {
    AllVendorPrefixes: new Rule('compatibleVendorPrefixes', localize('rule.vendorprefixes.all', "When using a vendor-specific prefix make sure to also include all other vendor-specific properties"), Ignore),
    IncludeStandardPropertyWhenUsingVendorPrefix: new Rule('vendorPrefix', localize('rule.standardvendorprefix.all', "When using a vendor-specific prefix also include the standard property"), Warning),
    DuplicateDeclarations: new Rule('duplicateProperties', localize('rule.duplicateDeclarations', "Do not use duplicate style definitions"), Ignore),
    EmptyRuleSet: new Rule('emptyRules', localize('rule.emptyRuleSets', "Do not use empty rulesets"), Warning),
    ImportStatemement: new Rule('importStatement', localize('rule.importDirective', "Import statements do not load in parallel"), Ignore),
    BewareOfBoxModelSize: new Rule('boxModel', localize('rule.bewareOfBoxModelSize', "Do not use width or height when using padding or border"), Ignore),
    UniversalSelector: new Rule('universalSelector', localize('rule.universalSelector', "The universal selector (*) is known to be slow"), Ignore),
    ZeroWithUnit: new Rule('zeroUnits', localize('rule.zeroWidthUnit', "No unit for zero needed"), Ignore),
    RequiredPropertiesForFontFace: new Rule('fontFaceProperties', localize('rule.fontFaceProperties', "@font-face rule must define 'src' and 'font-family' properties"), Warning),
    HexColorLength: new Rule('hexColorLength', localize('rule.hexColor', "Hex colors must consist of three, four, six or eight hex numbers"), Error),
    ArgsInColorFunction: new Rule('argumentsInColorFunction', localize('rule.colorFunction', "Invalid number of parameters"), Error),
    UnknownProperty: new Rule('unknownProperties', localize('rule.unknownProperty', "Unknown property."), Warning),
    UnknownAtRules: new Rule('unknownAtRules', localize('rule.unknownAtRules', "Unknown at-rule."), Warning),
    IEStarHack: new Rule('ieHack', localize('rule.ieHack', "IE hacks are only necessary when supporting IE7 and older"), Ignore),
    UnknownVendorSpecificProperty: new Rule('unknownVendorSpecificProperties', localize('rule.unknownVendorSpecificProperty', "Unknown vendor specific property."), Ignore),
    PropertyIgnoredDueToDisplay: new Rule('propertyIgnoredDueToDisplay', localize('rule.propertyIgnoredDueToDisplay', "Property is ignored due to the display."), Warning),
    AvoidImportant: new Rule('important', localize('rule.avoidImportant', "Avoid using !important. It is an indication that the specificity of the entire CSS has gotten out of control and needs to be refactored."), Ignore),
    AvoidFloat: new Rule('float', localize('rule.avoidFloat', "Avoid using 'float'. Floats lead to fragile CSS that is easy to break if one aspect of the layout changes."), Ignore),
    AvoidIdSelector: new Rule('idSelector', localize('rule.avoidIdSelector', "Selectors should not contain IDs because these rules are too tightly coupled with the HTML."), Ignore),
};
export var Settings = {
    ValidProperties: new Setting('validProperties', localize('rule.validProperties', "A list of properties that are not validated against the `unknownProperties` rule."), [])
};
var LintConfigurationSettings = /** @class */ (function () {
    function LintConfigurationSettings(conf) {
        if (conf === void 0) { conf = {}; }
        this.conf = conf;
    }
    LintConfigurationSettings.prototype.getRule = function (rule) {
        if (this.conf.hasOwnProperty(rule.id)) {
            var level = toLevel(this.conf[rule.id]);
            if (level) {
                return level;
            }
        }
        return rule.defaultValue;
    };
    LintConfigurationSettings.prototype.getSetting = function (setting) {
        return this.conf[setting.id];
    };
    return LintConfigurationSettings;
}());
export { LintConfigurationSettings };
function toLevel(level) {
    switch (level) {
        case 'ignore': return nodes.Level.Ignore;
        case 'warning': return nodes.Level.Warning;
        case 'error': return nodes.Level.Error;
    }
    return null;
}
