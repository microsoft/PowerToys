# How to use

We use the awesome [spdlog](https://github.com/gabime/spdlog) library for logging as a git submodule under the `deps` directory. To use it in your project, just include [spdlog.props](../../deps/spdlog.props) in a .vcxproj like this:

```xml
<Import Project="..\..\..\deps\spdlog.props" />
```
It'll add the required include dirs and link the library binary itself.

