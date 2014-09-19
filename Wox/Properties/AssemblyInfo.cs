using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;

// 有关程序集的常规信息通过以下
// 特性集控制。更改这些特性值可修改
// 与程序集关联的信息。
[assembly: AssemblyTitle("Wox")]
[assembly: AssemblyDescription("https://github.com/qianlifeng/Wox")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("Wox")]
[assembly: AssemblyCopyright("The MIT License (MIT)")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// 将 ComVisible 设置为 false 使此程序集中的类型
// 对 COM 组件不可见。如果需要从 COM 访问此程序集中的类型，
// 则将该类型上的 ComVisible 特性设置为 true。
[assembly: ComVisible(false)]

//若要开始生成可本地化的应用程序，请在 
//<PropertyGroup> 中的 .csproj 文件中
//设置 <UICulture>CultureYouAreCodingWith</UICulture>。例如，如果您在源文件中
//使用的是美国英语，请将 <UICulture> 设置为 en-US。然后取消
//对以下 NeutralResourceLanguage 特性的注释。更新
//以下行中的“en-US”以匹配项目文件中的 UICulture 设置。

//[assembly: NeutralResourcesLanguage("en-US", UltimateResourceFallbackLocation.Satellite)]


[assembly: ThemeInfo(
    ResourceDictionaryLocation.None, //主题特定资源词典所处位置
    //(在页面或应用程序资源词典中 
    // 未找到某个资源的情况下使用)
    ResourceDictionaryLocation.SourceAssembly //常规资源词典所处位置
    //(在页面、应用程序或任何主题特定资源词典中
    // 未找到某个资源的情况下使用)
)]


// 程序集的版本信息由下面四个值组成:
//
//      主版本
//      次版本 
//      生成号
//      修订号
//
// 可以指定所有这些值，也可以使用“生成号”和“修订号”的默认值，
// 方法是按如下所示使用“*”:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]