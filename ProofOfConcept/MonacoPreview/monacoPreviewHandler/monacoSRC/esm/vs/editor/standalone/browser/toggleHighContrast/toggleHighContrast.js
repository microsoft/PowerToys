/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { EditorAction, registerEditorAction } from '../../../browser/editorExtensions.js';
import { IStandaloneThemeService } from '../../common/standaloneThemeService.js';
import { ToggleHighContrastNLS } from '../../../common/standaloneStrings.js';
class ToggleHighContrast extends EditorAction {
    constructor() {
        super({
            id: 'editor.action.toggleHighContrast',
            label: ToggleHighContrastNLS.toggleHighContrast,
            alias: 'Toggle High Contrast Theme',
            precondition: undefined
        });
        this._originalThemeName = null;
    }
    run(accessor, editor) {
        const standaloneThemeService = accessor.get(IStandaloneThemeService);
        if (this._originalThemeName) {
            // We must toggle back to the integrator's theme
            standaloneThemeService.setTheme(this._originalThemeName);
            this._originalThemeName = null;
        }
        else {
            this._originalThemeName = standaloneThemeService.getColorTheme().themeName;
            standaloneThemeService.setTheme('hc-black');
        }
    }
}
registerEditorAction(ToggleHighContrast);
