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
	PCTSTR	Valuename;
	REG_SETTING_TYPE	Type;
    DWORD	Size; // Optional
	PVOID   Setting;
	double	DefaultSetting;
} REG_SETTING, *PREG_SETTING;


class CRegistry {

private:
	PTCHAR	m_Keyname;
	HKEY	hKey;

public:
	CRegistry( PCTSTR Keyname ) 
	{
		m_Keyname = _tcsdup( Keyname );
		hKey = NULL;
	}

	~CRegistry()
	{
		free( m_Keyname );
	}

	void ReadRegSettings( PREG_SETTING Settings )
	{
		PREG_SETTING	curSetting;

		hKey = NULL;
		RegOpenKeyEx(HKEY_CURRENT_USER, 
				m_Keyname, 0, KEY_READ, &hKey );
		curSetting = Settings;
		while( curSetting->Valuename ) {

			switch( curSetting->Type ) {
			case SETTING_TYPE_DWORD:
                ReadValue( curSetting->Valuename, static_cast<PDWORD>(curSetting->Setting),
					static_cast<DWORD>(curSetting->DefaultSetting));
				break;
			case SETTING_TYPE_BOOLEAN:
                ReadValue( curSetting->Valuename, static_cast<PBOOLEAN>(curSetting->Setting),
					static_cast<BOOLEAN>(curSetting->DefaultSetting));
				break;
			case SETTING_TYPE_DOUBLE:
                ReadValue( curSetting->Valuename, static_cast<double *>(curSetting->Setting),
					curSetting->DefaultSetting );
				break;
			case SETTING_TYPE_WORD:
                ReadValue( curSetting->Valuename, static_cast<short *>(curSetting->Setting),
					static_cast<WORD>(curSetting->DefaultSetting));
				break;
			case SETTING_TYPE_STRING:
				ReadValue( curSetting->Valuename, static_cast<PTCHAR>(curSetting->Setting),
					curSetting->Size, reinterpret_cast<PTCHAR>(static_cast<DWORD_PTR>(curSetting->DefaultSetting)));
				break;
			case SETTING_TYPE_DWORD_ARRAY:
				ReadValueArray( curSetting->Valuename, curSetting->Size/sizeof DWORD,
					static_cast<PWORD>(curSetting->Setting));
				break;
			case SETTING_TYPE_WORD_ARRAY:
				ReadValueArray( curSetting->Valuename, curSetting->Size/sizeof(short),
					static_cast<PWORD>(curSetting->Setting));
				break;
			case SETTING_TYPE_BINARY:
				ReadValueBinary( curSetting->Valuename, static_cast<PBYTE>(curSetting->Setting),
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
				m_Keyname, NULL, NULL, 0, KEY_WRITE, NULL, &hKey, NULL )) {
					
			curSetting = Settings;
			while( curSetting->Valuename ) {

				switch( curSetting->Type ) {
				case SETTING_TYPE_DWORD:
					WriteValue( curSetting->Valuename, *static_cast<PDWORD>(curSetting->Setting));
					break;
				case SETTING_TYPE_BOOLEAN:
                    WriteValue( curSetting->Valuename, *static_cast<PBOOLEAN>(curSetting->Setting));
					break;
				case SETTING_TYPE_DOUBLE:
                    WriteValue( curSetting->Valuename, *static_cast<double *>(curSetting->Setting));
					break;
				case SETTING_TYPE_WORD:
					WriteValue( curSetting->Valuename, *static_cast<short *>(curSetting->Setting));
					break;
				case SETTING_TYPE_STRING:
                    WriteValue( curSetting->Valuename, static_cast<PTCHAR>(curSetting->Setting));
					break;
				case SETTING_TYPE_DWORD_ARRAY:
					WriteValueArray( curSetting->Valuename, curSetting->Size/sizeof DWORD,
						static_cast<PDWORD>(curSetting->Setting));
					break;
				case SETTING_TYPE_WORD_ARRAY:
					WriteValueArray( curSetting->Valuename, curSetting->Size/sizeof(short),
						static_cast<PWORD>(curSetting->Setting));
					break;
				case SETTING_TYPE_BINARY:
                    WriteValueBinary( curSetting->Valuename, static_cast<PBYTE>(curSetting->Setting),
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
	void ReadValue( PCTSTR Valuename, PDWORD Value, DWORD Default = 0 )
	{
		DWORD	length = sizeof(DWORD);
		if( RegQueryValueEx( hKey, Valuename, NULL, NULL, reinterpret_cast<PBYTE>(Value),
			&length )) {
	
			*Value = Default;
		}
	}
	void ReadValue( PCTSTR Valuename, PBOOLEAN Value, BOOLEAN Default = FALSE )
	{
		DWORD	length = sizeof(DWORD);
        DWORD	val = static_cast<DWORD>(*Value);
		if( RegQueryValueEx( hKey, Valuename, NULL, NULL, reinterpret_cast<PBYTE>(&val),
			&length )) {

			*Value = Default;

		} else {

			*Value = static_cast<BOOLEAN>(val);
		}
	}
	void ReadValue( PCTSTR Valuename, short *Value, short Default = 0 )
	{
		DWORD	length = sizeof(DWORD);
        DWORD	val = static_cast<DWORD>(*Value);
		if( RegQueryValueEx( hKey, Valuename, NULL, NULL, reinterpret_cast<PBYTE>(&val),
			&length )) {
	
			*Value = Default;
		
		} else {

			*Value = static_cast<short>(val);
		}
	}
	void ReadValue( PCTSTR Valuename, double *Value, double Default = 0.0 )
	{
		DWORD	length = sizeof(double);
		if( RegQueryValueEx( hKey, Valuename, NULL, NULL, reinterpret_cast<PBYTE>(Value),
			&length )) {
	
			*Value = Default;

		} 
	}
	void ReadValue( PCTSTR Valuename, PTCHAR Value, DWORD Length, PCTSTR Default )
	{
		if( RegQueryValueEx( hKey, Valuename, NULL, NULL, reinterpret_cast<PBYTE>(Value),
			&Length ) && Default ) {
	
			_tcscpy_s( Value, Length, Default );
		}
	}
	void ReadValueBinary( PCTSTR Valuename, PBYTE Value, DWORD Length )
	{
		RegQueryValueEx( hKey, Valuename, NULL, NULL, Value,
			&Length );
	}
	void ReadValueArray( PCTSTR Valuename, DWORD Number, PDWORD Entries )
	{
		HKEY	hSubKey;
		TCHAR	subVal[16];
		DWORD	length;

		if( !RegOpenKeyEx(hKey, 
				Valuename, 0, KEY_READ, &hSubKey )) {	

			for( DWORD i = 0; i < Number; i++ ) {
			
				length = sizeof(DWORD);
				_stprintf_s( subVal, _countof(subVal), _T("%d"), i );
				RegQueryValueEx( hSubKey, subVal, NULL, NULL, reinterpret_cast<PBYTE>(&Entries[i]), &length);
			}
			RegCloseKey( hSubKey );
		}
	}	
	void ReadValueArray( PCTSTR Valuename, DWORD Number, PWORD Entries )
	{
		HKEY	hSubKey;
		TCHAR	subVal[16];
		DWORD	length;
		DWORD	val;

		if( !RegOpenKeyEx(hKey, 
				Valuename, 0, KEY_READ, &hSubKey )) {	

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
	void WriteValue( PCTSTR Valuename, DWORD Value ) 
	{
		RegSetValueEx( hKey, Valuename, 0, REG_DWORD, reinterpret_cast<PBYTE>(&Value),
				sizeof(DWORD));
	}
	void WriteValue( PCTSTR Valuename, short Value ) 
	{
        DWORD val = static_cast<DWORD>(Value);
		RegSetValueEx( hKey, Valuename, 0, REG_DWORD, reinterpret_cast<PBYTE>(&val),
				sizeof(DWORD));
	}
	void WriteValue( PCTSTR Valuename, BOOLEAN Value ) 
	{
        DWORD val = static_cast<DWORD>(Value);
		RegSetValueEx( hKey, Valuename, 0, REG_DWORD, reinterpret_cast<PBYTE>(&val),
				sizeof(DWORD));
	}
	void WriteValue( PCTSTR Valuename, double Value ) 
	{
		RegSetValueEx( hKey, Valuename, 0, REG_BINARY, reinterpret_cast<PBYTE>(&Value),
				sizeof(double));
	}
	void WriteValue( PCTSTR Valuename, PTCHAR Value ) 
	{
		RegSetValueEx( hKey, Valuename, 0, REG_SZ, reinterpret_cast<PBYTE>(Value),
				static_cast<DWORD>(_tcslen( Value )) * sizeof(TCHAR));
	}
	void WriteValueBinary( PCTSTR Valuename, PBYTE Value, DWORD Length ) 
	{
		RegSetValueEx( hKey, Valuename, 0, REG_BINARY, Value,
				Length );
	}
	void WriteValueArray( PCTSTR Valuename, DWORD Number, PDWORD Entries )
	{
		HKEY	hSubKey;
		TCHAR	subVal[16];

		if( !RegCreateKeyEx(hKey, 
				Valuename, NULL, NULL, 0, KEY_WRITE, NULL, &hSubKey, NULL )) {	

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
	void WriteValueArray( PCTSTR Valuename, DWORD Number, PWORD Entries )
	{
		HKEY	hSubKey;
		TCHAR	subVal[16];
		DWORD	val;

		if( !RegCreateKeyEx(hKey, 
				Valuename, NULL, NULL, 0, KEY_WRITE, NULL, &hSubKey, NULL )) {	

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
