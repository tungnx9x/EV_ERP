using EV_ERP.Models.Entities.Auth;
using System.ComponentModel.DataAnnotations.Schema;

namespace EV_ERP.Models.Entities.System;

// ─── ATTACHMENT (Đính kèm file — polymorphic) ────────
public class Attachment
{
    public int AttachmentId { get; set; }
    /// <summary>RFQ, QUOTATION, SALES_ORDER, PURCHASE_ORDER, STOCK_TRANSACTION, ADVANCE_REQUEST, VENDOR_INVOICE</summary>
    public string ReferenceType { get; set; } = string.Empty;
    public int ReferenceId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public long? FileSize { get; set; }
    public string? ContentType { get; set; }
    /// <summary>CUSTOMER_PO, DELIVERY_RECEIPT, ADVANCE_DOC, INVOICE, OTHER</summary>
    public string? FileCategory { get; set; }
    public string? Description { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.Now;
    public int UploadedBy { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual User UploadedByUser { get; set; } = null!;
}

// ─── AUDIT LOG ───────────────────────────────────────
public class AuditLog
{
    public long AuditLogId { get; set; }
    public string TableName { get; set; } = string.Empty;
    public int RecordId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? ChangedFields { get; set; }
    public int? UserId { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public virtual User? User { get; set; }
}

// ─── NOTIFICATION ────────────────────────────────────
public class Notification
{
    public long NotificationId { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string NotificationType { get; set; } = string.Empty;
    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public string Severity { get; set; } = "INFO";          // INFO, WARNING, DANGER
    public string? ActionUrl { get; set; }                   // VD: /SalesOrder/Detail/123
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public virtual User User { get; set; } = null!;
}

// ─── SLA CONFIG (Cấu hình thời gian cho từng bước) ──
public class SlaConfig
{
    public int SlaConfigId { get; set; }
    public string EntityType { get; set; } = string.Empty;       // RFQ, QUOTATION, SALES_ORDER
    public string FromStatus { get; set; } = string.Empty;
    public decimal DurationHours { get; set; }
    public bool DurationCalendar { get; set; }                   // 0 = giờ làm việc, 1 = 24/7
    public decimal WarningPercent { get; set; } = 80;
    public bool NotifyAssignee { get; set; } = true;
    public bool NotifyManager { get; set; }
    public bool EscalateOnOverdue { get; set; }
    public string ConfigName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public int CreatedBy { get; set; }

    public virtual User CreatedByUser { get; set; } = null!;
}

// ─── SLA TRACKING (Theo dõi deadline realtime) ──────
public class SlaTracking
{
    public long SlaTrackingId { get; set; }
    public int SlaConfigId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string TrackedStatus { get; set; } = string.Empty;
    public int? AssigneeId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime WarningAt { get; set; }
    public DateTime DeadlineAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    /// <summary>ACTIVE, WARNING, OVERDUE, COMPLETED, SKIPPED</summary>
    public string Status { get; set; } = "ACTIVE";
    public DateTime? WarningNotifiedAt { get; set; }
    public DateTime? OverdueNotifiedAt { get; set; }

    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public decimal? ElapsedHours { get; private set; }

    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public bool? IsOnTime { get; private set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public virtual SlaConfig SlaConfig { get; set; } = null!;
    public virtual User? Assignee { get; set; }
}
