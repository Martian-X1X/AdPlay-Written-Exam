// Q13. PRODUCT SEARCH API

// Query params (everything is optional)
public class ProductSearchQuery
{
    public string?    Keyword    { get; set; }       // search in name/description
    public decimal?   MinPrice   { get; set; }
    public decimal?   MaxPrice   { get; set; }
    public List<int>? Categories { get; set; }       // multiple categories
    public List<int>? Brands     { get; set; }       // multiple brands
    public string?    SortBy     { get; set; }        // "price", "name", "newest"
    public bool       SortDesc   { get; set; }
    public int        Page       { get; set; } = 1;
    public int        PageSize   { get; set; } = 20;
}

// Response wrapper with total count for frontend pagination
public class PagedResult<T>
{
    public List<T> Items      { get; set; } = new();
    public int     TotalCount { get; set; }
    public int     Page       { get; set; }
    public int     PageSize   { get; set; }
    public int     TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public class ProductDto
{
    public int     Id       { get; set; }
    public string  Name     { get; set; }
    public decimal Price    { get; set; }
    public string  Brand    { get; set; }
    public string  Category { get; set; }
}

// EF Core Product entity
public class Product
{
    public int      Id          { get; set; }
    public string   Name        { get; set; }
    public string   Description { get; set; }
    public decimal  Price       { get; set; }
    public int      BrandId     { get; set; }
    public int      CategoryId  { get; set; }
    public string   Brand       { get; set; }
    public string   Category    { get; set; }
    public DateTime CreatedAt   { get; set; }
}

// Service
public class ProductService
{
    private readonly AppDbContext _db;
    public ProductService(AppDbContext db) => _db = db;

    public async Task<PagedResult<ProductDto>> SearchAsync(ProductSearchQuery q)
    {
        IQueryable<Product> query = _db.Products.AsNoTracking();

        if (!string.IsNullOrEmpty(q.Keyword))
            query = query.Where(p =>
                p.Name.Contains(q.Keyword) ||
                p.Description.Contains(q.Keyword));

        if (q.MinPrice.HasValue)
            query = query.Where(p => p.Price >= q.MinPrice.Value);
        if (q.MaxPrice.HasValue)
            query = query.Where(p => p.Price <= q.MaxPrice.Value);

        if (q.Categories?.Any() == true)
            query = query.Where(p => q.Categories.Contains(p.CategoryId));

        if (q.Brands?.Any() == true)
            query = query.Where(p => q.Brands.Contains(p.BrandId));

        var totalCount = await query.CountAsync();

        query = q.SortBy?.ToLower() switch
        {
            "price"  => q.SortDesc ? query.OrderByDescending(p => p.Price)     : query.OrderBy(p => p.Price),
            "name"   => q.SortDesc ? query.OrderByDescending(p => p.Name)      : query.OrderBy(p => p.Name),
            "newest" => q.SortDesc ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt),
            _        => query.OrderBy(p => p.Id)
        };

        // Defensive bounds avoid negative Skip or absurd PageSize from bad input
        if (q.Page < 1) q.Page = 1;
        if (q.PageSize < 1 || q.PageSize > 100) q.PageSize = 20;

        var items = await query
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .Select(p => new ProductDto
            {
                Id       = p.Id,
                Name     = p.Name,
                Price    = p.Price,
                Brand    = p.Brand,
                Category = p.Category
            })
            .ToListAsync();

        return new PagedResult<ProductDto>
        {
            Items      = items,
            TotalCount = totalCount,
            Page       = q.Page,
            PageSize   = q.PageSize
        };
    }
}

// Controller
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly ProductService _service;
    public ProductsController(ProductService s) => _service = s;

    // Supports BOTH formats:
    //   ?categories=1&categories=2     (standard ASP.NET Core repeated-param binding)
    //   ?categories=1,2                (comma-separated, needs manual parsing below)
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] ProductSearchQuery query)
    {
        query.Categories = ParseIntList(Request.Query["categories"]) ?? query.Categories;
        query.Brands     = ParseIntList(Request.Query["brands"])     ?? query.Brands;

        var result = await _service.SearchAsync(query);
        return Ok(result); // 200 with { items, totalCount, page, totalPages }
    }

    // Handles both ?categories=1,2 and ?categories=1&categories=2
    // StringValues can contain either multiple separate values or single values with embedded commas,
    //so we split every value on comma and flatten the result either way.
    private static List<int>? ParseIntList(Microsoft.Extensions.Primitives.StringValues raw)
    {
        if (raw.Count == 0) return null;

        var ids = raw
            .SelectMany(v => v.Split(',', StringSplitOptions.RemoveEmptyEntries))
            .Select(v => int.TryParse(v.Trim(), out var n) ? (int?)n : null)
            .Where(n => n.HasValue)
            .Select(n => n!.Value)
            .ToList();

        return ids.Count > 0 ? ids : null;
    }
}