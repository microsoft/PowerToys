/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { registerLanguage } from '../_.contribution.js';
registerLanguage({
    id: 'xml',
    extensions: [
        '.xml',
        '.dtd',
        '.ascx',
        '.csproj',
        '.config',
        '.wxi',
        '.wxl',
        '.wxs',
        '.xaml',
        '.svg',
        '.svgz',
        '.opf',
        '.xsl'
    ],
    firstLine: '(\\<\\?xml.*)|(\\<svg)|(\\<\\!doctype\\s+svg)',
    aliases: ['XML', 'xml'],
    mimetypes: ['text/xml', 'application/xml', 'application/xaml+xml', 'application/xml-dtd'],
    loader: function () { return import('./xml.js'); }
});
