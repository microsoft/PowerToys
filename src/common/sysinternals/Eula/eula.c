#pragma once

#pragma warning( disable: 4996) 

#include <windows.h>
#include <stdio.h>
#include <tchar.h>
#include <Richedit.h>
#include <conio.h>
#include <fcntl.h>
#include <io.h>
#include "Eula.h"
#include "dll.h"

#define	IDC_TEXT	500
#define IDC_PRINT	501
#define	IDC_TEXT1	502

static const char * EulaText[] = {
"{\\rtf1\\ansi\\ansicpg1252\\deff0\\nouicompat\\deflang1033{\\fonttbl{\\f0\\fswiss\\fprq2\\fcharset0 Tahoma;}{\\f1\\fnil\\fcharset0 Calibri;}}",
"{\\colortbl ;\\red0\\green0\\blue255;\\red0\\green0\\blue0;}",
"{\\*\\generator Riched20 10.0.10240}\\viewkind4\\uc1 ",
"\\pard\\brdrb\\brdrs\\brdrw10\\brsp20 \\sb120\\sa120\\b\\f0\\fs24 SYSINTERNALS SOFTWARE LICENSE TERMS\\fs28\\par",
"\\pard\\sb120\\sa120\\b0\\fs19 These license terms are an agreement between Sysinternals (a wholly owned subsidiary of Microsoft Corporation) and you.  Please read them.  They apply to the software you are downloading from Sysinternals.com, which includes the media on which you received it, if any.  The terms also apply to any Sysinternals\\par",
"\\pard\\fi-363\\li720\\sb120\\sa120\\tx720\\'b7\\tab updates,\\par",
"\\pard\\fi-363\\li720\\sb120\\sa120\\'b7\\tab supplements,\\par",
"\\'b7\\tab Internet-based services, and \\par",
"\\'b7\\tab support services\\par",
"\\pard\\sb120\\sa120 for this software, unless other terms accompany those items.  If so, those terms apply.\\par",
"\\b BY USING THE SOFTWARE, YOU ACCEPT THESE TERMS.  IF YOU DO NOT ACCEPT THEM, DO NOT USE THE SOFTWARE.\\par",
"\\pard\\brdrt\\brdrs\\brdrw10\\brsp20 \\sb120\\sa120 If you comply with these license terms, you have the rights below.\\par",
"\\pard\\fi-357\\li357\\sb120\\sa120\\tx360\\fs20 1.\\tab\\fs19 INSTALLATION AND USE RIGHTS.  \\b0 You may install and use any number of copies of the software on your devices.\\b\\par",
"\\caps\\fs20 2.\\tab\\fs19 Scope of License\\caps0 .\\b0   The software is licensed, not sold. This agreement only gives you some rights to use the software.  Sysinternals reserves all other rights.  Unless applicable law gives you more rights despite this limitation, you may use the software only as expressly permitted in this agreement.  In doing so, you must comply with any technical limitations in the software that only allow you to use it in certain ways.    You may not\\b\\par",
"\\pard\\fi-363\\li720\\sb120\\sa120\\tx720\\b0\\'b7\\tab work around any technical limitations in the binary versions of the software;\\par",
"\\pard\\fi-363\\li720\\sb120\\sa120\\'b7\\tab reverse engineer, decompile or disassemble the binary versions of the software, except and only to the extent that applicable law expressly permits, despite this limitation;\\par",
"\\'b7\\tab make more copies of the software than specified in this agreement or allowed by applicable law, despite this limitation;\\par",
"\\'b7\\tab publish the software for others to copy;\\par",
"\\'b7\\tab rent, lease or lend the software;\\par",
"\\'b7\\tab transfer the software or this agreement to any third party; or\\par",
"\\'b7\\tab use the software for commercial software hosting services.\\par",
"\\pard\\fi-357\\li357\\sb120\\sa120\\tx360\\b\\fs20 3.\\tab SENSITIVE INFORMATION. \\b0  Please be aware that, similar to other debug tools that capture \\ldblquote process state\\rdblquote  information, files saved by Sysinternals tools may include personally identifiable or other sensitive information (such as usernames, passwords, paths to files accessed, and paths to registry accessed). By using this software, you acknowledge that you are aware of this and take sole responsibility for any personally identifiable or other sensitive information provided to Microsoft or any other party through your use of the software.\\b\\par",
"5. \\tab\\fs19 DOCUMENTATION.\\b0   Any person that has valid access to your computer or internal network may copy and use the documentation for your internal, reference purposes.\\b\\par",
"\\caps\\fs20 6.\\tab\\fs19 Export Restrictions\\caps0 .\\b0   The software is subject to United States export laws and regulations.  You must comply with all domestic and international export laws and regulations that apply to the software.  These laws include restrictions on destinations, end users and end use.  For additional information, see {\\cf1\\ul{\\field{\\*\\fldinst{HYPERLINK www.microsoft.com/exporting }}{\\fldrslt{www.microsoft.com/exporting}}}}\\cf1\\ul\\f0\\fs19  <{{\\field{\\*\\fldinst{HYPERLINK \"http://www.microsoft.com/exporting\"}}{\\fldrslt{http://www.microsoft.com/exporting}}}}\\f0\\fs19 >\\cf0\\ulnone .\\b\\par",
"\\caps\\fs20 7.\\tab\\fs19 SUPPORT SERVICES.\\caps0  \\b0 Because this software is \"as is, \" we may not provide support services for it.\\b\\par",
"\\caps\\fs20 8.\\tab\\fs19 Entire Agreement.\\b0\\caps0   This agreement, and the terms for supplements, updates, Internet-based services and support services that you use, are the entire agreement for the software and support services.\\par",
"\\pard\\keepn\\fi-360\\li360\\sb120\\sa120\\tx360\\cf2\\b\\caps\\fs20 9.\\tab\\fs19 Applicable Law\\caps0 .\\par",
"\\pard\\fi-363\\li720\\sb120\\sa120\\tx720\\cf0\\fs20 a.\\tab\\fs19 United States.\\b0   If you acquired the software in the United States, Washington state law governs the interpretation of this agreement and applies to claims for breach of it, regardless of conflict of laws principles.  The laws of the state where you live govern all other claims, including claims under state consumer protection laws, unfair competition laws, and in tort.\\b\\par",
"\\pard\\fi-363\\li720\\sb120\\sa120\\fs20 b.\\tab\\fs19 Outside the United States.\\b0   If you acquired the software in any other country, the laws of that country apply.\\b\\par",
"\\pard\\fi-357\\li357\\sb120\\sa120\\tx360\\caps\\fs20 10.\\tab\\fs19 Legal Effect.\\b0\\caps0   This agreement describes certain legal rights.  You may have other rights under the laws of your country.  You may also have rights with respect to the party from whom you acquired the software.  This agreement does not change your rights under the laws of your country if the laws of your country do not permit it to do so.\\b\\caps\\par",
"\\fs20 11.\\tab\\fs19 Disclaimer of Warranty.\\caps0    \\caps The software is licensed \"as - is.\"  You bear the risk of using it.  SYSINTERNALS gives no express warranties, guarantees or conditions.  You may have additional consumer rights under your local laws which this agreement cannot change.  To the extent permitted under your local laws, SYSINTERNALS excludes the implied warranties of merchantability, fitness for a particular purpose and non-infringement.\\par",
"\\pard\\fi-360\\li360\\sb120\\sa120\\tx360\\fs20 12.\\tab\\fs19 Limitation on and Exclusion of Remedies and Damages.  You can recover from SYSINTERNALS and its suppliers only direct damages up to U.S. $5.00.  You cannot recover any other damages, including consequential, lost profits, special, indirect or incidental damages.\\par",
"\\pard\\li357\\sb120\\sa120\\b0\\caps0 This limitation applies to\\par",
"\\pard\\fi-363\\li720\\sb120\\sa120\\tx720\\'b7\\tab anything related to the software, services, content (including code) on third party Internet sites, or third party programs; and\\par",
"\\pard\\fi-363\\li720\\sb120\\sa120\\'b7\\tab claims for breach of contract, breach of warranty, guarantee or condition, strict liability, negligence, or other tort to the extent permitted by applicable law.\\par",
"\\pard\\li360\\sb120\\sa120 It also applies even if Sysinternals knew or should have known about the possibility of the damages.  The above limitation or exclusion may not apply to you because your country may not allow the exclusion or limitation of incidental, consequential or other damages.\\par",
"\\pard\\b Please note: As this software is distributed in Quebec, Canada, some of the clauses in this agreement are provided below in French.\\par",
"\\pard\\sb240\\lang1036 Remarque : Ce logiciel \\'e9tant distribu\\'e9 au Qu\\'e9bec, Canada, certaines des clauses dans ce contrat sont fournies ci-dessous en fran\\'e7ais.\\par",
"\\pard\\sb120\\sa120 EXON\\'c9RATION DE GARANTIE.\\b0  Le logiciel vis\\'e9 par une licence est offert \\'ab tel quel \\'bb. Toute utilisation de ce logiciel est \\'e0 votre seule risque et p\\'e9ril. Sysinternals n'accorde aucune autre garantie expresse. Vous pouvez b\\'e9n\\'e9ficier de droits additionnels en vertu du droit local sur la protection dues consommateurs, que ce contrat ne peut modifier. La ou elles sont permises par le droit locale, les garanties implicites de qualit\\'e9 marchande, d'ad\\'e9quation \\'e0 un usage particulier et d'absence de contrefa\\'e7on sont exclues.\\par",
"\\pard\\keepn\\sb120\\sa120\\b LIMITATION DES DOMMAGES-INT\\'c9R\\'caTS ET EXCLUSION DE RESPONSABILIT\\'c9 POUR LES DOMMAGES.\\b0   Vous pouvez obtenir de Sysinternals et de ses fournisseurs une indemnisation en cas de dommages directs uniquement \\'e0 hauteur de 5,00 $ US. Vous ne pouvez pr\\'e9tendre \\'e0 aucune indemnisation pour les autres dommages, y compris les dommages sp\\'e9ciaux, indirects ou accessoires et pertes de b\\'e9n\\'e9fices.\\par",
"\\lang1033 Cette limitation concerne :\\par",
"\\pard\\keepn\\fi-360\\li720\\sb120\\sa120\\tx720\\lang1036\\'b7\\tab tout  ce qui est reli\\'e9 au logiciel, aux services ou au contenu (y compris le code) figurant sur des sites Internet tiers ou dans des programmes tiers ; et\\par",
"\\pard\\fi-363\\li720\\sb120\\sa120\\tx720\\'b7\\tab les r\\'e9clamations au titre de violation de contrat ou de garantie, ou au titre de responsabilit\\'e9 stricte, de n\\'e9gligence ou d'une autre faute dans la limite autoris\\'e9e par la loi en vigueur.\\par",
"\\pard\\sb120\\sa120 Elle s'applique \\'e9galement, m\\'eame si Sysinternals connaissait ou devrait conna\\'eetre l'\\'e9ventualit\\'e9 d'un tel dommage.  Si votre pays n'autorise pas l'exclusion ou la limitation de responsabilit\\'e9 pour les dommages indirects, accessoires ou de quelque nature que ce soit, il se peut que la limitation ou l'exclusion ci-dessus ne s'appliquera pas \\'e0 votre \\'e9gard.\\par",
"\\b EFFET JURIDIQUE.\\b0   Le pr\\'e9sent contrat d\\'e9crit certains droits juridiques. Vous pourriez avoir d'autres droits pr\\'e9vus par les lois de votre pays.  Le pr\\'e9sent contrat ne modifie pas les droits que vous conf\\'e8rent les lois de votre pays si celles-ci ne le permettent pas.\\b\\par",
"\\pard\\b0\\fs20\\lang1033\\par",
"\\pard\\sa200\\sl276\\slmult1\\f1\\fs22\\lang9\\par",
"}",
NULL
};
 
