// PaymentRequest.cs
public class PaymentRequest
{
    public string IdempotencyKey { get; set; }
    public decimal Amount        { get; set; }
    public string Currency       { get; set; }
    public string FromAccount    { get; set; }
    public string ToAccount      { get; set; }
}


// PaymentResult.cs
public class PaymentResult
{
    public string PaymentId { get; set; }
    public string Status    { get; set; } // "Success" | "Failed"
    public int    HttpStatusCode { get; set; } // so we can replay the exact same response
}


// Account.cs (entity)
public class Account
{
    public int     Id           { get; set; }
    public string  AccountNumber{ get; set; }
    public decimal Balance      { get; set; }

    [Timestamp] // optimistic concurrency token protects balance updates
    public byte[] RowVersion    { get; set; }
}

public class Payment
{
    public int      Id              { get; set; }
    public string    PaymentId        { get; set; }
    public string    IdempotencyKey   { get; set; }
    public decimal   Amount           { get; set; }
    public string    Currency         { get; set; }
    public string    FromAccount      { get; set; }
    public string    ToAccount        { get; set; }
    public string    Status           { get; set; }
    public DateTime  CreatedAt        { get; set; }
}


// IdempotencyStore.cs
public interface IIdempotencyStore
{
    Task<PaymentResult?> GetAsync(string key);
    Task SetAsync(string key, PaymentResult result, TimeSpan ttl);
}

public class InMemoryIdempotencyStore : IIdempotencyStore
{
    private class Entry { public PaymentResult Result; public DateTime Expiry; }

    private static readonly Dictionary<string, Entry> _cache = new();
    private static readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<PaymentResult?> GetAsync(string key)
    {
        await _lock.WaitAsync();
        try
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                if (entry.Expiry > DateTime.UtcNow) return entry.Result;
                _cache.Remove(key); // expired = treat as miss
            }
            return null;
        }
        finally { _lock.Release(); }
    }

    public async Task SetAsync(string key, PaymentResult result, TimeSpan ttl)
    {
        await _lock.WaitAsync();
        try { _cache[key] = new Entry { Result = result, Expiry = DateTime.UtcNow.Add(ttl) }; }
        finally { _lock.Release(); }
    }
}


// IDistributedLockService.cs / InMemoryLockService.cs
// TTL is enforced. In production: Redis SET NX PX <ttl>,
// which gives auto-expiry and cross-instance locking for free.
public interface IDistributedLockService
{
    Task<bool> AcquireAsync(string resource, TimeSpan ttl);
    Task ReleaseAsync(string resource);
}

public class InMemoryLockService : IDistributedLockService
{
    private static readonly Dictionary<string, DateTime> _locks = new();
    private static readonly object _obj = new();

    public Task<bool> AcquireAsync(string resource, TimeSpan ttl)
    {
        lock (_obj)
        {
            if (_locks.TryGetValue(resource, out var expiry) && expiry > DateTime.UtcNow)
                return Task.FromResult(false); // still held by someone else

            _locks[resource] = DateTime.UtcNow.Add(ttl); // acquire (or take over an expired lock)
            return Task.FromResult(true);
        }
    }

    public Task ReleaseAsync(string resource)
    {
        lock (_obj) { _locks.Remove(resource); }
        return Task.CompletedTask;
    }
}


// Custom exception for non-transient, expected business failures
// (insufficient funds, account not found) = these are not retried.
public class PaymentBusinessException : Exception
{
    public PaymentBusinessException(string message) : base(message) { }
}


// PaymentService.cs
public class PaymentService
{
    private readonly AppDbContext _db;

    public PaymentService(AppDbContext db) => _db = db;

    public async Task<string> ProcessAsync(PaymentRequest req)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var sender = await _db.Accounts
                .FirstOrDefaultAsync(a => a.AccountNumber == req.FromAccount);
            var receiver = await _db.Accounts
                .FirstOrDefaultAsync(a => a.AccountNumber == req.ToAccount);

            if (sender == null || receiver == null)
                throw new PaymentBusinessException("Sender or receiver account not found.");

            if (sender.Balance < req.Amount)
                throw new PaymentBusinessException("Insufficient funds.");

            sender.Balance   -= req.Amount;
            receiver.Balance += req.Amount;

