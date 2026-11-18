// Languages PowerToys adds on top of monaco-editor. IDs and extensions must stay in sync with
// monaco_languages.json, which the runner, installer, and preview handlers read.
import * as monaco from 'monaco-editor/editor';
import * as cpp from 'monaco-editor/languages/definitions/cpp/cpp';
import * as ini from 'monaco-editor/languages/definitions/ini/ini';
import * as razor from 'monaco-editor/languages/definitions/razor/razor';
import * as shell from 'monaco-editor/languages/definitions/shell/shell';
import * as vb from 'monaco-editor/languages/definitions/vb/vb';
import * as xml from 'monaco-editor/languages/definitions/xml/xml';
import { gitignoreDefinition } from '../customLanguages/gitignore.js';
import { regDefinition } from '../customLanguages/reg.js';
import { srtDefinition } from '../customLanguages/srt.js';

// Extra extensions mapped onto an upstream grammar. A null grammar means plain text.
// monaco-editor has no Monarch grammar for JSON, so jsonExt stays plain text as it did with the AMD build.
const aliasLanguages = [
    ['cppExt', ['.ino', '.pde'], cpp],
    ['xmlExt', ['.wsdl', '.projitems', '.csproj', '.fsproj', '.shproj', '.vcxproj', '.vbproj', '.resx', '.resw'], xml],
    ['txtExt', ['.sln', '.log', '.vsconfig', '.env', '.ahk', '.ion'], null],
    ['razorExt', ['.razor'], razor],
    ['vbExt', ['.vbs'], vb],
    ['iniExt', ['.inf', '.gitconfig', '.gitattributes', '.editorconfig'], ini],
    ['shellExt', ['.ksh', '.zsh', '.bsh'], shell],
    ['jsonExt', ['.uplugin', '.uproject'], null],
];

const customLanguages = [
    ['reg', ['.reg'], regDefinition()],
    ['gitignore', ['.gitignore'], gitignoreDefinition()],
    ['srt', ['.srt'], srtDefinition()],
];

export function registerPowerToysLanguages() {
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
