# AdvancedPaste.xaml 节点结构分析

## 根节点结构

```
Page (根节点)
├── x:Name="RootPage"
├── x:Class="Microsoft.PowerToys.Settings.UI.Views.AdvancedPastePage"
└── AutomationProperties.LandmarkType="Main"

├── Page.Resources
│   └── ResourceDictionary
│       ├── ResourceDictionary.ThemeDictionaries
│       │   ├── ResourceDictionary (x:Key="Default")
│       │   │   └── ImageSource (x:Key="DialogHeaderImage")
│       │   ├── ResourceDictionary (x:Key="Light")
│       │   │   └── ImageSource (x:Key="DialogHeaderImage")
│       │   └── ResourceDictionary (x:Key="HighContrast")
│       │       └── ImageSource (x:Key="DialogHeaderImage")
│       └── DataTemplate (x:Key="AdditionalActionTemplate")
│           └── StackPanel
│               ├── controls:ShortcutControl
│               └── ToggleSwitch

└── Grid (主容器)
    ├── controls:SettingsPageControl (x:Uid="AdvancedPaste")
    │   ├── controls:SettingsPageControl.ModuleContent
    │   │   └── StackPanel (主内容区域)
    │   │       ├── tkcontrols:SettingsCard (启用开关)
    │   │       │   ├── x:Uid="AdvancedPaste_EnableToggleControl_HeaderText"
    │   │       │   └── ToggleSwitch (x:Uid="ToggleSwitch")
    │   │       │
    │   │       ├── InfoBar (GPO管理信息)
    │   │       │   └── x:Uid="GPO_SettingIsManaged"
    │   │       │
    │   │       ├── controls:SettingsGroup (AI设置组)
    │   │       │   ├── x:Uid="AdvancedPaste_EnableAISettingsGroup"
    │   │       │   ├── InfoBar (AI GPO信息)
    │   │       │   │   └── x:Uid="GPO_AdvancedPasteAi_SettingIsManaged"
    │   │       │   ├── tkcontrols:SettingsCard (启用AI)
    │   │       │   │   ├── x:Uid="AdvancedPaste_EnableAISettingsCard"
    │   │       │   │   ├── tkcontrols:SwitchPresenter
    │   │       │   │   │   ├── tkcontrols:Case (Value="True")
    │   │       │   │   │   │   └── Button (x:Uid="AdvancedPaste_DisableAIButton")
    │   │       │   │   │   └── tkcontrols:Case (Value="False")
    │   │       │   │   │       └── Button (x:Uid="AdvancedPaste_EnableAIButton")
    │   │       │   │   └── tkcontrols:SettingsCard.Description
    │   │       │   │       └── StackPanel
    │   │       │   │           ├── TextBlock (x:Uid="AdvancedPaste_EnableAISettingsCardDescription")
    │   │       │   │           └── HyperlinkButton (x:Uid="AdvancedPaste_EnableAISettingsCardDescriptionLearnMore")
    │   │       │   └── tkcontrols:SettingsCard (高级AI)
    │   │       │       ├── x:Uid="AdvancedPaste_EnableAdvancedAI"
    │   │       │       └── ToggleSwitch
    │   │       │
    │   │       ├── controls:SettingsGroup (行为设置组)
    │   │       │   ├── x:Uid="AdvancedPaste_BehaviorSettingsGroup"
    │   │       │   ├── tkcontrols:SettingsCard (剪贴板历史)
    │   │       │   │   ├── x:Uid="AdvancedPaste_Clipboard_History_Enabled_SettingsCard"
    │   │       │   │   └── ToggleSwitch
    │   │       │   ├── InfoBar (剪贴板历史GPO信息)
    │   │       │   │   └── x:Uid="GPO_SettingIsManaged"
    │   │       │   ├── tkcontrols:SettingsCard (失去焦点后关闭)
    │   │       │   │   ├── x:Uid="AdvancedPaste_CloseAfterLosingFocus"
    │   │       │   │   └── ToggleSwitch
    │   │       │   └── tkcontrols:SettingsCard (显示自定义预览)
    │   │       │       ├── x:Uid="AdvancedPaste_ShowCustomPreviewSettingsCard"
    │   │       │       └── ToggleSwitch
    │   │       │
    │   │       ├── controls:SettingsGroup (直接访问热键组)
    │   │       │   ├── x:Uid="AdvancedPaste_Direct_Access_Hotkeys_GroupSettings"
    │   │       │   ├── tkcontrols:SettingsCard (操作)
    │   │       │   │   ├── x:Uid="AdvancedPasteUI_Actions"
    │   │       │   │   └── Button (x:Uid="AdvancedPasteUI_AddCustomActionButton")
    │   │       │   ├── tkcontrols:SettingsCard (UI快捷键)
    │   │       │   │   ├── x:Uid="AdvancedPasteUI_Shortcut"
    │   │       │   │   └── controls:ShortcutControl
    │   │       │   ├── tkcontrols:SettingsCard (纯文本粘贴快捷键)
    │   │       │   │   ├── x:Uid="PasteAsPlainText_Shortcut"
    │   │       │   │   └── controls:ShortcutControl
    │   │       │   ├── tkcontrols:SettingsCard (Markdown粘贴快捷键)
    │   │       │   │   ├── x:Uid="PasteAsMarkdown_Shortcut"
    │   │       │   │   └── controls:ShortcutControl
    │   │       │   ├── tkcontrols:SettingsCard (JSON粘贴快捷键)
    │   │       │   │   ├── x:Uid="PasteAsJson_Shortcut"
    │   │       │   │   └── controls:ShortcutControl
    │   │       │   ├── ItemsControl (自定义操作列表)
    │   │       │   │   ├── x:Name="CustomActions"
    │   │       │   │   ├── x:Uid="CustomActions"
    │   │       │   │   └── ItemsControl.ItemTemplate
    │   │       │   │       └── DataTemplate
    │   │       │   │           └── tkcontrols:SettingsCard
    │   │       │   │               └── StackPanel
    │   │       │   │                   ├── controls:ShortcutControl
    │   │       │   │                   ├── ToggleSwitch (x:Uid="Enable_CustomAction")
    │   │       │   │                   └── Button (x:Uid="More_Options_Button")
    │   │       │   │                       └── Button.Flyout
    │   │       │   │                           └── MenuFlyout
    │   │       │   │                               ├── MenuFlyoutItem (x:Uid="MoveUp")
    │   │       │   │                               ├── MenuFlyoutItem (x:Uid="MoveDown")
    │   │       │   │                               ├── MenuFlyoutSeparator
    │   │       │   │                               └── MenuFlyoutItem (x:Uid="RemoveItem")
    │   │       │   └── InfoBar (快捷键警告)
    │   │       │       └── x:Uid="AdvancedPaste_ShortcutWarning"
    │   │       │
    │   │       └── controls:SettingsGroup (附加操作组)
    │   │           ├── x:Uid="AdvancedPaste_Additional_Actions_GroupSettings"
    │   │           ├── tkcontrols:SettingsCard (图像转文本)
    │   │           │   ├── x:Uid="ImageToText"
    │   │           │   └── ContentControl
    │   │           ├── tkcontrols:SettingsExpander (粘贴为文件)
    │   │           │   ├── x:Uid="PasteAsFile"
    │   │           │   ├── tkcontrols:SettingsExpander.Content
    │   │           │   │   └── ToggleSwitch
    │   │           │   └── tkcontrols:SettingsExpander.Items
    │   │           │       ├── tkcontrols:SettingsCard (隐藏卡片)
    │   │           │       ├── tkcontrols:SettingsCard (x:Uid="PasteAsTxtFile")
    │   │           │       ├── tkcontrols:SettingsCard (x:Uid="PasteAsPngFile")
    │   │           │       ├── tkcontrols:SettingsCard (x:Uid="PasteAsHtmlFile")
    │   │           │       └── tkcontrols:SettingsCard (隐藏卡片)
    │   │           ├── tkcontrols:SettingsExpander (转码)
    │   │           │   ├── x:Uid="Transcode"
    │   │           │   ├── tkcontrols:SettingsExpander.Content
    │   │           │   │   └── ToggleSwitch
    │   │           │   └── tkcontrols:SettingsExpander.Items
    │   │           │       ├── tkcontrols:SettingsCard (隐藏卡片)
    │   │           │       ├── tkcontrols:SettingsCard (x:Uid="TranscodeToMp3")
    │   │           │       ├── tkcontrols:SettingsCard (x:Uid="TranscodeToMp4")
    │   │           │       └── tkcontrols:SettingsCard (隐藏卡片)
    │   │           └── InfoBar (附加操作快捷键警告)
    │   │               └── x:Uid="AdvancedPaste_ShortcutWarning"
    │   │
    │   └── controls:SettingsPageControl.PrimaryLinks
    │       └── controls:PageLink (x:Uid="LearnMore_AdvancedPaste")
    │
    ├── ContentDialog (启用AI对话框)
    │   ├── x:Name="EnableAIDialog"
    │   ├── x:Uid="EnableAIDialog"
    │   └── ScrollViewer
    │       └── Grid
    │           ├── Grid.RowDefinitions (4行)
    │           ├── Image (对话框头部图像)
    │           ├── TextBlock (描述文本)
    │           │   ├── Run (x:Uid="AdvancedPaste_EnableAIDialog_Description")
    │           │   ├── Hyperlink (条款链接)
    │           │   │   └── Run (x:Uid="TermsLink")
    │           │   ├── Run (x:Uid="AIFooterSeparator")
    │           │   └── Hyperlink (隐私政策链接)
    │           │       └── Run (x:Uid="PrivacyLink")
    │           ├── StackPanel (配置OpenAI密钥)
    │           │   ├── TextBlock (x:Uid="AdvancedPaste_EnableAIDialog_ConfigureOpenAIKey")
    │           │   └── TextBlock (详细说明)
    │           │       ├── Run (x:Uid="AdvancedPaste_EnableAIDialog_LoginIntoText")
    │           │       ├── Hyperlink (API密钥链接)
    │           │       │   └── Run (x:Uid="AdvancedPaste_EnableAIDialog_OpenAIApiKeysOverviewText")
    │           │       ├── Run (x:Uid="AdvancedPaste_EnableAIDialog_CreateNewKeyText")
    │           │       └── Run (x:Uid="AdvancedPaste_EnableAIDialog_NoteAICreditsText")
    │           └── Grid (API密钥输入)
    │               ├── TextBlock (x:Uid="AdvancedPaste_EnableAIDialogOpenAIApiKey")
    │               └── TextBox (x:Name="AdvancedPaste_EnableAIDialogOpenAIApiKey")
    │
    └── ContentDialog (自定义操作对话框)
        ├── x:Name="CustomActionDialog"
        ├── x:Uid="CustomActionDialog"
        └── StackPanel
            ├── TextBox (x:Uid="AdvancedPasteUI_CustomAction_Name")
            └── TextBox (x:Uid="AdvancedPasteUI_CustomAction_Prompt")
```