static const wchar_t *Raw_EulaText = L"SYSINTERNALS SOFTWARE LICENSE TERMS\nThese license terms are an agreement between Sysinternals(a wholly owned subsidiary of Microsoft Corporation) and you.Please read them.They apply to the software you are downloading from technet.microsoft.com / sysinternals, which includes the media on which you received it, if any.The terms also apply to any Sysinternals\n* updates,\n*supplements,\n*Internet - based services,\n*and support services\nfor this software, unless other terms accompany those items.If so, those terms apply.\nBY USING THE SOFTWARE, YOU ACCEPT THESE TERMS.IF YOU DO NOT ACCEPT THEM, DO NOT USE THE SOFTWARE.\n\nIf you comply with these license terms, you have the rights below.\nINSTALLATION AND USER RIGHTS\nYou may install and use any number of copies of the software on your devices.\n\nSCOPE OF LICENSE\nThe software is licensed, not sold.This agreement only gives you some rights to use the software.Sysinternals reserves all other rights.Unless applicable law gives you more rights despite this limitation, you may use the software only as expressly permitted in this agreement.In doing so, you must comply with any technical limitations in the software that only allow you to use it in certain ways.You may not\n* work around any technical limitations in the software;\n*reverse engineer, decompile or disassemble the software, except and only to the extent that applicable law expressly permits, despite this limitation;\n*make more copies of the software than specified in this agreement or allowed by applicable law, despite this limitation;\n*publish the software for others to copy;\n*rent, lease or lend the software;\n*transfer the software or this agreement to any third party; or\n* use the software for commercial software hosting services.\n\nSENSITIVE INFORMATION\nPlease be aware that, similar to other debug tools that capture “process state” information, files saved by Sysinternals tools may include personally identifiable or other sensitive information(such as usernames, passwords, paths to files accessed, and paths to registry accessed).By using this software, you acknowledge that you are aware of this and take sole responsibility for any personally identifiable or other sensitive information provided to Microsoft or any other party through your use of the software.\n\nDOCUMENTATION\nAny person that has valid access to your computer or internal network may copy and use the documentation for your internal, reference purposes.\n\nEXPORT RESTRICTIONS\nThe software is subject to United States export laws and regulations.You must comply with all domestic and international export laws and regulations that apply to the software.These laws include restrictions on destinations, end users and end use.For additional information, see www.microsoft.com / exporting .\n\nSUPPORT SERVICES\nBecause this software is \"as is, \" we may not provide support services for it.\n\nENTIRE AGREEMENT\nThis agreement, and the terms for supplements, updates, Internet - based services and support services that you use, are the entire agreement for the software and support services.\n\nAPPLICABLE LAW\nUnited States.If you acquired the software in the United States, Washington state law governs the interpretation of this agreement and applies to claims for breach of it, regardless of conflict of laws principles.The laws of the state where you live govern all other claims, including claims under state consumer protection laws, unfair competition laws, and in tort.\nOutside the United States.If you acquired the software in any other country, the laws of that country apply.\n\nLEGAL EFFECT\nThis agreement describes certain legal rights.You may have other rights under the laws of your country.You may also have rights with respect to the party from whom you acquired the software.This agreement does not change your rights under the laws of your country if the laws of your country do not permit it to do so.\n\nDISCLAIMER OF WARRANTY\nThe software is licensed \"as - is.\" You bear the risk of using it.Sysinternals gives no express warranties, guarantees or conditions.You may have additional consumer rights under your local laws which this agreement cannot change.To the extent permitted under your local laws, sysinternals excludes the implied warranties of merchantability, fitness for a particular purpose and non - infringement.\n\nLIMITATION ON AND EXCLUSION OF REMEDIES AND DAMAGES\nYou can recover from sysinternals and its suppliers only direct damages up to U.S.$5.00.You cannot recover any other damages, including consequential, lost profits, special, indirect or incidental damages.\nThis limitation applies to\n* anything related to the software, services, content(including code) on third party Internet sites, or third party programs; and\n* claims for breach of contract, breach of warranty, guarantee or condition, strict liability, negligence, or other tort to the extent permitted by applicable law.\nIt also applies even if Sysinternals knew or should have known about the possibility of the damages.The above limitation or exclusion may not apply to you because your country may not allow the exclusion or limitation of incidental, consequential or other damages.\nPlease note : As this software is distributed in Quebec, Canada, some of the clauses in this agreement are provided below in French.\nRemarque : Ce logiciel étant distribué au Québec, Canada, certaines des clauses dans ce contrat sont fournies ci - dessous en français.\n		   EXONÉRATION DE GARANTIE.Le logiciel visé par une licence est offert « tel quel ».Toute utilisation de ce logiciel est à votre seule risque et péril.Sysinternals n'accorde aucune autre garantie expresse. Vous pouvez bénéficier de droits additionnels en vertu du droit local sur la protection dues consommateurs, que ce contrat ne peut modifier. La ou elles sont permises par le droit locale, les garanties implicites de qualité marchande, d'adéquation à un usage particulier et d'absence de contrefaçon sont exclues.\n		   LIMITATION DES DOMMAGES - INTÉRÊTS ET EXCLUSION DE RESPONSABILITÉ POUR LES DOMMAGES.Vous pouvez obtenir de Sysinternals et de ses fournisseurs une indemnisation en cas de dommages directs uniquement à hauteur de 5, 00 $ US.Vous ne pouvez prétendre à aucune indemnisation pour les autres dommages, y compris les dommages spéciaux, indirects ou accessoires et pertes de bénéfices.\n\n		   Cette limitation concerne :\ntout ce qui est relié au logiciel, aux services ou au contenu(y compris le code) figurant sur des sites Internet tiers ou dans des programmes tiers; et\nles réclamations au titre de violation de contrat ou de garantie, ou au titre de responsabilité stricte, de négligence ou d'une autre faute dans la limite autorisée par la loi en vigueur.\n\nElle s'applique également, même si Sysinternals connaissait ou devrait connaître l'éventualité d'un tel dommage. Si votre pays n'autorise pas l'exclusion ou la limitation de responsabilité pour les dommages indirects, accessoires ou de quelque nature que ce soit, il se peut que la limitation ou l'exclusion ci - dessus ne s'appliquera pas à votre égard.\nEFFET JURIDIQUE.Le présent contrat décrit certains droits juridiques.Vous pourriez avoir d'autres droits prévus par les lois de votre pays. Le présent contrat ne modifie pas les droits que vous confèrent les lois de votre pays si celles-ci ne le permettent pas.\n\n";