            var paymentId = Guid.NewGuid().ToString();

            _db.Payments.Add(new Payment
            {
                PaymentId      = paymentId,
                IdempotencyKey = req.IdempotencyKey,
                Amount         = req.Amount,
                Currency       = req.Currency,
                FromAccount    = req.FromAccount,
                ToAccount      = req.ToAccount,
                Status         = "Success",
                CreatedAt      = DateTime.UtcNow
            });

            // RowVersion mismatch on sender/receiver -> DbUpdateConcurrencyException,
            // which is transient and safe to retry.
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return paymentId;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}


// PaymentController.cs
[ApiController]
[Route("api/[controller]")]
public class PaymentController : ControllerBase
{
    private readonly PaymentService          _paymentService;
    private readonly IDistributedLockService _lockService;
    private readonly IIdempotencyStore       _idempotencyStore;

    private static readonly TimeSpan LockTtl       = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CacheTtl      = TimeSpan.FromHours(24);
    private const int MaxRetries = 3;

    public PaymentController(
        PaymentService ps,
        IDistributedLockService ls,
        IIdempotencyStore idem)
    {
        _paymentService   = ps;
        _lockService       = ls;
        _idempotencyStore  = idem;
    }

    [HttpPost]
    public async Task<IActionResult> Pay([FromBody] PaymentRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.IdempotencyKey))
            return BadRequest("IdempotencyKey is required."); // 400

        if (req.Amount <= 0)
            return BadRequest("Amount must be greater than zero."); // 400

        // First check — fast path, avoids taking a lock for already-completed requests
        var cached = await _idempotencyStore.GetAsync(req.IdempotencyKey);
        if (cached != null) return Replay(cached);

        var lockKey = $"payment:{req.IdempotencyKey}";
        var locked = await _lockService.AcquireAsync(lockKey, LockTtl);
        if (!locked)
            return Conflict("Payment already in progress. Please retry shortly."); // 409

        try
        {
            // Second check closes the race between the first check and acquiring the lock
            cached = await _idempotencyStore.GetAsync(req.IdempotencyKey);
            if (cached != null) return Replay(cached);

            string paymentId = null;
            Exception lastError = null;

            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    paymentId = await _paymentService.ProcessAsync(req);
                    lastError = null;
                    break;
                }
                catch (PaymentBusinessException)
                {
                    // Deterministic failure (insufficient funds, bad account) 
                    // no retry on these, fails immediately.
                    throw;
                }
                catch (Exception ex) when (attempt < MaxRetries && IsTransient(ex))
                {
                    lastError = ex;
                    await Task.Delay(200 * attempt); // simple backoff
                }
            }

            if (paymentId == null)
                throw lastError ?? new Exception("Payment failed after retries.");

            var success = new PaymentResult
            {
                PaymentId      = paymentId,
                Status         = "Success",
                HttpStatusCode = StatusCodes.Status200OK
            };
            await _idempotencyStore.SetAsync(req.IdempotencyKey, success, CacheTtl);
            return Ok(new { success.PaymentId, success.Status });
        }
        catch (PaymentBusinessException ex)
        {
            var failed = new PaymentResult
            {
                Status         = "Failed",
                HttpStatusCode = StatusCodes.Status422UnprocessableEntity
            };
            // Cache the failure too same idempotency key should keep returning
            // the same rejection, not silently retry a deduction on every call.
            await _idempotencyStore.SetAsync(req.IdempotencyKey, failed, CacheTtl);
            return UnprocessableEntity(new { message = ex.Message }); // 422
        }
        catch (Exception ex)
        {
            // Do NOT cache transient/unexpected failures allow the client to retry
            // with the same idempotency key once the underlying issue clears.
            return StatusCode(500, new { message = $"Payment failed: {ex.Message}" }); // 500
        }
        finally
        {
            await _lockService.ReleaseAsync(lockKey);
        }
    }

    private IActionResult Replay(PaymentResult cached) =>
        StatusCode(cached.HttpStatusCode, new { cached.PaymentId, cached.Status });

    private static bool IsTransient(Exception ex) =>
        ex is DbUpdateConcurrencyException
        || ex is TimeoutException
        || ex is DbUpdateException; // refine to your provider's actual transient error set
}