using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Vercel containers serve on $PORT (default 80). Locally we default to 5080.
var port = Environment.GetEnvironmentVariable("PORT") ?? "5080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration["DATABASE_URL"]
    ?? throw new InvalidOperationException("DATABASE_URL is not set.");

builder.Services.AddSingleton(_ => new NpgsqlDataSourceBuilder(ToNpgsqlConnectionString(databaseUrl)).Build());
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();

// Containers on Vercel are stateless and scale to zero, so the schema check runs on every cold start (idempotent).
await using (var cmd = app.Services.GetRequiredService<NpgsqlDataSource>().CreateCommand("""
    CREATE TABLE IF NOT EXISTS products (
        id         SERIAL PRIMARY KEY,
        name       TEXT NOT NULL,
        price      NUMERIC(12,2) NOT NULL DEFAULT 0,
        created_at TIMESTAMPTZ NOT NULL DEFAULT now()
    )
    """))
{
    await cmd.ExecuteNonQueryAsync();
}

var api = app.MapGroup("/api");

api.MapGet("/health", async (NpgsqlDataSource db) =>
{
    await using var cmd = db.CreateCommand("SELECT version()");
    return Results.Ok(new { status = "ok", database = (string?)await cmd.ExecuteScalarAsync() });
});

api.MapGet("/products", async (NpgsqlDataSource db) =>
{
    await using var cmd = db.CreateCommand("SELECT id, name, price, created_at FROM products ORDER BY id");
    await using var r = await cmd.ExecuteReaderAsync();
    var list = new List<Product>();
    while (await r.ReadAsync()) list.Add(Read(r));
    return Results.Ok(list);
});

api.MapGet("/products/{id:int}", async (int id, NpgsqlDataSource db) =>
{
    await using var cmd = db.CreateCommand("SELECT id, name, price, created_at FROM products WHERE id = $1");
    cmd.Parameters.AddWithValue(id);
    await using var r = await cmd.ExecuteReaderAsync();
    return await r.ReadAsync() ? Results.Ok(Read(r)) : Results.NotFound();
});

api.MapPost("/products", async (ProductInput input, NpgsqlDataSource db) =>
{
    if (string.IsNullOrWhiteSpace(input.Name)) return Results.BadRequest(new { error = "Name is required." });
    await using var cmd = db.CreateCommand(
        "INSERT INTO products (name, price) VALUES ($1, $2) RETURNING id, name, price, created_at");
    cmd.Parameters.AddWithValue(input.Name.Trim());
    cmd.Parameters.AddWithValue(input.Price);
    await using var r = await cmd.ExecuteReaderAsync();
    await r.ReadAsync();
    var p = Read(r);
    return Results.Created($"/api/products/{p.Id}", p);
});

api.MapPut("/products/{id:int}", async (int id, ProductInput input, NpgsqlDataSource db) =>
{
    if (string.IsNullOrWhiteSpace(input.Name)) return Results.BadRequest(new { error = "Name is required." });
    await using var cmd = db.CreateCommand(
        "UPDATE products SET name = $1, price = $2 WHERE id = $3 RETURNING id, name, price, created_at");
    cmd.Parameters.AddWithValue(input.Name.Trim());
    cmd.Parameters.AddWithValue(input.Price);
    cmd.Parameters.AddWithValue(id);
    await using var r = await cmd.ExecuteReaderAsync();
    return await r.ReadAsync() ? Results.Ok(Read(r)) : Results.NotFound();
});

api.MapDelete("/products/{id:int}", async (int id, NpgsqlDataSource db) =>
{
    await using var cmd = db.CreateCommand("DELETE FROM products WHERE id = $1");
    cmd.Parameters.AddWithValue(id);
    return await cmd.ExecuteNonQueryAsync() > 0 ? Results.NoContent() : Results.NotFound();
});

app.Run();

static Product Read(NpgsqlDataReader r) =>
    new(r.GetInt32(0), r.GetString(1), r.GetDecimal(2), r.GetFieldValue<DateTime>(3));

// Neon gives a postgresql:// URI; Npgsql wants key=value pairs.
static string ToNpgsqlConnectionString(string url)
{
    if (!url.StartsWith("postgres")) return url;
    var uri = new Uri(url);
    var userInfo = uri.UserInfo.Split(':', 2);
    var csb = new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
        Database = uri.AbsolutePath.TrimStart('/'),
        Username = Uri.UnescapeDataString(userInfo[0]),
        Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
        SslMode = SslMode.Require,
    };
    var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
    if (query["channel_binding"] == "require") csb.ChannelBinding = ChannelBinding.Require;
    return csb.ConnectionString;
}

record Product(int Id, string Name, decimal Price, DateTime CreatedAt);
record ProductInput(string Name, decimal Price);
