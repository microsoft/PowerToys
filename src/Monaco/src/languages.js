// Every language registration for the PowerToys Monaco bundle: monaco-editor's built-in languages plus the
// PowerToys layer. tools/generate-languages.mjs runs this module to produce monaco_languages.json, which the
// runner, installer, and preview handlers read, so keep all registrations here and in this order.
import 'monaco-editor/languages/definitions/register.all';
import * as monaco from 'monaco-editor/editor';
import * as cpp from 'monaco-editor/languages/definitions/cpp/cpp';
import * as ini from 'monaco-editor/languages/definitions/ini/ini';
import * as razor from 'monaco-editor/languages/definitions/razor/razor';
import * as shell from 'monaco-editor/languages/definitions/shell/shell';
import * as vb from 'monaco-editor/languages/definitions/vb/vb';
import * as xml from 'monaco-editor/languages/definitions/xml/xml';
import { gitignoreDefinition } from './customLanguages/gitignore.js';
import * as json from './customLanguages/json.js';
import { regDefinition } from './customLanguages/reg.js';
import { srtDefinition } from './customLanguages/srt.js';

// Same registration monaco-editor's JSON language service makes; see customLanguages/json.js for why it isn't used.
const jsonLanguage = {
    id: 'json',
    extensions: ['.json', '.bowerrc', '.jshintrc', '.jscsrc', '.eslintrc', '.babelrc', '.har'],
    aliases: ['JSON', 'json'],
    mimetypes: ['application/json'],
};

// Extra extensions mapped onto an existing grammar. A null grammar means plain text.
const aliasLanguages = [
    ['cppExt', ['.ino', '.pde'], cpp],
    ['xmlExt', ['.wsdl', '.projitems', '.csproj', '.fsproj', '.shproj', '.vcxproj', '.vbproj', '.resx', '.resw'], xml],
    ['txtExt', ['.sln', '.log', '.vsconfig', '.env', '.ahk', '.ion'], null],
    ['razorExt', ['.razor'], razor],
    ['vbExt', ['.vbs'], vb],
    ['iniExt', ['.inf', '.gitconfig', '.gitattributes', '.editorconfig'], ini],
    ['shellExt', ['.ksh', '.zsh', '.bsh'], shell],
    ['jsonExt', ['.uplugin', '.uproject'], json],
];

const customLanguages = [
    ['reg', ['.reg'], regDefinition()],
    ['gitignore', ['.gitignore'], gitignoreDefinition()],
    ['srt', ['.srt'], srtDefinition()],
];

export function registerPowerToysLanguages() {
    monaco.languages.register(jsonLanguage);
    monaco.languages.setLanguageConfiguration(jsonLanguage.id, json.conf);
    monaco.languages.setMonarchTokensProvider(jsonLanguage.id, json.language);

    for (const [id, extensions, grammar] of aliasLanguages) {
        monaco.languages.register({ id, extensions });
        if (grammar) {
            monaco.languages.setLanguageConfiguration(id, grammar.conf);
            monaco.languages.setMonarchTokensProvider(id, grammar.language);
        }
    }

    for (const [id, extensions, definition] of customLanguages) {
        monaco.languages.register({ id, extensions });
        monaco.languages.setMonarchTokensProvider(id, definition);
    }
}