## 具有x:Uid和AutomationProperties.AutomationId的元素统计

### 主要设置元素
1. **AdvancedPaste** - 页面控件
2. **AdvancedPaste_EnableToggleControl_HeaderText** - 启用开关卡片
3. **ToggleSwitch** - 主开关
4. **GPO_SettingIsManaged** - GPO管理信息栏
5. **AdvancedPaste_EnableAISettingsGroup** - AI设置组
6. **GPO_AdvancedPasteAi_SettingIsManaged** - AI GPO信息栏
7. **AdvancedPaste_EnableAISettingsCard** - AI启用卡片
8. **AdvancedPaste_DisableAIButton** - 禁用AI按钮
9. **AdvancedPaste_EnableAIButton** - 启用AI按钮
10. **AdvancedPaste_EnableAISettingsCardDescription** - AI描述文本
11. **AdvancedPaste_EnableAISettingsCardDescriptionLearnMore** - 了解更多链接
12. **AdvancedPaste_EnableAdvancedAI** - 高级AI卡片

### 行为设置元素
13. **AdvancedPaste_BehaviorSettingsGroup** - 行为设置组
14. **AdvancedPaste_Clipboard_History_Enabled_SettingsCard** - 剪贴板历史卡片
15. **AdvancedPaste_CloseAfterLosingFocus** - 失去焦点关闭卡片
16. **AdvancedPaste_ShowCustomPreviewSettingsCard** - 自定义预览卡片

