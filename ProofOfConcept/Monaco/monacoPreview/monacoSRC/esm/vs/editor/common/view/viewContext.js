/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
export class EditorTheme {
    constructor(theme) {
        this._theme = theme;
    }
    get type() {
        return this._theme.type;
    }
    update(theme) {
        this._theme = theme;
    }
    getColor(color) {
        return this._theme.getColor(color);
    }
}
export class ViewContext {
    constructor(configuration, theme, model) {
        this.configuration = configuration;
        this.theme = new EditorTheme(theme);
        this.model = model;
        this.viewLayout = model.viewLayout;
    }
    addEventHandler(eventHandler) {
        this.model.addViewEventHandler(eventHandler);
    }
    removeEventHandler(eventHandler) {
        this.model.removeViewEventHandler(eventHandler);
    }
}
