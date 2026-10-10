# Unicode Sentence_Break data

Advanced Paste vendors the Unicode 17.0.0 `SentenceBreakProperty.txt` file from
<https://www.unicode.org/Public/17.0.0/ucd/auxiliary/SentenceBreakProperty.txt>.
Its required SHA-256 is
`871c0c985ad95125e25b302414065a10839d068970bceb383ecec138f22a0a18`.

From the repository root, regenerate the lookup with:

```powershell
dotnet run --project src/modules/AdvancedPaste/tools/GenerateSentenceBreakData -- src/modules/AdvancedPaste/UnicodeData/SentenceBreakProperty.txt src/modules/AdvancedPaste/AdvancedPaste/Helpers/SentenceBreakData.g.cs
```

Verify the source, generated output, and exhaustive scalar lookup without
rewriting any file with:

```powershell
dotnet run --project src/modules/AdvancedPaste/tools/GenerateSentenceBreakData -- --verify src/modules/AdvancedPaste/UnicodeData/SentenceBreakProperty.txt src/modules/AdvancedPaste/AdvancedPaste/Helpers/SentenceBreakData.g.cs
```

To update Unicode, replace the source file, update the version, URL, and hash in
the generator and this document, regenerate the output, and update tests and
expected property counts together. The generated file must never be edited
manually. Normal builds and CI consume only vendored/generated files and require
no internet access.

The official source groups ranges by property. The generator deterministically
sorts parsed ranges by scalar value, then rejects any overlap before generating
the boundary table.

The official Unicode license is in `LICENSE.txt`. Generated-file provenance also
points to it. This data provides exact `Sentence_Break` property classification;
it does not claim to implement full UAX #29 sentence segmentation.
