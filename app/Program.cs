using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TrainingApp;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o =>
{
    o.SingleLine = true;
    o.TimestampFormat = "HH:mm:ss ";
});

// Lab 5: a ConfigMap mounted at /config and a Secret mounted at /secrets.
// Environment variables and command-line arguments are added again afterwards,
// so they still win over the mounted files, as in a default ASP.NET Core app.
builder.Configuration
    .AddJsonFile("/config/appsettings.json", optional: true, reloadOnChange: true)
    .AddKeyPerFile("/secrets", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

var version = Environment.GetEnvironmentVariable("APP_VERSION") ?? "dev";
var pod = Environment.MachineName;
var connectionString = builder.Configuration.GetConnectionString("Default");
var hasDb = !string.IsNullOrWhiteSpace(connectionString);

if (builder.Configuration.GetValue<bool>("App:RequireDatabase") && !hasDb)
{
    Console.Error.WriteLine("FATAL: ConnectionStrings:Default is not set. Set the ConnectionStrings__Default environment variable.");
    return 1;
}

if (args.Contains("migrate"))
{
    // On the first run EF Core logs a failed query while it checks for its history table. That is expected and only confuses.
    builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.None);
}

var state = new AppState();
builder.Services.AddSingleton(state);

if (hasDb)
{
    builder.Services.AddDbContext<AppDbContext>(o => o
        .UseNpgsql(connectionString)
        .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));
}

var health = builder.Services.AddHealthChecks()
    .AddCheck("alive", () => state.Hung
        ? HealthCheckResult.Unhealthy("hung on purpose via /admin/hang")
        : HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck("ready", () => state.Ready
        ? HealthCheckResult.Healthy()
        : HealthCheckResult.Unhealthy("marked unready via /admin/unready"), tags: ["ready"]);
if (hasDb)
{
    // Readiness only: a database outage should take pods out of the Service, not restart them.
    health.AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);
}

var app = builder.Build();
var log = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("app");

if (args.Contains("migrate"))
{
    return await Migrate(app, hasDb, log);
}

log.LogInformation("Starting version {Version} on pod {Pod}", version, pod);
log.LogInformation("Config file /config/appsettings.json: {State}", File.Exists("/config/appsettings.json") ? "found" : "not mounted");
log.LogInformation("Secret directory /secrets: {State}", Directory.Exists("/secrets") ? "found" : "not mounted");
log.LogInformation("Database: {State}", hasDb ? "configured" : "not configured");
log.LogInformation("CPUs visible to .NET: {Cpus}, GC heap limit: {Heap} MB",
    Environment.ProcessorCount, GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024 / 1024);

app.Lifetime.ApplicationStopping.Register(() =>
    log.LogInformation("SIGTERM received, finishing in-flight requests and stopping"));

app.Use(async (ctx, next) =>
{
    var sw = Stopwatch.StartNew();
    await next();
    var path = ctx.Request.Path.Value ?? "";
    if (path is "/healthz" or "/readyz" && ctx.Response.StatusCode == 200) return;
    log.LogInformation("{Method} {Path} -> {Status} ({Ms} ms)",
        ctx.Request.Method, path, ctx.Response.StatusCode, sw.ElapsedMilliseconds);
});

app.MapGet("/", async (IConfiguration config, IServiceProvider services) =>
{
    var greeting = config["Greeting:Text"] ?? "Hello";
    var visits = "";
    if (hasDb)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        try
        {
            db.Visits.Add(new Visit { At = DateTimeOffset.UtcNow, Pod = pod });
            await db.SaveChangesAsync();
            visits = $" | visits: {await db.Visits.CountAsync()}";
        }
        catch (Exception ex)
        {
            var reason = ex.GetBaseException().Message.Split('\n')[0];
            var hint = reason.Contains("does not exist") ? " Has the migration Job run?" : "";
            visits = $" | visits: unavailable ({reason}){hint}";
        }
    }
    return Results.Text($"{greeting} | version: {version} | pod: {pod}{visits}\n");
});

app.MapGet("/api/info", (IConfiguration config) => Results.Json(new
{
    version,
    pod,
    greeting = config["Greeting:Text"],
    processorCount = Environment.ProcessorCount,
    gcHeapLimitMb = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024 / 1024,
    apiKey = Mask(config["Secrets:ApiKey"]),
    database = hasDb ? "configured" : "not configured",
    ready = state.Ready,
    hung = state.Hung,
}));

app.MapHealthChecks("/healthz", new() { Predicate = c => c.Tags.Contains("live") });
app.MapHealthChecks("/readyz", new() { Predicate = c => c.Tags.Contains("ready") });

