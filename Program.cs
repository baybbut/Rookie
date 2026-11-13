using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Rookie.Controllers;
using Rookie.Data;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews(o=>o.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o=>{o.Cookie.HttpOnly=true;o.Cookie.IsEssential=true;o.IdleTimeout=TimeSpan.FromHours(8);});
builder.Services.AddScoped<Rookie.Services.CartService>();
builder.Services.AddRateLimiter(o=>{o.RejectionStatusCode=429;o.AddPolicy("auth",context=>System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString()??"local",_=>new System.Threading.RateLimiting.FixedWindowRateLimiterOptions{PermitLimit=10,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));});
builder.Services.AddDbContext<ShopDbContext>(o => o.UseSqlite(builder.Configuration.GetConnectionString("Shop") ?? "Data Source=rookie.db"));
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
});
builder.Services.AddAuthorization();
var app = builder.Build();
using (var scope = app.Services.CreateScope()) {
 // Mật khẩu quản trị lấy từ cấu hình Admin:Password; riêng môi trường Development có mặc định "admin".
 DbInit.Prepare(scope.ServiceProvider.GetRequiredService<ShopDbContext>(), app.Configuration["Admin:Password"] ?? (app.Environment.IsDevelopment() ? "admin" : null), app.Environment.IsDevelopment(), m => app.Logger.LogWarning("{Msg}", m));
}
app.UseRequestLocalization(new RequestLocalizationOptions().SetDefaultCulture("vi-VN").AddSupportedCultures("vi-VN").AddSupportedUICultures("vi-VN"));
app.UseStaticFiles(); app.UseRouting(); app.UseRateLimiter(); app.UseSession(); app.UseAuthentication(); app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Run();
