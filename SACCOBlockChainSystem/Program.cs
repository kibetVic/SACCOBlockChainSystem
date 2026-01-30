using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning; // Add this
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks; // Add this
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Repositories;
using SACCOBlockChainSystem.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient(); // For API calls if needed
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Add Authentication services
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
    });

builder.Services.AddAuthorization(options =>
{
    // Add role-based policies
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin", "SuperAdmin"));

    options.AddPolicy("MemberOnly", policy =>
        policy.RequireRole("Member", "Admin", "SuperAdmin"));

    options.AddPolicy("RequireAuthenticatedUser", policy =>
        policy.RequireAuthenticatedUser());
});

// Database Context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("BlockchainDb")));

// Register Repository Pattern
builder.Services.AddScoped<IMemberRepository, MemberRepository>();
builder.Services.AddScoped<ILoanRepository, LoanRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();

// Register Application Services
builder.Services.AddScoped<IBlockchainService, BlockchainService>();
builder.Services.AddScoped<IMemberService, MemberService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<ILoanService, LoanService>();
builder.Services.AddScoped<IShareService, ShareService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<ICompanyContextService, CompanyContextService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

// Background Services
builder.Services.AddHostedService<BlockchainSyncService>();
builder.Services.AddHostedService<TransactionProcessorService>();

// Caching
builder.Services.AddMemoryCache();

// Add logging
builder.Services.AddLogging(configure =>
    configure.AddConsole().AddDebug().SetMinimumLevel(LogLevel.Information));

// SIMPLIFIED Health Checks (without AddDbContextCheck)
builder.Services.AddHealthChecks()
    .AddCheck("Database", () =>
        HealthCheckResult.Healthy("Database connection is healthy"));

// SIMPLIFIED API Versioning (optional - you can remove if not needed)
builder.Services.AddApiVersioning(options =>
{
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ReportApiVersions = true;
});

// SIMPLIFIED Swagger/OpenAPI for API documentation (optional - for development)
if (builder.Environment.IsDevelopment())
{
    // Minimal Swagger configuration
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
}

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();

    // Enable Swagger UI (only if Swagger was configured)
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseSession();
app.UseRouting();

// Add Authentication and Authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// SIMPLIFIED Health check endpoint
app.MapHealthChecks("/health");

//// Global authorization filter
//app.Use(async (context, next) =>
//{
//    var endpoint = context.GetEndpoint();
//    if (endpoint?.Metadata?.GetMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>() != null)
//    {
//        if (!context.User.Identity?.IsAuthenticated ?? true)
//        {
//            context.Response.Redirect("/Account/Login");
//            return;
//        }
//    }

//    await next();
//});

// Map controller routes
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "api",
    pattern: "api/{controller}/{action}/{id?}");

// Add a fallback route
//app.MapFallback(context =>
//{
//    if (!context.User.Identity?.IsAuthenticated ?? true)
//    {
//        context.Response.Redirect("/Account/Login");
//    }
//    else
//    {
//        context.Response.Redirect("/Home/Index");
//    }
//    return Task.CompletedTask;
//});

app.Run();