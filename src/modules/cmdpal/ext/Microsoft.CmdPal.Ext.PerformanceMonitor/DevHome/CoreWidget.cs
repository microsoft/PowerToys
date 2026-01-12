// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using CoreWidgetProvider.Widgets.Enums;
using Microsoft.Windows.Widgets.Providers;
using Windows.ApplicationModel;

namespace CoreWidgetProvider.Widgets;

internal abstract class CoreWidget : WidgetImpl
{
    protected static readonly string EmptyJson = new JsonObject().ToJsonString();

    protected WidgetActivityState ActivityState { get; set; } = WidgetActivityState.Unknown;

    protected WidgetDataState DataState { get; set; } = WidgetDataState.Unknown;

    protected WidgetPageState Page { get; set; } = WidgetPageState.Unknown;

    protected string ContentData { get; set; } = EmptyJson;

    protected bool Enabled
    {
        get; set;
    }

    protected Dictionary<WidgetPageState, string> Template { get; set; } = new();

    public CoreWidget()
    {
    }

    public virtual string GetConfiguration(string data) => throw new NotImplementedException();

    public virtual void LoadContentData() => throw new NotImplementedException();

    public override void CreateWidget(WidgetContext widgetContext, string state)
    {
        Id = widgetContext.Id;
        Enabled = widgetContext.IsActive;
        UpdateActivityState();
    }

    public override void Activate(WidgetContext widgetContext)
    {
        Enabled = true;
        UpdateActivityState();
    }

    public override void Deactivate(string widgetId)
    {
        Enabled = false;
        UpdateActivityState();
    }

    public override void DeleteWidget(string widgetId, string customState)
    {
        Enabled = false;
        SetDeleted();
    }

    public override void OnWidgetContextChanged(WidgetContextChangedArgs contextChangedArgs)
    {
        Enabled = contextChangedArgs.WidgetContext.IsActive;
        UpdateActivityState();
    }

    public override void OnActionInvoked(WidgetActionInvokedArgs actionInvokedArgs) => throw new NotImplementedException();

    public override void OnCustomizationRequested(WidgetCustomizationRequestedArgs customizationRequestedArgs) => throw new NotImplementedException();

    protected WidgetAction GetWidgetActionForVerb(string verb)
    {
        try
        {
            return Enum.Parse<WidgetAction>(verb);
        }
        catch (Exception)
        {
            // Invalid verb.
            // Log.Error($"Unknown WidgetAction verb: {verb}");
            return WidgetAction.Unknown;
        }
    }

    public virtual void UpdateActivityState()
    {
        if (Enabled)
        {
            SetActive();
            return;
        }

        SetInactive();
    }

    public virtual void UpdateWidget()
    {
        LoadContentData();

        WidgetUpdateRequestOptions updateOptions = new(Id)
        {
            Data = GetData(Page),
            Template = GetTemplateForPage(Page),
            CustomState = State(),
        };

        // Log.Debug($"Updating widget for {Page}");
        try
        {
            WidgetManager.GetDefault().UpdateWidget(updateOptions);
        }
        catch (Exception)
        {
            // Log.Error(ex, "Exception updating widget via WidgetManager.");
        }
    }

    public virtual string GetTemplatePath(WidgetPageState page)
    {
        return string.Empty;
    }

    public virtual string GetData(WidgetPageState page)
    {
        return string.Empty;
    }

    protected string GetTemplateForPage(WidgetPageState page)
    {
        if (Template.TryGetValue(page, out var value))
        {
            // Log.Debug($"Using cached template for {page}");
            return value;
        }

        try
        {
            var path = Path.Combine(Package.Current.EffectivePath, GetTemplatePath(page));
            var template = File.ReadAllText(path, Encoding.Default) ?? throw new FileNotFoundException(path);

            // template = Resources.ReplaceIdentifers(template, Resources.GetWidgetResourceIdentifiers(), Log);
            // Log.Debug($"Caching template for {page}");
            Template[page] = template;
            return template;
        }
        catch (Exception)
        {
            // Log.Error(e, "Error getting template.");
            return string.Empty;
        }
    }

    protected string GetCurrentState()
    {
        return $"State: {ActivityState}  Page: {Page}  Data: {DataState}  State: {State()}";
    }

    protected void LogCurrentState()
    {
        // Log.Debug(GetCurrentState());
    }

    protected virtual void SetActive()
    {
        ActivityState = WidgetActivityState.Active;
        Page = WidgetPageState.Content;
        if (ContentData == EmptyJson)
        {
            LoadContentData();
        }

        LogCurrentState();
        UpdateWidget();
    }

    protected virtual void SetInactive()
    {
        ActivityState = WidgetActivityState.Inactive;

        LogCurrentState();
    }

    protected virtual void SetDeleted()
    {
        SetState(string.Empty);
        ActivityState = WidgetActivityState.Unknown;
        LogCurrentState();
    }
}
