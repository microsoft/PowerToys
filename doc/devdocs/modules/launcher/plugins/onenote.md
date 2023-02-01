# OneNote Plugin
The OneNote plugin searches your locally synced OneNote notebooks based on the user query.

![Image of OneNote plugin](/doc/images/launcher/plugins/onenote.png)

The code itself is very simple, basically just a call into OneNote interop via the https://github.com/scipbe/ScipBe-Common-Office library.

```csharp
var pages = OneNoteProvider.FindPages(query.Search);
```

The query results will be cached for 1 day, and if cached results are found they'll be returned in the initial `Query()` call, otherwise OneNote itself will be queried in the `delayedExecution:true` overload.

If the user actions on a result, it'll open it in the OneNote app, and restore and/or focus the app as well if necessary.

```csharp
if (PInvoke.IsIconic(handle))
{
    PInvoke.ShowWindow(handle, SHOW_WINDOW_CMD.SW_RESTORE);
}

PInvoke.SetForegroundWindow(handle);
```

The plugin attempts to call the library in the constructor, and if it fails with a COMException then it'll note that OneNote isn't available and not attempt to query it again.

```csharp
try
{
    _ = OneNoteProvider.PageItems.Any();
    _oneNoteInstalled = true;
}
catch (COMException)
{
    // OneNote isn't installed, plugin won't do anything.
    _oneNoteInstalled = false;
}
```
