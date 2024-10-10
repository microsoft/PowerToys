// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class ZoomItPage : Page, IRefreshablePage
    {
        private ZoomItViewModel ViewModel { get; set; }

        private const int MaxPath = 260; // ZoomIt doesn't support LONG_PATHS. We need to change it here once it does.

        private static string PickFileDialog(string filter, string title, string initialDir = null, int initialFilter = 0)
        {
            // this code was changed to solve the problem with WinUI3 that prevents to select a file
            // while running elevated, when the issue is solved in WinUI3 it should be changed back
            OpenFileName openFileName = new OpenFileName();
            openFileName.StructSize = Marshal.SizeOf(openFileName);
            openFileName.Filter = filter;

            // make buffer double MAX_PATH since it can use 2 chars per char.
            openFileName.File = new string(new char[MaxPath * 2]);
            openFileName.MaxFile = openFileName.File.Length;
            openFileName.FileTitle = new string(new char[MaxPath * 2]);
            openFileName.MaxFileTitle = openFileName.FileTitle.Length;
            openFileName.InitialDir = initialDir;
            openFileName.Title = title;
            openFileName.FilterIndex = initialFilter;
            openFileName.DefExt = null;
            IntPtr windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(App.GetSettingsWindow());
            openFileName.Hwnd = windowHandle;

            bool result = NativeMethods.GetOpenFileName(openFileName);
            if (result)
            {
                return openFileName.File;
            }

            return null;
        }

        private static LOGFONT PickFontDialog(LOGFONT font)
        {
            IntPtr pLogfont = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(LOGFONT)));
            if (font != null)
            {
                font.lfHeight = -21;
                Marshal.StructureToPtr(font, pLogfont, false);
            }
            else
            {
                LOGFONT logfont = new LOGFONT();
                logfont.lfHeight = -21;
                Marshal.StructureToPtr(logfont, pLogfont, false);
            }

            CHOOSEFONT chf = new CHOOSEFONT();
            IntPtr windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(App.GetSettingsWindow());
            chf.hwndOwner = windowHandle;
            chf.Flags = (int)(CHOOSEFONTFLAGS.CF_SCREENFONTS | CHOOSEFONTFLAGS.CF_INITTOLOGFONTSTRUCT | CHOOSEFONTFLAGS.CF_LIMITSIZE);
            chf.rgbColors = 0;
            chf.lCustData = 0;
            chf.nSizeMin = 16;
            chf.nSizeMax = 16;
            chf.nFontType = 0x2000; // SCREEN_FONTTYPE as in the original ZoomIt source.
            chf.hInstance = Marshal.GetHINSTANCE(typeof(ZoomItPage).Module);

            // TODO: chf.lpTemplateName = FORMATDLGORD31; and CHOOSEFONTFLAGS.CF_ENABLETEMPLATE
            chf.lpLogFont = pLogfont;

            IntPtr pChoosefont = Marshal.AllocHGlobal(Marshal.SizeOf(chf));
            Marshal.StructureToPtr(chf, pChoosefont, false);

            bool callResult = NativeMethods.ChooseFont(pChoosefont);
            if (!callResult)
            {
                int error = NativeMethods.CommDlgExtendedError();
                if (error > 0)
                {
                    Logger.LogError($"ChooseFont failed with extended error code {error}");
                }

                Marshal.FreeHGlobal(pLogfont);
                Marshal.FreeHGlobal(pChoosefont);
                return null;
            }

            CHOOSEFONT dialogResult = Marshal.PtrToStructure<CHOOSEFONT>(pChoosefont);
            LOGFONT result = Marshal.PtrToStructure<LOGFONT>(dialogResult.lpLogFont);

            Marshal.FreeHGlobal(pLogfont);
            Marshal.FreeHGlobal(pChoosefont);
            return result;
        }

        public ZoomItPage()
        {
            var settingsUtils = new SettingsUtils();
            ViewModel = new ZoomItViewModel(settingsUtils, SettingsRepository<GeneralSettings>.GetInstance(settingsUtils), ShellPage.SendDefaultIPCMessage, PickFileDialog, PickFontDialog);
            DataContext = ViewModel;
            InitializeComponent();
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }
    }
}
