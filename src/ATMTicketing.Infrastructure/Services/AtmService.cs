using ATMTicketing.Application.Common;
using ATMTicketing.Application.DTOs;
using ATMTicketing.Application.Interfaces.Repositories;
using ATMTicketing.Application.Interfaces.Services;
using ATMTicketing.Domain.Entities;
using ATMTicketing.Domain.Enums;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace ATMTicketing.Infrastructure.Services;

public class AtmService : IAtmService
{
    private static readonly int[] OpenStatusIds =
    {
        (int)TicketStatusCode.New, (int)TicketStatusCode.Assigned, (int)TicketStatusCode.InProgress,
        (int)TicketStatusCode.PendingParts, (int)TicketStatusCode.PendingCustomer, (int)TicketStatusCode.Escalated
    };

    private readonly IUnitOfWork _uow;

    public AtmService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<DataTableResponse<AtmListItemDto>> GetPagedAsync(DataTableRequest request, CancellationToken ct = default)
    {
        var query = _uow.Atms.Query().Include(a => a.Region).Include(a => a.Vendor).AsQueryable();
        var totalCount = await query.CountAsync(ct);

        if (!string.IsNullOrWhiteSpace(request.SearchValue))
        {
            var term = request.SearchValue.Trim();
            query = query.Where(a =>
                a.AtmCode.Contains(term) || a.AtmName.Contains(term) || a.City.Contains(term) || a.BankName.Contains(term));
        }

        var filteredCount = await query.CountAsync(ct);

        query = (request.SortColumn, request.SortDirection) switch
        {
            ("atmName", "asc") => query.OrderBy(a => a.AtmName),
            ("atmName", _) => query.OrderByDescending(a => a.AtmName),
            (_, "asc") => query.OrderBy(a => a.AtmCode),
            _ => query.OrderByDescending(a => a.CreatedDate)
        };

        var atms = await query.Skip(request.Start).Take(request.Length <= 0 ? 25 : request.Length).ToListAsync(ct);
        var atmIds = atms.Select(a => a.Id).ToList();

        var openCounts = await _uow.Tickets.Query()
            .Where(t => atmIds.Contains(t.AtmId) && OpenStatusIds.Contains(t.StatusId))
            .GroupBy(t => t.AtmId)
            .Select(g => new { AtmId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.AtmId, g => g.Count, ct);

        var items = atms.Select(a => new AtmListItemDto
        {
            Id = a.Id,
            AtmCode = a.AtmCode,
            AtmName = a.AtmName,
            BankName = a.BankName,
            RegionName = a.Region.RegionName,
            Zone = a.Zone,
            State = a.State,
            City = a.City,
            VendorName = a.Vendor.VendorName,
            AtmType = a.AtmType.ToString(),
            Status = a.Status.ToString(),
            OpenTicketCount = openCounts.GetValueOrDefault(a.Id, 0)
        }).ToList();

        return new DataTableResponse<AtmListItemDto>
        {
            Draw = request.Draw,
            RecordsTotal = totalCount,
            RecordsFiltered = filteredCount,
            Data = items
        };
    }

    public async Task<AtmEditDto?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var atm = await _uow.Atms.GetByIdAsync(id, ct);
        if (atm is null)
        {
            return null;
        }

        return new AtmEditDto
        {
            Id = atm.Id,
            AtmCode = atm.AtmCode,
            AtmName = atm.AtmName,
            BankName = atm.BankName,
            RegionId = atm.RegionId,
            Zone = atm.Zone,
            State = atm.State,
            City = atm.City,
            Address = atm.Address,
            VendorId = atm.VendorId,
            AtmType = atm.AtmType,
            Status = atm.Status,
            Latitude = atm.Latitude,
            Longitude = atm.Longitude
        };
    }

    public async Task<ServiceResult> CreateAsync(AtmEditDto dto, CancellationToken ct = default)
    {
        var codeExists = await _uow.Atms.Query().AnyAsync(a => a.AtmCode == dto.AtmCode, ct);
        if (codeExists)
        {
            return ServiceResult.Failure("An ATM with this code already exists.");
        }

        var atm = MapToEntity(dto, new Atm());
        await _uow.Atms.AddAsync(atm, ct);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success("ATM created successfully.");
    }

    public async Task<ServiceResult> UpdateAsync(AtmEditDto dto, CancellationToken ct = default)
    {
        var atm = await _uow.Atms.Query(asNoTracking: false).FirstOrDefaultAsync(a => a.Id == dto.Id, ct);
        if (atm is null)
        {
            return ServiceResult.Failure("ATM not found.");
        }

        var codeExists = await _uow.Atms.Query().AnyAsync(a => a.AtmCode == dto.AtmCode && a.Id != dto.Id, ct);
        if (codeExists)
        {
            return ServiceResult.Failure("Another ATM already uses this code.");
        }

        MapToEntity(dto, atm);
        atm.UpdatedDate = DateTime.UtcNow;
        _uow.Atms.Update(atm);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success("ATM updated successfully.");
    }

