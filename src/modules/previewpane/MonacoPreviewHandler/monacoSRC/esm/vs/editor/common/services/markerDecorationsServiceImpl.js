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
import { IMarkerService, MarkerSeverity } from '../../../platform/markers/common/markers.js';
import { Disposable, toDisposable } from '../../../base/common/lifecycle.js';
import { OverviewRulerLane, MinimapPosition } from '../model.js';
import { themeColorFromId } from '../../../platform/theme/common/themeService.js';
import { overviewRulerWarning, overviewRulerInfo, overviewRulerError } from '../view/editorColorRegistry.js';
import { IModelService } from './modelService.js';
import { Range } from '../core/range.js';
import { Schemas } from '../../../base/common/network.js';
import { Emitter } from '../../../base/common/event.js';
import { minimapWarning, minimapError } from '../../../platform/theme/common/colorRegistry.js';
function MODEL_ID(resource) {
    return resource.toString();
}
class MarkerDecorations extends Disposable {
    constructor(model) {
        super();
        this.model = model;
        this._markersData = new Map();
        this._register(toDisposable(() => {
            this.model.deltaDecorations([...this._markersData.keys()], []);
            this._markersData.clear();
        }));
    }
    update(markers, newDecorations) {
        const oldIds = [...this._markersData.keys()];
        this._markersData.clear();
        const ids = this.model.deltaDecorations(oldIds, newDecorations);
        for (let index = 0; index < ids.length; index++) {
            this._markersData.set(ids[index], markers[index]);
        }
        return oldIds.length !== 0 || ids.length !== 0;
    }
    getMarker(decoration) {
        return this._markersData.get(decoration.id);
    }
}
let MarkerDecorationsService = class MarkerDecorationsService extends Disposable {
    constructor(modelService, _markerService) {
        super();
        this._markerService = _markerService;
        this._onDidChangeMarker = this._register(new Emitter());
        this._markerDecorations = new Map();
        modelService.getModels().forEach(model => this._onModelAdded(model));
        this._register(modelService.onModelAdded(this._onModelAdded, this));
        this._register(modelService.onModelRemoved(this._onModelRemoved, this));
        this._register(this._markerService.onMarkerChanged(this._handleMarkerChange, this));
    }
    dispose() {
        super.dispose();
        this._markerDecorations.forEach(value => value.dispose());
        this._markerDecorations.clear();
    }
    getMarker(uri, decoration) {
        const markerDecorations = this._markerDecorations.get(MODEL_ID(uri));
        return markerDecorations ? (markerDecorations.getMarker(decoration) || null) : null;
    }
    _handleMarkerChange(changedResources) {
        changedResources.forEach((resource) => {
            const markerDecorations = this._markerDecorations.get(MODEL_ID(resource));
            if (markerDecorations) {
                this._updateDecorations(markerDecorations);
            }
        });
    }
    _onModelAdded(model) {
        const markerDecorations = new MarkerDecorations(model);
        this._markerDecorations.set(MODEL_ID(model.uri), markerDecorations);
        this._updateDecorations(markerDecorations);
    }
    _onModelRemoved(model) {
        const markerDecorations = this._markerDecorations.get(MODEL_ID(model.uri));
        if (markerDecorations) {
            markerDecorations.dispose();
            this._markerDecorations.delete(MODEL_ID(model.uri));
        }
        // clean up markers for internal, transient models
        if (model.uri.scheme === Schemas.inMemory
            || model.uri.scheme === Schemas.internal
            || model.uri.scheme === Schemas.vscode) {
            if (this._markerService) {
                this._markerService.read({ resource: model.uri }).map(marker => marker.owner).forEach(owner => this._markerService.remove(owner, [model.uri]));
            }
        }
    }
    _updateDecorations(markerDecorations) {
        // Limit to the first 500 errors/warnings
        const markers = this._markerService.read({ resource: markerDecorations.model.uri, take: 500 });
        let newModelDecorations = markers.map((marker) => {
            return {
                range: this._createDecorationRange(markerDecorations.model, marker),
                options: this._createDecorationOption(marker)
            };
        });
        if (markerDecorations.update(markers, newModelDecorations)) {
            this._onDidChangeMarker.fire(markerDecorations.model);
        }
    }
    _createDecorationRange(model, rawMarker) {
        let ret = Range.lift(rawMarker);
        if (rawMarker.severity === MarkerSeverity.Hint && !this._hasMarkerTag(rawMarker, 1 /* Unnecessary */) && !this._hasMarkerTag(rawMarker, 2 /* Deprecated */)) {
            // * never render hints on multiple lines
            // * make enough space for three dots
            ret = ret.setEndPosition(ret.startLineNumber, ret.startColumn + 2);
        }
        ret = model.validateRange(ret);
        if (ret.isEmpty()) {
            let word = model.getWordAtPosition(ret.getStartPosition());
            if (word) {
                ret = new Range(ret.startLineNumber, word.startColumn, ret.endLineNumber, word.endColumn);
            }
            else {
                let maxColumn = model.getLineLastNonWhitespaceColumn(ret.startLineNumber) ||
                    model.getLineMaxColumn(ret.startLineNumber);
                if (maxColumn === 1) {
                    // empty line
                    // console.warn('marker on empty line:', marker);
                }
                else if (ret.endColumn >= maxColumn) {
                    // behind eol
                    ret = new Range(ret.startLineNumber, maxColumn - 1, ret.endLineNumber, maxColumn);
                }
                else {
                    // extend marker to width = 1
                    ret = new Range(ret.startLineNumber, ret.startColumn, ret.endLineNumber, ret.endColumn + 1);
                }
            }
        }
        else if (rawMarker.endColumn === Number.MAX_VALUE && rawMarker.startColumn === 1 && ret.startLineNumber === ret.endLineNumber) {
            let minColumn = model.getLineFirstNonWhitespaceColumn(rawMarker.startLineNumber);
            if (minColumn < ret.endColumn) {
                ret = new Range(ret.startLineNumber, minColumn, ret.endLineNumber, ret.endColumn);
                rawMarker.startColumn = minColumn;
            }
        }
        return ret;
    }
    _createDecorationOption(marker) {
        let className;
        let color = undefined;
        let zIndex;
        let inlineClassName = undefined;
        let minimap;
        switch (marker.severity) {
            case MarkerSeverity.Hint:
                if (this._hasMarkerTag(marker, 2 /* Deprecated */)) {
                    className = undefined;
                }
                else if (this._hasMarkerTag(marker, 1 /* Unnecessary */)) {
                    className = "squiggly-unnecessary" /* EditorUnnecessaryDecoration */;
                }
                else {
                    className = "squiggly-hint" /* EditorHintDecoration */;
                }
                zIndex = 0;
                break;
            case MarkerSeverity.Warning:
                className = "squiggly-warning" /* EditorWarningDecoration */;
                color = themeColorFromId(overviewRulerWarning);
                zIndex = 20;
                minimap = {
                    color: themeColorFromId(minimapWarning),
                    position: MinimapPosition.Inline
                };
                break;
            case MarkerSeverity.Info:
                className = "squiggly-info" /* EditorInfoDecoration */;
                color = themeColorFromId(overviewRulerInfo);
                zIndex = 10;
                break;
            case MarkerSeverity.Error:
            default:
                className = "squiggly-error" /* EditorErrorDecoration */;
                color = themeColorFromId(overviewRulerError);
                zIndex = 30;
                minimap = {
                    color: themeColorFromId(minimapError),
                    position: MinimapPosition.Inline
                };
                break;
        }
        if (marker.tags) {
            if (marker.tags.indexOf(1 /* Unnecessary */) !== -1) {
                inlineClassName = "squiggly-inline-unnecessary" /* EditorUnnecessaryInlineDecoration */;
            }
            if (marker.tags.indexOf(2 /* Deprecated */) !== -1) {
                inlineClassName = "squiggly-inline-deprecated" /* EditorDeprecatedInlineDecoration */;
            }
        }
        return {
            stickiness: 1 /* NeverGrowsWhenTypingAtEdges */,
            className,
            showIfCollapsed: true,
            overviewRuler: {
                color,
                position: OverviewRulerLane.Right
            },
            minimap,
            zIndex,
            inlineClassName,
        };
    }
    _hasMarkerTag(marker, tag) {
        if (marker.tags) {
            return marker.tags.indexOf(tag) >= 0;
        }
        return false;
    }
};
MarkerDecorationsService = __decorate([
    __param(0, IModelService),
    __param(1, IMarkerService)
], MarkerDecorationsService);
export { MarkerDecorationsService };
