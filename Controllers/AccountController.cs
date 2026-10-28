using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Rookie.Data;
using Rookie.Models;
namespace Rookie.Controllers;
public class AccountController(CommerceDbContext db):Controller {
 readonly PasswordHasher<Customer> hasher=new();
 int UserId=>int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
 [HttpGet] public IActionResult Register()=>View(new RegisterInput());
 [HttpPost,EnableRateLimiting("auth")] public async Task<IActionResult> Register(RegisterInput input) {
 if(!ModelState.IsValid)return View(input);
 var email=input.Email.Trim().ToLowerInvariant();
 if(await db.Customers.AnyAsync(c=>c.Email==email)){ModelState.AddModelError("","Email này đã được đăng ký.");return View(input);}
 var c=new Customer{Name=input.Name.Trim(),Email=email};c.PasswordHash=hasher.HashPassword(c,input.Password);db.Customers.Add(c);
 try{await db.SaveChangesAsync();}catch(DbUpdateException){ModelState.AddModelError("","Không thể đăng ký email này.");return View(input);}
 await SignIn(c);return RedirectToAction("Profile");
 }
 [HttpGet] public IActionResult Login()=>View(new LoginInput());
 [HttpPost,EnableRateLimiting("auth")] public async Task<IActionResult> Login(LoginInput input,string? returnUrl) {
 if(!ModelState.IsValid)return View(input);
 var c=await db.Customers.SingleOrDefaultAsync(c=>c.Email==input.Email.Trim().ToLowerInvariant());
 if(c==null||hasher.VerifyHashedPassword(c,c.PasswordHash,input.Password)==PasswordVerificationResult.Failed){ModelState.AddModelError("","Email hoặc mật khẩu chưa đúng.");return View(input);}
 await SignIn(c);return Url.IsLocalUrl(returnUrl)?LocalRedirect(returnUrl!):RedirectToAction("Index","Home");
 }
 Task SignIn(Customer c)=>HttpContext.SignInAsync("Rookie",new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,c.Id.ToString()),new Claim(ClaimTypes.Name,c.Name)},"Rookie")));
 [Authorize,HttpPost] public async Task<IActionResult> Logout(){await HttpContext.SignOutAsync("Rookie");return RedirectToAction("Index","Home");}
 [Authorize,HttpGet] public async Task<IActionResult> Profile(){var c=await db.Customers.FindAsync(UserId);if(c==null)return Challenge();return View(new ProfileInput{Name=c.Name,Phone=c.Phone,Address=c.Address});}
 [Authorize,HttpPost] public async Task<IActionResult> Profile(ProfileInput input){if(!ModelState.IsValid)return View(input);var c=await db.Customers.FindAsync(UserId);if(c==null)return Challenge();c.Name=input.Name.Trim();c.Phone=input.Phone.Trim();c.Address=input.Address.Trim();await db.SaveChangesAsync();await SignIn(c);TempData["Message"]="Đã lưu hồ sơ và địa chỉ giao hàng.";return RedirectToAction("Profile");}
}
