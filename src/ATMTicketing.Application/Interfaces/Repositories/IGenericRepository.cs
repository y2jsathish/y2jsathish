using System.Linq.Expressions;

namespace ATMTicketing.Application.Interfaces.Repositories;

public interface IGenericRepository<T> where T : class
{
    IQueryable<T> Query(bool asNoTracking = true);
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);
}
