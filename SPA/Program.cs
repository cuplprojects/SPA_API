using SPA.Data;
using Microsoft.EntityFrameworkCore;
using SPA.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policyBuilder =>
    {
        policyBuilder.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
    });
});

builder.Services.AddDbContext<FirstDbContext>(options =>
    options.UseMySql(builder.Configuration.GetConnectionString("Database1"),
        new MySqlServerVersion(new Version(8, 0, 2)),
        mysqlOptions =>
        {
            mysqlOptions.CommandTimeout(180); // Set command timeout to 180 seconds (3 minutes)
        }));

builder.Services.AddDbContext<SecondDbContext>(options =>
    options.UseMySql(builder.Configuration.GetConnectionString("Database2"),
        new MySqlServerVersion(new Version(8, 0, 2)),
        mysqlOptions =>
        {
            mysqlOptions.CommandTimeout(180); // Set command timeout to 180 seconds (3 minutes)
        }));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Issuer"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    };
});

builder.Services.AddScoped<IChangeLogger, Changelogger>();
builder.Services.AddScoped<OmrDataService>();
builder.Services.AddScoped<FieldConfigService>();
builder.Services.AddScoped<RegistrationDataService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<ISecurityService, SecurityService>();
builder.Services.AddTransient<DatabaseConnectionChecker>();
builder.Services.AddScoped<ILoggerService, LoggerService>();
/*builder.Services.AddScoped<RollNumberCheckService>();*/

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors();

app.UseAuthentication(); // Ensure authentication comes before authorization
app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    /*var firstDbContext = services.GetRequiredService<FirstDbContext>();
    firstDbContext.Database.Migrate();

    var secondDbContext = services.GetRequiredService<SecondDbContext>();
    secondDbContext.Database.Migrate();*/
}

app.Run();
