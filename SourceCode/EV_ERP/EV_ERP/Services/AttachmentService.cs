using EV_ERP.Models.Entities.System;
using EV_ERP.Repositories.Interfaces;
using EV_ERP.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EV_ERP.Services;

public class AttachmentService : IAttachmentService
{
    private readonly IUnitOfWork _uow;
    private readonly string _storageRoot;
    private readonly ILogger<AttachmentService> _logger;

    private static readonly HashSet<string> AllowedExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];
    private const long MaxFileSize = 5 * 1024 * 1024; // 5MB

    public AttachmentService(IUnitOfWork uow, IConfiguration config,
        IWebHostEnvironment env, ILogger<AttachmentService> logger)
    {
        _uow = uow;
        _storageRoot = config["FileStorage:RootPath"] ?? Path.Combine(env.ContentRootPath, "ERP_Files");
        _logger = logger;
    }

    public async Task<AttachmentResult?> UploadImageAsync(IFormFile file, string referenceType,
        int referenceId, string? fileCategory, string? description, int uploadedBy)
    {
        if (file == null || file.Length == 0) return null;

        var ext = Path.GetExtension(file.FileName).ToLower();
        if (!AllowedExtensions.Contains(ext)) return null;
        if (file.Length > MaxFileSize) return null;

        var subfolder = Path.Combine("Attachments", referenceType);
        var dir = Path.Combine(_storageRoot, subfolder);
        Directory.CreateDirectory(dir);

        var fileName = $"{referenceType.ToLower()}-{referenceId}-{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(dir, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        var relativeUrl = $"/uploads/{subfolder}/{fileName}".Replace('\\', '/');

        var attachment = new Attachment
        {
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            FileName = file.FileName,
            FileUrl = relativeUrl,
            FileSize = file.Length,
            ContentType = file.ContentType,
            FileCategory = fileCategory,
            Description = description,
            UploadedAt = DateTime.Now,
            UploadedBy = uploadedBy,
            IsActive = true
        };

        await _uow.Repository<Attachment>().AddAsync(attachment);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("Attachment uploaded: {Type} #{RefId} by UserId={UserId}",
            referenceType, referenceId, uploadedBy);

        return new AttachmentResult
        {
            AttachmentId = attachment.AttachmentId,
            FileUrl = relativeUrl,
            FileName = file.FileName
        };
    }

    public async Task<List<AttachmentDto>> GetListAsync(string referenceType, int referenceId)
    {
        return await _uow.Repository<Attachment>().Query()
            .Where(a => a.ReferenceType == referenceType && a.ReferenceId == referenceId && a.IsActive)
            .OrderBy(a => a.UploadedAt)
            .Select(a => new AttachmentDto
            {
                AttachmentId = a.AttachmentId,
                FileName = a.FileName,
                FileUrl = a.FileUrl,
                FileCategory = a.FileCategory,
                Description = a.Description,
                FileSize = a.FileSize,
                UploadedAt = a.UploadedAt,
                UploadedByName = a.UploadedByUser.FullName
            })
            .ToListAsync();
    }

    public async Task<bool> DeleteAsync(int attachmentId, int userId)
    {
        var att = await _uow.Repository<Attachment>().Query()
            .FirstOrDefaultAsync(a => a.AttachmentId == attachmentId && a.IsActive);

        if (att == null) return false;

        att.IsActive = false;
        await _uow.SaveChangesAsync();

        _logger.LogInformation("Attachment soft-deleted: #{Id} by UserId={UserId}", attachmentId, userId);
        return true;
    }
}
