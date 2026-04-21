using System.Linq.Expressions;

namespace EV_ERP.Repositories.Interfaces
{
    /// <summary>
    /// Generic Repository — CRUD cơ bản cho mọi entity
    /// </summary>
    public interface IRepository<T> where T : class
    {
        Task<T?> GetByIdAsync(int id);
        Task<T?> GetByIdAsync(long id);
        Task<IEnumerable<T>> GetAllAsync();
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
        IQueryable<T> Query();
        Task<T> AddAsync(T entity);
        Task AddRangeAsync(IEnumerable<T> entities);
        void Update(T entity);
        void Remove(T entity);
        void RemoveRange(IEnumerable<T> entities);
        Task<bool> AnyAsync(Expression<Func<T, bool>> predicate);
        Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null);
    }

    /// <summary>
    /// Unit of Work — đảm bảo nhiều repository dùng chung 1 transaction
    /// </summary>
    public interface IUnitOfWork : IDisposable
    {
        IRepository<T> Repository<T>() where T : class;
        Task<int> SaveChangesAsync();
        Task BeginTransactionAsync();
        Task CommitAsync();
        Task RollbackAsync();

        /// <summary>Lấy giá trị tiếp theo từ SQL Server Sequence</summary>
        Task<long> NextSequenceValueAsync(string sequenceName);
    }
}
