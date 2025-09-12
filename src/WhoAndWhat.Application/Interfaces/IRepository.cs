using System.Linq.Expressions;

namespace WhoAndWhat.Application.Interfaces;

public interface IRepository<T> where T : class
{
    public Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    public Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default);
    public Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    public Task<T?> GetByConditionAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    public Task<IEnumerable<T>> GetAllByConditionAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    public Task<int> CountByConditionAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    public Task AddAsync(T entity, CancellationToken cancellationToken = default);
    public Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
    public void Update(T entity);
    public Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
    public void Remove(T entity);
    public Task DeleteAsync(T entity, CancellationToken cancellationToken = default);
    public void RemoveRange(IEnumerable<T> entities);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
