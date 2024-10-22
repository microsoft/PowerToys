# The spec is next to this script:
$pathToSpec = Join-Path (Split-Path -parent $MyInvocation.MyCommand.Definition) initial-sdk-spec.md
# First, use the mistletoe library to parse the markdown file to json
$jsonText = mistletoe $pathToSpec --renderer mistletoe.ast_renderer.AstRenderer
$json =  $jsonText | ConvertFrom-Json

$sdkContents = ""
foreach ($item in $json.children) {
    if ($item.type -eq 'CodeFence') {
        # we only care about code fences with language 'csharp'
        if ($item.language -eq 'c#') {
            $code = $item.children.content
            # Each line that starts with with `runtimeclass` or `interface` should be prefixed with the contract attribute
            $code = $code -replace "(?m)^(runtimeclass|interface) ", "[contract(Microsoft.CmdPal.Extensions.ExtensionsContract, 1)]`n`$1 "

            # all the lines that start with `(whitespace)async (T)` should be translated to `IAsyncOperation<T>`
            $code = $code -replace "(?m)^(\s*)async\s+(void)\s+([A-Za-z0-9_]+)\s*\(", "`$1Windows.Foundation.IAsyncAction `$3("
            $code = $code -replace "(?m)^(\s*)async\s+([A-Za-z0-9_<>]+)\s+([A-Za-z0-9_]+)\s*\(", "`$1Windows.Foundation.IAsyncOperation<`$2> `$3("
            $code = $code -replace ">>", "> >"

            # Add four spaces to each line of the code block
            $code = $code -replace "(?m)^", "    "

            $sdkContents += $code + "`n"
        }
    }
}
foreach ($item in $json.children) {
    if ($item.type -eq 'CodeFence') {
        # we only care about code fences with language 'csharp'
        if ($item.language -eq 'csharp') {
            $code = $item.children.content
            # Each line that starts with with `runtimeclass` or `interface` should be prefixed with the contract attribute
            $code = $code -replace "(?m)^(runtimeclass|interface) ", "[contract(Microsoft.CmdPal.Extensions.ExtensionsContract, 1)]`n`$1 "

            # all the lines that start with `(whitespace)async (T)` should be translated to `IAsyncOperation<T>`
            $code = $code -replace "(?m)^(\s*)async\s+(void)\s+([A-Za-z0-9_]+)\s*\(", "`$1Windows.Foundation.IAsyncAction `$3("
            $code = $code -replace "(?m)^(\s*)async\s+([A-Za-z0-9_<>]+)\s+([A-Za-z0-9_]+)\s*\(", "`$1Windows.Foundation.IAsyncOperation<`$2> `$3("
            $code = $code -replace ">>", "> >"

            # Add four spaces to each line of the code block
            $code = $code -replace "(?m)^", "    "

            $sdkContents += $code + "`n"
        }
    }
}

# now, write the fully formatted interface with headers and all:
Write-Output @"
namespace Microsoft.CmdPal.Extensions
{
    [contractversion(1)]
    apicontract ExtensionsContract {}

    [contract(Microsoft.CmdPal.Extensions.ExtensionsContract, 1)]
    interface IExtension {
        IInspectable GetProvider(ProviderType providerType);
        void Dispose();
    };

    [contract(Microsoft.CmdPal.Extensions.ExtensionsContract, 1)]
    enum ProviderType {
        Commands = 0,
    };

    [contract(Microsoft.CmdPal.Extensions.ExtensionsContract, 1)]
    runtimeclass IconDataType {
        IconDataType(String iconString);
        String Icon { get; };
    };

    [contract(Microsoft.CmdPal.Extensions.ExtensionsContract, 1)]
    interface INotifyPropChanged {
        event Windows.Foundation.TypedEventHandler<Object, PropChangedEventArgs> PropChanged;
    };

    [contract(Microsoft.CmdPal.Extensions.ExtensionsContract, 1)]
    runtimeclass PropChangedEventArgs {
        PropChangedEventArgs(String propertyName);
        String PropertyName { get; };
    };

$sdkContents
}
"@
