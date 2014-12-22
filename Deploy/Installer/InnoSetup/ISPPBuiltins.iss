; BEGIN ISPPBUILTINS.ISS
//
// Inno Setup Preprocessor 5
//
// Copyright (C) 2001-2004 Alex Yackimoff. All Rights Reserved.
// Portions by Martijn Laan.
// http://ispp.sourceforge.net
//
// Inno Setup (C) 1997-2009 Jordan Russell. All Rights Reserved.
// Portions by Martijn Laan.
//
// $Id: ISPPBuiltins.iss,v 1.3 2010/12/29 15:20:26 mlaan Exp $
//
#if defined(ISPP_INVOKED) && !defined(_BUILTINS_ISS_)
//
#if PREPROCVER < 0x01000000
# error Inno Setup Preprocessor version is outdated
#endif
//
#define _BUILTINS_ISS_
//
// ===========================================================================
//
// Default states for options.
//
//#pragma parseroption -b+ ; short circuit boolean evaluation: on
//#pragma parseroption -m- ; short circuit multiplication evaluation (0 * A will not eval A): off
//#pragma parseroption -p+ ; string literals without escape sequences: on
//#pragma parseroption -u- ; allow undeclared identifiers: off
//#pragma option -c+       ; pass script to the compiler: on
//#pragma option -e-       ; emit empty lines to translation: off
//#pragma option -v-       ; verbose mode: off
//
// ---------------------------------------------------------------------------
//
// Verbose levels:
// 0 - #include and #file acknowledgements
// 1 - information about any temp files created by #file
// 2 - #insert and #append acknowledgements
// 3 - reserved
// 4 - #dim, #define and #undef acknowledgements
// 5 - reserved
// 6 - conditional inclusion acknowledgements
// 7 - reserved
// 8 - show strings emitted with #emit directive
// 9 - macro and functions successfull call acknowledgements
//10 - Local macro array allocation acknowledgements
//
//#pragma verboselevel 0
//
#ifndef __POPT_P__
# define private CStrings
# pragma parseroption -p+
#endif
//
#pragma spansymbol "\"
//
#define True               1
#define False              0
#define Yes                True
#define No								 False
//
#define MaxInt             0x7FFFFFFFL
#define MinInt             0x80000000L
//
#define NULL
#define void
//
// TypeOf constants
//
#define TYPE_ERROR         0
#define TYPE_NULL          1
#define TYPE_INTEGER       2
#define TYPE_STRING        3
#define TYPE_MACRO         4
#define TYPE_FUNC          5
#define TYPE_ARRAY         6
//
// Helper macro to find out the type of an array element or expression. TypeOf
// standard function only allows identifier as its parameter. Use this macro
// to convert an expression to identifier.
//
#define TypeOf2(any Expr) TypeOf(Expr)
//
// ReadReg constants
//
#define HKEY_CLASSES_ROOT  0x80000000UL
#define HKEY_CURRENT_USER  0x80000001UL
#define HKEY_LOCAL_MACHINE 0x80000002UL
#define HKEY_USERS         0x80000003UL
//
#define HKCR               HKEY_CLASSES_ROOT
#define HKCU               HKEY_CURRENT_USER
#define HKLM               HKEY_LOCAL_MACHINE
#define HKU                HKEY_USERS
//
// Exec constants
//
#define SW_HIDE            0
#define SW_SHOWNORMAL      1
#define SW_NORMAL          1
#define SW_SHOWMINIMIZED   2
#define SW_SHOWMAXIMIZED   3
#define SW_MAXIMIZE        3
#define SW_SHOWNOACTIVATE  4
#define SW_SHOW            5
#define SW_MINIMIZE        6
#define SW_SHOWMINNOACTIVE 7
#define SW_SHOWNA          8
#define SW_RESTORE         9
#define SW_SHOWDEFAULT     10
#define SW_MAX             10
//
// Find constants
//
#define FIND_MATCH         0x00
#define FIND_BEGINS        0x01
#define FIND_ENDS          0x02
#define FIND_CONTAINS      0x03
#define FIND_CASESENSITIVE 0x04 
#define FIND_SENSITIVE     FIND_CASESENSITIVE
#define FIND_AND           0x00
#define FIND_OR            0x08
#define FIND_NOT           0x10
#define FIND_TRIM          0x20
//
// FindFirst constants
//
#define faReadOnly         0x00000001
#define faHidden           0x00000002
#define faSysFile          0x00000004
#define faVolumeID         0x00000008
#define faDirectory        0x00000010
#define faArchive          0x00000020
#define faSymLink          0x00000040
#define faAnyFile          0x0000003F
//
// GetStringFileInfo standard names
//
#define COMPANY_NAME       "CompanyName"
#define FILE_DESCRIPTION   "FileDescription"
#define FILE_VERSION       "FileVersion"
#define INTERNAL_NAME      "InternalName"
#define LEGAL_COPYRIGHT    "LegalCopyright"
#define ORIGINAL_FILENAME  "OriginalFilename"
#define PRODUCT_NAME       "ProductName"
#define PRODUCT_VERSION    "ProductVersion"
//
// GetStringFileInfo helpers
//
#define GetFileCompany(str FileName) GetStringFileInfo(FileName, COMPANY_NAME)
#define GetFileCopyright(str FileName) GetStringFileInfo(FileName, LEGAL_COPYRIGHT)
#define GetFileDescription(str FileName) GetStringFileInfo(FileName, FILE_DESCRIPTION)
#define GetFileProductVersion(str FileName) GetStringFileInfo(FileName, PRODUCT_VERSION)
#define GetFileVersionString(str FileName) GetStringFileInfo(FileName, FILE_VERSION)
//
// ParseVersion
//
// Macro internally calls GetFileVersion function and parses string returned
// by that function (in form "0.0.0.0"). All four version elements are stored
// in by-reference parameters Major, Minor, Rev, and Build. Macro returns
// string returned by GetFileVersion.
//
#define DeleteToFirstPeriod(str *S) \
  Local[1] = Copy(S, 1, (Local[0] = Pos(".", S)) - 1), \
  S = Copy(S, Local[0] + 1), \
  Local[1]
