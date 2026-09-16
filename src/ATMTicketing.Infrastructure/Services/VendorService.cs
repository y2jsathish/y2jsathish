using ATMTicketing.Application.Common;
using ATMTicketing.Application.DTOs;
using ATMTicketing.Application.Interfaces.Repositories;
using ATMTicketing.Application.Interfaces.Services;
using ATMTicketing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ATMTicketing.Infrastructure.Services;

public class VendorService : IVendorService
{
    private readonly IUnitOfWork _uow;

    public VendorService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<DataTableResponse<VendorListItemDto>> GetPagedAsync(DataTableRequest request, CancellationToken ct = default)
    {
        var query = _uow.Vendors.Query().Include(v => v.ServiceRegion).AsQueryable();
        var totalCount = await query.CountAsync(ct);

        if (!string.IsNullOrWhiteSpace(request.SearchValue))
        {
            var term = request.SearchValue.Trim();
            query = query.Where(v => v.VendorName.Contains(term) || v.VendorCode.Contains(term) || v.ContactPerson.Contains(term));
        }

        var filteredCount = await query.CountAsync(ct);
        query = request.SortDirection == "asc" ? query.OrderBy(v => v.VendorName) : query.OrderByDescending(v => v.VendorName);

        var vendors = await query.Skip(request.Start).Take(request.Length <= 0 ? 25 : request.Length).ToListAsync(ct);
        var vendorIds = vendors.Select(v => v.Id).ToList();

        var atmCounts = await _uow.Atms.Query()
            .Where(a => vendorIds.Contains(a.VendorId))
            .GroupBy(a => a.VendorId)
            .Select(g => new { VendorId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.VendorId, g => g.Count, ct);

        var items = vendors.Select(v => new VendorListItemDto
        {
            Id = v.Id,
            VendorCode = v.VendorCode,
            VendorName = v.VendorName,
            ContactPerson = v.ContactPerson,
            ContactNumber = v.ContactNumber,
            Email = v.Email,
            ServiceRegionName = v.ServiceRegion.RegionName,
            AtmCount = atmCounts.GetValueOrDefault(v.Id, 0),
            IsActive = v.IsActive
        }).ToList();

        return new DataTableResponse<VendorListItemDto>
        {
            Draw = request.Draw,
            RecordsTotal = totalCount,
            RecordsFiltered = filteredCount,
            Data = items
        };
    }

    public async Task<VendorEditDto?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var vendor = await _uow.Vendors.GetByIdAsync(id, ct);
        if (vendor is null)
        {
            return null;
        }

        return new VendorEditDto
        {
            Id = vendor.Id,
            VendorCode = vendor.VendorCode,
            VendorName = vendor.VendorName,
            ContactPerson = vendor.ContactPerson,
            ContactNumber = vendor.ContactNumber,
            Email = vendor.Email,
            ServiceRegionId = vendor.ServiceRegionId,
            IsActive = vendor.IsActive
        };
    }

    public async Task<ServiceResult> CreateAsync(VendorEditDto dto, CancellationToken ct = default)
    {
        var codeExists = await _uow.Vendors.Query().AnyAsync(v => v.VendorCode == dto.VendorCode, ct);
        if (codeExists)
        {
            return ServiceResult.Failure("A vendor with this code already exists.");
        }

        var vendor = MapToEntity(dto, new Vendor());
        await _uow.Vendors.AddAsync(vendor, ct);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success("Vendor created successfully.");
    }

    public async Task<ServiceResult> UpdateAsync(VendorEditDto dto, CancellationToken ct = default)
    {
        var vendor = await _uow.Vendors.Query(asNoTracking: false).FirstOrDefaultAsync(v => v.Id == dto.Id, ct);
        if (vendor is null)
        {
            return ServiceResult.Failure("Vendor not found.");
        }

        MapToEntity(dto, vendor);
        vendor.UpdatedDate = DateTime.UtcNow;
        _uow.Vendors.Update(vendor);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success("Vendor updated successfully.");
    }

    public async Task<ServiceResult> DeleteAsync(int id, CancellationToken ct = default)
    {
        var vendor = await _uow.Vendors.GetByIdAsync(id, ct);
        if (vendor is null)
        {
            return ServiceResult.Failure("Vendor not found.");
        }

        var hasAtms = await _uow.Atms.Query().AnyAsync(a => a.VendorId == id, ct);
        if (hasAtms)
        {
            return ServiceResult.Failure("Cannot delete a vendor with assigned ATMs. Deactivate it instead.");
        }

        _uow.Vendors.Remove(vendor);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success("Vendor deleted.");
    }

    private static Vendor MapToEntity(VendorEditDto dto, Vendor vendor)
    {
        vendor.VendorCode = dto.VendorCode;
        vendor.VendorName = dto.VendorName;
        vendor.ContactPerson = dto.ContactPerson;
        vendor.ContactNumber = dto.ContactNumber;
        vendor.Email = dto.Email;
        vendor.ServiceRegionId = dto.ServiceRegionId;
        vendor.IsActive = dto.IsActive;
        return vendor;
    }
}
