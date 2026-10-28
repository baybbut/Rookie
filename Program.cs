using Microsoft.EntityFrameworkCore;
using Rookie.Data;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews(o=>o.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o=>{o.Cookie.HttpOnly=true;o.Cookie.IsEssential=true;o.IdleTimeout=TimeSpan.FromHours(8);});
builder.Services.AddAuthentication("Rookie").AddCookie("Rookie",o=>{o.LoginPath="/Account/Login";o.ExpireTimeSpan=TimeSpan.FromDays(7);
 o.Events.OnRedirectToLogin=context=>{
 if(context.Request.Headers.Accept.ToString().Contains("application/json")){context.Response.StatusCode=401;return Task.CompletedTask;}
 context.Response.Redirect(context.Request.Method=="POST"?"/Account/Login?returnUrl=%2FShop":context.RedirectUri);return Task.CompletedTask;
 };});
builder.Services.AddAuthorization();
builder.Services.AddScoped<Rookie.Services.CartService>();
builder.Services.AddDbContext<CommerceDbContext>(o=>o.UseSqlite(builder.Configuration.GetConnectionString("Commerce")??"Data Source=rookie-commerce.db"));
builder.Services.AddRateLimiter(o=>{o.RejectionStatusCode=429;o.AddPolicy("auth",context=>System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString()??"local",_=>new System.Threading.RateLimiting.FixedWindowRateLimiterOptions{PermitLimit=10,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));});
builder.Services.AddDbContext<ShopDbContext>(o => o.UseSqlite(builder.Configuration.GetConnectionString("Shop") ?? "Data Source=rookie.db"));
var app = builder.Build();
using (var scope = app.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>(); db.Database.EnsureCreated(); var commerce=scope.ServiceProvider.GetRequiredService<CommerceDbContext>();commerce.Database.EnsureCreated();
 var conn=commerce.Database.GetDbConnection();conn.Open();
 using(var cmd=conn.CreateCommand()) {
 cmd.CommandText="SELECT COUNT(*) FROM pragma_table_info('Customers') WHERE name='UserName'";
 if(Convert.ToInt32(cmd.ExecuteScalar())==0)commerce.Database.ExecuteSqlRaw("ALTER TABLE Customers ADD COLUMN UserName TEXT NULL");
 }
 commerce.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_Customers_UserName ON Customers(UserName) WHERE UserName IS NOT NULL");conn.Close(); }
app.UseRequestLocalization(new RequestLocalizationOptions().SetDefaultCulture("vi-VN").AddSupportedCultures("vi-VN").AddSupportedUICultures("vi-VN"));
app.UseStaticFiles(); app.UseRouting(); app.UseRateLimiter(); app.UseSession(); app.UseAuthentication(); app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Run();