### 快捷键设置元素
17. **AdvancedPaste_Direct_Access_Hotkeys_GroupSettings** - 直接访问热键组
18. **AdvancedPasteUI_Actions** - 操作卡片
19. **AdvancedPasteUI_AddCustomActionButton** - 添加自定义操作按钮
20. **AdvancedPasteUI_Shortcut** - UI快捷键卡片
21. **PasteAsPlainText_Shortcut** - 纯文本粘贴快捷键
22. **PasteAsMarkdown_Shortcut** - Markdown粘贴快捷键
23. **PasteAsJson_Shortcut** - JSON粘贴快捷键
24. **CustomActions** - 自定义操作列表
25. **Enable_CustomAction** - 启用自定义操作开关
26. **More_Options_Button** - 更多选项按钮
27. **MoveUp** - 上移菜单项
28. **MoveDown** - 下移菜单项
29. **RemoveItem** - 删除菜单项
30. **More_Options_ButtonTooltip** - 更多选项按钮工具提示
31. **AdvancedPaste_ShortcutWarning** - 快捷键警告信息栏

### 附加操作元素
32. **AdvancedPaste_Additional_Actions_GroupSettings** - 附加操作组
33. **ImageToText** - 图像转文本卡片
34. **PasteAsFile** - 粘贴为文件扩展器
35. **PasteAsTxtFile** - 粘贴为TXT文件卡片
36. **PasteAsPngFile** - 粘贴为PNG文件卡片
37. **PasteAsHtmlFile** - 粘贴为HTML文件卡片
38. **Transcode** - 转码扩展器
39. **TranscodeToMp3** - 转码为MP3卡片
40. **TranscodeToMp4** - 转码为MP4卡片

