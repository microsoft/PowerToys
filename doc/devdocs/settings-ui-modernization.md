# Settings UI Modernization Guide

This document describes the modernization patterns implemented in the PowerToys Settings UI to improve startup performance and maintainability.

## Overview

The Settings UI has been modernized with the following improvements:

1. **Dependency Injection (DI)** - Microsoft.Extensions.DependencyInjection for service resolution
2. **Page Caching** - Navigation caching to avoid page reconstruction
3. **Async ViewModel Initialization** - Non-blocking startup with IAsyncInitializable pattern
4. **Optimized Search** - ReaderWriterLockSlim for concurrent access

## Dependency Injection

### Configuration

Services are configured in `Services/AppServices.cs`:

```csharp
public static void Configure(Action<IServiceCollection> configureServices = null)
{
    var services = new ServiceCollection();
    ConfigureCoreServices(services);
    configureServices?.Invoke(services);
    _serviceProvider = services.BuildServiceProvider();
}
```

### Registered Services

| Interface | Implementation | Lifetime |
|-----------|----------------|----------|
| `INavigationService` | `NavigationServiceAdapter` | Singleton |
| `ISettingsService` | `SettingsService` | Singleton |
| `IIPCService` | `IPCService` | Singleton |
| `ViewModelLocator` | `ViewModelLocator` | Singleton |

### Usage

To resolve services:

```csharp
// In App.xaml.cs or any code
var navigationService = App.GetService<INavigationService>();

// Or directly from AppServices
var settingsService = AppServices.GetService<ISettingsService>();
```

## Page Caching

Pages are cached to avoid reconstruction on every navigation.

### Configuration

1. **Frame.CacheSize** - Set in `ShellPage.xaml.cs`:
   ```csharp
   navigationView.Frame.CacheSize = 10;
   ```

2. **NavigationCacheMode** - Enabled in `NavigablePage` base class:
   ```csharp
   protected NavigablePage()
   {
       NavigationCacheMode = NavigationCacheMode.Enabled;
   }
   ```

All pages inheriting from `NavigablePage` automatically get caching.

## Async ViewModel Initialization

### IAsyncInitializable Interface

```csharp
public interface IAsyncInitializable
{
    bool IsInitialized { get; }
    bool IsLoading { get; }
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
```

### PageViewModelBase Implementation

The base ViewModel class implements this pattern:

```csharp
public abstract class PageViewModelBase : IAsyncInitializable
{
    public bool IsInitialized { get; private set; }
    public bool IsLoading { get; private set; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (IsInitialized) return;

        IsLoading = true;
        try
        {
            await InitializeCoreAsync(cancellationToken);
            IsInitialized = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected virtual Task InitializeCoreAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
```

### Usage in ViewModels

Override `InitializeCoreAsync()` for async initialization:

```csharp
public class DashboardViewModel : PageViewModelBase
{
    protected override async Task InitializeCoreAsync(CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            // Heavy initialization work
            BuildModuleList();
        }, cancellationToken);
    }
}
```

## Search Service Optimization

### ReaderWriterLockSlim

The `SearchIndexService` uses `ReaderWriterLockSlim` for concurrent access:

- **Read operations**: Use `EnterReadLock()` for cache lookups
- **Write operations**: Use `EnterWriteLock()` for cache mutations

```csharp
private static readonly ReaderWriterLockSlim _cacheLock = new(LockRecursionPolicy.SupportsRecursion);

public static List<SettingEntry> Search(string query)
{
    _cacheLock.EnterReadLock();
    try
    {
        // Read from cache
    }
    finally
    {
        _cacheLock.ExitReadLock();
    }
}
```

### Async Index Building

Search index is built asynchronously after first paint:

```csharp
private void ShellPage_Loaded(object sender, RoutedEventArgs e)
{
    _ = SearchIndexService.BuildIndexAsync();
}
```

## Migration Guide

### Migrating Existing Pages

1. **Ensure page inherits from NavigablePage** (for caching)
2. **Update ViewModel to use InitializeCoreAsync()** for heavy initialization
3. **Replace static service calls** with DI-resolved services where feasible

### Example Migration

Before:
```csharp
public class MyViewModel
{
    public MyViewModel()
    {
        // Heavy sync initialization
        LoadSettings();
        BuildList();
    }
}
```

After:
```csharp
public class MyViewModel : PageViewModelBase
{
    protected override string ModuleName => "MyModule";

    protected override async Task InitializeCoreAsync(CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            LoadSettings();
            BuildList();
        }, cancellationToken);
    }
}
```

## Performance Metrics

| Metric | Before | After |
|--------|--------|-------|
| Dashboard → General navigation | ~500-800ms | <100ms (cached) |
| Search during navigation | Blocked | Concurrent |
| App startup | Blocking | Non-blocking init |

## Future Work

- [ ] Migrate remaining 30+ pages to new patterns
- [ ] Add loading indicators during async initialization
- [ ] Further reduce GeneralViewModel constructor parameters (13 → 4)
- [ ] Add startup telemetry for performance monitoring
