//============================================================================
//
// Process Explorer
// Copyright (C) 1999-2005 Mark Russinovich
// Sysinternals - www.sysinternals.com
//
// Registry.h
//
//============================================================================
#pragma once

typedef enum {
    SETTING_TYPE_DWORD,
    SETTING_TYPE_BOOLEAN,
    SETTING_TYPE_DOUBLE,
    SETTING_TYPE_WORD,
    SETTING_TYPE_STRING,
    SETTING_TYPE_DWORD_ARRAY,
    SETTING_TYPE_WORD_ARRAY,
    SETTING_TYPE_BINARY
} REG_SETTING_TYPE;

typedef struct {
    PCTSTR	ValueName;
    REG_SETTING_TYPE	Type;
    DWORD	Size; // Optional
    PVOID   Setting;
    double	DefaultSetting;
} REG_SETTING, *PREG_SETTING;


class ClassRegistry {

private:
    PTCHAR	m_KeyName;
    HKEY	hKey;

public:
    ClassRegistry( PCTSTR KeyName ) 
    {
        m_KeyName = _tcsdup( KeyName );
        hKey = NULL;
    }

    ~ClassRegistry()
    {
        free( m_KeyName );
    }

    void ReadRegSettings( PREG_SETTING Settings )
    {
        PREG_SETTING	curSetting;

        hKey = NULL;
        RegOpenKeyEx(HKEY_CURRENT_USER, 
                m_KeyName, 0, KEY_READ, &hKey );
        curSetting = Settings;
        while( curSetting->ValueName ) {

            switch( curSetting->Type ) {
            case SETTING_TYPE_DWORD:
                ReadValue( curSetting->ValueName, static_cast<PDWORD>(curSetting->Setting),
                    static_cast<DWORD>(curSetting->DefaultSetting));
                break;
            case SETTING_TYPE_BOOLEAN:
                ReadValue( curSetting->ValueName, static_cast<PBOOLEAN>(curSetting->Setting),
                    static_cast<BOOLEAN>(curSetting->DefaultSetting));
                break;
            case SETTING_TYPE_DOUBLE:
                ReadValue( curSetting->ValueName, static_cast<double *>(curSetting->Setting),
                    curSetting->DefaultSetting );
                break;
            case SETTING_TYPE_WORD:
                ReadValue( curSetting->ValueName, static_cast<short *>(curSetting->Setting),
                    static_cast<WORD>(curSetting->DefaultSetting));
                break;
            case SETTING_TYPE_STRING:
                ReadValue( curSetting->ValueName, static_cast<PTCHAR>(curSetting->Setting),
                    curSetting->Size, reinterpret_cast<PTCHAR>(static_cast<DWORD_PTR>(curSetting->DefaultSetting)));
                break;
            case SETTING_TYPE_DWORD_ARRAY:
                ReadValueArray( curSetting->ValueName, curSetting->Size/sizeof DWORD,
                    static_cast<PWORD>(curSetting->Setting));
                break;
            case SETTING_TYPE_WORD_ARRAY:
                ReadValueArray( curSetting->ValueName, curSetting->Size/sizeof(short),
                    static_cast<PWORD>(curSetting->Setting));
                break;
            case SETTING_TYPE_BINARY:
                ReadValueBinary( curSetting->ValueName, static_cast<PBYTE>(curSetting->Setting),
                    curSetting->Size );
                break;
            }
            curSetting++;
        }
        if( hKey ) {

            RegCloseKey( hKey );
        }
    }
    void WriteRegSettings( PREG_SETTING Settings )
    {
        PREG_SETTING	curSetting;

        if( !RegCreateKeyEx(HKEY_CURRENT_USER, 
                m_KeyName, NULL, NULL, 0, KEY_WRITE, NULL, &hKey, NULL )) {
                    
            curSetting = Settings;
            while( curSetting->ValueName ) {

                switch( curSetting->Type ) {
                case SETTING_TYPE_DWORD:
                    WriteValue( curSetting->ValueName, *static_cast<PDWORD>(curSetting->Setting));
                    break;
                case SETTING_TYPE_BOOLEAN:
                    WriteValue( curSetting->ValueName, *static_cast<PBOOLEAN>(curSetting->Setting));
                    break;
                case SETTING_TYPE_DOUBLE:
                    WriteValue( curSetting->ValueName, *static_cast<double *>(curSetting->Setting));
                    break;
                case SETTING_TYPE_WORD:
                    WriteValue( curSetting->ValueName, *static_cast<short *>(curSetting->Setting));
                    break;
                case SETTING_TYPE_STRING:
                    WriteValue( curSetting->ValueName, static_cast<PTCHAR>(curSetting->Setting));
                    break;
                case SETTING_TYPE_DWORD_ARRAY:
                    WriteValueArray( curSetting->ValueName, curSetting->Size/sizeof DWORD,
                        static_cast<PDWORD>(curSetting->Setting));
                    break;
                case SETTING_TYPE_WORD_ARRAY:
                    WriteValueArray( curSetting->ValueName, curSetting->Size/sizeof(short),
                        static_cast<PWORD>(curSetting->Setting));
                    break;
                case SETTING_TYPE_BINARY:
                    WriteValueBinary( curSetting->ValueName, static_cast<PBYTE>(curSetting->Setting),
                        curSetting->Size );
                    break;
                }
                curSetting++;
            }
            RegCloseKey( hKey );
        }
    }

private:
    // Reads
    void ReadValue( PCTSTR ValueName, PDWORD Value, DWORD Default = 0 )
    {
        DWORD	length = sizeof(DWORD);
        if( RegQueryValueEx( hKey, ValueName, NULL, NULL, reinterpret_cast<PBYTE>(Value),
            &length )) {
    
            *Value = Default;
        }
    }
    void ReadValue( PCTSTR ValueName, PBOOLEAN Value, BOOLEAN Default = FALSE )
    {
        DWORD	length = sizeof(DWORD);
        DWORD	val = static_cast<DWORD>(*Value);
        if( RegQueryValueEx( hKey, ValueName, NULL, NULL, reinterpret_cast<PBYTE>(&val),
            &length )) {

            *Value = Default;

        } else {

            *Value = static_cast<BOOLEAN>(val);
        }
    }
    void ReadValue( PCTSTR ValueName, short *Value, short Default = 0 )
    {
        DWORD	length = sizeof(DWORD);
        DWORD	val = static_cast<DWORD>(*Value);
        if( RegQueryValueEx( hKey, ValueName, NULL, NULL, reinterpret_cast<PBYTE>(&val),
            &length )) {
    
            *Value = Default;
        
        } else {

            *Value = static_cast<short>(val);
        }
    }
    void ReadValue( PCTSTR ValueName, double *Value, double Default = 0.0 )
    {
        DWORD	length = sizeof(double);
        if( RegQueryValueEx( hKey, ValueName, NULL, NULL, reinterpret_cast<PBYTE>(Value),
            &length )) {
    
            *Value = Default;

        } 
    }
    void ReadValue( PCTSTR ValueName, PTCHAR Value, DWORD Length, PCTSTR Default )
    {
        if( RegQueryValueEx( hKey, ValueName, NULL, NULL, reinterpret_cast<PBYTE>(Value),
            &Length ) && Default ) {
    
            _tcscpy_s( Value, Length, Default );
        }
    }
    void ReadValueBinary( PCTSTR ValueName, PBYTE Value, DWORD Length )
    {
        RegQueryValueEx( hKey, ValueName, NULL, NULL, Value,
            &Length );
    }
    void ReadValueArray( PCTSTR ValueName, DWORD Number, PDWORD Entries )
    {
        HKEY	hSubKey;
        TCHAR	subVal[16];
        DWORD	length;

        if( !RegOpenKeyEx(hKey, 
                ValueName, 0, KEY_READ, &hSubKey )) {	

            for( DWORD i = 0; i < Number; i++ ) {
            
                length = sizeof(DWORD);
                _stprintf_s( subVal, _countof(subVal), _T("%d"), i );
                RegQueryValueEx( hSubKey, subVal, NULL, NULL, reinterpret_cast<PBYTE>(&Entries[i]), &length);
            }
            RegCloseKey( hSubKey );
        }
    }	
    void ReadValueArray( PCTSTR ValueName, DWORD Number, PWORD Entries )
    {
        HKEY	hSubKey;
        TCHAR	subVal[16];
        DWORD	length;
        DWORD	val;

        if( !RegOpenKeyEx(hKey, 
                ValueName, 0, KEY_READ, &hSubKey )) {	

            for( DWORD i = 0; i < Number; i++ ) {
            
                length = sizeof(DWORD);
                _stprintf_s( subVal, _countof(subVal), _T("%d"), i );
                if( !RegQueryValueEx( hSubKey, subVal, NULL, NULL, reinterpret_cast<PBYTE>(&val), &length)) {
                
                    Entries[i] = static_cast<WORD>(val);

                } 
            }
            RegCloseKey( hSubKey );
        }
    }	

