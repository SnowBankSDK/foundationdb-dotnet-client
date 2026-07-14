using FoundationDB.Client;
using FoundationDB.DependencyInjection;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFoundationDb(740, options =>
{
    options.ConnectionOptions.ConnectionString = "docker:docker@127.0.0.1:4500";
});

// describe the endpoints in the OpenAPI format
builder.Services.AddOpenApi();

var app = builder.Build();

// Create the demo directory once at startup, so every endpoint below can just Resolve it.
{
    var startup = app.Services.GetRequiredService<IFdbDatabaseProvider>();
    await startup.WriteAsync(tr => startup.Root["Examples"]["Greetings"].CreateOrOpenAsync(tr), CancellationToken.None);
}

// serve the OpenAPI document at /openapi/v1.json
app.MapOpenApi();

// render it as a clickable UI at /scalar/v1
app.MapScalarApiReference();

// Liveness: proves the app can reach the cluster.
app.MapGet("/readversion", async (IFdbDatabaseProvider db, CancellationToken ct) =>
{
    long readVersion = await db.ReadAsync(tr => tr.GetReadVersionAsync(), ct);
    return Results.Ok(new { readVersion });
});

app.MapPost("/greetings", async (NewGreeting input, IFdbDatabaseProvider db, CancellationToken ct) =>
{
    // Generate the id OUTSIDE the retry loop, so a retry reuses the same id.
    var id = Guid.NewGuid().ToString("N");
    var location = db.Root["Examples"]["Greetings"];
    await db.WriteAsync(async tr =>
    {
        // resolve the subspace where the Greetings collection's keys are stored
        var subspace = await location.Resolve(tr);
        tr.Set(subspace.Key(id), FdbValue.ToTextUtf8(input.Text));
    }, ct);
    return Results.Created($"/greetings/{id}", new Greeting(id, input.Text));
});

app.MapGet("/greetings", async (IFdbDatabaseProvider db, CancellationToken ct) =>
{
    var location = db.Root["Examples"]["Greetings"];
    var greetings = await db.ReadAsync(async tr =>
    {
        var subspace = await location.Resolve(tr);

        // scan the whole subspace: decode each key back into its id, read the text from the
        // value, and ToListAsync() pulls the matches into a List
        return await tr.GetRange(subspace.ToRange())
            .Select(kv => new Greeting(subspace.DecodeLast<string>(kv.Key)!, kv.Value.ToStringUtf8()!))
            .ToListAsync();
    }, ct);
    return Results.Ok(greetings);
});

app.MapDelete("/greetings/{id}", async (string id, IFdbDatabaseProvider db, CancellationToken ct) =>
{
    var location = db.Root["Examples"]["Greetings"];
    await db.WriteAsync(async tr =>
    {
        var subspace = await location.Resolve(tr);
        // remove a single key
        tr.Clear(subspace.Key(id));
    }, ct);
    return Results.NoContent();
});

app.Run();

record NewGreeting(string Text);
record Greeting(string Id, string Text);