### 链接和对话框元素
41. **LearnMore_AdvancedPaste** - 了解更多链接
42. **EnableAIDialog** - 启用AI对话框
43. **AdvancedPaste_EnableAIDialog_Description** - AI对话框描述
44. **TermsLink** - 条款链接
45. **AIFooterSeparator** - AI页脚分隔符
46. **PrivacyLink** - 隐私政策链接
47. **AdvancedPaste_EnableAIDialog_ConfigureOpenAIKey** - 配置OpenAI密钥标题
48. **AdvancedPaste_EnableAIDialog_LoginIntoText** - 登录说明文本
49. **AdvancedPaste_EnableAIDialog_OpenAIApiKeysOverviewText** - API密钥概述文本
50. **AdvancedPaste_EnableAIDialog_CreateNewKeyText** - 创建新密钥文本
51. **AdvancedPaste_EnableAIDialog_NoteAICreditsText** - AI积分说明文本
52. **AdvancedPaste_EnableAIDialogOpenAIApiKey** - OpenAI API密钥标签
53. **CustomActionDialog** - 自定义操作对话框
54. **AdvancedPasteUI_CustomAction_Name** - 自定义操作名称输入框
55. **AdvancedPasteUI_CustomAction_Prompt** - 自定义操作提示输入框

## 总结
- **总节点数**: 55个具有x:Uid和AutomationProperties.AutomationId的元素
- **主要容器**: Page → Grid → SettingsPageControl → StackPanel
- **设置组数**: 4个主要设置组
- **对话框数**: 2个内容对话框
- **层级深度**: 最深约8-10层嵌套 