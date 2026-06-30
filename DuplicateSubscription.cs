// SubscriptionRequest.cs
public class SubscriptionRequest
{
    public string Mobile { get; set; }
}


// User.cs (entity)
public class User
{
    public int Id { get; set; }
    public string Mobile { get; set; }
    public bool IsSubscribed { get; set; }
}


// AppDbContext.cs
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Mobile should be unique as a data-integrity rule on its own merits
        // (not the mechanism preventing duplicate subscriptions, see controller).
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Mobile)
            .IsUnique();
    }
}


// SubscriptionController.cs
public async Task<IActionResult> Subscribe(SubscriptionRequest request)
{
    if (request == null || string.IsNullOrWhiteSpace(request.Mobile))
        return BadRequest("Mobile number is required.");

    try
    {
        // Atomic conditional update : the DB checks IsSubscribed == false 
        // AND sets it to true in a single statement. No read-then-write gap,
        // so concurrent requests can't race each other.
        var rowsAffected = await _context.Users
            .Where(x => x.Mobile == request.Mobile && !x.IsSubscribed)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsSubscribed, true));

        if (rowsAffected == 1)
            return Ok();

        // 0 rows affected: user doesn't exist, or was already subscribed
        // (possibly by a concurrent request that won the race).
        var user = await _context.Users
            .FirstOrDefaultAsync(x => x.Mobile == request.Mobile);

        if (user == null)
            return BadRequest("User not found.");

        return Conflict("User is already subscribed.");
    }
    catch (DbUpdateException)
    {
        return StatusCode(500, "An unexpected error occurred while processing the subscription.");
    }
}