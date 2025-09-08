using DockyHub.Data;
using DockyHub.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DockyHub.Repositories;

public interface IModelRepository
{
    Task<IEnumerable<ModelEntity>> GetAllAsync();
    Task<ModelEntity?> GetByNameAsync(string name);
    Task<ModelEntity?> GetByIdAsync(int id);
    Task<ModelEntity> CreateAsync(ModelEntity model);
    Task<ModelEntity> UpdateAsync(ModelEntity model);
    Task<bool> DeleteAsync(int id);
    Task<bool> ExistsAsync(string name);
}

public class ModelRepository : IModelRepository
{
    private readonly DockyDbContext _context;

    public ModelRepository(DockyDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<ModelEntity>> GetAllAsync()
    {
        return await _context.Models
            .Where(m => m.IsActive)
            .OrderBy(m => m.Name)
            .ToListAsync();
    }

    public async Task<ModelEntity?> GetByNameAsync(string name)
    {
        return await _context.Models
            .FirstOrDefaultAsync(m => m.Name.ToLower() == name.ToLower() && m.IsActive);
    }

    public async Task<ModelEntity?> GetByIdAsync(int id)
    {
        return await _context.Models
            .FirstOrDefaultAsync(m => m.Id == id && m.IsActive);
    }

    public async Task<ModelEntity> CreateAsync(ModelEntity model)
    {
        model.CreatedAt = DateTime.UtcNow;
        model.UpdatedAt = DateTime.UtcNow;
        
        _context.Models.Add(model);
        await _context.SaveChangesAsync();
        
        return model;
    }

    public async Task<ModelEntity> UpdateAsync(ModelEntity model)
    {
        model.UpdatedAt = DateTime.UtcNow;
        
        _context.Models.Update(model);
        await _context.SaveChangesAsync();
        
        return model;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var model = await GetByIdAsync(id);
        if (model == null)
            return false;

        model.IsActive = false;
        model.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ExistsAsync(string name)
    {
        return await _context.Models
            .AnyAsync(m => m.Name.ToLower() == name.ToLower() && m.IsActive);
    }
}
