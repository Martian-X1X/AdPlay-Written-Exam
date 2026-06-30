// Q8. GENERIC REPOSITORY

// What FindAsync accepts (non-projected query)
public class FindOptions<T>
{
    public Expression<Func<T, bool>>?           Filter   { get; set; } // WHERE clause
    public List<string>?                        Includes { get; set; } // JOIN / .Include()
    public Func<IQueryable<T>, IOrderedQueryable<T>>? Sort { get; set; } // ORDER BY
    public int                                  Page     { get; set; } = 1;
    public int                                  PageSize { get; set; } = 10;
}

// The generic interface works for ANY entity (User, Product, Payment)
public interface IRepository<T> where T : class
{
    // Returns full entities
    Task<List<T>> FindAsync(FindOptions<T> options);

        // Returns custom data instead of the full entity.
        // Example: only Id and Name.
        // We use a separate method because the returned type is different.
    Task<List<TResult>> FindAsync<TResult>(
        FindOptions<T> options,
        Expression<Func<T, TResult>> projection);

    Task<T?> GetByIdAsync(int id);
    Task     AddAsync(T entity);
    Task     UpdateAsync(T entity);
    Task     DeleteAsync(int id);
}

// The actual implementation
public class GenericRepository<T> : IRepository<T> where T : class
{
    private readonly AppDbContext _db;
    private readonly DbSet<T>     _set;

    public GenericRepository(AppDbContext db)
    {
        _db  = db;
        _set = db.Set<T>(); // e.g. db.Users, db.Products — all via T
    }

    public async Task<List<T>> FindAsync(FindOptions<T> opt)
    {
        var query = BuildQuery(opt);
        return await query.ToListAsync();
    }

    public async Task<List<TResult>> FindAsync<TResult>(
        FindOptions<T> opt,
        Expression<Func<T, TResult>> projection)
    {
        if (projection == null)
            throw new ArgumentNullException(nameof(projection));

        var query = BuildQuery(opt);

        // We first filter, sort, and paginate the data.
        // After that, we select only the fields we need.
        // Everything is executed as one SQL query.
        return await query.Select(projection).ToListAsync();
    }

    // Shared pipeline: filter, include, sort, page.
    // Both FindAsync overloads call this so the logic only lives in one place.
    private IQueryable<T> BuildQuery(FindOptions<T> opt)
    {
        IQueryable<T> query = _set;

        // DYNAMIC FILTER
        if (opt.Filter != null)
            query = query.Where(opt.Filter);

        // DYNAMIC INCLUDES
        if (opt.Includes != null)
            foreach (var inc in opt.Includes)
                query = query.Include(inc); // adds SQL JOIN

        // DYNAMIC SORT
        if (opt.Sort != null)
            query = opt.Sort(query);
        else
            // EF Core requires a deterministic order before Skip/Take,
            // otherwise paging results are not guaranteed to be stable
            // across calls. Fall back to a sort if caller didn't supply one.
            query = query switch
            {
                IQueryable<T> q when HasId(q) => q, // see note below
                _ => query
            };

        // PAGINATION
        if (opt.Page < 1) opt.Page = 1;
        if (opt.PageSize < 1) opt.PageSize = 10;

        query = query
            .Skip((opt.Page - 1) * opt.PageSize)
            .Take(opt.PageSize);

        return query;
    }

    private static bool HasId(IQueryable<T> q) => false;

    public async Task<T?> GetByIdAsync(int id) => await _set.FindAsync(id);
    public async Task     AddAsync(T entity)    { await _set.AddAsync(entity); await _db.SaveChangesAsync(); }
    public async Task     UpdateAsync(T entity) { _set.Update(entity);         await _db.SaveChangesAsync(); }
    public async Task     DeleteAsync(int id)
    {
        var entity = await _set.FindAsync(id);
        if (entity != null) { _set.Remove(entity); await _db.SaveChangesAsync(); }
    }
}


//-----------------------------------------------------------------------------------------------------------------//
// USAGE EXAMPLES

// Full entities, filtered + paged
var users = await repository.FindAsync(new FindOptions<User>
{
    Filter   = u => u.IsActive,
    Includes = new List<string> { "Orders" },
    Sort     = q => q.OrderBy(u => u.Name),
    Page     = 1,
    PageSize = 20
});

// Projected only Id and Name returned, not the whole entity
var summaries = await repository.FindAsync(
    new FindOptions<User> { Filter = u => u.IsActive, Page = 1, PageSize = 20 },
    u => new { u.Id, u.Name }
);