BOOL IsEulaRegkeyAdded(const TCHAR * ToolName);

static BOOL EulaCenter( HWND hwndChild, HWND hwndParent )
{
    RECT    rcChild, rcParent;
    int     cxChild, cyChild, cxParent, cyParent;
    int     cxScreen, cyScreen, xNew, yNew;
    HDC     hdc;

    // Get the Height and Width of the child window
    GetWindowRect(hwndChild, &rcChild);
    cxChild = rcChild.right - rcChild.left;
    cyChild = rcChild.bottom - rcChild.top;

    // Get the Height and Width of the parent window
    GetWindowRect(hwndParent, &rcParent);
    cxParent = rcParent.right - rcParent.left;
    cyParent = rcParent.bottom - rcParent.top;

    // Get the display limits
    hdc = GetDC(hwndChild);
    cxScreen = GetDeviceCaps(hdc, HORZRES);
    cyScreen = GetDeviceCaps(hdc, VERTRES);
    ReleaseDC(hwndChild, hdc);

    // Calculate new X position, then adjust for screen
    xNew = rcParent.left + ((cxParent - cxChild) / 2);
    if (xNew < 0)
    {
        xNew = 0;
    }
    else if ((xNew + cxChild) > cxScreen)
    {
        xNew = cxScreen - cxChild;
    }

    // Calculate new Y position, then adjust for screen
    yNew = rcParent.top  + ((cyParent - cyChild) / 2);
    if (yNew < 0)
    {
        yNew = 0;
    }
    else if ((yNew + cyChild) > cyScreen)
    {
        yNew = cyScreen - cyChild;
    }

    // Set it, and return
    return SetWindowPos(hwndChild,
                        NULL,
                        xNew, yNew,
                        0, 0,
                        SWP_NOSIZE | SWP_NOZORDER);
}



