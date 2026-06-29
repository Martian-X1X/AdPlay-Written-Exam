//PAYMENT API - POST /api/payment

//Idempotency KeyClient sends a unique key - we cache the response - same key = same response returned, no double charge
 
//1. The Request Body (what the client sends)
public class PaymentRequest
{
    public string IdempotencyKey { get; set; } // unique ID client sends so same payment isn't run twice
    public decimal Amount         { get; set; }
    public string Currency        { get; set; }
    public string ToAccount       { get; set; }
}
 
//2. In-memory store for idempotency
// In real life: use Redis or DB. Here we use a static dict for simplicity.
public static class IdempotencyStore
{
    // key = IdempotencyKey string, value = the saved response
    private static readonly Dictionary<string, IActionResult> _cache = new();
    private static readonly SemaphoreSlim _lock = new(1, 1); // only 1 thread at a time
 
    public static async Task<IActionResult?> GetAsync(string key)
    {
        await _lock.WaitAsync();
        try   { return _cache.TryGetValue(key, out var v) ? v : null; }
        finally { _lock.Release(); }
    }
 
    public static async Task SetAsync(string key, IActionResult result)
    {
        await _lock.WaitAsync();
        try   { _cache[key] = result; }
        finally { _lock.Release(); }
    }
}
 
//3. Distributed Lock (prevents parallel duplicate calls)
//only 1 person can hold it at a time.
public interface IDistributedLockService
{
    Task<bool> AcquireAsync(string resource, TimeSpan ttl);
    Task ReleaseAsync(string resource);
}
 
// Simple in-memory fake lock (replace with Redis SETNX in production)
public class InMemoryLockService : IDistributedLockService
{
    private static readonly HashSet<string> _locks = new();
    private static readonly object _obj = new();
 
    public Task<bool> AcquireAsync(string resource, TimeSpan ttl)
    {
        lock (_obj)
        {
            if (_locks.Contains(resource)) return Task.FromResult(false); // already locked
            _locks.Add(resource);
            return Task.FromResult(true);
        }
    }
 
    public Task ReleaseAsync(string resource)
    {
        lock (_obj) { _locks.Remove(resource); }
        return Task.CompletedTask;
    }
}
 
//4. The Payment Service
public class PaymentService
{
    private readonly AppDbContext _db;
 
    public PaymentService(AppDbContext db) => _db = db;
 
    // This runs inside a DB transaction — all or nothing
    public async Task<string> ProcessAsync(PaymentRequest req)
    {
        // Begin transaction — if anything fails, nothing is saved
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // TODO: deduct from sender, credit receiver, save Payment record
            var paymentId = Guid.NewGuid().ToString();
 
            await _db.SaveChangesAsync();
            await tx.CommitAsync(); //everything ok, save it
            return paymentId;
        }
        catch
        {
            await tx.RollbackAsync(); //something failed, undo everything
            throw;
        }
    }
}
 
//5.Controller where there is a POST endpoint for /api/payment
[ApiController]
[Route("api/[controller]")]
public class PaymentController : ControllerBase
{
    private readonly PaymentService        _paymentService;
    private readonly IDistributedLockService _lockService;
 
    public PaymentController(PaymentService ps, IDistributedLockService ls)
    {
        _paymentService = ps;
        _lockService    = ls;
    }
 
    [HttpPost]
    public async Task<IActionResult> Pay([FromBody] PaymentRequest req)
    {
        //Validate idempotency key exists in request
        if (string.IsNullOrEmpty(req.IdempotencyKey))
            return BadRequest("IdempotencyKey is required"); // 400
 
        //Check if we already processed this exact request
        var cached = await IdempotencyStore.GetAsync(req.IdempotencyKey);
        if (cached != null) return cached; // return same response as before (no double charge)
 
        //Acquire distributed lock on this idempotency key
        // Prevents 2 parallel requests with same key from both running
        var lockKey = $"payment:{req.IdempotencyKey}";
        var locked  = await _lockService.AcquireAsync(lockKey, TimeSpan.FromSeconds(30));
        if (!locked)
            return Conflict("Payment already in progress"); // 409
 
        try
        {
            // Check cache again
            cached = await IdempotencyStore.GetAsync(req.IdempotencyKey);
            if (cached != null) return cached;
 
            //Retry mechanism (try up to 3 times on failure)
            string? paymentId = null;
            int maxRetries = 3;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    paymentId = await _paymentService.ProcessAsync(req); // runs in DB transaction
                    break; // success — exit retry loop
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    // transient error — wait a bit then retry
                    await Task.Delay(200 * attempt);
                }
            }
 
            //Save result so duplicate calls return same response
            var response = Ok(new { PaymentId = paymentId, Status = "Success" }); // 200
            await IdempotencyStore.SetAsync(req.IdempotencyKey, response);
            return response;
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Payment failed: {ex.Message}"); // 500
        }
        finally
        {
            // Always release lock — even if something crashed
            await _lockService.ReleaseAsync(lockKey);
        }
    }
}