// Q10. REQUEST LOGGING MIDDLEWARE

// Middleware = code that runs for EVERY request/response
// Request comes in -log it - pass to app - response comes out - log it

// Must NOT break the pipeline (no throwing, always call next)
 
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;   // next = the rest of the pipeline
    private readonly ILogger<RequestLoggingMiddleware> _logger;
 
    public RequestLoggingMiddleware(RequestDelegate next,
                                    ILogger<RequestLoggingMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }
 
    public async Task InvokeAsync(HttpContext ctx)
    {
        // A unique ID so you can trace one request across all log lines
        var correlationId = ctx.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                            ?? Guid.NewGuid().ToString(); // use client's or make one
 
        // Attach to response headers so client can see it too
        ctx.Response.Headers["X-Correlation-ID"] = correlationId;
 
        // Put it in log scope ,  all logs inside here will include it automatically
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        });
 
        // By default request body is a forward-only stream (can only read once)
        // EnableBuffering() lets us read it and then let the app read it again
        ctx.Request.EnableBuffering();
        var requestBody = await ReadBodyAsync(ctx.Request.Body);
        ctx.Request.Body.Position = 0; // rewind so the controller can read it too
 
        _logger.LogInformation(
            "[{CorrelationId}] REQUEST  {Method} {Path} | Body: {Body}",
            correlationId,
            ctx.Request.Method,
            ctx.Request.Path,
            requestBody);
 
        // Response body is also write-only by default; we swap it with a MemoryStream
        // so we can read what the app wrote
        var originalStream  = ctx.Response.Body;
        using var memStream = new MemoryStream();
        ctx.Response.Body   = memStream; // app now writes into our memory stream
 
        //Start Timer
        var stopwatch = Stopwatch.StartNew();
 
        try
        {
            //Run the rest of the pipeline
            await _next(ctx); //this is where your actual endpoint runs
        }
        catch (Exception ex)
        {
            //Unhandled exception
            //re-throw so error handling middleware works
            _logger.LogError(ex,
                "[{CorrelationId}] UNHANDLED EXCEPTION: {Message}",
                correlationId, ex.Message);
 
            throw;
        }
        finally
        {
            //Stop Timer
            stopwatch.Stop();
 
            //Read the response body from memory stream
            memStream.Position = 0;
            var responseBody = await new StreamReader(memStream).ReadToEndAsync();
 
            //Copy the contents of memory stream to original stream, which is then returned to client
            // Without this, the client gets an empty response!
            memStream.Position = 0;
            await memStream.CopyToAsync(originalStream);
            ctx.Response.Body = originalStream; // restore original stream
 
            //Log the response
            _logger.LogInformation(
                "[{CorrelationId}] RESPONSE {StatusCode} | Time: {Ms}ms | Body: {Body}",
                correlationId,
                ctx.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                responseBody);
        }
    }
 
    // Helper: read a stream into a string safely
    private static async Task<string> ReadBodyAsync(Stream body)
    {
        if (!body.CanRead) return "[unreadable]";
        using var reader = new StreamReader(body, leaveOpen: true); // don't close the stream
        return await reader.ReadToEndAsync();
    }
}
 
// ---- REGISTER IN Program.cs --------------------------------
// app.UseMiddleware<RequestLoggingMiddleware>();
// Put it EARLY in the pipeline so it wraps everything
 
// ---- SAMPLE LOG OUTPUT -------------------------------------
// [abc-123] REQUEST  POST /api/payment | Body: {"amount":100}
// [abc-123] RESPONSE 200 | Time: 42ms  | Body: {"paymentId":"xyz"}
//
// On error:
// [abc-123] UNHANDLED EXCEPTION: Object reference not set...
// [abc-123] RESPONSE 500 | Time: 5ms  | Body: ...