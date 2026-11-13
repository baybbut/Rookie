using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Rookie.Controllers;
using Rookie.Controllers.Api;
using Rookie.Data;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews(o=>o.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o=>{o.Cookie.HttpOnly=true;o.Cookie.IsEssential=true;o.IdleTimeout=TimeSpan.FromHours(8);});
builder.Services.AddScoped<Rookie.Services.CartService>();
builder.Services.AddRateLimiter(o=>{o.RejectionStatusCode=429;o.AddPolicy("auth",context=>System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString()??"local",_=>new System.Threading.RateLimiting.FixedWindowRateLimiterOptions{PermitLimit=10,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));});
builder.Services.AddDbContext<ShopDbContext>(o => o.UseSqlite(builder.Configuration.GetConnectionString("Shop") ?? "Data Source=rookie.db"));
// shortcut: thiếu Jwt:Key (≥ 32 ký tự) thì sinh khóa ngẫu nhiên, mọi token mất hiệu lực khi restart; đặt Jwt:Key khi triển khai thật.
var jwtKey = new SymmetricSecurityKey(builder.Configuration["Jwt:Key"] is { Length: >= 32 } k ? System.Text.Encoding.UTF8.GetBytes(k) : System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
builder.Services.AddSingleton(jwtKey);
// Scheme mặc định là phiên khách (trang bán bánh); /admin chỉ định rõ Auth.Staff.
builder.Services.AddAuthentication(Auth.Customer)
 .AddCookie(Auth.Customer,o=>{o.LoginPath="/Account/Login";o.ExpireTimeSpan=TimeSpan.FromDays(7);
 o.Events.OnRedirectToLogin=context=>{
 if(context.Request.Headers.Accept.ToString().Contains("application/json")){context.Response.StatusCode=401;return Task.CompletedTask;}
 context.Response.Redirect(context.Request.Method=="POST"?"/Account/Login?returnUrl=%2FShop":context.RedirectUri);return Task.CompletedTask;
 };})
 .AddCookie(Auth.Staff, o => {
 o.LoginPath = "/admin/login"; o.AccessDeniedPath = "/admin"; o.Cookie.Name = "rookie-staff"; o.ExpireTimeSpan = TimeSpan.FromHours(8);
 // Nhân viên bị xóa/khóa thì phiên đang mở bị hủy ngay.
 o.Events.OnValidatePrincipal = async ctx => {
  var s = int.TryParse(ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? await ctx.HttpContext.RequestServices.GetRequiredService<ShopDbContext>().Staffs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id) : null;
  if (s is null || s.IsLocked) { ctx.RejectPrincipal(); await ctx.HttpContext.SignOutAsync(Auth.Staff); }
 };
})
 // Web API (/api) cho React và WinForms: JWT. Token bị từ chối ngay nếu tài khoản đã bị khóa/xóa hoặc nhân viên đổi vai trò.
 .AddJwtBearer(Auth.Api, o => { o.MapInboundClaims = false; o.TokenValidationParameters = new() { ValidateIssuer = false, ValidateAudience = false, IssuerSigningKey = jwtKey, NameClaimType = "name", RoleClaimType = "role" };
 o.Events = new() { OnTokenValidated = async ctx => {
  var db = ctx.HttpContext.RequestServices.GetRequiredService<ShopDbContext>(); var role = ctx.Principal?.FindFirstValue("role");
  var ok = int.TryParse(ctx.Principal?.FindFirstValue("sub"), out var id) && (role == Roles.Customer
   ? await db.Customers.AnyAsync(c => c.Id == id && !c.IsLocked)
   : await db.Staffs.AnyAsync(s => s.Id == id && !s.IsLocked && s.Role == role));
  if (!ok) ctx.Fail("Tài khoản không còn hiệu lực.");
 } }; });
builder.Services.AddAuthorization();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173", "http://localhost:3000"]).AllowAnyHeader().AllowAnyMethod()));
// Swagger UI (/swagger) để thử Web API: chỉ liệt kê route /api, nút "Authorize" nhận JWT lấy từ /api/auth/*.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o => {
 o.SwaggerDoc("v1", new() { Title = "Rookie Bakery API", Version = "v1" });
 o.DocInclusionPredicate((_, api) => api.RelativePath?.StartsWith("api/") == true);
 o.AddSecurityDefinition("Bearer", new() { Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", Description = "Dán token lấy từ POST /api/auth/login hoặc /api/auth/staff." });
 o.AddSecurityRequirement(new() { [new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } }] = [] });
});
var app = builder.Build();
// Chỉ mở Swagger khi Development để không lộ cấu trúc API khi triển khai thật.
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
using (var scope = app.Services.CreateScope()) {
 // Mật khẩu quản trị lấy từ cấu hình Admin:Password; riêng môi trường Development có mặc định "admin".
 DbInit.Prepare(scope.ServiceProvider.GetRequiredService<ShopDbContext>(), app.Configuration["Admin:Password"] ?? (app.Environment.IsDevelopment() ? "admin" : null), app.Environment.IsDevelopment(), m => app.Logger.LogWarning("{Msg}", m));
}
app.UseRequestLocalization(new RequestLocalizationOptions().SetDefaultCulture("vi-VN").AddSupportedCultures("vi-VN").AddSupportedUICultures("vi-VN"));
app.UseStaticFiles(); app.UseRouting(); app.UseCors(); app.UseRateLimiter(); app.UseSession(); app.UseAuthentication(); app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Run();
