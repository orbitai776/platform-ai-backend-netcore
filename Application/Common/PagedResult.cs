// AdminService.Application/Common/PagedResult.cs
namespace AdminService.Application.Common;

public class PagedResult<T>
{
    public List<T> Data { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int Limit { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)Total / Limit);
}