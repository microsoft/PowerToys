// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// The notice's UI Automation element: a tool tip named with the notice's heading and message, which screen readers
/// hear about through a notification event as it appears. For that event to reach them, a popup window has to answer
/// WM_GETOBJECT with a provider of its own that has the Window pattern; closing through that pattern dismisses the
/// notice. UI Automation calls it on its own threads, so it only reads what the notice's thread set.
/// </summary>
[GeneratedComClass]
internal sealed unsafe partial class NoticeAutomation : IRawElementProviderSimple, IWindowProvider
{
    internal const string AutomationId = "TextExpanderNotice";

    private const string ActivityId = "PowerToys.TextExpander.Notice";

    private static readonly StrategyBasedComWrappers Wrappers = new();

    private readonly IntPtr _window;
    private readonly Action _close;
    private IntPtr _provider;
    private volatile string _name = string.Empty;

    /// <param name="window">The notice's window.</param>
    /// <param name="close">Dismisses the notice. Called on one of UI Automation's threads.</param>
    public NoticeAutomation(IntPtr window, Action close)
    {
        _window = window;
        _close = close;
        IntPtr unknown = Wrappers.GetOrCreateComInterfaceForObject(this, CreateComInterfaceFlags.None);
        try
        {
            Guid iid = typeof(IRawElementProviderSimple).GUID;
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(unknown, in iid, out _provider));
        }
        finally
        {
            _ = Marshal.Release(unknown);
        }
    }

    /// <summary>Gets or sets what screen readers read for the notice: its heading and message.</summary>
    public string Name
    {
        get => _name;
        set => _name = value;
    }

    /// <summary>
    /// Joins the heading and message into what screen readers say, with the punctuation <paramref name="format"/> puts
    /// between them in the user's language. Either can be left out.
    /// </summary>
    internal static string SpokenText(string heading, string message, string format)
    {
        heading = heading.Trim();
        message = message.Trim();
        if (heading.Length == 0 || message.Length == 0)
        {
            return heading.Length == 0 ? message : heading;
        }

        try
        {
            return string.Format(CultureInfo.CurrentCulture, format, heading, message);
        }
        catch (FormatException)
        {
            return heading + ". " + message;
        }
    }

    /// <summary>Gets what screen readers say for a notice, in the user's language.</summary>
    public static string SpokenText(string heading, string message) => SpokenText(heading, message, EngineText.Get("Notice_SpokenText", "{0}. {1}"));

    /// <summary>
    /// Gets a UI Automation property the notice gives itself: a tool tip, named with what it says, that never takes the
    /// focus. Null means the window's own, such as its bounds, process and class.
    /// </summary>
    internal static object? Property(int propertyId, string name) => propertyId switch
    {
        Native.UIA_ControlTypePropertyId => Native.UIA_ToolTipControlTypeId,
        Native.UIA_NamePropertyId => name,
        Native.UIA_AutomationIdPropertyId => AutomationId,
        Native.UIA_IsKeyboardFocusablePropertyId or Native.UIA_HasKeyboardFocusPropertyId => false,
        _ => null,
    };

    /// <summary>Answers UI Automation's WM_GETOBJECT for the notice's window.</summary>
    public IntPtr Answer(IntPtr wParam, IntPtr lParam) => Native.UiaReturnRawElementProvider(_window, wParam, lParam, _provider);

    /// <summary>Tells screen readers about the notice that has just appeared, unless none is listening.</summary>
    public void Announce()
    {
        if (_provider != IntPtr.Zero && Native.UiaClientsAreListening())
        {
            _ = Native.UiaRaiseNotificationEvent(_provider, Native.NotificationKind_Other, Native.NotificationProcessing_ImportantMostRecent, _name, ActivityId);
        }
    }

    int IRawElementProviderSimple.GetProviderOptions(out int options)
    {
        options = Native.ProviderOptions_ServerSideProvider;
        return Native.S_OK;
    }

    int IRawElementProviderSimple.GetPatternProvider(int patternId, out IntPtr provider)
    {
        provider = patternId == Native.UIA_WindowPatternId ? Wrappers.GetOrCreateComInterfaceForObject(this, CreateComInterfaceFlags.None) : IntPtr.Zero;
        return Native.S_OK;
    }

    int IRawElementProviderSimple.GetPropertyValue(int propertyId, ComVariant* value)
    {
        if (value == null)
        {
            return Native.E_POINTER;
        }

        *value = Property(propertyId, _name) switch
        {
            int number => ComVariant.Create(number),
            string text => ComVariant.Create(text),
            bool flag => ComVariant.Create(flag),
            _ => default,
        };
        return Native.S_OK;
    }

    int IRawElementProviderSimple.GetHostRawElementProvider(out IntPtr provider)
        => _window == IntPtr.Zero ? ReturnNone(out provider) : Native.UiaHostProviderFromHwnd(_window, out provider);

    int IWindowProvider.SetVisualState(int state) => Native.UIA_E_INVALIDOPERATION;

    int IWindowProvider.Close()
    {
        _close();
        return Native.S_OK;
    }

    int IWindowProvider.WaitForInputIdle(int milliseconds, out int idle) => ReturnValue(1, out idle);

    int IWindowProvider.GetCanMaximize(out int canMaximize) => ReturnValue(0, out canMaximize);

    int IWindowProvider.GetCanMinimize(out int canMinimize) => ReturnValue(0, out canMinimize);

    int IWindowProvider.GetIsModal(out int isModal) => ReturnValue(0, out isModal);

    int IWindowProvider.GetWindowVisualState(out int state) => ReturnValue(Native.WindowVisualState_Normal, out state);

    int IWindowProvider.GetWindowInteractionState(out int state) => ReturnValue(Native.WindowInteractionState_ReadyForUserInteraction, out state);

    int IWindowProvider.GetIsTopmost(out int isTopmost) => ReturnValue(1, out isTopmost);

    private static int ReturnValue(int value, out int result)
    {
        result = value;
        return Native.S_OK;
    }

    private static int ReturnNone(out IntPtr provider)
    {
        provider = IntPtr.Zero;
        return Native.S_OK;
    }
}
