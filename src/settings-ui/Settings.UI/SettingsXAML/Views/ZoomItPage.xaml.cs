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
    public sealed partial class ZoomItPage : NavigablePage, IRefreshablePage
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
            openFileName.Flags = (int)OpenFileNameFlags.OFN_NOCHANGEDIR; // OFN_NOCHANGEDIR flag is needed, because otherwise GetOpenFileName overwrites the process working directory.
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
            IntPtr pLogFont = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(LOGFONT)));
            if (font != null)
            {
                font.lfHeight = -21;
                Marshal.StructureToPtr(font, pLogFont, false);
            }
            else
            {
                LOGFONT logFont = new LOGFONT();
                logFont.lfHeight = -21;
                Marshal.StructureToPtr(logFont, pLogFont, false);
            }

            CHOOSEFONT chooseFont = new CHOOSEFONT();
            IntPtr windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(App.GetSettingsWindow());
            chooseFont.hwndOwner = windowHandle;
            chooseFont.Flags = (int)(CHOOSE_FONT_FLAGS.CF_SCREENFONTS | CHOOSE_FONT_FLAGS.CF_INITTOLOGFONTSTRUCT | CHOOSE_FONT_FLAGS.CF_LIMITSIZE);
            chooseFont.rgbColors = 0;
            chooseFont.lCustData = 0;
            chooseFont.nSizeMin = 16;
            chooseFont.nSizeMax = 16;
            chooseFont.nFontType = 0x2000; // SCREEN_FONTTYPE as in the original ZoomIt source.
            chooseFont.hInstance = Marshal.GetHINSTANCE(typeof(ZoomItPage).Module);

            // TODO: chooseFont.lpTemplateName = FORMATDLGORD31; and CHOOSE_FONT_FLAGS.CF_ENABLETEMPLATE
            chooseFont.lpLogFont = pLogFont;

            IntPtr pChooseFont = Marshal.AllocHGlobal(Marshal.SizeOf(chooseFont));
            Marshal.StructureToPtr(chooseFont, pChooseFont, false);

            bool callResult = NativeMethods.ChooseFont(pChooseFont);
            if (!callResult)
            {
                int error = NativeMethods.CommDlgExtendedError();
                if (error > 0)
                {
                    Logger.LogError($"ChooseFont failed with extended error code {error}");
                }

                Marshal.FreeHGlobal(pLogFont);
                Marshal.FreeHGlobal(pChooseFont);
                return null;
            }

            CHOOSEFONT dialogResult = Marshal.PtrToStructure<CHOOSEFONT>(pChooseFont);
            LOGFONT result = Marshal.PtrToStructure<LOGFONT>(dialogResult.lpLogFont);

            Marshal.FreeHGlobal(pLogFont);
            Marshal.FreeHGlobal(pChooseFont);
            return result;
        }

        public ZoomItPage()
        {
            var settingsUtils = SettingsUtils.Default;
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
