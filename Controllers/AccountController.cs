using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Rookie.Data;
using Rookie.Models;
namespace Rookie.Controllers;
// Tài khoản khách dùng chung bảng Customers với trang quản trị; mật khẩu băm bằng Pw như nhân viên/app WinForms.
[Authorize(AuthenticationSchemes=Auth.Customer)]
public class AccountController(ShopDbContext db):Controller {
 int UserId=>int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
 [AllowAnonymous,HttpGet] public IActionResult Register()=>View(new RegisterInput());
 [AllowAnonymous,HttpPost,EnableRateLimiting("auth")] public async Task<IActionResult> Register(RegisterInput input) {
 if(!ModelState.IsValid)return View(input);
 var email=input.Email.Trim().ToLowerInvariant();var user=input.UserName.ToLowerInvariant();
 if(await db.Customers.AnyAsync(c=>c.Email.ToLower()==email||c.UserName==user)){ModelState.AddModelError("","Email hoặc tên tài khoản này đã được đăng ký.");return View(input);}
 var c=new Customer{FullName=input.Name.Trim(),Email=email,UserName=user,PasswordHash=Pw.Hash(input.Password)};db.Customers.Add(c);
 try{await db.SaveChangesAsync();}catch(DbUpdateException){ModelState.AddModelError("","Không thể đăng ký email này.");return View(input);}
 await SignIn(c);return RedirectToAction("Profile");
 }
 [AllowAnonymous,HttpGet] public IActionResult Login()=>View(new LoginInput());
 [AllowAnonymous,HttpPost,EnableRateLimiting("auth")] public async Task<IActionResult> Login(LoginInput input,string? returnUrl) {
 if(!ModelState.IsValid)return View(input);
 var key=input.Email.Trim().ToLowerInvariant();
 var c=await db.Customers.FirstOrDefaultAsync(c=>c.Email.ToLower()==key||c.UserName==key);
 if(c==null||!Pw.Verify(input.Password,c.PasswordHash)){ModelState.AddModelError("","Email hoặc mật khẩu chưa đúng.");return View(input);}
 if(c.IsLocked){ModelState.AddModelError("","Tài khoản này đang bị khóa. Vui lòng liên hệ tiệm.");return View(input);}
 await SignIn(c);return Url.IsLocalUrl(returnUrl)?LocalRedirect(returnUrl!):RedirectToAction("Profile");
 }
 Task SignIn(Customer c)=>HttpContext.SignInAsync(Auth.Customer,new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,c.Id.ToString()),new Claim(ClaimTypes.Name,c.FullName)},Auth.Customer)));
 [HttpPost] public async Task<IActionResult> Logout(){await HttpContext.SignOutAsync(Auth.Customer);return RedirectToAction("Index","Home");}
 [HttpGet] public async Task<IActionResult> Profile(){var c=await db.Customers.FindAsync(UserId);if(c==null)return Challenge();ViewBag.Email=c.Email;ViewBag.UserName=c.UserName??"Chưa có tên tài khoản (tài khoản do tiệm tạo)";return View(new ProfileInput{Name=c.FullName,Phone=c.Phone,Address=c.Address??""});}
 [HttpPost] public async Task<IActionResult> Profile(ProfileInput input){var c=await db.Customers.FindAsync(UserId);if(c!=null){ViewBag.Email=c.Email;ViewBag.UserName=c.UserName??"Chưa có tên tài khoản";}if(!ModelState.IsValid)return View(input);if(c==null)return Challenge();c.FullName=input.Name.Trim();c.Phone=(input.Phone??"").Trim();c.Address=string.IsNullOrWhiteSpace(input.Address)?null:input.Address.Trim();await db.SaveChangesAsync();await SignIn(c);TempData["Message"]="Đã lưu hồ sơ và địa chỉ giao hàng.";return RedirectToAction("Profile");}
}
// Hai phiên đăng nhập tách riêng: khách (cookie "Rookie", scheme mặc định) và nhân viên (/admin, cookie "rookie-staff").
public static class Auth { public const string Customer="Rookie", Staff="Cookies"; }