//
#define ParseVersion(str FileName, *Major, *Minor, *Rev, *Build) \
  Local[1]  = Local[0] = GetFileVersion(FileName), \
  Local[1] == "" ? "" : ( \
    Major   = Int(DeleteToFirstPeriod(Local[1])), \
    Minor   = Int(DeleteToFirstPeriod(Local[1])), \
    Rev     = Int(DeleteToFirstPeriod(Local[1])), \
    Build   = Int(Local[1]), \
  Local[0])
//
// EncodeVer
//
// Encodes given four version elements to a 32 bit integer number (8 bits for
// each element, i.e. elements must be within 0...255 range).
//
#define EncodeVer(int Major, int Minor, int Revision = 0, int Build = -1) \
  Major << 24 | (Minor & 0xFF) << 16 | (Revision & 0xFF) << 8 | (Build >= 0 ? Build & 0xFF : 0)
//
// DecodeVer
//
// Decodes given 32 bit integer encoded version to its string representation,
// Digits parameter indicates how many elements to show (if the fourth element
// is 0, it won't be shown anyway).
//
#define DecodeVer(int Ver, int Digits = 3) \
  Str(Ver >> 0x18 & 0xFF) + (Digits > 1 ? "." : "") + \
  (Digits > 1 ? \
    Str(Ver >> 0x10 & 0xFF) + (Digits > 2 ? "." : "") : "") + \
  (Digits > 2 ? \
    Str(Ver >> 0x08 & 0xFF) + (Digits > 3 && (Local = Ver & 0xFF) ? "." : "") : "") + \
  (Digits > 3 && Local ? \
    Str(Ver & 0xFF) : "")
//
// FindSection
//
// Returns index of the line following the header of the section. This macro
// is intended to be used with #insert directive.
//
#define FindSection(str Section = "Files") \
  Find(0, "[" + Section + "]", FIND_MATCH | FIND_TRIM) + 1
//
// FindSectionEnd
//
// Returns index of the line following last entry of the section. This macro
// is intended to be used with #insert directive.
//
#if VER >= 0x03000000
# define FindNextSection(int Line) \
    Find(Line, "[", FIND_BEGINS | FIND_TRIM, "]", FIND_ENDS | FIND_AND)
