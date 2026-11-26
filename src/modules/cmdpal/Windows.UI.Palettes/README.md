# Windows.UI.Palettes contract

`Windows.UI.Palettes.idl` is copied unmodified from commit
`ee050e42`.

The contract is available before its addition to the public Windows SDK
metadata. Two projects use it:

* `Windows.UI.Palettes.vcxproj` compiles the IDL into
  `Windows.UI.Palettes.winmd` with a `<Midl>` item.
* `..\Windows.UI.Palettes.Projection` uses CsWinRT to generate the C# projection.

When the SDK includes `Windows.UI.Palettes`, remove this folder and the projection project.

When you update the contract, copy both files from the same OsClient commit.
Then update the commit value in this file.