    public async Task<ServiceResult> DeleteAsync(int id, CancellationToken ct = default)
    {
        var atm = await _uow.Atms.GetByIdAsync(id, ct);
        if (atm is null)
        {
            return ServiceResult.Failure("ATM not found.");
        }

        var hasTickets = await _uow.Tickets.Query().AnyAsync(t => t.AtmId == id, ct);
        if (hasTickets)
        {
            return ServiceResult.Failure("Cannot delete an ATM that has ticket history. Deactivate it instead.");
        }

        _uow.Atms.Remove(atm);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success("ATM deleted.");
    }

    public async Task<IReadOnlyList<AtmListItemDto>> SearchAsync(string term, CancellationToken ct = default)
    {
        return await _uow.Atms.Query()
            .Include(a => a.Region).Include(a => a.Vendor)
            .Where(a => a.Status == AtmOperationalStatus.Active &&
                        (a.AtmCode.Contains(term) || a.AtmName.Contains(term) || a.City.Contains(term)))
            .Take(20)
            .Select(a => new AtmListItemDto
            {
                Id = a.Id,
                AtmCode = a.AtmCode,
                AtmName = a.AtmName,
                BankName = a.BankName,
                RegionName = a.Region.RegionName,
                City = a.City,
                VendorName = a.Vendor.VendorName,
                Status = a.Status.ToString()
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AtmImportRowResult>> ImportFromExcelAsync(Stream fileStream, CancellationToken ct = default)
    {
        var results = new List<AtmImportRowResult>();
        using var workbook = new XLWorkbook(fileStream);
        var worksheet = workbook.Worksheet(1);
        var regions = (await _uow.Regions.GetAllAsync(ct)).ToDictionary(r => r.RegionName, StringComparer.OrdinalIgnoreCase);
        var vendors = (await _uow.Vendors.GetAllAsync(ct)).ToDictionary(v => v.VendorCode, StringComparer.OrdinalIgnoreCase);

        // Expected header row (1): AtmCode | AtmName | BankName | Region | Zone | State | City | Address | VendorCode | AtmType | Status
        var rows = worksheet.RowsUsed().Skip(1);
        foreach (var row in rows)
        {
            var rowNumber = row.RowNumber();
            var atmCode = row.Cell(1).GetString().Trim();
            try
            {
                if (string.IsNullOrWhiteSpace(atmCode))
                {
                    continue;
                }

                if (!regions.TryGetValue(row.Cell(4).GetString().Trim(), out var region))
                {
                    results.Add(new AtmImportRowResult { RowNumber = rowNumber, AtmCode = atmCode, Success = false, Error = "Unknown region." });
                    continue;
                }
                if (!vendors.TryGetValue(row.Cell(9).GetString().Trim(), out var vendor))
                {
                    results.Add(new AtmImportRowResult { RowNumber = rowNumber, AtmCode = atmCode, Success = false, Error = "Unknown vendor code." });
                    continue;
                }
                if (!Enum.TryParse<AtmType>(row.Cell(10).GetString().Trim(), true, out var atmType))
                {
                    atmType = AtmType.Onsite;
                }
                if (!Enum.TryParse<AtmOperationalStatus>(row.Cell(11).GetString().Trim(), true, out var status))
                {
                    status = AtmOperationalStatus.Active;
                }

                var existing = await _uow.Atms.Query(asNoTracking: false).FirstOrDefaultAsync(a => a.AtmCode == atmCode, ct);
                var atm = existing ?? new Atm { AtmCode = atmCode };
                atm.AtmName = row.Cell(2).GetString().Trim();
                atm.BankName = row.Cell(3).GetString().Trim();
                atm.RegionId = region.Id;
                atm.Zone = row.Cell(5).GetString().Trim();
                atm.State = row.Cell(6).GetString().Trim();
                atm.City = row.Cell(7).GetString().Trim();
                atm.Address = row.Cell(8).GetString().Trim();
                atm.VendorId = vendor.Id;
                atm.AtmType = atmType;
                atm.Status = status;

                if (existing is null)
                {
                    await _uow.Atms.AddAsync(atm, ct);
                }
                else
                {
                    _uow.Atms.Update(atm);
                }

                results.Add(new AtmImportRowResult { RowNumber = rowNumber, AtmCode = atmCode, Success = true });
            }
            catch (Exception ex)
            {
                results.Add(new AtmImportRowResult { RowNumber = rowNumber, AtmCode = atmCode, Success = false, Error = ex.Message });
            }
        }

        await _uow.SaveChangesAsync(ct);
        return results;
    }

    private static Atm MapToEntity(AtmEditDto dto, Atm atm)
    {
        atm.AtmCode = dto.AtmCode;
        atm.AtmName = dto.AtmName;
        atm.BankName = dto.BankName;
        atm.RegionId = dto.RegionId;
        atm.Zone = dto.Zone;
        atm.State = dto.State;
        atm.City = dto.City;
        atm.Address = dto.Address;
        atm.VendorId = dto.VendorId;
        atm.AtmType = dto.AtmType;
        atm.Status = dto.Status;
        atm.Latitude = dto.Latitude;
        atm.Longitude = dto.Longitude;
        return atm;
    }
}
