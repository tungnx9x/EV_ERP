using Microsoft.AspNetCore.Http;

namespace EV_ERP.Services.Interfaces;

public interface IAttachmentService
{
    Task<AttachmentResult?> UploadImageAsync(IFormFile file, string referenceType, int referenceId,
        string? fileCategory, string? description, int uploadedBy);

    Task<List<AttachmentDto>> GetListAsync(string referenceType, int referenceId);

    Task<bool> DeleteAsync(int attachmentId, int userId);
}

public class AttachmentResult
{
    public int AttachmentId { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}

public class AttachmentDto
{
    public int AttachmentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string? FileCategory { get; set; }
    public string? Description { get; set; }
    public long? FileSize { get; set; }
    public DateTime UploadedAt { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
}
