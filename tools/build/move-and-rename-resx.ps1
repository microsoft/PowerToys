[CmdletBinding()]

# This script finds all C#/.NET resx files and renames them from
# Folder/Language/x.resw to Folder/x.Language.resw, with language
# mapping. This is required because Touchdown localization uses a
# different directory structure.
$Items = Get-ChildItem . -Recurse -Filter *.resx

# Each of the projects we care about stores its resources
# in a Properties directory. We **DO NOT** want to move
# resource files from other projects (since we use resx files
# in standard Win32 projects as well.)
$Items = $Items | Where-Object {
	$_.Directory.Parent.Name -Eq "Properties"
}

If ($Items.Count -Le 0) {
	# Nothing to do.
	Write-Verbose "Nothing to do."
	Exit 0
}

ForEach($Item in $Items) {
	$PropertiesRoot = $Item.Directory.Parent
	$Language = $Item.Directory.Name
	$Destination = Join-Path $PropertiesRoot.FullName ("{0}.{1}{2}" -F ($Item.BaseName, $Language, $Item.Extension))
	Write-Verbose "Renaming $($Item.FullName) to $Destination"
	Move-Item $Item.FullName $Destination
}
