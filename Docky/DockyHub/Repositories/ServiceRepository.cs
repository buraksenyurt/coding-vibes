using DockyHub.Data;
using DockyHub.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DockyHub.Repositories;

public interface IServiceRepository
{
    Task<IEnumerable<ServiceEntity>> GetAllAsync();
    Task<ServiceEntity?> GetByNameAsync(string name);
    Task<ServiceEntity?> GetByIdAsync(int id);
    Task<ServiceEntity> CreateAsync(ServiceEntity service);
    Task<ServiceEntity> UpdateAsync(ServiceEntity service);
    Task<bool> DeleteAsync(int id);
    Task<bool> ExistsAsync(string name);
}

public class ServiceRepository : IServiceRepository
{
    private readonly DockyDbContext _context;

    public ServiceRepository(DockyDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<ServiceEntity>> GetAllAsync()
    {
        return await _context.Services
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<ServiceEntity?> GetByNameAsync(string name)
    {
        return await _context.Services
            .FirstOrDefaultAsync(s => s.Name.ToLower() == name.ToLower() && s.IsActive);
    }

    public async Task<ServiceEntity?> GetByIdAsync(int id)
    {
        return await _context.Services
            .FirstOrDefaultAsync(s => s.Id == id && s.IsActive);
    }

    public async Task<ServiceEntity> CreateAsync(ServiceEntity service)
    {
        service.CreatedAt = DateTime.UtcNow;
        service.UpdatedAt = DateTime.UtcNow;
        
        _context.Services.Add(service);
        await _context.SaveChangesAsync();
        
        return service;
    }

    public async Task<ServiceEntity> UpdateAsync(ServiceEntity service)
    {
        service.UpdatedAt = DateTime.UtcNow;
        
        _context.Services.Update(service);
        await _context.SaveChangesAsync();
        
        return service;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var service = await GetByIdAsync(id);
        if (service == null)
            return false;

        service.IsActive = false;
        service.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ExistsAsync(string name)
    {
        return await _context.Services
            .AnyAsync(s => s.Name.ToLower() == name.ToLower() && s.IsActive);
    }
}
