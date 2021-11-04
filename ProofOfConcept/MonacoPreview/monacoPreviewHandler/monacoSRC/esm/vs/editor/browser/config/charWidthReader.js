/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
export class CharWidthRequest {
    constructor(chr, type) {
        this.chr = chr;
        this.type = type;
        this.width = 0;
    }
    fulfill(width) {
        this.width = width;
    }
}
class DomCharWidthReader {
    constructor(bareFontInfo, requests) {
        this._bareFontInfo = bareFontInfo;
        this._requests = requests;
        this._container = null;
        this._testElements = null;
    }
    read() {
        // Create a test container with all these test elements
        this._createDomElements();
        // Add the container to the DOM
        document.body.appendChild(this._container);
        // Read character widths
        this._readFromDomElements();
        // Remove the container from the DOM
        document.body.removeChild(this._container);
        this._container = null;
        this._testElements = null;
    }
    _createDomElements() {
        const container = document.createElement('div');
        container.style.position = 'absolute';
        container.style.top = '-50000px';
        container.style.width = '50000px';
        const regularDomNode = document.createElement('div');
        regularDomNode.style.fontFamily = this._bareFontInfo.getMassagedFontFamily();
        regularDomNode.style.fontWeight = this._bareFontInfo.fontWeight;
        regularDomNode.style.fontSize = this._bareFontInfo.fontSize + 'px';
        regularDomNode.style.fontFeatureSettings = this._bareFontInfo.fontFeatureSettings;
        regularDomNode.style.lineHeight = this._bareFontInfo.lineHeight + 'px';
        regularDomNode.style.letterSpacing = this._bareFontInfo.letterSpacing + 'px';
        container.appendChild(regularDomNode);
        const boldDomNode = document.createElement('div');
        boldDomNode.style.fontFamily = this._bareFontInfo.getMassagedFontFamily();
        boldDomNode.style.fontWeight = 'bold';
        boldDomNode.style.fontSize = this._bareFontInfo.fontSize + 'px';
        boldDomNode.style.fontFeatureSettings = this._bareFontInfo.fontFeatureSettings;
        boldDomNode.style.lineHeight = this._bareFontInfo.lineHeight + 'px';
        boldDomNode.style.letterSpacing = this._bareFontInfo.letterSpacing + 'px';
        container.appendChild(boldDomNode);
        const italicDomNode = document.createElement('div');
        italicDomNode.style.fontFamily = this._bareFontInfo.getMassagedFontFamily();
        italicDomNode.style.fontWeight = this._bareFontInfo.fontWeight;
        italicDomNode.style.fontSize = this._bareFontInfo.fontSize + 'px';
        italicDomNode.style.fontFeatureSettings = this._bareFontInfo.fontFeatureSettings;
        italicDomNode.style.lineHeight = this._bareFontInfo.lineHeight + 'px';
        italicDomNode.style.letterSpacing = this._bareFontInfo.letterSpacing + 'px';
        italicDomNode.style.fontStyle = 'italic';
        container.appendChild(italicDomNode);
        const testElements = [];
        for (const request of this._requests) {
            let parent;
            if (request.type === 0 /* Regular */) {
                parent = regularDomNode;
            }
            if (request.type === 2 /* Bold */) {
                parent = boldDomNode;
            }
            if (request.type === 1 /* Italic */) {
                parent = italicDomNode;
            }
            parent.appendChild(document.createElement('br'));
            const testElement = document.createElement('span');
            DomCharWidthReader._render(testElement, request);
            parent.appendChild(testElement);
            testElements.push(testElement);
        }
        this._container = container;
        this._testElements = testElements;
    }
    static _render(testElement, request) {
        if (request.chr === ' ') {
            let htmlString = '\u00a0';
            // Repeat character 256 (2^8) times
            for (let i = 0; i < 8; i++) {
                htmlString += htmlString;
            }
            testElement.innerText = htmlString;
        }
        else {
            let testString = request.chr;
            // Repeat character 256 (2^8) times
            for (let i = 0; i < 8; i++) {
                testString += testString;
            }
            testElement.textContent = testString;
        }
    }
    _readFromDomElements() {
        for (let i = 0, len = this._requests.length; i < len; i++) {
            const request = this._requests[i];
            const testElement = this._testElements[i];
            request.fulfill(testElement.offsetWidth / 256);
        }
    }
}
export function readCharWidths(bareFontInfo, requests) {
    const reader = new DomCharWidthReader(bareFontInfo, requests);
    reader.read();
}