static BOOL PrintRichedit( HWND hRichedit )
{
    // Get the printer.
    PRINTDLG	pd = { 0 };

    pd.lStructSize	= sizeof pd;
    pd.hwndOwner	= hRichedit;
    pd.hInstance	= GetModuleHandle(NULL);
    pd.Flags		= PD_RETURNDC | PD_NOPAGENUMS | PD_NOSELECTION | PD_PRINTSETUP;
    if ( !PrintDlg( &pd ) )  
        return FALSE;

    {
        HCURSOR		oldCursor	= SetCursor( LoadCursor( NULL, IDC_WAIT ) );
        int         nHorzRes	= GetDeviceCaps( pd.hDC, HORZRES );
        int			nVertRes	= GetDeviceCaps( pd.hDC, VERTRES );
        int			nLogPixelsX = GetDeviceCaps( pd.hDC, LOGPIXELSX );
        int			nLogPixelsY = GetDeviceCaps( pd.hDC, LOGPIXELSY );
        FORMATRANGE fr = { 0 };
        DOCINFO		di = { 0 };
        int			TotalLength;
        
        // Ensure the printer DC is in MM_TEXT mode.
        SetMapMode( pd.hDC, MM_TEXT );
        
        // Rendering to the same DC we are measuring.
        fr.hdc			= pd.hDC;
        fr.hdcTarget	= pd.hDC;
        
        // Set up the page.
        fr.rcPage.top		= 0;
        fr.rcPage.left		= 0;
        fr.rcPage.bottom	= (nVertRes/nLogPixelsY) * 1440;
        fr.rcPage.right		= (nHorzRes/nLogPixelsX) * 1440;
        
        // Set up 1" margins all around.
        fr.rc = fr.rcPage;
        InflateRect( &fr.rc, -1440, -1440 );
        
        // Default the range of text to print as the entire document.
        fr.chrg.cpMin = 0;
        fr.chrg.cpMax = -1;
        
        // Set up the print job (standard printing stuff here).
        di.cbSize = sizeof di;
        di.lpszDocName = _T("Sysinternals License");
        
        // Start the document.
        StartDoc( pd.hDC, &di );
        
        // Find out real size of document in characters.
        TotalLength = (int) SendMessage ( hRichedit, WM_GETTEXTLENGTH, 0, 0 );
        for (;;)  {
            int NextPage;

            // Start the page.
            StartPage( pd.hDC );
            
            // Print as much text as can fit on a page. The return value is
            // the index of the first character on the next page. 
            NextPage = (int) SendMessage( hRichedit, EM_FORMATRANGE, TRUE, (LPARAM)&fr );
            
            // Print last page.
            EndPage( pd.hDC );
            
            if ( NextPage >= TotalLength )
                break;

            // Adjust the range of characters to start printing at the first character of the next page.
            fr.chrg.cpMin = NextPage;
            fr.chrg.cpMax = -1;
        }

        // Tell the control to release cached information.
        SendMessage( hRichedit, EM_FORMATRANGE, 0, (LPARAM)NULL );
        EndDoc( pd.hDC );

        SetCursor( oldCursor );
    }

    return TRUE;
} 

