/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { Disposable } from '../../../base/common/lifecycle.js';
import { IBulkEditService } from '../../browser/services/bulkEditService.js';
import { ICodeEditorService } from '../../browser/services/codeEditorService.js';
import { IEditorWorkerService } from '../../common/services/editorWorkerService.js';
import { EditorWorkerServiceImpl } from '../../common/services/editorWorkerServiceImpl.js';
import { IModeService } from '../../common/services/modeService.js';
import { ModeServiceImpl } from '../../common/services/modeServiceImpl.js';
import { IModelService } from '../../common/services/modelService.js';
import { ModelServiceImpl } from '../../common/services/modelServiceImpl.js';
import { ITextResourceConfigurationService, ITextResourcePropertiesService } from '../../common/services/textResourceConfigurationService.js';
import { SimpleBulkEditService, SimpleConfigurationService, SimpleDialogService, SimpleNotificationService, SimpleEditorProgressService, SimpleResourceConfigurationService, SimpleResourcePropertiesService, SimpleUriLabelService, SimpleWorkspaceContextService, StandaloneCommandService, StandaloneKeybindingService, StandaloneTelemetryService, SimpleLayoutService } from './simpleServices.js';
import { StandaloneCodeEditorServiceImpl } from './standaloneCodeServiceImpl.js';
import { StandaloneThemeServiceImpl } from './standaloneThemeServiceImpl.js';
import { IStandaloneThemeService } from '../common/standaloneThemeService.js';
import { IMenuService } from '../../../platform/actions/common/actions.js';
import { ICommandService } from '../../../platform/commands/common/commands.js';
import { IConfigurationService } from '../../../platform/configuration/common/configuration.js';
import { ContextKeyService } from '../../../platform/contextkey/browser/contextKeyService.js';
import { IContextKeyService } from '../../../platform/contextkey/common/contextkey.js';
import { ContextMenuService } from '../../../platform/contextview/browser/contextMenuService.js';
import { IContextMenuService, IContextViewService } from '../../../platform/contextview/browser/contextView.js';
import { ContextViewService } from '../../../platform/contextview/browser/contextViewService.js';
import { IDialogService } from '../../../platform/dialogs/common/dialogs.js';
import { IInstantiationService, createDecorator } from '../../../platform/instantiation/common/instantiation.js';
import { InstantiationService } from '../../../platform/instantiation/common/instantiationService.js';
import { ServiceCollection } from '../../../platform/instantiation/common/serviceCollection.js';
import { IKeybindingService } from '../../../platform/keybinding/common/keybinding.js';
import { ILabelService } from '../../../platform/label/common/label.js';
import { IListService, ListService } from '../../../platform/list/browser/listService.js';
import { ConsoleLogService, ILogService } from '../../../platform/log/common/log.js';
import { MarkerService } from '../../../platform/markers/common/markerService.js';
import { IMarkerService } from '../../../platform/markers/common/markers.js';
import { INotificationService } from '../../../platform/notification/common/notification.js';
import { IEditorProgressService } from '../../../platform/progress/common/progress.js';
import { IStorageService, InMemoryStorageService } from '../../../platform/storage/common/storage.js';
import { ITelemetryService } from '../../../platform/telemetry/common/telemetry.js';
import { IThemeService } from '../../../platform/theme/common/themeService.js';
import { IWorkspaceContextService } from '../../../platform/workspace/common/workspace.js';
import { MenuService } from '../../../platform/actions/common/menuService.js';
import { IMarkerDecorationsService } from '../../common/services/markersDecorationService.js';
import { MarkerDecorationsService } from '../../common/services/markerDecorationsServiceImpl.js';
import { IAccessibilityService } from '../../../platform/accessibility/common/accessibility.js';
import { ILayoutService } from '../../../platform/layout/browser/layoutService.js';
import { getSingletonServiceDescriptors } from '../../../platform/instantiation/common/extensions.js';
import { AccessibilityService } from '../../../platform/accessibility/common/accessibilityService.js';
import { IClipboardService } from '../../../platform/clipboard/common/clipboardService.js';
import { BrowserClipboardService } from '../../../platform/clipboard/browser/clipboardService.js';
import { IUndoRedoService } from '../../../platform/undoRedo/common/undoRedo.js';
import { UndoRedoService } from '../../../platform/undoRedo/common/undoRedoService.js';
import { StandaloneQuickInputServiceImpl } from './quickInput/standaloneQuickInputServiceImpl.js';
import { IQuickInputService } from '../../../platform/quickinput/common/quickInput.js';
export var StaticServices;
(function (StaticServices) {
    const _serviceCollection = new ServiceCollection();
    class LazyStaticService {
        constructor(serviceId, factory) {
            this._serviceId = serviceId;
            this._factory = factory;
            this._value = null;
        }
        get id() { return this._serviceId; }
        get(overrides) {
            if (!this._value) {
                if (overrides) {
                    this._value = overrides[this._serviceId.toString()];
                }
                if (!this._value) {
                    this._value = this._factory(overrides);
                }
                if (!this._value) {
                    throw new Error('Service ' + this._serviceId + ' is missing!');
                }
                _serviceCollection.set(this._serviceId, this._value);
            }
            return this._value;
        }
    }
    StaticServices.LazyStaticService = LazyStaticService;
    let _all = [];
    function define(serviceId, factory) {
        let r = new LazyStaticService(serviceId, factory);
        _all.push(r);
        return r;
    }
    function init(overrides) {
        // Create a fresh service collection
        let result = new ServiceCollection();
        // make sure to add all services that use `registerSingleton`
        for (const [id, descriptor] of getSingletonServiceDescriptors()) {
            result.set(id, descriptor);
        }
        // Initialize the service collection with the overrides
        for (let serviceId in overrides) {
            if (overrides.hasOwnProperty(serviceId)) {
                result.set(createDecorator(serviceId), overrides[serviceId]);
            }
        }
        // Make sure the same static services are present in all service collections
        _all.forEach(service => result.set(service.id, service.get(overrides)));
        // Ensure the collection gets the correct instantiation service
        let instantiationService = new InstantiationService(result, true);
        result.set(IInstantiationService, instantiationService);
        return [result, instantiationService];
    }
    StaticServices.init = init;
    StaticServices.instantiationService = define(IInstantiationService, () => new InstantiationService(_serviceCollection, true));
    const configurationServiceImpl = new SimpleConfigurationService();
    StaticServices.configurationService = define(IConfigurationService, () => configurationServiceImpl);
    StaticServices.resourceConfigurationService = define(ITextResourceConfigurationService, () => new SimpleResourceConfigurationService(configurationServiceImpl));
    StaticServices.resourcePropertiesService = define(ITextResourcePropertiesService, () => new SimpleResourcePropertiesService(configurationServiceImpl));
    StaticServices.contextService = define(IWorkspaceContextService, () => new SimpleWorkspaceContextService());
    StaticServices.labelService = define(ILabelService, () => new SimpleUriLabelService());
    StaticServices.telemetryService = define(ITelemetryService, () => new StandaloneTelemetryService());
    StaticServices.dialogService = define(IDialogService, () => new SimpleDialogService());
    StaticServices.notificationService = define(INotificationService, () => new SimpleNotificationService());
    StaticServices.markerService = define(IMarkerService, () => new MarkerService());
    StaticServices.modeService = define(IModeService, (o) => new ModeServiceImpl());
    StaticServices.standaloneThemeService = define(IStandaloneThemeService, () => new StandaloneThemeServiceImpl());
    StaticServices.logService = define(ILogService, () => new ConsoleLogService());
    StaticServices.undoRedoService = define(IUndoRedoService, (o) => new UndoRedoService(StaticServices.dialogService.get(o), StaticServices.notificationService.get(o)));
    StaticServices.modelService = define(IModelService, (o) => new ModelServiceImpl(StaticServices.configurationService.get(o), StaticServices.resourcePropertiesService.get(o), StaticServices.standaloneThemeService.get(o), StaticServices.logService.get(o), StaticServices.undoRedoService.get(o)));
    StaticServices.markerDecorationsService = define(IMarkerDecorationsService, (o) => new MarkerDecorationsService(StaticServices.modelService.get(o), StaticServices.markerService.get(o)));
    StaticServices.codeEditorService = define(ICodeEditorService, (o) => new StandaloneCodeEditorServiceImpl(StaticServices.standaloneThemeService.get(o)));
    StaticServices.editorProgressService = define(IEditorProgressService, () => new SimpleEditorProgressService());
    StaticServices.storageService = define(IStorageService, () => new InMemoryStorageService());
    StaticServices.editorWorkerService = define(IEditorWorkerService, (o) => new EditorWorkerServiceImpl(StaticServices.modelService.get(o), StaticServices.resourceConfigurationService.get(o), StaticServices.logService.get(o)));
})(StaticServices || (StaticServices = {}));
export class DynamicStandaloneServices extends Disposable {
    constructor(domElement, overrides) {
        super();
        const [_serviceCollection, _instantiationService] = StaticServices.init(overrides);
        this._serviceCollection = _serviceCollection;
        this._instantiationService = _instantiationService;
        const configurationService = this.get(IConfigurationService);
        const notificationService = this.get(INotificationService);
        const telemetryService = this.get(ITelemetryService);
        const themeService = this.get(IThemeService);
        const logService = this.get(ILogService);
        let ensure = (serviceId, factory) => {
            let value = null;
            if (overrides) {
                value = overrides[serviceId.toString()];
            }
            if (!value) {
                value = factory();
            }
            this._serviceCollection.set(serviceId, value);
            return value;
        };
        let contextKeyService = ensure(IContextKeyService, () => this._register(new ContextKeyService(configurationService)));
        ensure(IAccessibilityService, () => new AccessibilityService(contextKeyService, configurationService));
        ensure(IListService, () => new ListService(themeService));
        let commandService = ensure(ICommandService, () => new StandaloneCommandService(this._instantiationService));
        let keybindingService = ensure(IKeybindingService, () => this._register(new StandaloneKeybindingService(contextKeyService, commandService, telemetryService, notificationService, logService, domElement)));
        let layoutService = ensure(ILayoutService, () => new SimpleLayoutService(StaticServices.codeEditorService.get(ICodeEditorService), domElement));
        ensure(IQuickInputService, () => new StandaloneQuickInputServiceImpl(_instantiationService, StaticServices.codeEditorService.get(ICodeEditorService)));
        let contextViewService = ensure(IContextViewService, () => this._register(new ContextViewService(layoutService)));
        ensure(IClipboardService, () => new BrowserClipboardService());
        ensure(IContextMenuService, () => {
            const contextMenuService = new ContextMenuService(telemetryService, notificationService, contextViewService, keybindingService, themeService);
            contextMenuService.configure({ blockMouse: false }); // we do not want that in the standalone editor
            return this._register(contextMenuService);
        });
        ensure(IMenuService, () => new MenuService(commandService));
        ensure(IBulkEditService, () => new SimpleBulkEditService(StaticServices.modelService.get(IModelService)));
    }
    get(serviceId) {
        let r = this._serviceCollection.get(serviceId);
        if (!r) {
            throw new Error('Missing service ' + serviceId);
        }
        return r;
    }
    set(serviceId, instance) {
        this._serviceCollection.set(serviceId, instance);
    }
    has(serviceId) {
        return this._serviceCollection.has(serviceId);
    }
}
