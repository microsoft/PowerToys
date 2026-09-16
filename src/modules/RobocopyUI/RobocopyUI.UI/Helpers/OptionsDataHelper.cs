// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using RobocopyUI.Models;

namespace RobocopyUI.Helpers;

public static class OptionsDataHelper
{
    private static object CreateGroupedView(IEnumerable<OptionContent> options)
    {
        var groups = options
            .GroupBy(x => x.GroupName)
            .Select(g => new OptionGroup(g.Key, [.. g]))
            .ToList();

        var source = new CollectionViewSource
        {
            IsSourceGrouped = true,
            Source = groups,
            ItemsPath = new PropertyPath(nameof(OptionGroup.Items)),
        };

        return source.View;
    }

    public static object GetMainOptionsLeftAsGroupedView() => CreateGroupedView(GetMainOptionsLeft());

    public static object GetMainOptionsRightAsGroupedView() => CreateGroupedView(GetMainOptionsRight());

    public static object GetFilterOptionsAsGroupedView() => CreateGroupedView(GetFilterOptions());

    public static object GetLoggingOptionsAsGroupedView() => CreateGroupedView(GetLoggingOptions());

    public static object GetAdvancedOptionsAsGroupedView() => CreateGroupedView(GetAdvancedOptions());

    public static List<OptionContent> GetMainOptionsLeft()
    {
        var copyOptionsGroupName = ResourceLoaderInstance.ResourceLoader.GetString("CopyOptionsLabel/Text");

        return [
            new OptionContent { OptionName = "/S", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("SOption/OptionDescription"), GroupName = copyOptionsGroupName },
            new OptionContent { OptionName = "/E", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("EOption/OptionDescription"), GroupName = copyOptionsGroupName },
            new OptionContent { OptionName = "/LEV", IsNumberOption = true, OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("LEVOption/OptionDescription"), GroupName = copyOptionsGroupName },
            new OptionContent { OptionName = "/Z", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("ZOption/OptionDescription"), GroupName = copyOptionsGroupName },
            new OptionContent { OptionName = "/B", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("BOption/OptionDescription"), GroupName = copyOptionsGroupName },
            new OptionContent { OptionName = "/ZB", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("ZBOption/OptionDescription"), GroupName = copyOptionsGroupName },
            new OptionContent { OptionName = "/J", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("JOption/OptionDescription"), GroupName = copyOptionsGroupName },
            new OptionContent { OptionName = "/EFSRAW", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("EFSRAWOption/OptionDescription"), GroupName = copyOptionsGroupName },
            new OptionContent
            {
                OptionName = "/COPY",
                IsMultiSelectOption = true,
                MultiSelectOptions =
                [
                    new OptionContent { OptionName = "D", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("COPYOptionD/OptionDescription") },
                    new OptionContent { OptionName = "A", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("COPYOptionA/OptionDescription") },
                    new OptionContent { OptionName = "T", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("COPYOptionT/OptionDescription") },
                    new OptionContent { OptionName = "X", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("COPYOptionX/OptionDescription") },
                    new OptionContent { OptionName = "S", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("COPYOptionS/OptionDescription") },
                    new OptionContent { OptionName = "O", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("COPYOptionO/OptionDescription") },
                    new OptionContent { OptionName = "U", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("COPYOptionU/OptionDescription") },
                ],
                OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("COPYOption/OptionDescription"),
                GroupName = copyOptionsGroupName,
            },
            new OptionContent { OptionName = "/NOCOPY", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("NOCOPYOption/OptionDescription"), GroupName = copyOptionsGroupName },
            new OptionContent { OptionName = "/SECFIX", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("SECFIXOption/OptionDescription"), GroupName = copyOptionsGroupName },
            new OptionContent { OptionName = "/TIMFIX", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("TIMFIXOption/OptionDescription"), GroupName = copyOptionsGroupName },
            new OptionContent { OptionName = "/PURGE", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("PURGEOption/OptionDescription"), GroupName = copyOptionsGroupName },
            new OptionContent { OptionName = "/MIR", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("MIROption/OptionDescription"), GroupName = copyOptionsGroupName },
            new OptionContent { OptionName = "/MOV", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("MOVOption/OptionDescription"), GroupName = copyOptionsGroupName },
            new OptionContent { OptionName = "/MOVE", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("MOVEOption/OptionDescription"), GroupName = copyOptionsGroupName },
            new OptionContent
            {
                OptionName = "/A+",
                IsMultiSelectOption = true,
                MultiSelectOptions =
                [
                    new OptionContent { OptionName = "R", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("AOptionR/OptionDescription") },
                    new OptionContent { OptionName = "A", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("AOptionA/OptionDescription") },
                    new OptionContent { OptionName = "S", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("AOptionS/OptionDescription") },
                    new OptionContent { OptionName = "H", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("AOptionH/OptionDescription") },
                    new OptionContent { OptionName = "C", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("AOptionC/OptionDescription") },
                    new OptionContent { OptionName = "N", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("AOptionN/OptionDescription") },
                    new OptionContent { OptionName = "E", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("AOptionE/OptionDescription") },
                    new OptionContent { OptionName = "T", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("AOptionT/OptionDescription") },
                ],
                OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("APlusOption/OptionDescription"),
                GroupName = copyOptionsGroupName,
            },
            new OptionContent
            {
                OptionName = "/A-",
                IsMultiSelectOption = true,
                MultiSelectOptions =
                [
                    new OptionContent { OptionName = "R", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("AOptionR/OptionDescription") },
                    new OptionContent { OptionName = "A", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("AOptionA/OptionDescription") },
                    new OptionContent { OptionName = "S", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("AOptionS/OptionDescription") },
                    new OptionContent { OptionName = "H", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("AOptionH/OptionDescription") },
                    new OptionContent { OptionName = "C", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("AOptionC/OptionDescription") },
                    new OptionContent { OptionName = "N", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("AOptionN/OptionDescription") },
                    new OptionContent { OptionName = "E", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("AOptionE/OptionDescription") },
                    new OptionContent { OptionName = "T", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("AOptionT/OptionDescription") },
                    new OptionContent { OptionName = "O", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("AOptionO/OptionDescription") },
                ],
                OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("AMinusOption/OptionDescription"),
                GroupName = copyOptionsGroupName,
            },
            new OptionContent { OptionName = "/CREATE", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("CREATEOption/OptionDescription"), GroupName = copyOptionsGroupName },
            new OptionContent { OptionName = "/FAT", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("FATOption/OptionDescription"), GroupName = copyOptionsGroupName },
            new OptionContent { OptionName = "/256", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("256Option/OptionDescription"), GroupName = copyOptionsGroupName },
            new OptionContent { OptionName = "/MON", IsNumberOption = true, OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("MONOption/OptionDescription"), GroupName = copyOptionsGroupName },
            new OptionContent { OptionName = "/MOT", IsNumberOption = true, OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("MOTOption/OptionDescription"), GroupName = copyOptionsGroupName },
            new OptionContent { OptionName = "/RH", IsRunHoursOption = true, OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("RHOption/OptionDescription"), GroupName = copyOptionsGroupName },
            new OptionContent { OptionName = "/PF", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("PFOption/OptionDescription"), GroupName = copyOptionsGroupName },
            new OptionContent { OptionName = "/IPG", IsNumberOption = true, OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("IPGOption/OptionDescription"), GroupName = copyOptionsGroupName },
            new OptionContent { OptionName = "/SJ", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("SJOption/OptionDescription"), GroupName = copyOptionsGroupName },
            new OptionContent { OptionName = "/SL", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("SLOption/OptionDescription"), GroupName = copyOptionsGroupName },
            new OptionContent { OptionName = "/MT", IsNumberOption = true, OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("MTOption/OptionDescription"), GroupName = copyOptionsGroupName },
            new OptionContent
            {
                OptionName = "/DCOPY",
                IsMultiSelectOption = true,
                MultiSelectOptions =
                [
                    new OptionContent { OptionName = "D", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("COPYOptionD/OptionDescription") },
                    new OptionContent { OptionName = "A", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("COPYOptionA/OptionDescription") },
                    new OptionContent { OptionName = "T", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("COPYOptionT/OptionDescription") },
                    new OptionContent { OptionName = "X", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("COPYOptionX/OptionDescription") },
                    new OptionContent { OptionName = "E", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("COPYOptionE/OptionDescription") },
                ],
                OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("DCOPYOption/OptionDescription"),
                GroupName = copyOptionsGroupName,
            },
            new OptionContent { OptionName = "/NODCOPY", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("NODCOPYOption/OptionDescription"), GroupName = copyOptionsGroupName },
            new OptionContent { OptionName = "/NOOFFLOAD", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("NOOFFLOADOption/OptionDescription"), GroupName = copyOptionsGroupName },
            new OptionContent { OptionName = "/COMPRESS", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("COMPRESSOption/OptionDescription"), GroupName = copyOptionsGroupName },
            new OptionContent { OptionName = "/SPARSE:Y", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("SPARSEOptionY/OptionDescription"), GroupName = copyOptionsGroupName },
            new OptionContent { OptionName = "/SPARSE:N", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("SPARSEOptionN/OptionDescription"), GroupName = copyOptionsGroupName },
            new OptionContent { OptionName = "/NOCLONE", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("NOCLONEOption/OptionDescription"), GroupName = copyOptionsGroupName },
        ];
    }

