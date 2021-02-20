/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import * as platform from '../../registry/common/platform.js';
import { ThemeIcon } from './themeService.js';
import { Emitter } from '../../../base/common/event.js';
import { localize } from '../../../nls.js';
import { Extensions as JSONExtensions } from '../../jsonschemas/common/jsonContributionRegistry.js';
import { RunOnceScheduler } from '../../../base/common/async.js';
import * as Codicons from '../../../base/common/codicons.js';
//  ------ API types
// color registry
export const Extensions = {
    IconContribution: 'base.contributions.icons'
};
class IconRegistry {
    constructor() {
        this._onDidChange = new Emitter();
        this.onDidChange = this._onDidChange.event;
        this.iconSchema = {
            definitions: {
                icons: {
                    type: 'object',
                    properties: {
                        fontId: { type: 'string', description: localize('iconDefintion.fontId', 'The id of the font to use. If not set, the font that is defined first is used.') },
                        fontCharacter: { type: 'string', description: localize('iconDefintion.fontCharacter', 'The font character associated with the icon definition.') }
                    },
                    additionalProperties: false,
                    defaultSnippets: [{ body: { fontCharacter: '\\\\e030' } }]
                }
            },
            type: 'object',
            properties: {}
        };
        this.iconReferenceSchema = { type: 'string', enum: [], enumDescriptions: [] };
        this.iconsById = {};
    }
    registerIcon(id, defaults, description, deprecationMessage) {
        const existing = this.iconsById[id];
        if (existing) {
            if (description && !existing.description) {
                existing.description = description;
                this.iconSchema.properties[id].markdownDescription = `${description} $(${id})`;
                const enumIndex = this.iconReferenceSchema.enum.indexOf(id);
                if (enumIndex !== -1) {
                    this.iconReferenceSchema.enumDescriptions[enumIndex] = description;
                }
                this._onDidChange.fire();
            }
            return existing;
        }
        let iconContribution = { id, description, defaults, deprecationMessage };
        this.iconsById[id] = iconContribution;
        let propertySchema = { $ref: '#/definitions/icons' };
        if (deprecationMessage) {
            propertySchema.deprecationMessage = deprecationMessage;
        }
        if (description) {
            propertySchema.markdownDescription = `${description}: $(${id})`;
        }
        this.iconSchema.properties[id] = propertySchema;
        this.iconReferenceSchema.enum.push(id);
        this.iconReferenceSchema.enumDescriptions.push(description || '');
        this._onDidChange.fire();
        return { id };
    }
    getIconSchema() {
        return this.iconSchema;
    }
    getCSS() {
        const rules = [];
        for (let id in this.iconsById) {
            const rule = this.formatRule(id);
            if (rule) {
                rules.push(rule);
            }
        }
        return rules.join('\n');
    }
    formatRule(id) {
        let definition = this.iconsById[id].defaults;
        while (ThemeIcon.isThemeIcon(definition)) {
            const c = this.iconsById[definition.id];
            if (!c) {
                return undefined;
            }
            definition = c.defaults;
        }
        return `.codicon-${id}:before { content: '${definition.character}'; }`;
    }
    toString() {
        const sorter = (i1, i2) => {
            return i1.id.localeCompare(i2.id);
        };
        const classNames = (i) => {
            while (ThemeIcon.isThemeIcon(i.defaults)) {
                i = this.iconsById[i.defaults.id];
            }
            return `codicon codicon-${i ? i.id : ''}`;
        };
        let reference = [];
        reference.push(`| preview     | identifier                        | default codicon id                | description`);
        reference.push(`| ----------- | --------------------------------- | --------------------------------- | --------------------------------- |`);
        const contributions = Object.keys(this.iconsById).map(key => this.iconsById[key]);
        for (const i of contributions.filter(i => !!i.description).sort(sorter)) {
            reference.push(`|<i class="${classNames(i)}"></i>|${i.id}|${ThemeIcon.isThemeIcon(i.defaults) ? i.defaults.id : i.id}|${i.description || ''}|`);
        }
        reference.push(`| preview     | identifier                        `);
        reference.push(`| ----------- | --------------------------------- |`);
        for (const i of contributions.filter(i => !ThemeIcon.isThemeIcon(i.defaults)).sort(sorter)) {
            reference.push(`|<i class="${classNames(i)}"></i>|${i.id}|`);
        }
        return reference.join('\n');
    }
}
const iconRegistry = new IconRegistry();
platform.Registry.add(Extensions.IconContribution, iconRegistry);
export function registerIcon(id, defaults, description, deprecationMessage) {
    return iconRegistry.registerIcon(id, defaults, description, deprecationMessage);
}
export function getIconRegistry() {
    return iconRegistry;
}
function initialize() {
    for (const icon of Codicons.iconRegistry.all) {
        iconRegistry.registerIcon(icon.id, icon.definition, icon.description);
    }
    Codicons.iconRegistry.onDidRegister(icon => iconRegistry.registerIcon(icon.id, icon.definition, icon.description));
}
initialize();
export const iconsSchemaId = 'vscode://schemas/icons';
let schemaRegistry = platform.Registry.as(JSONExtensions.JSONContribution);
schemaRegistry.registerSchema(iconsSchemaId, iconRegistry.getIconSchema());
const delayer = new RunOnceScheduler(() => schemaRegistry.notifySchemaChanged(iconsSchemaId), 200);
iconRegistry.onDidChange(() => {
    if (!delayer.isScheduled()) {
        delayer.schedule();
    }
});
//setTimeout(_ => console.log(iconRegistry.toString()), 5000);
// common icons
export const widgetClose = registerIcon('widget-close', Codicons.Codicon.close, localize('widgetClose', 'Icon for the close action in widgets.'));
