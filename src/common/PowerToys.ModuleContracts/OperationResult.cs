namespace PowerToys.ModuleContracts;

/// <summary>
/// Lightweight result type for module operations.
/// </summary>
public readonly record struct OperationResult(bool Success, string? Error = null)
{
    public static OperationResult Ok() => new(true, null);

    public static OperationResult Fail(string error) => new(false, error);
}

/// <summary>
/// Result type with a payload.
/// </summary>
public readonly record struct OperationResult<T>(bool Success, T? Value, string? Error = null)
{
    public static OperationResult<T> Ok(T value) => new(true, value, null);

    public static OperationResult<T> Fail(string error) => new(false, default, error);
}