// Burns CPU on the request thread. Lab 7 sends a steady stream of these to trigger the HPA.
app.MapGet("/work", (int? ms) =>
{
    var duration = Math.Clamp(ms ?? 100, 1, 5000);
    var sw = Stopwatch.StartNew();
    double x = 0;
    while (sw.ElapsedMilliseconds < duration) x += Math.Sqrt(sw.ElapsedTicks);
    return Results.Text($"worked {duration} ms on {pod}\n");
});

// Admin endpoints accept GET as well as POST so they also work from a browser.
string[] getOrPost = ["GET", "POST"];

app.MapMethods("/admin/unready", getOrPost, () =>
{
    state.Ready = false;
    log.LogWarning("Marked unready: /readyz now fails");
    return $"{pod} is now unready\n";
});

app.MapMethods("/admin/ready", getOrPost, () =>
{
    state.Ready = true;
    log.LogInformation("Marked ready again");
    return $"{pod} is ready again\n";
});

app.MapMethods("/admin/hang", getOrPost, () =>
{
    state.Hung = true;
    log.LogWarning("Simulating a hung process: /healthz now fails, the process keeps running");
    return $"{pod} is now hung\n";
});

app.MapMethods("/admin/crash", getOrPost, () =>
{
    log.LogError("Crashing on purpose with exit code 1");
    _ = Task.Run(async () =>
    {
        await Task.Delay(200);
        Environment.Exit(1);
    });
    return $"{pod} is crashing\n";
});

app.MapMethods("/admin/leak", getOrPost, (bool? managed) =>
{
    if (managed == true)
    {
        _ = Task.Run(() => LeakManaged(log));
        return $"{pod} is leaking managed memory (byte[])\n";
    }
    _ = Task.Run(() => LeakNative(log));
    return $"{pod} is leaking native memory\n";
});

var delay = app.Configuration.GetValue<int>("App:StartupDelaySeconds");
if (delay > 0)
{
    // Stands in for real startup work (cache warm-up, connection pools). Nothing listens yet.
    log.LogInformation("Warming up for {Delay} s before accepting requests", delay);
    await Task.Delay(TimeSpan.FromSeconds(delay));
}

await app.RunAsync();
return 0;

static string Mask(string? value) => string.IsNullOrEmpty(value)
    ? "not set"
    : value.Length <= 4 ? "****" : "****" + value[^4..];

static async Task<int> Migrate(WebApplication app, bool hasDb, ILogger log)
{
    if (!hasDb)
    {
        log.LogError("migrate: ConnectionStrings:Default is not set");
        return 1;
    }
    for (var attempt = 1; attempt <= 30; attempt++)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
            await db.Database.MigrateAsync();
            log.LogInformation("migrate: applied {Count} migration(s): {Names}",
                pending.Count, pending.Count == 0 ? "none pending" : string.Join(", ", pending));
            return 0;
        }
        catch (Exception ex) when (ex is Npgsql.NpgsqlException or System.Net.Sockets.SocketException)
        {
            log.LogWarning("migrate: database not reachable yet (attempt {Attempt}/30): {Reason}",
                attempt, ex.GetBaseException().Message);
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }
    log.LogError("migrate: giving up, database still not reachable");
    return 1;
}

static unsafe void LeakNative(ILogger log)
{
    const int chunk = 16 * 1024 * 1024;
    long total = 0;
    while (true)
    {
        var p = (byte*)NativeMemory.Alloc(chunk);
        // Touch every page, otherwise Linux never actually hands the memory out.
        for (var i = 0; i < chunk; i += 4096) p[i] = 1;
        total += chunk;
        log.LogWarning("Leaked {Total} MB of native memory", total / 1024 / 1024);
        Thread.Sleep(250);
    }
}

static void LeakManaged(ILogger log)
{
    var hoard = new List<byte[]>();
    try
    {
        while (true)
        {
            var block = new byte[16 * 1024 * 1024];
            Array.Fill(block, (byte)1);
            hoard.Add(block);
            log.LogWarning("Leaked {Total} MB of managed memory", hoard.Count * 16);
            Thread.Sleep(250);
        }
    }
    catch (OutOfMemoryException)
    {
        var limit = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024 / 1024;
        log.LogError("OutOfMemoryException after {Total} MB. The GC heap limit is {Limit} MB, below the container limit, so .NET throws before Linux kills the container",
            hoard.Count * 16, limit);
        hoard.Clear();
        GC.Collect();
    }
}

public class AppState
{
    public volatile bool Ready = true;
    public volatile bool Hung;
}

public class DatabaseHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(2));
        try
        {
            return await db.Database.CanConnectAsync(cts.Token)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("cannot connect to the database");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("cannot connect to the database", ex);
        }
    }
}
