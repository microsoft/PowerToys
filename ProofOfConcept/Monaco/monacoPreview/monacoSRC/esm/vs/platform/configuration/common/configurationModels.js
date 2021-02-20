import { ResourceMap } from '../../../base/common/map.js';
import * as arrays from '../../../base/common/arrays.js';
import * as types from '../../../base/common/types.js';
import * as objects from '../../../base/common/objects.js';
import { URI } from '../../../base/common/uri.js';
import { OVERRIDE_PROPERTY_PATTERN, overrideIdentifierFromKey } from './configurationRegistry.js';
import { addToValueTree, toValuesTree, getConfigurationValue, getDefaultValues, getConfigurationKeys, removeFromValueTree } from './configuration.js';
export class ConfigurationModel {
    constructor(_contents = {}, _keys = [], _overrides = []) {
        this._contents = _contents;
        this._keys = _keys;
        this._overrides = _overrides;
        this.isFrozen = false;
    }
    get contents() {
        return this.checkAndFreeze(this._contents);
    }
    get overrides() {
        return this.checkAndFreeze(this._overrides);
    }
    get keys() {
        return this.checkAndFreeze(this._keys);
    }
    isEmpty() {
        return this._keys.length === 0 && Object.keys(this._contents).length === 0 && this._overrides.length === 0;
    }
    getValue(section) {
        return section ? getConfigurationValue(this.contents, section) : this.contents;
    }
    override(identifier) {
        const overrideContents = this.getContentsForOverrideIdentifer(identifier);
        if (!overrideContents || typeof overrideContents !== 'object' || !Object.keys(overrideContents).length) {
            // If there are no valid overrides, return self
            return this;
        }
        let contents = {};
        for (const key of arrays.distinct([...Object.keys(this.contents), ...Object.keys(overrideContents)])) {
            let contentsForKey = this.contents[key];
            let overrideContentsForKey = overrideContents[key];
            // If there are override contents for the key, clone and merge otherwise use base contents
            if (overrideContentsForKey) {
                // Clone and merge only if base contents and override contents are of type object otherwise just override
                if (typeof contentsForKey === 'object' && typeof overrideContentsForKey === 'object') {
                    contentsForKey = objects.deepClone(contentsForKey);
                    this.mergeContents(contentsForKey, overrideContentsForKey);
                }
                else {
                    contentsForKey = overrideContentsForKey;
                }
            }
            contents[key] = contentsForKey;
        }
        return new ConfigurationModel(contents, this.keys, this.overrides);
    }
    merge(...others) {
        const contents = objects.deepClone(this.contents);
        const overrides = objects.deepClone(this.overrides);
        const keys = [...this.keys];
        for (const other of others) {
            this.mergeContents(contents, other.contents);
            for (const otherOverride of other.overrides) {
                const [override] = overrides.filter(o => arrays.equals(o.identifiers, otherOverride.identifiers));
                if (override) {
                    this.mergeContents(override.contents, otherOverride.contents);
                }
                else {
                    overrides.push(objects.deepClone(otherOverride));
                }
            }
            for (const key of other.keys) {
                if (keys.indexOf(key) === -1) {
                    keys.push(key);
                }
            }
        }
        return new ConfigurationModel(contents, keys, overrides);
    }
    freeze() {
        this.isFrozen = true;
        return this;
    }
    mergeContents(source, target) {
        for (const key of Object.keys(target)) {
            if (key in source) {
                if (types.isObject(source[key]) && types.isObject(target[key])) {
                    this.mergeContents(source[key], target[key]);
                    continue;
                }
            }
            source[key] = objects.deepClone(target[key]);
        }
    }
    checkAndFreeze(data) {
        if (this.isFrozen && !Object.isFrozen(data)) {
            return objects.deepFreeze(data);
        }
        return data;
    }
    getContentsForOverrideIdentifer(identifier) {
        for (const override of this.overrides) {
            if (override.identifiers.indexOf(identifier) !== -1) {
                return override.contents;
            }
        }
        return null;
    }
    toJSON() {
        return {
            contents: this.contents,
            overrides: this.overrides,
            keys: this.keys
        };
    }
    // Update methods
    setValue(key, value) {
        this.addKey(key);
        addToValueTree(this.contents, key, value, e => { throw new Error(e); });
    }
    removeValue(key) {
        if (this.removeKey(key)) {
            removeFromValueTree(this.contents, key);
        }
    }
    addKey(key) {
        let index = this.keys.length;
        for (let i = 0; i < index; i++) {
            if (key.indexOf(this.keys[i]) === 0) {
                index = i;
            }
        }
        this.keys.splice(index, 1, key);
    }
    removeKey(key) {
        let index = this.keys.indexOf(key);
        if (index !== -1) {
            this.keys.splice(index, 1);
            return true;
        }
        return false;
    }
}
export class DefaultConfigurationModel extends ConfigurationModel {
    constructor() {
        const contents = getDefaultValues();
        const keys = getConfigurationKeys();
        const overrides = [];
        for (const key of Object.keys(contents)) {
            if (OVERRIDE_PROPERTY_PATTERN.test(key)) {
                overrides.push({
                    identifiers: [overrideIdentifierFromKey(key).trim()],
                    keys: Object.keys(contents[key]),
                    contents: toValuesTree(contents[key], message => console.error(`Conflict in default settings file: ${message}`)),
                });
            }
        }
        super(contents, keys, overrides);
    }
}
export class Configuration {
    constructor(_defaultConfiguration, _localUserConfiguration, _remoteUserConfiguration = new ConfigurationModel(), _workspaceConfiguration = new ConfigurationModel(), _folderConfigurations = new ResourceMap(), _memoryConfiguration = new ConfigurationModel(), _memoryConfigurationByResource = new ResourceMap(), _freeze = true) {
        this._defaultConfiguration = _defaultConfiguration;
        this._localUserConfiguration = _localUserConfiguration;
        this._remoteUserConfiguration = _remoteUserConfiguration;
        this._workspaceConfiguration = _workspaceConfiguration;
        this._folderConfigurations = _folderConfigurations;
        this._memoryConfiguration = _memoryConfiguration;
        this._memoryConfigurationByResource = _memoryConfigurationByResource;
        this._freeze = _freeze;
        this._workspaceConsolidatedConfiguration = null;
        this._foldersConsolidatedConfigurations = new ResourceMap();
        this._userConfiguration = null;
    }
    getValue(section, overrides, workspace) {
        const consolidateConfigurationModel = this.getConsolidateConfigurationModel(overrides, workspace);
        return consolidateConfigurationModel.getValue(section);
    }
    updateValue(key, value, overrides = {}) {
        let memoryConfiguration;
        if (overrides.resource) {
            memoryConfiguration = this._memoryConfigurationByResource.get(overrides.resource);
            if (!memoryConfiguration) {
                memoryConfiguration = new ConfigurationModel();
                this._memoryConfigurationByResource.set(overrides.resource, memoryConfiguration);
            }
        }
        else {
            memoryConfiguration = this._memoryConfiguration;
        }
        if (value === undefined) {
            memoryConfiguration.removeValue(key);
        }
        else {
            memoryConfiguration.setValue(key, value);
        }
        if (!overrides.resource) {
            this._workspaceConsolidatedConfiguration = null;
        }
    }
    get userConfiguration() {
        if (!this._userConfiguration) {
            this._userConfiguration = this._remoteUserConfiguration.isEmpty() ? this._localUserConfiguration : this._localUserConfiguration.merge(this._remoteUserConfiguration);
            if (this._freeze) {
                this._userConfiguration.freeze();
            }
        }
        return this._userConfiguration;
    }
    getConsolidateConfigurationModel(overrides, workspace) {
        let configurationModel = this.getConsolidatedConfigurationModelForResource(overrides, workspace);
        return overrides.overrideIdentifier ? configurationModel.override(overrides.overrideIdentifier) : configurationModel;
    }
    getConsolidatedConfigurationModelForResource({ resource }, workspace) {
        let consolidateConfiguration = this.getWorkspaceConsolidatedConfiguration();
        if (workspace && resource) {
            const root = workspace.getFolder(resource);
            if (root) {
                consolidateConfiguration = this.getFolderConsolidatedConfiguration(root.uri) || consolidateConfiguration;
            }
            const memoryConfigurationForResource = this._memoryConfigurationByResource.get(resource);
            if (memoryConfigurationForResource) {
                consolidateConfiguration = consolidateConfiguration.merge(memoryConfigurationForResource);
            }
        }
        return consolidateConfiguration;
    }
    getWorkspaceConsolidatedConfiguration() {
        if (!this._workspaceConsolidatedConfiguration) {
            this._workspaceConsolidatedConfiguration = this._defaultConfiguration.merge(this.userConfiguration, this._workspaceConfiguration, this._memoryConfiguration);
            if (this._freeze) {
                this._workspaceConfiguration = this._workspaceConfiguration.freeze();
            }
        }
        return this._workspaceConsolidatedConfiguration;
    }
    getFolderConsolidatedConfiguration(folder) {
        let folderConsolidatedConfiguration = this._foldersConsolidatedConfigurations.get(folder);
        if (!folderConsolidatedConfiguration) {
            const workspaceConsolidateConfiguration = this.getWorkspaceConsolidatedConfiguration();
            const folderConfiguration = this._folderConfigurations.get(folder);
            if (folderConfiguration) {
                folderConsolidatedConfiguration = workspaceConsolidateConfiguration.merge(folderConfiguration);
                if (this._freeze) {
                    folderConsolidatedConfiguration = folderConsolidatedConfiguration.freeze();
                }
                this._foldersConsolidatedConfigurations.set(folder, folderConsolidatedConfiguration);
            }
            else {
                folderConsolidatedConfiguration = workspaceConsolidateConfiguration;
            }
        }
        return folderConsolidatedConfiguration;
    }
    toData() {
        return {
            defaults: {
                contents: this._defaultConfiguration.contents,
                overrides: this._defaultConfiguration.overrides,
                keys: this._defaultConfiguration.keys
            },
            user: {
                contents: this.userConfiguration.contents,
                overrides: this.userConfiguration.overrides,
                keys: this.userConfiguration.keys
            },
            workspace: {
                contents: this._workspaceConfiguration.contents,
                overrides: this._workspaceConfiguration.overrides,
                keys: this._workspaceConfiguration.keys
            },
            folders: [...this._folderConfigurations.keys()].reduce((result, folder) => {
                const { contents, overrides, keys } = this._folderConfigurations.get(folder);
                result.push([folder, { contents, overrides, keys }]);
                return result;
            }, [])
        };
    }
    static parse(data) {
        const defaultConfiguration = this.parseConfigurationModel(data.defaults);
        const userConfiguration = this.parseConfigurationModel(data.user);
        const workspaceConfiguration = this.parseConfigurationModel(data.workspace);
        const folders = data.folders.reduce((result, value) => {
            result.set(URI.revive(value[0]), this.parseConfigurationModel(value[1]));
            return result;
        }, new ResourceMap());
        return new Configuration(defaultConfiguration, userConfiguration, new ConfigurationModel(), workspaceConfiguration, folders, new ConfigurationModel(), new ResourceMap(), false);
    }
    static parseConfigurationModel(model) {
        return new ConfigurationModel(model.contents, model.keys, model.overrides).freeze();
    }
}
export class ConfigurationChangeEvent {
    constructor(change, previous, currentConfiguraiton, currentWorkspace) {
        this.change = change;
        this.previous = previous;
        this.currentConfiguraiton = currentConfiguraiton;
        this.currentWorkspace = currentWorkspace;
        this._previousConfiguration = undefined;
        const keysSet = new Set();
        change.keys.forEach(key => keysSet.add(key));
        change.overrides.forEach(([, keys]) => keys.forEach(key => keysSet.add(key)));
        this.affectedKeys = [...keysSet.values()];
        const configurationModel = new ConfigurationModel();
        this.affectedKeys.forEach(key => configurationModel.setValue(key, {}));
        this.affectedKeysTree = configurationModel.contents;
    }
    get previousConfiguration() {
        if (!this._previousConfiguration && this.previous) {
            this._previousConfiguration = Configuration.parse(this.previous.data);
        }
        return this._previousConfiguration;
    }
    affectsConfiguration(section, overrides) {
        var _a;
        if (this.doesAffectedKeysTreeContains(this.affectedKeysTree, section)) {
            if (overrides) {
                const value1 = this.previousConfiguration ? this.previousConfiguration.getValue(section, overrides, (_a = this.previous) === null || _a === void 0 ? void 0 : _a.workspace) : undefined;
                const value2 = this.currentConfiguraiton.getValue(section, overrides, this.currentWorkspace);
                return !objects.equals(value1, value2);
            }
            return true;
        }
        return false;
    }
    doesAffectedKeysTreeContains(affectedKeysTree, section) {
        let requestedTree = toValuesTree({ [section]: true }, () => { });
        let key;
        while (typeof requestedTree === 'object' && (key = Object.keys(requestedTree)[0])) { // Only one key should present, since we added only one property
            affectedKeysTree = affectedKeysTree[key];
            if (!affectedKeysTree) {
                return false; // Requested tree is not found
            }
            requestedTree = requestedTree[key];
        }
        return true;
    }
}
