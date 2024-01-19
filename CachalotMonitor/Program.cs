using System.Text.Json.Serialization;
using CachalotMonitor.Model;
using CachalotMonitor.Services;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("front-end-dev",
        builder =>
        {
            builder.SetIsOriginAllowed(origin => new Uri(origin).Host == "localhost");
            builder.AllowAnyMethod();
            builder.AllowAnyHeader();
        });

    options.AddPolicy("linux-server",
        builder =>
        {
            builder.SetIsOriginAllowed(origin => new Uri(origin).Host == "51.15.23.61");
            builder.AllowAnyMethod();
            builder.AllowAnyHeader();
        });

    options.AddPolicy("cachalotdb",
        builder =>
        {
            builder.SetIsOriginAllowed(origin => new Uri(origin).Host.EndsWith("cachalotdb.com"));
            builder.AllowAnyMethod();
            builder.AllowAnyHeader();
        });

    options.AddPolicy("cachalot-db",
        builder =>
        {
            builder.SetIsOriginAllowed(origin => new Uri(origin).Host.EndsWith("cachalot-db.com"));
            builder.AllowAnyMethod();
            builder.AllowAnyHeader();
        });
});


// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        var enumConverter = new JsonStringEnumConverter();
        opts.JsonSerializerOptions.Converters.Add(enumConverter);
    });

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddTransient<IQueryService, QueryService>();
builder.Services.AddSingleton<IClusterService, ClusterService>();
builder.Services.AddSingleton<IAdminService, AdminService>();
builder.Services.AddSingleton<ISchemaService, SchemaService>();

builder.Services.AddTransient<IAuthenticationService, AuthenticationService>();

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

builder.Services.Configure<ShowcaseConfig>(
    builder.Configuration.GetSection(nameof(ShowcaseConfig)));


var app = builder.Build();

app.UseResponseCompression();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


//app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseAuthorization();

app.UseCors("front-end-dev");

app.MapControllers();

app.MapFallbackToFile("index.html");

app.Start();

var server = app.Services.GetService<IServer>();
if (server != null)
{
    var addressFeature = server.Features.Get<IServerAddressesFeature>();

    var logger = app.Services.GetService<ILogger<Program>>();
    
    logger!.LogInformation("Server started successfully");
    foreach (var address in addressFeature?.Addresses ?? Array.Empty<string>())
    {
        logger!.LogInformation($"Your monitoring page is available at:{address}");
        
    }
}


await app.WaitForShutdownAsync();
//app.Run();