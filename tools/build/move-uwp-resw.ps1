[CmdletBinding()]

# This script finds all UWP resw files that are emitted by Touchdown
# with a bad path (en-us/lang-lang) and moves them to the correct
# location.
$Items = Get-ChildItem . -Recurse -Filter *.resw |
	Where-Object FullName -Like "*\en-US\*\*.resw"

If ($Items.Count -Le 0) {
	# Nothing to do.
	Write-Verbose "Nothing to do."
	Exit 0
}

$Items | ForEach-Object {
	# Move each resw file's parent folder into its parent's parent's folder.
	Move-Item -Verbose $_.Directory.FullName $_.Directory.Parent.Parent.FullName -EA:Ignore
}
