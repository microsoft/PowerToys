$gitRoot = git rev-parse --show-toplevel

# $xamlFilesForStyler = (git ls-files "$gitRoot/**/*.xaml") -join ","
$xamlFilesForStyler = (git ls-files "$gitRoot/src/modules/cmdpal/**/*.xaml") -join ","
dotnet tool run xstyler -- -c "$gitRoot\src\Settings.XamlStyler" -f "$xamlFilesForStyler"