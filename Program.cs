using Microsoft.EntityFrameworkCore;
using Rookie.Data;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews(o=>o.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o=>{o.Cookie.HttpOnly=true;o.Cookie.IsEssential=true;o.IdleTimeout=TimeSpan.FromHours(8);});
builder.Services.AddAuthentication("Rookie").AddCookie("Rookie",o=>{o.LoginPath="/Account/Login";o.ExpireTimeSpan=TimeSpan.FromDays(7);});
builder.Services.AddAuthorization();
builder.Services.AddScoped<Rookie.Services.CartService>();
builder.Services.AddDbContext<CommerceDbContext>(o=>o.UseSqlite(builder.Configuration.GetConnectionString("Commerce")??"Data Source=rookie-commerce.db"));
builder.Services.AddRateLimiter(o=>{o.RejectionStatusCode=429;o.AddPolicy("auth",context=>System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString()??"local",_=>new System.Threading.RateLimiting.FixedWindowRateLimiterOptions{PermitLimit=10,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));});
builder.Services.AddDbContext<ShopDbContext>(o => o.UseSqlite(builder.Configuration.GetConnectionString("Shop") ?? "Data Source=rookie.db"));
var app = builder.Build();
using (var scope = app.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>(); db.Database.EnsureCreated(); scope.ServiceProvider.GetRequiredService<CommerceDbContext>().Database.EnsureCreated(); }
app.UseRequestLocalization(new RequestLocalizationOptions().SetDefaultCulture("vi-VN").AddSupportedCultures("vi-VN").AddSupportedUICultures("vi-VN"));
app.UseStaticFiles(); app.UseRouting(); app.UseRateLimiter(); app.UseSession(); app.UseAuthentication(); app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Run();