    public static List<OptionContent> GetMainOptionsRight()
    {
        var throttleOptionsGroupName = ResourceLoaderInstance.ResourceLoader.GetString("CopyFileThrottlingOptionsLabel/Text");
        var retryOptionsGroupName = ResourceLoaderInstance.ResourceLoader.GetString("RetryOptionsLabel/Text");

        return [
            new OptionContent { OptionName = "/IoMaxSize", IsNumberOption = true, IsStorageOption = true, OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("IoMaxSizeOption/OptionDescription"), GroupName = throttleOptionsGroupName },
            new OptionContent { OptionName = "/IoRate", IsNumberOption = true, IsStorageOption = true, OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("IoRateOption/OptionDescription"), GroupName = throttleOptionsGroupName },
            new OptionContent { OptionName = "/Threshold", IsNumberOption = true, IsStorageOption = true, OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("ThresholdOption/OptionDescription"), GroupName = throttleOptionsGroupName },
            new OptionContent { OptionName = "/R", IsNumberOption = true, OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("ROption/OptionDescription"), GroupName = retryOptionsGroupName },
            new OptionContent { OptionName = "/W", IsNumberOption = true, OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("WOption/OptionDescription"), GroupName = retryOptionsGroupName },
            new OptionContent { OptionName = "/REG", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("REGOption/OptionDescription"), GroupName = retryOptionsGroupName },
            new OptionContent { OptionName = "/TBD", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("TBDOption/OptionDescription"), GroupName = retryOptionsGroupName },
            new OptionContent { OptionName = "/LFSM", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("LFSMOption/OptionDescription"), GroupName = retryOptionsGroupName },
            new OptionContent { OptionName = "/LFSM", IsNumberOption = true, IsStorageOption = true, OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("LFSMNOption/OptionDescription"), GroupName = retryOptionsGroupName },
        ];
    }

