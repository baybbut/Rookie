using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Rookie.Data;
var builder = WebApplication.CreateBuilder(args);
// IncludeFields: DashboardVm/ReportVm dùng field thay vì property.
builder.Services.AddControllersWithViews().AddJsonOptions(o => o.JsonSerializerOptions.IncludeFields = true);
builder.Services.AddDbContext<ShopDbContext>(o => o.UseSqlite(builder.Configuration.GetConnectionString("Shop") ?? "Data Source=rookie.db"));
// shortcut: thiếu Jwt:Key (≥ 32 ký tự) thì sinh khóa ngẫu nhiên, mọi token mất hiệu lực khi restart; đặt Jwt:Key khi triển khai thật.
var jwtKey = new SymmetricSecurityKey(builder.Configuration["Jwt:Key"] is { Length: >= 32 } k ? Encoding.UTF8.GetBytes(k) : RandomNumberGenerator.GetBytes(32));
builder.Services.AddSingleton(jwtKey);
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o => { o.LoginPath = "/admin/login"; o.Cookie.Name = "rookie-admin"; o.ExpireTimeSpan = TimeSpan.FromHours(8); })
 .AddJwtBearer(o => { o.MapInboundClaims = false; o.TokenValidationParameters = new() { ValidateIssuer = false, ValidateAudience = false, IssuerSigningKey = jwtKey, NameClaimType = "name", RoleClaimType = "role" }; });
builder.Services.AddAuthorization();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173", "http://localhost:3000"]).AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddRateLimiter(o => { o.RejectionStatusCode = 429; o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "", _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) })); });
var app = builder.Build();
using (var scope = app.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>(); db.Database.EnsureCreated(); DemoData.Seed(db); }
app.UseStaticFiles(); app.UseRouting(); app.UseCors(); app.UseRateLimiter(); app.UseAuthentication(); app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Run();
