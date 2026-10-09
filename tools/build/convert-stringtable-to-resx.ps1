# This script is used to move the resources from a string table txt file to a resx file

# File containing only the rows of the string table
$stringTableFile = $args[0]

# Output resx file
$resxFile = $args[1]

# Temporary text file used by resgen
$tempFile = "temporaryResourceFile.txt"

$newLinesForTempFile = ""
foreach ($line in Get-Content $stringTableFile) {
    # Each line of string table text file is of the form IDS_ResName L"ResourceValue" where there can be any number of spaces between the two.
    $content = $line.Trim() -split "\s+", 2
    
    # Each line of the resgen text input needs to be of the form ResourceName=ResourceValue with no spaces. 
    # For the resource name for the resx file, we remove the IDS_ prefix and convert the words to title case. This can be imperfect since the parts between underscores may also comprise of multiple words, so that will have to be manually tweaked
    # For the resource value we only keep the content inside L""
    $lineInTempFileFormat = (Get-Culture).TextInfo.ToTitleCase($content[0].Substring(4).Replace("_", " ").ToLower()).Replace(" ", "_") + "=" + $content[1].Substring(2, $content[1].Length - 3)
    $newLinesForTempFile = $newLinesForTempFile + "`r`n" + $lineInTempFileFormat
}

# Save the text to a file
Set-Content -Path $tempFile -Value $newLinesForTempFile

# Use resgen to parse the txt to resx. More details at https://learn.microsoft.com/dotnet/framework/tools/resgen-exe-resource-file-generator#converting-between-resource-file-types
resgen $tempFile $resxFile

# Delete temporary text file used by resgen
Remove-Item $tempFile
