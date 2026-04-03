namespace EV_ERP.Models.Common
{
    /// <summary>
    /// Base entity với các trường audit chung
    /// </summary>
    public abstract class BaseEntity
    {
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
    /// <summary>
    /// Entity có theo dõi người tạo/sửa
    /// </summary>
    public abstract class AuditableEntity : BaseEntity
    {
        public int? CreatedBy { get; set; }
        public int? UpdatedBy { get; set; }
    }

    /// <summary>
    /// Interface soft delete — dùng cờ IsActive thay vì xóa vật lý
    /// </summary>
    public interface ISoftDeletable
    {
        bool IsActive { get; set; }
    }

    /// <summary>
    /// API response wrapper chuẩn cho Ajax calls
    /// </summary>
    public class ApiResult<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
        public List<string>? Errors { get; set; }

        public static ApiResult<T> Ok(T data, string? message = null)
            => new() { Success = true, Data = data, Message = message };

        public static ApiResult<T> Fail(string message, List<string>? errors = null)
            => new() { Success = false, Message = message, Errors = errors };
    }

    /// <summary>
    /// Phân trang
    /// </summary>
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasPrevious => PageIndex > 1;
        public bool HasNext => PageIndex < TotalPages;
    }
}
