// Q13. PRODUCT SEARCH API — GET /api/products

// Query params (everything is optional) 
public class ProductSearchQuery
{
    public string?        Keyword    { get; set; }       // search in name/description
    public decimal?       MinPrice   { get; set; }
    public decimal?       MaxPrice   { get; set; }
    public List<int>?     Categories { get; set; }       // multiple categories
    public List<int>?     Brands     { get; set; }       // multiple brands
    public string?        SortBy     { get; set; }       // "price", "name", "newest"
    public bool           SortDesc   { get; set; }
    public int            Page       { get; set; } = 1;
    public int            PageSize   { get; set; } = 20;
}

//Response wrapper with total count for frontend pagination
public class PagedResult<T>
{
    public List<T> Items       { get; set; } = new();
    public int     TotalCount  { get; set; } // total matching records (for frontend pagination)
    public int     Page        { get; set; }
    public int     PageSize    { get; set; }
    public int     TotalPages  => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public class ProductDto
{
    public int     Id         { get; set; }
    public string  Name       { get; set; }
    public decimal Price      { get; set; }
    public string  Brand      { get; set; }
    public string  Category   { get; set; }
}

//EF Core Product entity 
public class Product
{
    public int     Id          { get; set; }
    public string  Name        { get; set; }
    public string  Description { get; set; }
    public decimal Price       { get; set; }
    public int     BrandId     { get; set; }
    public int     CategoryId  { get; set; }
    public string  Brand       { get; set; }
    public string  Category    { get; set; }
    public DateTime CreatedAt  { get; set; }
}

//Service 
public class ProductService
{
    private readonly AppDbContext _db;
    public ProductService(AppDbContext db) => _db = db;

    public async Task<PagedResult<ProductDto>> SearchAsync(ProductSearchQuery q)
    {
        // Start with a base query — nothing executed yet, just building SQL
        IQueryable<Product> query = _db.Products.AsNoTracking(); // AsNoTracking = faster reads, no change tracking needed

        //FILTERS (each one adds a WHERE clause only if provided)

        // Keyword: search name OR description
        if (!string.IsNullOrEmpty(q.Keyword))
            query = query.Where(p =>
                p.Name.Contains(q.Keyword) ||
                p.Description.Contains(q.Keyword));

        // Price range
        if (q.MinPrice.HasValue)
            query = query.Where(p => p.Price >= q.MinPrice.Value);
        if (q.MaxPrice.HasValue)
            query = query.Where(p => p.Price <= q.MaxPrice.Value);

        // Multiple categories: WHERE CategoryId IN (1, 2, 3)
        if (q.Categories?.Any() == true)
            query = query.Where(p => q.Categories.Contains(p.CategoryId));

        // Multiple brands: WHERE BrandId IN (5, 6)
        if (q.Brands?.Any() == true)
            query = query.Where(p => q.Brands.Contains(p.BrandId));

        //get total count BEFORE pagination
        // Both queries share the same filters but run as separate SQL calls
        // CountAsync is cheap — no data fetched, just COUNT(*)
        var totalCount = await query.CountAsync();

        //SORTING
        query = q.SortBy?.ToLower() switch
        {
            "price"  => q.SortDesc ? query.OrderByDescending(p => p.Price)     : query.OrderBy(p => p.Price),
            "name"   => q.SortDesc ? query.OrderByDescending(p => p.Name)      : query.OrderBy(p => p.Name),
            "newest" => q.SortDesc ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt),
            _        => query.OrderBy(p => p.Id) // default sort
        };

        //PAGINATION: SKIP rows before this page, TAKE only PageSize rows
        var items = await query
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            // PROJECTION: only SELECT the columns we need — smaller payload, faster query
            .Select(p => new ProductDto
            {
                Id       = p.Id,
                Name     = p.Name,
                Price    = p.Price,
                Brand    = p.Brand,
                Category = p.Category
            })
            .ToListAsync(); // SQL executes HERE

        return new PagedResult<ProductDto>
        {
            Items      = items,
            TotalCount = totalCount,
            Page       = q.Page,
            PageSize   = q.PageSize
        };
    }
}

//Controller
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly ProductService _service;
    public ProductsController(ProductService s) => _service = s;

    // GET /api/products?keyword=shoe&minPrice=50&categories=1,2&page=1
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] ProductSearchQuery query)
    {
        var result = await _service.SearchAsync(query);
        return Ok(result); // 200 with { items, totalCount, page, totalPages }
    }
}

//PERFORMANCE TIPS (index these columns in DB)
// CREATE INDEX IX_Products_Name        ON Products(Name);
// CREATE INDEX IX_Products_Price       ON Products(Price);
// CREATE INDEX IX_Products_CategoryId  ON Products(CategoryId);
// CREATE INDEX IX_Products_BrandId     ON Products(BrandId);
// These make WHERE/ORDER BY on these columns fast even with millions of rows