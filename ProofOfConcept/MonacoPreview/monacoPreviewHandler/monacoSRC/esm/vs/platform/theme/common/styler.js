/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { contrastBorder, listFocusBackground, listFocusForeground, listActiveSelectionBackground, listActiveSelectionForeground, listInactiveSelectionForeground, listInactiveSelectionBackground, listInactiveFocusBackground, listHoverBackground, listHoverForeground, listDropBackground, widgetShadow, activeContrastBorder, badgeBackground, badgeForeground, menuForeground, menuBackground, menuSelectionForeground, menuSelectionBackground, menuSelectionBorder, menuBorder, menuSeparatorBackground, darken, listFilterWidgetOutline, listFilterWidgetNoMatchesOutline, listFilterWidgetBackground, treeIndentGuidesStroke, resolveColorValue } from './colorRegistry.js';
export function computeStyles(theme, styleMap) {
    const styles = Object.create(null);
    for (let key in styleMap) {
        const value = styleMap[key];
        if (value) {
            styles[key] = resolveColorValue(value, theme);
        }
    }
    return styles;
}
export function attachStyler(themeService, styleMap, widgetOrCallback) {
    function applyStyles(theme) {
        const styles = computeStyles(themeService.getColorTheme(), styleMap);
        if (typeof widgetOrCallback === 'function') {
            widgetOrCallback(styles);
        }
        else {
            widgetOrCallback.style(styles);
        }
    }
    applyStyles(themeService.getColorTheme());
    return themeService.onDidColorThemeChange(applyStyles);
}
export function attachBadgeStyler(widget, themeService, style) {
    return attachStyler(themeService, {
        badgeBackground: (style && style.badgeBackground) || badgeBackground,
        badgeForeground: (style && style.badgeForeground) || badgeForeground,
        badgeBorder: contrastBorder
    }, widget);
}
export function attachListStyler(widget, themeService, overrides) {
    return attachStyler(themeService, Object.assign(Object.assign({}, defaultListStyles), (overrides || {})), widget);
}
export const defaultListStyles = {
    listFocusBackground: listFocusBackground,
    listFocusForeground: listFocusForeground,
    listActiveSelectionBackground: darken(listActiveSelectionBackground, 0.1),
    listActiveSelectionForeground: listActiveSelectionForeground,
    listFocusAndSelectionBackground: listActiveSelectionBackground,
    listFocusAndSelectionForeground: listActiveSelectionForeground,
    listInactiveSelectionBackground: listInactiveSelectionBackground,
    listInactiveSelectionForeground: listInactiveSelectionForeground,
    listInactiveFocusBackground: listInactiveFocusBackground,
    listHoverBackground: listHoverBackground,
    listHoverForeground: listHoverForeground,
    listDropBackground: listDropBackground,
    listFocusOutline: activeContrastBorder,
    listSelectionOutline: activeContrastBorder,
    listHoverOutline: activeContrastBorder,
    listFilterWidgetBackground: listFilterWidgetBackground,
    listFilterWidgetOutline: listFilterWidgetOutline,
    listFilterWidgetNoMatchesOutline: listFilterWidgetNoMatchesOutline,
    listMatchesShadow: widgetShadow,
    treeIndentGuidesStroke: treeIndentGuidesStroke
};
export const defaultMenuStyles = {
    shadowColor: widgetShadow,
    borderColor: menuBorder,
    foregroundColor: menuForeground,
    backgroundColor: menuBackground,
    selectionForegroundColor: menuSelectionForeground,
    selectionBackgroundColor: menuSelectionBackground,
    selectionBorderColor: menuSelectionBorder,
    separatorColor: menuSeparatorBackground
};
export function attachMenuStyler(widget, themeService, style) {
    return attachStyler(themeService, Object.assign(Object.assign({}, defaultMenuStyles), style), widget);
}