// combine all text strings into a single string
char * GetEulaText()
{
    char	*	text;
    DWORD		len = 1;
    int			i;
    for ( i = 0; EulaText[i]; ++i )
        len += (DWORD) strlen( EulaText[i] );
    text = (char *) malloc( len );
    len = 0;
    for ( i = 0; EulaText[i]; ++i )  {
        strcpy( text+len, EulaText[i] );
        len += (DWORD) strlen( EulaText[i] );
    }
    text[len] = 0; 
    return text;
}

DWORD CALLBACK StreamCallback( DWORD_PTR dwCookie, LPBYTE pbBuff, LONG cb, LONG * pcb )
{
    const char	**	ptr = (const char **) dwCookie;
    LONG_PTR		len = strlen(*ptr);
    if ( cb > len )
        cb = (int) len;
    memcpy( pbBuff, *ptr, cb );
    *pcb = cb;
    *ptr += cb;
    return 0;
}

static INT_PTR CALLBACK EulaProc( HWND hwndDlg, UINT uMsg, WPARAM wParam, LPARAM lParam )
{
    switch ( uMsg ) {
    case WM_INITDIALOG:
        {
            TCHAR		title[MAX_PATH];
            char	*	text = GetEulaText();
            char	*	textptr = text;
            EDITSTREAM	stream = { 0, 0, StreamCallback };
            stream.dwCookie = (DWORD_PTR) &textptr;
            _stprintf_s( title, MAX_PATH, _T("%s License Agreement"), (TCHAR *) lParam );
            SetWindowText( hwndDlg, title );
            
            // enter RTF into edit box
            SendMessage( GetDlgItem(hwndDlg,IDC_TEXT), EM_EXLIMITTEXT, 0, 1024*1024 );
            SendMessage( GetDlgItem(hwndDlg,IDC_TEXT), EM_STREAMIN, SF_RTF, (LPARAM)&stream );
            free( text );
        }
        return TRUE;

    case WM_CTLCOLORSTATIC:
        // force background of read-only text window to be white
        if ( (HWND)lParam == GetDlgItem( hwndDlg, IDC_TEXT)  )  {
            return (INT_PTR)GetSysColorBrush( COLOR_WINDOW );
        }
        break;

    case WM_COMMAND:
        switch( LOWORD( wParam )) {
        case IDOK:
            EndDialog( hwndDlg, TRUE );
            return TRUE;
        case IDCANCEL:
            EndDialog( hwndDlg, FALSE );
            return TRUE;
        case IDC_PRINT:
            PrintRichedit( GetDlgItem(hwndDlg,IDC_TEXT) );
            return TRUE;
        }
        break;
    }
    return FALSE;
}


