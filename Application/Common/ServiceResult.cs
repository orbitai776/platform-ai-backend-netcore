// AdminService.Application/Common/ServiceResult.cs
namespace AdminService.Application.Common;

public class ServiceResult<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }

    public static ServiceResult<T> Ok(T data, string? msg = null)
        => new() { Success = true, Data = data, Message = msg };

    public static ServiceResult<T> Fail(string msg)
        => new() { Success = false, Message = msg };
}