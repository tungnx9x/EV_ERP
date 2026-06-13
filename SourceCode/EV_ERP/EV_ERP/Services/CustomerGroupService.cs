using EV_ERP.Models.Entities.Customers;
using EV_ERP.Models.ViewModels.Customers;
using EV_ERP.Repositories.Interfaces;
using EV_ERP.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EV_ERP.Services
{
    public class CustomerGroupService : ICustomerGroupService
    {
        private readonly IUnitOfWork _uow;
        private readonly ILogger<CustomerGroupService> _logger;

        public CustomerGroupService(IUnitOfWork uow, ILogger<CustomerGroupService> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        // ── List ─────────────────────────────────────────
        public async Task<CustomerGroupListViewModel> GetListAsync(string? keyword, string? status)
        {
            var query = _uow.Repository<CustomerGroup>().Query()
                .Include(g => g.Customers);

            var groups = await query
                .OrderBy(g => g.PriorityLevel).ThenBy(g => g.GroupName)
                .ToListAsync();

            bool? activeFilter = status == "active" ? true : status == "inactive" ? false : null;
            var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim().ToLower();

            var rows = groups
                .Where(g => !activeFilter.HasValue || g.IsActive == activeFilter.Value)
                .Where(g => kw == null ||
                            g.GroupName.ToLower().Contains(kw) ||
                            g.GroupCode.ToLower().Contains(kw))
                .Select(g => new CustomerGroupRowViewModel
                {
                    CustomerGroupId = g.CustomerGroupId,
                    GroupCode = g.GroupCode,
                    GroupName = g.GroupName,
                    Description = g.Description,
                    PriorityLevel = g.PriorityLevel,
                    CustomerCount = g.Customers.Count,
                    IsActive = g.IsActive
                })
                .ToList();

            return new CustomerGroupListViewModel
            {
                Groups = rows,
                SearchKeyword = keyword,
                FilterStatus = status,
                TotalCount = rows.Count
            };
        }

        // ── Form ─────────────────────────────────────────
        public async Task<CustomerGroupFormViewModel> GetFormAsync(int? customerGroupId = null)
        {
            if (!customerGroupId.HasValue || customerGroupId <= 0)
                return new CustomerGroupFormViewModel();

            var group = await _uow.Repository<CustomerGroup>().GetByIdAsync(customerGroupId.Value);
            if (group == null)
                return new CustomerGroupFormViewModel();

            return new CustomerGroupFormViewModel
            {
                CustomerGroupId = group.CustomerGroupId,
                GroupCode = group.GroupCode,
                GroupName = group.GroupName,
                Description = group.Description,
                PriorityLevel = group.PriorityLevel,
                IsActive = group.IsActive
            };
        }

        // ── Create ───────────────────────────────────────
        public async Task<(bool Success, string? ErrorMessage)> CreateAsync(CustomerGroupFormViewModel model)
        {
            var repo = _uow.Repository<CustomerGroup>();

            var code = model.GroupCode.Trim().ToUpper();
            var codeConflict = await repo.AnyAsync(g => g.GroupCode.ToUpper() == code);
            if (codeConflict)
                return (false, "Mã nhóm này đã tồn tại");

            var group = new CustomerGroup
            {
                GroupCode = code,
                GroupName = model.GroupName.Trim(),
                Description = model.Description?.Trim(),
                PriorityLevel = model.PriorityLevel,
                IsActive = model.IsActive,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            await repo.AddAsync(group);
            await _uow.SaveChangesAsync();

            _logger.LogInformation("CustomerGroup created: {Code}", code);
            return (true, null);
        }

        // ── Update ───────────────────────────────────────
        public async Task<(bool Success, string? ErrorMessage)> UpdateAsync(CustomerGroupFormViewModel model)
        {
            if (!model.CustomerGroupId.HasValue)
                return (false, "CustomerGroupId không hợp lệ");

            var repo = _uow.Repository<CustomerGroup>();
            var group = await repo.GetByIdAsync(model.CustomerGroupId.Value);
            if (group == null)
                return (false, "Không tìm thấy nhóm khách hàng");

            var code = model.GroupCode.Trim().ToUpper();
            var codeConflict = await repo.AnyAsync(g =>
                g.GroupCode.ToUpper() == code &&
                g.CustomerGroupId != model.CustomerGroupId.Value);
            if (codeConflict)
                return (false, "Mã nhóm này đã được sử dụng bởi nhóm khác");

            group.GroupCode = code;
            group.GroupName = model.GroupName.Trim();
            group.Description = model.Description?.Trim();
            group.PriorityLevel = model.PriorityLevel;
            group.IsActive = model.IsActive;
            group.UpdatedAt = DateTime.Now;

            repo.Update(group);
            await _uow.SaveChangesAsync();

            _logger.LogInformation("CustomerGroup updated: CustomerGroupId={Id}", model.CustomerGroupId);
            return (true, null);
        }

        // ── Delete ───────────────────────────────────────
        public async Task<(bool Success, string? ErrorMessage)> DeleteAsync(int customerGroupId)
        {
            var repo = _uow.Repository<CustomerGroup>();
            var group = await repo.GetByIdAsync(customerGroupId);
            if (group == null)
                return (false, "Không tìm thấy nhóm khách hàng");

            // Guard: refuse to delete a group that still has customers assigned
            var inUse = await _uow.Repository<Customer>()
                .AnyAsync(c => c.CustomerGroupId == customerGroupId);
            if (inUse)
                return (false, "Không thể xóa: nhóm đang được gán cho khách hàng. " +
                               "Vui lòng chuyển các khách hàng sang nhóm khác hoặc vô hiệu hóa nhóm này.");

            repo.Remove(group);
            await _uow.SaveChangesAsync();

            _logger.LogInformation("CustomerGroup deleted: CustomerGroupId={Id} Code={Code}",
                customerGroupId, group.GroupCode);
            return (true, null);
        }
    }
}
