namespace ATMTicketing.Application.DTOs;

/// <summary>Request shape posted by jQuery DataTables server-side processing mode.</summary>
public class DataTableRequest
{
    public int Draw { get; set; }
    public int Start { get; set; }
    public int Length { get; set; }
    public string? SearchValue { get; set; }
    public string? SortColumn { get; set; }
    public string? SortDirection { get; set; } = "asc";
}

public class DataTableResponse<T>
{
    public int Draw { get; set; }
    public int RecordsTotal { get; set; }
    public int RecordsFiltered { get; set; }
    public IReadOnlyList<T> Data { get; set; } = Array.Empty<T>();
}
