/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import Severity from '../../../base/common/severity.js';
import { registerThemingParticipant } from '../../theme/common/themeService.js';
import { problemsErrorIconForeground, problemsInfoIconForeground, problemsWarningIconForeground } from '../../theme/common/colorRegistry.js';
import { Codicon } from '../../../base/common/codicons.js';
export var SeverityIcon;
(function (SeverityIcon) {
    function className(severity) {
        switch (severity) {
            case Severity.Ignore:
                return 'severity-ignore ' + Codicon.info.classNames;
            case Severity.Info:
                return Codicon.info.classNames;
            case Severity.Warning:
                return Codicon.warning.classNames;
            case Severity.Error:
                return Codicon.error.classNames;
            default:
                return '';
        }
    }
    SeverityIcon.className = className;
})(SeverityIcon || (SeverityIcon = {}));
registerThemingParticipant((theme, collector) => {
    const errorIconForeground = theme.getColor(problemsErrorIconForeground);
    if (errorIconForeground) {
        const errorCodiconSelector = Codicon.error.cssSelector;
        collector.addRule(`
			.monaco-editor .zone-widget ${errorCodiconSelector},
			.markers-panel .marker-icon${errorCodiconSelector},
			.extensions-viewlet > .extensions ${errorCodiconSelector} {
				color: ${errorIconForeground};
			}
		`);
    }
    const warningIconForeground = theme.getColor(problemsWarningIconForeground);
    if (warningIconForeground) {
        const warningCodiconSelector = Codicon.warning.cssSelector;
        collector.addRule(`
			.monaco-editor .zone-widget ${warningCodiconSelector},
			.markers-panel .marker-icon${warningCodiconSelector},
			.extensions-viewlet > .extensions ${warningCodiconSelector},
			.extension-editor ${warningCodiconSelector} {
				color: ${warningIconForeground};
			}
		`);
    }
    const infoIconForeground = theme.getColor(problemsInfoIconForeground);
    if (infoIconForeground) {
        const infoCodiconSelector = Codicon.info.cssSelector;
        collector.addRule(`
			.monaco-editor .zone-widget ${infoCodiconSelector},
			.markers-panel .marker-icon${infoCodiconSelector},
			.extensions-viewlet > .extensions ${infoCodiconSelector},
			.extension-editor ${infoCodiconSelector} {
				color: ${infoIconForeground};
			}
		`);
    }
});
