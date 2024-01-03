# Value Generator Plugin

The Value Generator plugin is used to generate hashes for strings, to calculate base64 encodings, escape and encode URLs/URIs and to generate GUIDs versions 1, 3, 4 and 5.

![Image of Value Generator plugin](/doc/images/launcher/plugin/community.valuegenerator.png)

### [`IComputeRequest`](/src/modules/launcher/Plugins/Community.PowerToys.Run.Plugin.ValueGenerator/IComputeRequest.cs)
- Interface for a request for computation
- the `bool Compute()` method must populate the `IsSuccessful` and one of the `Result` and `ErrorMessage` fields
- The result of `string ResultToString()` will be used for the Result's title
- The `Description` field will be used for the Result's subtitle

### [`HashRequest`](/src/modules/launcher/Plugins/Community.PowerToys.Run.Plugin.ValueGenerator/Generators/Hashing/HashRequest.cs)
- Implements IComputeRequest
- Supports the hashing algorithms from System.Security.Cryptography:
  - MD5
  - SHA1
  - SHA256
  - SHA384
  - SHA512
- If other algorithms are added to System.Security.Cryptography, they can be added to the `_algorithms` dictionary. [`InputParser.ParseInput()`](#inputparser) will need to return a `HashRequest` for the algorithm in the query

### [`Base64Request`](/src/modules/launcher/Plugins/Community.PowerToys.Run.Plugin.ValueGenerator/Generators/Base64/Base64Request.cs)
- Implements IComputeRequest
- `Compute()` will populate `Result` with the base64 encoding of the byte array passed in the constructor

### [`Base64DecodeRequest`](/src/modules/launcher/Plugins/Community.PowerToys.Run.Plugin.ValueGenerator/Generators/Base64/Base64DecodeRequest.cs)
- Implements IComputeRequest
- `Compute()` will populate `Result` with the decoded byte array of the base64 string passed in the constructor

### [`GUIDRequest`](/src/modules/launcher/Plugins/Community.PowerToys.Run.Plugin.ValueGenerator/Generators/GUID/GUIDRequest.cs)
- Implements IComputeRequest
- Uses the [`GUIDGenerator`](#guidgenerator) class to generate or compute the requested GUID

### [`GUIDGenerator`](/src/modules/launcher/Plugins/Community.PowerToys.Run.Plugin.ValueGenerator/Generators/GUID/GUIDGenerator.cs)
- Utility class for generating or calculating GUIDs
- Generating GUID versions 1 and 4 is done using builtin APIs. [`UuidCreateSequential`](https://learn.microsoft.com/en-us/windows/win32/api/rpcdce/nf-rpcdce-uuidcreatesequential) for version 1 and `System.Guid.NewGuid()` for version 4
- Versions 3 and 5 take two parameters, a namespace and a name
- The namespace must be a valid GUID or one of the [predefined ones](https://datatracker.ietf.org/doc/html/rfc4122#appendix-C)
- The `PredefinedNamespaces` dictionary contains aliases for the predefined namespaces
- The name can be any string

### [`UrlEncodeRequest`](/src/modules/launcher/Plugins/Community.PowerToys.Run.Plugin.ValueGenerator/Generators/Uri/UrlEncodeRequest.cs)
- Implements IComputeRequest
- `Compute()` will populate `Result` with the encoded url converted using `HttpUtility.UrlEncode()`.

### [`UrlDecodeRequest`](/src/modules/launcher/Plugins/Community.PowerToys.Run.Plugin.ValueGenerator/Generators/Uri/UrlDecodeRequest.cs)
- Implements IComputeRequest
- `Compute()` will populate `Result` with the decoded url converted using `HttpUtility.UrlDecode()`.

### [`DataEscapeRequest`](/src/modules/launcher/Plugins/Community.PowerToys.Run.Plugin.ValueGenerator/Generators/Uri/DataEscapeRequest.cs)
- Implements IComputeRequest
- `Compute()` will populate `Result` with the escaped data string converted using `System.Uri.EscapeDataString()`.

### [`DataUnescapeRequest`](/src/modules/launcher/Plugins/Community.PowerToys.Run.Plugin.ValueGenerator/Generators/Uri/DataUnescapeRequest.cs)
- Implements IComputeRequest
- `Compute()` will populate `Result` with the unescaped data string converted using `System.Uri.UnescapeDataString()`.

### [`HexEscapeRequest`](/src/modules/launcher/Plugins/Community.PowerToys.Run.Plugin.ValueGenerator/Generators/Uri/HexEscapeRequest.cs)
- Implements IComputeRequest
- `Compute()` will populate `Result` with the escaped data string converted using `System.Uri.HexEscape()`.
- Only single characters are supported as input.

### [`HexUnescapeRequest`](/src/modules/launcher/Plugins/Community.PowerToys.Run.Plugin.ValueGenerator/Generators/Uri/HexUnescapeRequest.cs)
- Implements IComputeRequest
- `Compute()` will populate `Result` with the unescaped data string converted using `System.Uri.HexUnescape()`.
- Only the first hexadecimal character in the string gets unescaped. The rest of the user input is ignored.

### [`InputParser`](/src/modules/launcher/Plugins/Community.PowerToys.Run.Plugin.ValueGenerator/InputParser.cs)
- It is responsible only for parsing the query from the user
- Based on the user query, the `ParseInput()` method must return an object that implements the `IComputeRequest` interface or it must throw one of `FormatException` or `ArgumentException`
- Throwing an `ArgumentException` should signal the fact the query contains a mistake that the user can fix (eg. an unsupported hash function, an invalid GUID version, an invalid namespace, etc)
> The error message will be shown to the user and no log message will be created
- Throwing a `FormatException` should signal either:
  - that the query may become valid, and so it does not make sense to show an error just yet (eg. the query does not contain a request yet, a hash request without a string to hash)
  - that the query is completely invalid
> The error message will not be shown to the user but a log message will be created

### Adding a new value generator
1. To add a new value generator, create a folder under `/src/modules/launcher/Plugins/Community.PowerToys.Run.Plugin.ValueGenerator/Generators/` and inside it add a class that implements `IComputeRequest`.
2. Add any utility classes that are specific to the new generator inside the same folder to keep them separated from the other generators.
3. Modify the `InputParser.ParseInput()` to handle a request for the new generator and return an instance of the class you created in step 1