using EV_ERP.Filters;
using EV_ERP.Helpers;
using EV_ERP.Models.Entities.System;
using EV_ERP.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EV_ERP.Controllers;

[RequireLogin]
public class SlaConfigController : Controller
{
    private readonly IUnitOfWork _uow;

    public SlaConfigController(IUnitOfWork uow)
    {
        _uow = uow;
    }

    private CurrentUser CurrentUserInfo =>
        HttpContext.Session.GetObject<CurrentUser>(SessionKeys.CurrentUser)!;

    public async Task<IActionResult> Index()
    {
        if (CurrentUserInfo.RoleCode != "ADMIN")
            return RedirectToAction("Index", "Workspace");

        ViewData["Title"] = "Cấu hình SLA";

        var configs = await _uow.Repository<SlaConfig>().Query()
            .OrderBy(c => c.EntityType)
            .ThenBy(c => c.SlaConfigId)
            .ToListAsync();

        return View(configs);
    }

    [HttpPost]
    public async Task<IActionResult> Update([FromBody] SlaConfigUpdateModel model)
    {
        if (CurrentUserInfo.RoleCode != "ADMIN")
            return Json(new { Success = false, Message = "Không có quyền" });

        var config = await _uow.Repository<SlaConfig>().GetByIdAsync(model.SlaConfigId);
        if (config == null)
            return Json(new { Success = false, Message = "Không tìm thấy cấu hình" });

        if (model.DurationHours <= 0)
            return Json(new { Success = false, Message = "Thời gian phải lớn hơn 0" });

        if (model.WarningPercent < 1 || model.WarningPercent > 99)
            return Json(new { Success = false, Message = "Ngưỡng cảnh báo phải từ 1 đến 99%" });

        config.DurationHours = model.DurationHours;
        config.DurationCalendar = model.DurationCalendar;
        config.WarningPercent = model.WarningPercent;
        config.NotifyAssignee = model.NotifyAssignee;
        config.NotifyManager = model.NotifyManager;
        config.EscalateOnOverdue = model.EscalateOnOverdue;
        config.ConfigName = model.ConfigName.Trim();
        config.Description = model.Description?.Trim();
        config.IsActive = model.IsActive;
        config.UpdatedAt = DateTime.Now;

        _uow.Repository<SlaConfig>().Update(config);
        await _uow.SaveChangesAsync();

        return Json(new { Success = true });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SlaConfigCreateModel model)
    {
        if (CurrentUserInfo.RoleCode != "ADMIN")
            return Json(new { Success = false, Message = "Không có quyền" });

        if (string.IsNullOrWhiteSpace(model.EntityType) || string.IsNullOrWhiteSpace(model.FromStatus))
            return Json(new { Success = false, Message = "Vui lòng chọn loại và trạng thái" });

        if (model.DurationHours <= 0)
            return Json(new { Success = false, Message = "Thời gian phải lớn hơn 0" });

        // Check duplicate
        var exists = await _uow.Repository<SlaConfig>().Query()
            .AnyAsync(c => c.EntityType == model.EntityType && c.FromStatus == model.FromStatus);
        if (exists)
            return Json(new { Success = false, Message = "Đã có cấu hình cho loại và trạng thái này" });

        var config = new SlaConfig
        {
            EntityType = model.EntityType,
            FromStatus = model.FromStatus,
            DurationHours = model.DurationHours,
            DurationCalendar = model.DurationCalendar,
            WarningPercent = model.WarningPercent > 0 ? model.WarningPercent : 80,
            NotifyAssignee = model.NotifyAssignee,
            NotifyManager = model.NotifyManager,
            EscalateOnOverdue = model.EscalateOnOverdue,
            ConfigName = model.ConfigName.Trim(),
            Description = model.Description?.Trim(),
            IsActive = true,
            CreatedBy = CurrentUserInfo.UserId,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        await _uow.Repository<SlaConfig>().AddAsync(config);
        await _uow.SaveChangesAsync();

        return Json(new { Success = true, SlaConfigId = config.SlaConfigId });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        if (CurrentUserInfo.RoleCode != "ADMIN")
            return Json(new { Success = false, Message = "Không có quyền" });

        var config = await _uow.Repository<SlaConfig>().GetByIdAsync(id);
        if (config == null)
            return Json(new { Success = false, Message = "Không tìm thấy cấu hình" });

        // Check if has tracking records
        var hasTracking = await _uow.Repository<SlaTracking>().Query()
            .AnyAsync(t => t.SlaConfigId == id);

        if (hasTracking)
        {
            // Soft delete - just deactivate
            config.IsActive = false;
            config.UpdatedAt = DateTime.Now;
            _uow.Repository<SlaConfig>().Update(config);
        }
        else
        {
            _uow.Repository<SlaConfig>().Remove(config);
        }

        await _uow.SaveChangesAsync();
        return Json(new { Success = true });
    }
}

public class SlaConfigUpdateModel
{
    public int SlaConfigId { get; set; }
    public decimal DurationHours { get; set; }
    public bool DurationCalendar { get; set; }
    public decimal WarningPercent { get; set; }
    public bool NotifyAssignee { get; set; }
    public bool NotifyManager { get; set; }
    public bool EscalateOnOverdue { get; set; }
    public string ConfigName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class SlaConfigCreateModel
{
    public string EntityType { get; set; } = string.Empty;
    public string FromStatus { get; set; } = string.Empty;
    public decimal DurationHours { get; set; }
    public bool DurationCalendar { get; set; }
    public decimal WarningPercent { get; set; } = 80;
    public bool NotifyAssignee { get; set; } = true;
    public bool NotifyManager { get; set; }
    public bool EscalateOnOverdue { get; set; }
    public string ConfigName { get; set; } = string.Empty;
    public string? Description { get; set; }
}
