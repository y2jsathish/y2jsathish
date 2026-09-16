namespace ATMTicketing.Application.Common;

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int TotalCount { get; init; }
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public class ServiceResult
{
    public bool Succeeded { get; init; }
    public string? Message { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public static ServiceResult Success(string? message = null) => new() { Succeeded = true, Message = message };
    public static ServiceResult Failure(params string[] errors) => new() { Succeeded = false, Errors = errors };
}

public class ServiceResult<T> : ServiceResult
{
    public T? Data { get; init; }

    public static ServiceResult<T> Success(T data, string? message = null) =>
        new() { Succeeded = true, Data = data, Message = message };

    public static new ServiceResult<T> Failure(params string[] errors) =>
        new() { Succeeded = false, Errors = errors };
}