# define FindSectionEnd(str Section = "Files") \
    FindNextSection(FindSection(Section))
#else
# define FindSectionEnd(str Section = "Files") \
    FindSection(Section) + EntryCount(Section)
#endif
//
// FindCode
//
// Returns index of the line (of translation) following either [Code] section
// header, or "program" keyword, if any.
//
#define FindCode() \
    Local[1] = FindSection("Code"), \
    Local[0] = Find(Local[1] - 1, "program", FIND_BEGINS, ";", FIND_ENDS | FIND_AND), \
    (Local[0] < 0 ? Local[1] : Local[0] + 1)
//
// ExtractFilePath
//
// Returns directory portion of the given filename without backslash (unless
// it is a root directory). If PathName doesn't contain directory portion,
// the result is an empty string.
//
#define ExtractFilePath(str PathName) \
  (Local[0] = \
    !(Local[1] = RPos("\", PathName)) ? \
      "" : \
      Copy(PathName, 1, Local[1] - 1)), \
  Local[0] + \
    ((Local[2] = Len(Local[0])) == 2 && Copy(Local[0], Local[2]) == ":" ? \
      "\" : \
      "")
#define ExtractFileDir(str PathName) \
  RemoveBackslash(ExtractFilePath(PathName))

#define ExtractFileExt(str PathName) \
  Local[0] = RPos(".", PathName), \
  Copy(PathName, Local[0] + 1)
//
// ExtractFileName
//
// Returns name portion of the given filename. If PathName ends with
// a backslash, the result is an empty string.
//
#define ExtractFileName(str PathName) \
  !(Local[0] = RPos("\", PathName)) ? \
    PathName : \
    Copy(PathName, Local[0] + 1)
//
// ChangeFileExt
//
// Changes extension in FileName with NewExt. NewExt must not contain
// period.
//
#define ChangeFileExt(str FileName, str NewExt) \
  !(Local[0] = RPos(".", FileName)) ? \
    FileName + "." + NewExt : \
    Copy(FileName, 1, Local[0]) + NewExt
//
// AddBackslash
//
// Adds a backslash to the string, if it's not already there.
//
#define AddBackslash(str S) \
  Copy(S, Len(S)) == "\" ? S : S + "\"
//
// RemoveBackslash
//
// Removes trailing backslash from the string unless the string points to
// a root directory.
//
#define RemoveBackslash(str S) \
  Local[0] = Len(S), \
  Local[0] > 0 ? \
    Copy(S, Local[0]) == "\" ? \
      (Local[0] == 3 && Copy(S, 2, 1) == ":" ? \
        S : \
        Copy(S, 1, Local[0] - 1)) : \
      S : \
    ""
//
// Delete
//
// Deletes specified number of characters beginning with Index from S. S is
// passed by reference (therefore is modified). Acts like Delete function in
// Delphi (from System unit).
//
#define Delete(str *S, int Index, int Count = MaxInt) \
  S = Copy(S, 1, Index - 1) + Copy(S, Index + Count)
//
// Insert
//
// Inserts specified Substr at Index'th character into S. S is passed by
// reference (therefore is modified).
//
#define Insert(str *S, int Index, str Substr) \
  Index > Len(S) + 1 ? \
    S : \
    S = Copy(S, 1, Index - 1) + SubStr + Copy(S, Index)
//
// YesNo, IsDirSet
//
// Returns nonzero value if given string is "yes", "true" or "1". Intended to
// be used with SetupSetting function. This macro replaces YesNo function
// available in previous releases.
//
#define YesNo(str S) \
  (S = LowerCase(S)) == "yes" || S == "true" || S == "1"
//
#define IsDirSet(str SetupDirective) \
  YesNo(SetupSetting(SetupDirective))
//
//
#define Power(int X, int P = 2) \
  !P ? 1 : X * Power(X, P - 1)
//
#define Min(int A, int B, int C = MaxInt)  \
  A < B ? A < C ? Int(A) : Int(C) : Int(B)
//
#define Max(int A, int B, int C = MinInt)  \
  A > B ? A > C ? Int(A) : Int(C) : Int(B)
//

#ifdef CStrings
# pragma parseroption -p-
#endif
#endif
; END ISPPBUILTINS.ISS