    public static List<OptionContent> GetFilterOptions()
    {
        var filterOptionsGroupName = ResourceLoaderInstance.ResourceLoader.GetString("FiltersContentHeaderLabel/Text");

        return [
            new OptionContent { OptionName = "/A", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("AOption/OptionDescription"), GroupName = filterOptionsGroupName },
            new OptionContent { OptionName = "/M", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("MOption/OptionDescription"), GroupName = filterOptionsGroupName },
            new OptionContent
            {
                OptionName = "/IA",
                IsMultiSelectOption = true,
                MultiSelectOptions =
                [
                    new OptionContent { OptionName = "R", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("IAOptionR/OptionDescription") },
                    new OptionContent { OptionName = "A", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("IAOptionA/OptionDescription") },
                    new OptionContent { OptionName = "S", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("IAOptionS/OptionDescription") },
                    new OptionContent { OptionName = "H", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("IAOptionH/OptionDescription") },
                    new OptionContent { OptionName = "C", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("IAOptionC/OptionDescription") },
                    new OptionContent { OptionName = "N", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("IAOptionN/OptionDescription") },
                    new OptionContent { OptionName = "E", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("IAOptionE/OptionDescription") },
                    new OptionContent { OptionName = "T", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("IAOptionT/OptionDescription") },
                    new OptionContent { OptionName = "O", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("IAOptionO/OptionDescription") },
                ],
                OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("IAOption/OptionDescription"),
            },
            new OptionContent
            {
                OptionName = "/XA",
                IsMultiSelectOption = true,
                MultiSelectOptions =
                [
                    new OptionContent { OptionName = "R", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("XAOptionR/OptionDescription"), GroupName = filterOptionsGroupName },
                    new OptionContent { OptionName = "A", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("XAOptionA/OptionDescription"), GroupName = filterOptionsGroupName },
                    new OptionContent { OptionName = "S", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("XAOptionS/OptionDescription"), GroupName = filterOptionsGroupName },
                    new OptionContent { OptionName = "H", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("XAOptionH/OptionDescription"), GroupName = filterOptionsGroupName },
                    new OptionContent { OptionName = "C", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("XAOptionC/OptionDescription"), GroupName = filterOptionsGroupName },
                    new OptionContent { OptionName = "N", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("XAOptionN/OptionDescription"), GroupName = filterOptionsGroupName },
                    new OptionContent { OptionName = "E", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("XAOptionE/OptionDescription"), GroupName = filterOptionsGroupName },
                    new OptionContent { OptionName = "T", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("XAOptionT/OptionDescription"), GroupName = filterOptionsGroupName },
                    new OptionContent { OptionName = "O", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("XAOptionO/OptionDescription"), GroupName = filterOptionsGroupName },
                ],
                OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("XAOption/OptionDescription"),
            },
            new OptionContent { OptionName = "/XC", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("XCOption/OptionDescription"), GroupName = filterOptionsGroupName },
            new OptionContent { OptionName = "/XN", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("XNOption/OptionDescription"), GroupName = filterOptionsGroupName },
            new OptionContent { OptionName = "/XO", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("XOOption/OptionDescription"), GroupName = filterOptionsGroupName },
            new OptionContent { OptionName = "/XX", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("XXOption/OptionDescription"), GroupName = filterOptionsGroupName },
            new OptionContent { OptionName = "/XL", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("XLOption/OptionDescription"), GroupName = filterOptionsGroupName },
            new OptionContent { OptionName = "/IS", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("ISOption/OptionDescription"), GroupName = filterOptionsGroupName },
            new OptionContent { OptionName = "/IT", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("ITOption/OptionDescription"), GroupName = filterOptionsGroupName },
            new OptionContent { OptionName = "/MAX", IsNumberOption = true, OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("MAXOption/OptionDescription"), GroupName = filterOptionsGroupName },
            new OptionContent { OptionName = "/MIN", IsNumberOption = true, OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("MINOption/OptionDescription"), GroupName = filterOptionsGroupName },
            new OptionContent { OptionName = "/MAXAGE", IsNumberOption = true, OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("MAXAGEOption/OptionDescription"), GroupName = filterOptionsGroupName },
            new OptionContent { OptionName = "/MINAGE", IsNumberOption = true, OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("MINAGEOption/OptionDescription"), GroupName = filterOptionsGroupName },
            new OptionContent { OptionName = "/MAXLAD", IsNumberOption = true, OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("MAXLADOption/OptionDescription"), GroupName = filterOptionsGroupName },
            new OptionContent { OptionName = "/MINLAD", IsNumberOption = true, OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("MINLADOption/OptionDescription"), GroupName = filterOptionsGroupName },
            new OptionContent { OptionName = "/FFT", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("FFTOption/OptionDescription"), GroupName = filterOptionsGroupName },
            new OptionContent { OptionName = "/DST", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("DSTOption/OptionDescription"), GroupName = filterOptionsGroupName },
            new OptionContent { OptionName = "/XJ", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("XJOption/OptionDescription"), GroupName = filterOptionsGroupName },
            new OptionContent { OptionName = "/XJD", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("XJDOption/OptionDescription"), GroupName = filterOptionsGroupName },
            new OptionContent { OptionName = "/XJF", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("XJFOption/OptionDescription"), GroupName = filterOptionsGroupName },
            new OptionContent { OptionName = "/IM", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("IMOption/OptionDescription"), GroupName = filterOptionsGroupName },
            new OptionContent { OptionName = "/XF", IsTextOption = true, OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("XFOption/OptionDescription"), GroupName = filterOptionsGroupName },
            new OptionContent { OptionName = "/XD", IsTextOption = true, OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("XDOption/OptionDescription"), GroupName = filterOptionsGroupName },
        ];
    }

