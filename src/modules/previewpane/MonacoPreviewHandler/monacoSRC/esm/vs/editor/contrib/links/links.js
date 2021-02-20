/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
var __decorate = (this && this.__decorate) || function (decorators, target, key, desc) {
    var c = arguments.length, r = c < 3 ? target : desc === null ? desc = Object.getOwnPropertyDescriptor(target, key) : desc, d;
    if (typeof Reflect === "object" && typeof Reflect.decorate === "function") r = Reflect.decorate(decorators, target, key, desc);
    else for (var i = decorators.length - 1; i >= 0; i--) if (d = decorators[i]) r = (c < 3 ? d(r) : c > 3 ? d(target, key, r) : d(target, key)) || r;
    return c > 3 && r && Object.defineProperty(target, key, r), r;
};
var __param = (this && this.__param) || function (paramIndex, decorator) {
    return function (target, key) { decorator(target, key, paramIndex); }
};
var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    function adopt(value) { return value instanceof P ? value : new P(function (resolve) { resolve(value); }); }
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : adopt(result.value).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};
import './links.css';
import * as nls from '../../../nls.js';
import * as async from '../../../base/common/async.js';
import { CancellationToken } from '../../../base/common/cancellation.js';
import { onUnexpectedError } from '../../../base/common/errors.js';
import { MarkdownString } from '../../../base/common/htmlContent.js';
import { DisposableStore } from '../../../base/common/lifecycle.js';
import * as platform from '../../../base/common/platform.js';
import { EditorAction, registerEditorAction, registerEditorContribution } from '../../browser/editorExtensions.js';
import { ModelDecorationOptions } from '../../common/model/textModel.js';
import { LinkProviderRegistry } from '../../common/modes.js';
import { ClickLinkGesture } from '../gotoSymbol/link/clickLinkGesture.js';
import { getLinks } from './getLinks.js';
import { INotificationService } from '../../../platform/notification/common/notification.js';
import { IOpenerService } from '../../../platform/opener/common/opener.js';
import { editorActiveLinkForeground } from '../../../platform/theme/common/colorRegistry.js';
import { registerThemingParticipant } from '../../../platform/theme/common/themeService.js';
import { URI } from '../../../base/common/uri.js';
import { Schemas } from '../../../base/common/network.js';
import * as resources from '../../../base/common/resources.js';
function getHoverMessage(link, useMetaKey) {
    const executeCmd = link.url && /^command:/i.test(link.url.toString());
    const label = link.tooltip
        ? link.tooltip
        : executeCmd
            ? nls.localize('links.navigate.executeCmd', 'Execute command')
            : nls.localize('links.navigate.follow', 'Follow link');
    const kb = useMetaKey
        ? platform.isMacintosh
            ? nls.localize('links.navigate.kb.meta.mac', "cmd + click")
            : nls.localize('links.navigate.kb.meta', "ctrl + click")
        : platform.isMacintosh
            ? nls.localize('links.navigate.kb.alt.mac', "option + click")
            : nls.localize('links.navigate.kb.alt', "alt + click");
    if (link.url) {
        let nativeLabel = '';
        if (/^command:/i.test(link.url.toString())) {
            // Don't show complete command arguments in the native tooltip
            const match = link.url.toString().match(/^command:([^?#]+)/);
            if (match) {
                const commandId = match[1];
                const nativeLabelText = nls.localize('tooltip.explanation', "Execute command {0}", commandId);
                nativeLabel = ` "${nativeLabelText}"`;
            }
        }
        const hoverMessage = new MarkdownString('', true).appendMarkdown(`[${label}](${link.url.toString(true)}${nativeLabel}) (${kb})`);
        return hoverMessage;
    }
    else {
        return new MarkdownString().appendText(`${label} (${kb})`);
    }
}
const decoration = {
    general: ModelDecorationOptions.register({
        stickiness: 1 /* NeverGrowsWhenTypingAtEdges */,
        collapseOnReplaceEdit: true,
        inlineClassName: 'detected-link'
    }),
    active: ModelDecorationOptions.register({
        stickiness: 1 /* NeverGrowsWhenTypingAtEdges */,
        collapseOnReplaceEdit: true,
        inlineClassName: 'detected-link-active'
    })
};
class LinkOccurrence {
    constructor(link, decorationId) {
        this.link = link;
        this.decorationId = decorationId;
    }
    static decoration(link, useMetaKey) {
        return {
            range: link.range,
            options: LinkOccurrence._getOptions(link, useMetaKey, false)
        };
    }
    static _getOptions(link, useMetaKey, isActive) {
        const options = Object.assign({}, (isActive ? decoration.active : decoration.general));
        options.hoverMessage = getHoverMessage(link, useMetaKey);
        return options;
    }
    activate(changeAccessor, useMetaKey) {
        changeAccessor.changeDecorationOptions(this.decorationId, LinkOccurrence._getOptions(this.link, useMetaKey, true));
    }
    deactivate(changeAccessor, useMetaKey) {
        changeAccessor.changeDecorationOptions(this.decorationId, LinkOccurrence._getOptions(this.link, useMetaKey, false));
    }
}
let LinkDetector = class LinkDetector {
    constructor(editor, openerService, notificationService) {
        this.listenersToRemove = new DisposableStore();
        this.editor = editor;
        this.openerService = openerService;
        this.notificationService = notificationService;
        let clickLinkGesture = new ClickLinkGesture(editor);
        this.listenersToRemove.add(clickLinkGesture);
        this.listenersToRemove.add(clickLinkGesture.onMouseMoveOrRelevantKeyDown(([mouseEvent, keyboardEvent]) => {
            this._onEditorMouseMove(mouseEvent, keyboardEvent);
        }));
        this.listenersToRemove.add(clickLinkGesture.onExecute((e) => {
            this.onEditorMouseUp(e);
        }));
        this.listenersToRemove.add(clickLinkGesture.onCancel((e) => {
            this.cleanUpActiveLinkDecoration();
        }));
        this.enabled = editor.getOption(57 /* links */);
        this.listenersToRemove.add(editor.onDidChangeConfiguration((e) => {
            const enabled = editor.getOption(57 /* links */);
            if (this.enabled === enabled) {
                // No change in our configuration option
                return;
            }
            this.enabled = enabled;
            // Remove any links (for the getting disabled case)
            this.updateDecorations([]);
            // Stop any computation (for the getting disabled case)
            this.stop();
            // Start computing (for the getting enabled case)
            this.beginCompute();
        }));
        this.listenersToRemove.add(editor.onDidChangeModelContent((e) => this.onChange()));
        this.listenersToRemove.add(editor.onDidChangeModel((e) => this.onModelChanged()));
        this.listenersToRemove.add(editor.onDidChangeModelLanguage((e) => this.onModelModeChanged()));
        this.listenersToRemove.add(LinkProviderRegistry.onDidChange((e) => this.onModelModeChanged()));
        this.timeout = new async.TimeoutTimer();
        this.computePromise = null;
        this.activeLinksList = null;
        this.currentOccurrences = {};
        this.activeLinkDecorationId = null;
        this.beginCompute();
    }
    static get(editor) {
        return editor.getContribution(LinkDetector.ID);
    }
    onModelChanged() {
        this.currentOccurrences = {};
        this.activeLinkDecorationId = null;
        this.stop();
        this.beginCompute();
    }
    onModelModeChanged() {
        this.stop();
        this.beginCompute();
    }
    onChange() {
        this.timeout.setIfNotSet(() => this.beginCompute(), LinkDetector.RECOMPUTE_TIME);
    }
    beginCompute() {
        return __awaiter(this, void 0, void 0, function* () {
            if (!this.editor.hasModel() || !this.enabled) {
                return;
            }
            const model = this.editor.getModel();
            if (!LinkProviderRegistry.has(model)) {
                return;
            }
            if (this.activeLinksList) {
                this.activeLinksList.dispose();
                this.activeLinksList = null;
            }
            this.computePromise = async.createCancelablePromise(token => getLinks(model, token));
            try {
                this.activeLinksList = yield this.computePromise;
                this.updateDecorations(this.activeLinksList.links);
            }
            catch (err) {
                onUnexpectedError(err);
            }
            finally {
                this.computePromise = null;
            }
        });
    }
    updateDecorations(links) {
        const useMetaKey = (this.editor.getOption(64 /* multiCursorModifier */) === 'altKey');
        let oldDecorations = [];
        let keys = Object.keys(this.currentOccurrences);
        for (let i = 0, len = keys.length; i < len; i++) {
            let decorationId = keys[i];
            let occurance = this.currentOccurrences[decorationId];
            oldDecorations.push(occurance.decorationId);
        }
        let newDecorations = [];
        if (links) {
            // Not sure why this is sometimes null
            for (const link of links) {
                newDecorations.push(LinkOccurrence.decoration(link, useMetaKey));
            }
        }
        let decorations = this.editor.deltaDecorations(oldDecorations, newDecorations);
        this.currentOccurrences = {};
        this.activeLinkDecorationId = null;
        for (let i = 0, len = decorations.length; i < len; i++) {
            let occurance = new LinkOccurrence(links[i], decorations[i]);
            this.currentOccurrences[occurance.decorationId] = occurance;
        }
    }
    _onEditorMouseMove(mouseEvent, withKey) {
        const useMetaKey = (this.editor.getOption(64 /* multiCursorModifier */) === 'altKey');
        if (this.isEnabled(mouseEvent, withKey)) {
            this.cleanUpActiveLinkDecoration(); // always remove previous link decoration as their can only be one
            const occurrence = this.getLinkOccurrence(mouseEvent.target.position);
            if (occurrence) {
                this.editor.changeDecorations((changeAccessor) => {
                    occurrence.activate(changeAccessor, useMetaKey);
                    this.activeLinkDecorationId = occurrence.decorationId;
                });
            }
        }
        else {
            this.cleanUpActiveLinkDecoration();
        }
    }
    cleanUpActiveLinkDecoration() {
        const useMetaKey = (this.editor.getOption(64 /* multiCursorModifier */) === 'altKey');
        if (this.activeLinkDecorationId) {
            const occurrence = this.currentOccurrences[this.activeLinkDecorationId];
            if (occurrence) {
                this.editor.changeDecorations((changeAccessor) => {
                    occurrence.deactivate(changeAccessor, useMetaKey);
                });
            }
            this.activeLinkDecorationId = null;
        }
    }
    onEditorMouseUp(mouseEvent) {
        if (!this.isEnabled(mouseEvent)) {
            return;
        }
        const occurrence = this.getLinkOccurrence(mouseEvent.target.position);
        if (!occurrence) {
            return;
        }
        this.openLinkOccurrence(occurrence, mouseEvent.hasSideBySideModifier, true /* from user gesture */);
    }
    openLinkOccurrence(occurrence, openToSide, fromUserGesture = false) {
        if (!this.openerService) {
            return;
        }
        const { link } = occurrence;
        link.resolve(CancellationToken.None).then(uri => {
            // Support for relative file URIs of the shape file://./relativeFile.txt or file:///./relativeFile.txt
            if (typeof uri === 'string' && this.editor.hasModel()) {
                const modelUri = this.editor.getModel().uri;
                if (modelUri.scheme === Schemas.file && uri.startsWith(`${Schemas.file}:`)) {
                    const parsedUri = URI.parse(uri);
                    if (parsedUri.scheme === Schemas.file) {
                        const fsPath = resources.originalFSPath(parsedUri);
                        let relativePath = null;
                        if (fsPath.startsWith('/./')) {
                            relativePath = `.${fsPath.substr(1)}`;
                        }
                        else if (fsPath.startsWith('//./')) {
                            relativePath = `.${fsPath.substr(2)}`;
                        }
                        if (relativePath) {
                            uri = resources.joinPath(modelUri, relativePath);
                        }
                    }
                }
            }
            return this.openerService.open(uri, { openToSide, fromUserGesture, allowContributedOpeners: true });
        }, err => {
            const messageOrError = err instanceof Error ? err.message : err;
            // different error cases
            if (messageOrError === 'invalid') {
                this.notificationService.warn(nls.localize('invalid.url', 'Failed to open this link because it is not well-formed: {0}', link.url.toString()));
            }
            else if (messageOrError === 'missing') {
                this.notificationService.warn(nls.localize('missing.url', 'Failed to open this link because its target is missing.'));
            }
            else {
                onUnexpectedError(err);
            }
        });
    }
    getLinkOccurrence(position) {
        if (!this.editor.hasModel() || !position) {
            return null;
        }
        const decorations = this.editor.getModel().getDecorationsInRange({
            startLineNumber: position.lineNumber,
            startColumn: position.column,
            endLineNumber: position.lineNumber,
            endColumn: position.column
        }, 0, true);
        for (const decoration of decorations) {
            const currentOccurrence = this.currentOccurrences[decoration.id];
            if (currentOccurrence) {
                return currentOccurrence;
            }
        }
        return null;
    }
    isEnabled(mouseEvent, withKey) {
        return Boolean((mouseEvent.target.type === 6 /* CONTENT_TEXT */)
            && (mouseEvent.hasTriggerModifier || (withKey && withKey.keyCodeIsTriggerKey)));
    }
    stop() {
        var _a;
        this.timeout.cancel();
        if (this.activeLinksList) {
            (_a = this.activeLinksList) === null || _a === void 0 ? void 0 : _a.dispose();
            this.activeLinksList = null;
        }
        if (this.computePromise) {
            this.computePromise.cancel();
            this.computePromise = null;
        }
    }
    dispose() {
        this.listenersToRemove.dispose();
        this.stop();
        this.timeout.dispose();
    }
};
LinkDetector.ID = 'editor.linkDetector';
LinkDetector.RECOMPUTE_TIME = 1000; // ms
LinkDetector = __decorate([
    __param(1, IOpenerService),
    __param(2, INotificationService)
], LinkDetector);
export { LinkDetector };
class OpenLinkAction extends EditorAction {
    constructor() {
        super({
            id: 'editor.action.openLink',
            label: nls.localize('label', "Open Link"),
            alias: 'Open Link',
            precondition: undefined
        });
    }
    run(accessor, editor) {
        let linkDetector = LinkDetector.get(editor);
        if (!linkDetector) {
            return;
        }
        if (!editor.hasModel()) {
            return;
        }
        let selections = editor.getSelections();
        for (let sel of selections) {
            let link = linkDetector.getLinkOccurrence(sel.getEndPosition());
            if (link) {
                linkDetector.openLinkOccurrence(link, false);
            }
        }
    }
}
registerEditorContribution(LinkDetector.ID, LinkDetector);
registerEditorAction(OpenLinkAction);
registerThemingParticipant((theme, collector) => {
    const activeLinkForeground = theme.getColor(editorActiveLinkForeground);
    if (activeLinkForeground) {
        collector.addRule(`.monaco-editor .detected-link-active { color: ${activeLinkForeground} !important; }`);
    }
});
