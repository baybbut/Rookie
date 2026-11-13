using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Rookie.Data;
using Rookie.Models;
namespace Rookie.Controllers.Api;
public static class Roles { public const string Admin = "Admin", Customer = "Customer"; public const string Scheme = "Bearer"; }

public record RegisterReq([Required, StringLength(80)] string FullName, [Required, EmailAddress, StringLength(120)] string Email, [Required, Phone, StringLength(20)] string Phone, [StringLength(200)] string? Address, [Required, StringLength(100, MinimumLength = 6)] string Password);
public record LoginReq([Required] string Email, [Required] string Password);
public record AdminLoginReq([Required] string Password);

[ApiController, Route("api/auth"), EnableRateLimiting("auth")]
public class AuthApiController(ShopDbContext db, SymmetricSecurityKey key, IConfiguration config, IWebHostEnvironment env) : ControllerBase {
 static readonly PasswordHasher<Customer> Hasher = new();

 [HttpPost("register")]
 public async Task<IActionResult> Register(RegisterReq r) {
  var email = r.Email.Trim().ToLowerInvariant();
  if (await db.Customers.AnyAsync(c => c.Email.ToLower() == email)) return Conflict(new { error = "Email này đã được sử dụng." });
  var c = new Customer { FullName = r.FullName.Trim(), Email = email, Phone = r.Phone.Trim(), Address = string.IsNullOrWhiteSpace(r.Address) ? null : r.Address.Trim() };
  c.PasswordHash = Hasher.HashPassword(c, r.Password);
  db.Customers.Add(c); await db.SaveChangesAsync();
  return Ok(Token(c.Id.ToString(), c.FullName, Roles.Customer));
 }

 [HttpPost("login")]
 public async Task<IActionResult> Login(LoginReq r) {
  var email = r.Email.Trim().ToLowerInvariant();
  var c = await db.Customers.FirstOrDefaultAsync(x => x.Email.ToLower() == email && x.PasswordHash != null);
  if (c is null || Hasher.VerifyHashedPassword(c, c.PasswordHash!, r.Password) == PasswordVerificationResult.Failed) return Unauthorized(new { error = "Sai email hoặc mật khẩu." });
  if (c.IsLocked) return StatusCode(403, new { error = "Tài khoản đang bị khóa." });
  return Ok(Token(c.Id.ToString(), c.FullName, Roles.Customer));
 }

 // Dùng chung mật khẩu Admin:Password với trang /admin (cho app WinForms).
 [HttpPost("admin")]
 public IActionResult AdminLogin(AdminLoginReq r) {
  var expected = AdminController.AdminPasswordFrom(config, env);
  if (expected is null) return StatusCode(503, new { error = "Chưa cấu hình Admin:Password." });
  return AdminController.PasswordMatches(expected, r.Password) ? Ok(Token("admin", "admin", Roles.Admin)) : Unauthorized(new { error = "Sai mật khẩu." });
 }

 [Authorize(AuthenticationSchemes = Roles.Scheme), HttpGet("me")]
 public object Me() => new { id = User.FindFirstValue("sub"), name = User.Identity?.Name, role = User.FindFirstValue("role") };

 object Token(string sub, string name, string role) {
  var expires = DateTime.UtcNow.AddHours(8);
  var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor { Subject = new ClaimsIdentity([new Claim("sub", sub), new Claim("name", name), new Claim("role", role)]), Expires = expires, SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256) });
  return new { token, expires, role, name };
 }
}