    // Writes
    void WriteValue( PCTSTR ValueName, DWORD Value ) 
    {
        RegSetValueEx( hKey, ValueName, 0, REG_DWORD, reinterpret_cast<PBYTE>(&Value),
                sizeof(DWORD));
    }
    void WriteValue( PCTSTR ValueName, short Value ) 
    {
        DWORD val = static_cast<DWORD>(Value);
        RegSetValueEx( hKey, ValueName, 0, REG_DWORD, reinterpret_cast<PBYTE>(&val),
                sizeof(DWORD));
    }
    void WriteValue( PCTSTR ValueName, BOOLEAN Value ) 
    {
        DWORD val = static_cast<DWORD>(Value);
        RegSetValueEx( hKey, ValueName, 0, REG_DWORD, reinterpret_cast<PBYTE>(&val),
                sizeof(DWORD));
    }
    void WriteValue( PCTSTR ValueName, double Value ) 
    {
        RegSetValueEx( hKey, ValueName, 0, REG_BINARY, reinterpret_cast<PBYTE>(&Value),
                sizeof(double));
    }
    void WriteValue( PCTSTR ValueName, PTCHAR Value ) 
    {
        RegSetValueEx( hKey, ValueName, 0, REG_SZ, reinterpret_cast<PBYTE>(Value),
                static_cast<DWORD>(_tcslen( Value )) * sizeof(TCHAR));
    }
    void WriteValueBinary( PCTSTR ValueName, PBYTE Value, DWORD Length ) 
    {
        RegSetValueEx( hKey, ValueName, 0, REG_BINARY, Value,
                Length );
    }
    void WriteValueArray( PCTSTR ValueName, DWORD Number, PDWORD Entries )
    {
        HKEY	hSubKey;
        TCHAR	subVal[16];

        if( !RegCreateKeyEx(hKey, 
                ValueName, NULL, NULL, 0, KEY_WRITE, NULL, &hSubKey, NULL )) {	

            for( DWORD i = 0; i < Number; i++ ) {
            
                _stprintf_s( subVal, _countof(subVal), _T("%d"), i );
                if( Entries[i] ) 
                    RegSetValueEx( hSubKey, subVal, 0, REG_DWORD, reinterpret_cast<PBYTE>(&Entries[i]), sizeof(DWORD));
                else
                    RegDeleteValue( hSubKey, subVal );
            }
            RegCloseKey( hSubKey );
        }
    }
    void WriteValueArray( PCTSTR ValueName, DWORD Number, PWORD Entries )
    {
        HKEY	hSubKey;
        TCHAR	subVal[16];
        DWORD	val;

        if( !RegCreateKeyEx(hKey, 
                ValueName, NULL, NULL, 0, KEY_WRITE, NULL, &hSubKey, NULL )) {	

            for( DWORD i = 0; i < Number; i++ ) {
            
                _stprintf_s( subVal, _countof(subVal), _T("%d"), i );
                val = static_cast<DWORD>(Entries[i]);
                if( Entries[i] ) 
                    RegSetValueEx( hSubKey, subVal, 0, REG_DWORD, reinterpret_cast<PBYTE>(&val), sizeof(DWORD));
                else
                    RegDeleteValue( hSubKey, subVal );
            }
            RegCloseKey( hSubKey );
        }
    }

};
