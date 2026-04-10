using EV_ERP.Models.Common;
using EV_ERP.Models.Entities.Auth;

namespace EV_ERP.Models.Entities.Templates;

// ─── PDF TEMPLATE (Mẫu PDF) ─────────────────────────
public class PdfTemplate : BaseEntity, ISoftDeletable
{
    public int TemplateId { get; set; }
    public string TemplateCode { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    /// <summary>QUOTATION, SALES_ORDER, DELIVERY_NOTE</summary>
    public string TemplateType { get; set; } = string.Empty;
    public string Language { get; set; } = "vi";
    public string HtmlContent { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? PreviewImageUrl { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public int CreatedBy { get; set; }

    public virtual User CreatedByUser { get; set; } = null!;
    public virtual ICollection<TemplateAssignment> Assignments { get; set; } = [];
    public virtual ICollection<GeneratedPdf> GeneratedPdfs { get; set; } = [];
}

// ─── TEMPLATE ASSIGNMENT (Gán template cho đối tượng) ─
// Waterfall khi xuất PDF:
//   1. Template gán riêng cho Customer     (TargetType=CUSTOMER,       Priority=10)
//   2. Template gán cho CustomerGroup       (TargetType=CUSTOMER_GROUP, Priority=5)
//   3. Template IsDefault=1 theo TemplateType
//   4. Hiện danh sách cho user chọn thủ công
public class TemplateAssignment
{
    public int AssignmentId { get; set; }
    public int TemplateId { get; set; }
    /// <summary>CUSTOMER hoặc CUSTOMER_GROUP</summary>
    public string TargetType { get; set; } = string.Empty;
    /// <summary>CustomerId hoặc CustomerGroupId tùy TargetType</summary>
    public int TargetId { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public virtual PdfTemplate Template { get; set; } = null!;
}

// ─── GENERATED PDF (File PDF đã xuất) ────────────────
public class GeneratedPdf
{
    public int GeneratedPdfId { get; set; }
    public int TemplateId { get; set; }
    public string ReferenceType { get; set; } = string.Empty;
    public int ReferenceId { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public int? FileSize { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.Now;
    public int GeneratedBy { get; set; }

    public virtual PdfTemplate Template { get; set; } = null!;
    public virtual User GeneratedByUser { get; set; } = null!;
}
