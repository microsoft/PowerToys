export function regDefinition() {
    return {
        tokenPostfix: '.reg',
        tokenizer: {
            root: [
                // Header (case sensitive)
                [/Windows Registry Editor Version 5.00/, 'comment'],
                [/REGEDIT4/, 'comment'],
                // Comments
                [/;.*/, "comment"],
                // Keys
                [/\[\-.*\]/, 'invalid'],
                [/\\.*[^\]]/, 'keyword'],
                // Values
                [/@/, "keyword"],
                [/\".*\"=\-/, "invalid"],
                [/\".*\"(?=\=)/, "keyword"],
                [/\".*\"(?!\=)/, 'string'],
                [/hex\({0,1}[0-9,a,b]\)|hex|dword(?=\:)/, "type"],
                [/[0-9,a-f,A-F][0-9,a-f,A-F],*/, 'string'],
                // Hive names (case in-sensitive)
                [/HKEY_CLASSES_ROOT/, 'type'],
                [/HKEY_LOCAL_MACHINE/, 'type'],
                [/HKEY_USERS/, 'type'],
                [/HKEY_CURRENT_USER/, 'type'],
                [/HKEY_PERFORMANCE_DATA/, 'type'],
                [/HKEY_DYN_DATA/, 'type'],
                [/hkey_classes_root/, 'type'],
                [/hkey_local_machine/, 'type'],
                [/hkey_users/, 'type'],
                [/hkey_current_user/, 'type'],
                [/hkey_performance_data/, 'type'],
                [/hkey_dyn_data/, 'type'],
                // Symbols (For better contrast on hc-black)
                [/=/, 'delimiter'],
                [/\[/, 'delimiter'],
                [/]/, 'delimiter'],
                [/:/, 'delimiter'],
            ]
        }
    }
};