using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Rookie.Data;
using Rookie.Models;
namespace Rookie.Controllers.Api;
// Vai trò trong JWT: khách = "Customer"; nhân viên dùng đúng vai trò trong bảng Staffs ("Quản trị" / "Nhân viên").
public static class Roles { public const string Customer = "Customer", Staff = StaffRole.Admin + "," + StaffRole.Staff; }

public record StaffLoginReq([Required] string Username, [Required] string Password);

// API không dùng cookie nên không cần chống CSRF (token Bearer không tự gửi kèm như cookie).
[ApiController, IgnoreAntiforgeryToken, Route("api/auth"), EnableRateLimiting("auth")]
public class AuthApiController(ShopDbContext db, SymmetricSecurityKey key) : ControllerBase {
 // Cùng quy tắc với /Account/Register của trang khách.
 [HttpPost("register")]
 public async Task<IActionResult> Register(RegisterInput r) {
  var email = r.Email.Trim().ToLowerInvariant(); var user = r.UserName.ToLowerInvariant();
  if (await db.Customers.AnyAsync(c => c.Email.ToLower() == email || c.UserName == user)) return Conflict(new { error = "Email hoặc tên tài khoản này đã được đăng ký." });
  var c = new Customer { FullName = r.Name.Trim(), Email = email, UserName = user, PasswordHash = Pw.Hash(r.Password) };
  db.Customers.Add(c); await db.SaveChangesAsync();
  return Ok(Token(c.Id, c.FullName, Roles.Customer));
 }

 // Đăng nhập khách bằng email hoặc tên tài khoản.
 [HttpPost("login")]
 public async Task<IActionResult> Login(LoginInput r) {
  var k = r.Email.Trim().ToLowerInvariant();
  var c = await db.Customers.FirstOrDefaultAsync(x => x.Email.ToLower() == k || x.UserName == k);
  if (c is null || !Pw.Verify(r.Password, c.PasswordHash)) return Unauthorized(new { error = "Email hoặc mật khẩu chưa đúng." });
  if (c.IsLocked) return StatusCode(403, new { error = "Tài khoản này đang bị khóa." });
  return Ok(Token(c.Id, c.FullName, Roles.Customer));
 }

 // Đăng nhập nhân viên (app WinForms) bằng tài khoản trong bảng Staffs, giống /admin/login.
 [HttpPost("staff")]
 public async Task<IActionResult> StaffLogin(StaffLoginReq r) {
  var u = r.Username.Trim().ToLowerInvariant();
  var s = await db.Staffs.FirstOrDefaultAsync(x => x.Username == u);
  if (s is null || !Pw.Verify(r.Password, s.PasswordHash)) return Unauthorized(new { error = "Sai tên đăng nhập hoặc mật khẩu." });
  if (s.IsLocked) return StatusCode(403, new { error = "Tài khoản này đã bị khóa." });
  return Ok(Token(s.Id, s.FullName, s.Role));
 }

 [Authorize(AuthenticationSchemes = Auth.Api), HttpGet("me")]
 public object Me() => new { id = User.FindFirstValue("sub"), name = User.Identity?.Name, role = User.FindFirstValue("role") };

 object Token(int id, string name, string role) {
  var expires = DateTime.UtcNow.AddHours(8);
  var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor { Subject = new ClaimsIdentity([new Claim("sub", id.ToString()), new Claim("name", name), new Claim("role", role)]), Expires = expires, SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256) });
  return new { token, expires, role, name };
 }
}