static WORD * Align2( WORD * pos )
{
    return (WORD *)(((DWORD_PTR)pos + 1) & ~((DWORD_PTR) 1));
}
static WORD * Align4( WORD * pos )
{
    return (WORD *)(((DWORD_PTR)pos + 3) & ~((DWORD_PTR) 3));
}

static int CopyText( WORD * pos, const WCHAR * text )
{
    int len = (int) wcslen( text ) + 1;
    wcscpy( (PWCHAR) pos, text );
    return len;
}

BOOL ShowEulaInternal( const TCHAR * ToolName, DWORD eulaAccepted )
{
#if !defined(SYSMON_SHARED)
    HKEY		hKey = NULL;
    TCHAR		keyName[MAX_PATH];

    _stprintf_s( keyName, MAX_PATH, _T("Software\\Sysinternals\\%s"), ToolName );

    //
    // check the regkey value if no -accepteula switch append
    //
    if (!eulaAccepted)
    {
        eulaAccepted = IsEulaRegkeyAdded(ToolName);
    }
#endif

    if( !eulaAccepted ) {
        if (IsIoTEdition())
        {
            eulaAccepted = ShowEulaConsole();	// display Eula to console and prompt for Eula Accepted.
            {
            }
        }
        else if (IsRemoteOnlyEdition() || IsRunningRemotely()) // Nano and in remote session will not be able to accept eula from prompt
        {
            ShowEulaConsoleNoPrompt();
        }
        else
        {
            DLGTEMPLATE		*	dlg = (DLGTEMPLATE	*)LocalAlloc(LPTR, 1000);
            WORD			*	extra = (WORD *)(dlg + 1);
            DLGITEMTEMPLATE	*	item;

#if defined(SYSMON_SHARED)
            printf( "Displaying EULA Gui dialog box ... (use -accepteula to avoid).\n" );
#endif

            LoadLibrarySafe(_T("Riched32.dll"), DLL_LOAD_LOCATION_SYSTEM );	// Richedit 1.0 library

            // header
            dlg->style = DS_MODALFRAME | DS_CENTER | DS_SETFONT | WS_POPUP | WS_CAPTION | WS_SYSMENU | DS_NOFAILCREATE;
            dlg->x = 0;
            dlg->y = 0;
            dlg->cx = 312;
            dlg->cy = 180;
            dlg->cdit = 0;	// number of controls

            *extra++ = 0;	// menu
            *extra++ = 0;	// class
            extra += CopyText(extra, L"License Agreement");
            *extra++ = 8;	// font size
            extra += CopyText(extra, L"MS Shell Dlg");

            // Command-line message
            item = (DLGITEMTEMPLATE *)Align4(extra);
            item->x = 7;
            item->y = 3;
            item->cx = 298;
            item->cy = 14;
            item->id = IDC_TEXT1;
            item->style = WS_CHILD | WS_VISIBLE;
            extra = (WORD *)(item + 1);
            *extra++ = 0xFFFF;	// class is ordinal
            *extra++ = 0x0082;	// class is static
            extra += CopyText(extra, L"You can also use the /accepteula command-line switch to accept the EULA.");
            *extra++ = 0;		// creation data
            dlg->cdit++;

            // Agree button
            item = (DLGITEMTEMPLATE *)Align4(extra);
            item->x = 201;
            item->y = 159;
            item->cx = 50;
            item->cy = 14;
            item->id = IDOK;
            item->style = BS_PUSHBUTTON | WS_CHILD | WS_VISIBLE | WS_TABSTOP; // | WS_DEFAULT;
            extra = (WORD *)(item + 1);
            *extra++ = 0xFFFF;	// class is ordinal
            *extra++ = 0x0080;	// class is button
            extra += CopyText(extra, L"&Agree");
            *extra++ = 0;		// creation data
            dlg->cdit++;

            // Decline button
            item = (DLGITEMTEMPLATE *)Align4(extra);
            item->x = 255;
            item->y = 159;
            item->cx = 50;
            item->cy = 14;
            item->id = IDCANCEL;
            item->style = BS_PUSHBUTTON | WS_CHILD | WS_VISIBLE | WS_TABSTOP;
            extra = (WORD *)(item + 1);
            *extra++ = 0xFFFF;	// class is ordinal
            *extra++ = 0x0080;	// class is button
            extra += CopyText(extra, L"&Decline");
            *extra++ = 0;		// creation data
            dlg->cdit++;

            // Print button
            item = (DLGITEMTEMPLATE *)Align4(extra);
            item->x = 7;
            item->y = 159;
            item->cx = 50;
            item->cy = 14;
            item->id = IDC_PRINT;
            item->style = BS_PUSHBUTTON | WS_CHILD | WS_VISIBLE | WS_TABSTOP;
            extra = (WORD *)(item + 1);
            *extra++ = 0xFFFF;	// class is ordinal
            *extra++ = 0x0080;	// class is button
            extra += CopyText(extra, L"&Print");
            *extra++ = 0;		// creation data
            dlg->cdit++;

            // Edit box
            item = (DLGITEMTEMPLATE *)Align4(extra);
            item->x = 7;
            item->y = 14;
            item->cx = 298;
            item->cy = 140;
            item->id = IDC_TEXT;
            item->style = WS_BORDER | ES_MULTILINE | ES_AUTOVSCROLL | ES_WANTRETURN | WS_VSCROLL | ES_READONLY | WS_CHILD | WS_VISIBLE | WS_TABSTOP;
            extra = (WORD *)(item + 1);
            extra += CopyText(extra, L"RICHEDIT");
            extra += CopyText(extra, L"&Decline");
            *extra++ = 0;		// creation data
            dlg->cdit++;

            eulaAccepted = (DWORD)DialogBoxIndirectParam(NULL, dlg, NULL, EulaProc, (LPARAM)ToolName);
            LocalFree(dlg);
        }
    }
#if !defined(SYSMON_SHARED)
    if ( eulaAccepted ) {
        if (RegCreateKey(HKEY_CURRENT_USER, keyName, &hKey) == ERROR_SUCCESS) {
            RegSetValueEx(hKey, _T("EulaAccepted"), 0, REG_DWORD, (BYTE *)&eulaAccepted, sizeof(eulaAccepted));
            RegCloseKey(hKey);
        }
    }
#endif

    return eulaAccepted != 0;
}

