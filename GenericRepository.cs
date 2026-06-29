// Q8. GENERIC REPOSITORY
// repository.FindAsync(filter, include, sort, page, projection)
 
// What FindAsync accepts
public class FindOptions<T>
{
    public Expression<Func<T, bool>>?    Filter     { get; set; } // WHERE clause
    public List<string>?                 Includes   { get; set; } // JOIN / .Include()
    public Func<IQueryable<T>,
               IOrderedQueryable<T>>?   Sort       { get; set; } // ORDER BY
    public int                           Page       { get; set; } = 1;
    public int                           PageSize   { get; set; } = 10;
    public Expression<Func<T, object>>?  Projection { get; set; } // SELECT only some columns
}
 
// The generic interface — works for ANY entity (User, Product, Payment…)
public interface IRepository<T> where T : class
{
    Task<List<T>>  FindAsync(FindOptions<T> options);
    Task<T?>       GetByIdAsync(int id);
    Task           AddAsync(T entity);
    Task           UpdateAsync(T entity);
    Task           DeleteAsync(int id);
}
 
// The actual implementation
public class GenericRepository<T> : IRepository<T> where T : class
{
    private readonly AppDbContext _db;
    private readonly DbSet<T>    _set;
 
    public GenericRepository(AppDbContext db)
    {
        _db  = db;
        _set = db.Set<T>(); // e.g. db.Users, db.Products — all via T
    }
 
    public async Task<List<T>> FindAsync(FindOptions<T> opt)
    {
        IQueryable<T> query = _set;
 
        //DYNAMIC FILTER
        if (opt.Filter != null)
            query = query.Where(opt.Filter);
 
        //DYNAMIC INCLUDES
        if (opt.Includes != null)
            foreach (var inc in opt.Includes)
                query = query.Include(inc); // adds SQL JOIN
 
        //DYNAMIC SORT
        if (opt.Sort != null)
            query = opt.Sort(query);
 
        //PAGINATION
        query = query
            .Skip((opt.Page - 1) * opt.PageSize) // skip first N rows
            .Take(opt.PageSize);                  // take only PageSize rows
 
        // 5️⃣ PROJECTION only return Id and Name, not the whole object
        return await query.ToListAsync();
    }
 
    public async Task<T?> GetByIdAsync(int id)    => await _set.FindAsync(id);
    public async Task     AddAsync(T entity)       { await _set.AddAsync(entity); await _db.SaveChangesAsync(); }
    public async Task     UpdateAsync(T entity)    { _set.Update(entity);         await _db.SaveChangesAsync(); }
    public async Task     DeleteAsync(int id)
    {
        var entity = await _set.FindAsync(id);
        if (entity != null) { _set.Remove(entity); await _db.SaveChangesAsync(); }
    }
}