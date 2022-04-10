export function regDefinition() {
    return {
        tokenPostfix: '.reg',
        tokenizer: {
            root: [
                // Header
                [/Windows Registry Editor Version 5.00/, 'keyword'],
                [/REGEDIT4/, 'keyword'],
                // Comments
                [/;.*/, "comment"],
                // Keys
                [/\[/, 'number.float'],
                [/\\.*\]/, 'number.float'],
                // Values
                [/@=/, "keyword"],
                [/\".*\"=/, "keyword"],
                [/\".*\"(?!\=)/, 'string'],
                [/((hex\({0,1}[0-9,a,b]*\){0,1})|dword):.*/, "string"],
                // Hive names
                [/HKEY_CLASSES_ROOT/, 'constant'],
                [/HKEY_LOCAL_MACHINE/, 'constant'],
                [/HKEY_USERS/, 'constant'],
                [/HKEY_CURRENT_USER/, 'constant'],
                [/HKEY_PERFORMANCE_DATA/, 'constant'],
                [/HKEY_DYN_DATA/, 'constant'],
            ]
        }

    }
};