BOOL ShowEulaW( const TCHAR * ToolName, int *argc, PWCHAR argv[] )
{
    DWORD		eulaAccepted = 0;
    int			i;

    if ( argc == NULL  ||  argv == NULL )  {
        typedef LPWSTR * (WINAPI * type_CommandLineToArgvW)( LPCWSTR lpCmdLine, int *pNumArgs );
        type_CommandLineToArgvW pCommandLineToArgvW = (type_CommandLineToArgvW) GetProcAddress( LoadLibrarySafe(_T("Shell32.dll"), DLL_LOAD_LOCATION_SYSTEM), "CommandLineToArgvW" );
        if ( pCommandLineToArgvW )  {
            static int argc2;
            argc = &argc2;
            argv = (*pCommandLineToArgvW)( GetCommandLineW(), argc );
        } else {
            argc = NULL;
        }
    }


    //
    // See if its accepted via command line switch
    //
    if( argc ) {

        for( i = 0; i < *argc; i++ ) {

            eulaAccepted = (!_wcsicmp( argv[i], L"/accepteula") ||
                            !_wcsicmp( argv[i], L"-accepteula"));
            if( eulaAccepted ) {

                for( ; i < *argc - 1; i++ ) {

                    argv[i] = argv[i+1];
                }
                (*argc)--;
                break;
            }
        }
    }
    if( ShowEulaInternal( ToolName, eulaAccepted )) {

        eulaAccepted = 1;
    }
    return eulaAccepted != 0;
}


BOOL ShowEula( const TCHAR * ToolName, int *argc, PTCHAR argv[] )
{
    DWORD		eulaAccepted = 0;
    int			i;

    if ( argc == NULL  ||  argv == NULL )  {
        return ShowEulaW( ToolName, NULL, NULL );
    }

    //
    // See if its accepted via command line switch
    //
    if( argc ) {

        for( i = 0; i < *argc; i++ ) {

            eulaAccepted = (!_tcsicmp( argv[i], _T("/accepteula")) ||
                            !_tcsicmp( argv[i], _T("-accepteula")));
            if( eulaAccepted ) {

                for( ; i < *argc - 1; i++ ) {

                    argv[i] = argv[i+1];
                }
                (*argc)--;
                break;
            }
        }
    }
    if( ShowEulaInternal( ToolName, eulaAccepted )) {

        eulaAccepted = 1;
    }
    return eulaAccepted != 0;
}

// Determine whether we are on the IoT SKU by looking at the ProductName.
BOOL IsIoTEdition()
{
    HKEY		hKey = NULL;
    wchar_t		ProductName[MAX_PATH];
    BOOL		bRet = FALSE;	// assume "not" IoT Edition
    DWORD		dwSize = sizeof(ProductName);
    DWORD		type = 0;

    if (ERROR_SUCCESS == RegOpenKey(HKEY_LOCAL_MACHINE, _T("Software\\Microsoft\\windows nt\\currentversion"), &hKey))
    {
        if (ERROR_SUCCESS == RegQueryValueExW(hKey, L"ProductName", 0, &type, (LPBYTE)ProductName, &dwSize))
        {
            if (!_wcsicmp(L"iotuap", ProductName))
                bRet = TRUE;
        }
        RegCloseKey(hKey);
    }

    return bRet;
}