    public static List<OptionContent> GetLoggingOptions()
    {
        var loggingOptionsGroupName = ResourceLoaderInstance.ResourceLoader.GetString("LoggingContentHeaderLabel/Text");

        return [
            new OptionContent { OptionName = "/L", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("LOption/OptionDescription"), GroupName = loggingOptionsGroupName },
            new OptionContent { OptionName = "/X", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("XOption/OptionDescription"), GroupName = loggingOptionsGroupName },
            new OptionContent { OptionName = "/V", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("VOption/OptionDescription"), GroupName = loggingOptionsGroupName },
            new OptionContent { OptionName = "/TS", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("TSOption/OptionDescription"), GroupName = loggingOptionsGroupName },
            new OptionContent { OptionName = "/FP", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("FPOption/OptionDescription"), GroupName = loggingOptionsGroupName },
            new OptionContent { OptionName = "/BYTES", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("BYTESOption/OptionDescription"), GroupName = loggingOptionsGroupName },
            new OptionContent { OptionName = "/NS", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("NSOption/OptionDescription"), GroupName = loggingOptionsGroupName },
            new OptionContent { OptionName = "/NC", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("NCOption/OptionDescription"), GroupName = loggingOptionsGroupName },
            new OptionContent { OptionName = "/NFL", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("NFLOption/OptionDescription"), GroupName = loggingOptionsGroupName },
            new OptionContent { OptionName = "/NDL", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("NDLOption/OptionDescription"), GroupName = loggingOptionsGroupName },
            new OptionContent { OptionName = "/NP", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("NPOption/OptionDescription"), GroupName = loggingOptionsGroupName },
            new OptionContent { OptionName = "/ETA", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("ETAOption/OptionDescription"), GroupName = loggingOptionsGroupName },
            new OptionContent { OptionName = "/LOG", IsTextOption = true, OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("LOGOption/OptionDescription"), GroupName = loggingOptionsGroupName },
            new OptionContent { OptionName = "/LOG+", IsTextOption = true, OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("LogPlusOption/OptionDescription"), GroupName = loggingOptionsGroupName },
            new OptionContent { OptionName = "/UNILOG", IsTextOption = true, OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("UNILOGOption/OptionDescription"), GroupName = loggingOptionsGroupName },
            new OptionContent { OptionName = "/UNILOG+", IsTextOption = true, OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("UNILOGPlusOption/OptionDescription"), GroupName = loggingOptionsGroupName },
            new OptionContent { OptionName = "/TEE", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("TEEOption/OptionDescription"), GroupName = loggingOptionsGroupName },
            new OptionContent { OptionName = "/NJH", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("NJHOption/OptionDescription"), GroupName = loggingOptionsGroupName },
            new OptionContent { OptionName = "/NJS", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("NJSOption/OptionDescription"), GroupName = loggingOptionsGroupName },
            new OptionContent { OptionName = "/UNICODE", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("UNICODEOption/OptionDescription"), GroupName = loggingOptionsGroupName },
        ];
    }

    public static List<OptionContent> GetAdvancedOptions()
    {
        var advancedOptionsGroupName = ResourceLoaderInstance.ResourceLoader.GetString("AdvancedContentHeaderLabel/Text");

        return [
            new OptionContent { OptionName = "/QUIT", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("QUITOption/OptionDescription"), GroupName = advancedOptionsGroupName },
            new OptionContent { OptionName = "/NOSD", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("NOSDOption/OptionDescription"), GroupName = advancedOptionsGroupName },
            new OptionContent { OptionName = "/NODD", OptionDescription = ResourceLoaderInstance.ResourceLoader.GetString("NODDOption/OptionDescription"), GroupName = advancedOptionsGroupName },
        ];
    }
}
