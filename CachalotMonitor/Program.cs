using System.Text.Json.Serialization;
using CachalotMonitor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: "front-end-dev",
        builder =>
        {
            builder.SetIsOriginAllowed(origin => new Uri(origin).Host == "localhost");
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


var app = builder.Build();

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

app.Run();