// Determine whether we are on the remote only edition, where we cannot prompt for user input.
BOOL IsRemoteOnlyEdition()
{
    HKEY		hKey = NULL;
    DWORD		dwNanoServer = 0;
    BOOL		bRet = FALSE;
    DWORD		dwSize = sizeof(dwNanoServer);
    DWORD		type = 0;

    // Currently Nano is the only remote only edtion.
    if (ERROR_SUCCESS == RegOpenKey(HKEY_LOCAL_MACHINE, _T("Software\\Microsoft\\Windows NT\\CurrentVersion\\Server\\ServerLevels"), &hKey))
    {
        if (ERROR_SUCCESS == RegQueryValueEx(hKey, _T("NanoServer"), 0, &type, (LPBYTE)&dwNanoServer, &dwSize))
        {
            if (type == REG_DWORD && dwNanoServer == 1)
                bRet = TRUE;
        }
        RegCloseKey(hKey);
    }

    return bRet;
}

BOOL IsRunningRemotely()
{
    // running from a remote session will not support input interaction
    DWORD fileType = GetFileType(GetStdHandle(STD_OUTPUT_HANDLE));
    return fileType == FILE_TYPE_PIPE;
}

DWORD ShowEulaConsole()
{
    DWORD dwRet = 0;
    char ch;
    BOOLEAN eulaAcknowledged = FALSE;

    wprintf(Raw_EulaText);

    while( eulaAcknowledged != TRUE )
    {
        printf("Accept Eula (Y/N)?");
        ch = (char) _getch();
        printf("%c\n", ch);
        if ('y' == ch || 'Y' == ch)
        {
            dwRet = 1;	// EULA Accepted.
            eulaAcknowledged = TRUE;
        }

        if ('n' == ch || 'N' == ch)
        {
            // EULA not accepted.
            eulaAcknowledged = TRUE;
        }
    }
    return dwRet;
}

void ShowEulaConsoleNoPrompt()
{
    wprintf_s(L"%ls", Raw_EulaText);
    wprintf_s(L"This is the first run of this program. You must accept EULA to continue.\n");
    wprintf_s(L"Use -accepteula to accept EULA.\n\n");

    // exit here to avoid printing the misleading "Eula declined".
    exit(1);
}

BOOL IsEulaAcceptedValueExist(HKEY hKeyRoot, LPCTSTR lpSubKey)
{
    HKEY		hKey = NULL;
    DWORD		length;
    DWORD       eulaAccepted = 0;
    DWORD       ret;

    //
    // check if it is set by external channel for all tools
    // assuming external channel do not set to WOW6432Node
    //
    if (RegOpenKeyEx(hKeyRoot, lpSubKey, 0, KEY_QUERY_VALUE | KEY_WOW64_64KEY, &hKey) == ERROR_SUCCESS)
    {
        length = sizeof(eulaAccepted);
        ret = RegQueryValueEx(hKey, _T("EulaAccepted"), NULL, NULL, (LPBYTE)&eulaAccepted, &length);
        RegCloseKey(hKey);

        if (ret == ERROR_SUCCESS && eulaAccepted)
        {
            return TRUE;
        }
    }
    
    return FALSE;
}

BOOL IsEulaRegkeyAdded(const TCHAR * ToolName)
{
    TCHAR       perToolRegKey[MAX_PATH];
    PTCHAR      suiteRegKey = _T("Software\\Sysinternals");

    _stprintf_s(perToolRegKey, MAX_PATH, _T("%s\\%s"), suiteRegKey, ToolName);

    //
    // check if it is set by external channel for all tools
    // assuming external channel do not set to WOW6432Node
    //
    if (IsEulaAcceptedValueExist(HKEY_LOCAL_MACHINE, suiteRegKey) ||
        IsEulaAcceptedValueExist(HKEY_CURRENT_USER, suiteRegKey))
    {
        return TRUE;
    }

    //
    // per tool check
    //
    if (IsEulaAcceptedValueExist(HKEY_CURRENT_USER, perToolRegKey))
    {
        return TRUE;
    }

    return FALSE;
}

BOOL IsEulaSwitchAppended(int *argc, PTCHAR argv[])
{
    DWORD       eulaAccepted = 0;
    int         i;

    //
    // See if its accepted via command line switch
    //
    if (*argc > 1) {
        for (i = 1; i < *argc; i++) {
            eulaAccepted = (!_tcsicmp(argv[i], _T("/accepteula")) ||
                !_tcsicmp(argv[i], _T("-accepteula")));
            if (eulaAccepted) {
                break;
            }
        }
    }

    return eulaAccepted;
}

//
// Determine if Eula is accepted, either already have regkey added
// or have -accepteula switch appended
//
BOOL IsEulaAccepted(const TCHAR * ToolName, int *argc, PTCHAR argv[])
{
    return IsEulaRegkeyAdded(ToolName) || IsEulaSwitchAppended(argc, argv);
}
