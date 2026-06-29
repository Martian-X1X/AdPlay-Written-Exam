//Step 1 Model and DBContext
public class User
{
    public int Id { get; set; }
    public string Mobile { get; set; }
    public bool IsSubscribed { get; set; }

    [Timestamp] // EF Core optimistic concurrency token (maps to rowversion/xmin)
    public byte[] RowVersion { get; set; }
}


//AppDbContext.cs

            //The database itself will reject
            // the second INSERT/UPDATE that violates this unique index
public DbSet<User> Users { get; set; }


modelBuilder.Entity<User>()
    .HasIndex(u => u.Mobile)
    .IsUnique();


//Step 2
//ControlFlowBuilder/Reposiotry
public async Task<IActionResult> Subscribe(SubscriptionRequest request)
{
    const int maxRetries = 3;
    int attempt = 0;

    // Retry loop: handles requests that lose a concurrency race.
    // On retry, IsSubscribed will read as true and we return Conflict() cleanly.
    while (true)
    {
        attempt++;

        // Transaction + Serializable isolation makes read then check then write
        using var transaction = await _context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable);

        try
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.Mobile == request.Mobile);

            if (user == null)
            {
                await transaction.RollbackAsync();
                return BadRequest("User not found.");
            }

            if (user.IsSubscribed)
            {
                // Already subscribed — handles the normal sequential duplicate case.
                await transaction.RollbackAsync();
                return Conflict("User is already subscribed.");
            }

            user.IsSubscribed = true;

            // RowVersion mismatch here -> DbUpdateConcurrencyException
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok();
        }
        catch (DbUpdateConcurrencyException)
        {
            // Lost the race: another concurrent request updated this row first.
            await transaction.RollbackAsync();

            if (attempt >= maxRetries)
                return Conflict("Subscription is already being processed. Please try again.");

            await Task.Delay(50 * attempt); // small backoff before retrying
            continue;
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Belt-and-suspenders: DB unique index caught a race the app logic missed.
            await transaction.RollbackAsync();
            return Conflict("User is already subscribed.");
        }
        catch (Exception)
        {
            // Catch-all for anything unexpected (e.g. connection drop).
            await transaction.RollbackAsync();
            return StatusCode(500, "An unexpected error occurred while processing the subscription.");
        }
    }
}

// Helper: confirms a DbUpdateException was caused by the unique constraint
private static bool IsUniqueConstraintViolation(DbUpdateException ex)
{
    return ex.InnerException?.Message
        .Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true;
}