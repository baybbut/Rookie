using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rookie.Data;
using Rookie.Models;
namespace Rookie.Controllers;
// Quản lý nhân sự: chỉ vai trò "Quản trị" truy cập được. Không khóa/hạ quyền người quản trị cuối cùng để khỏi tự khóa mình khỏi hệ thống.
public partial class AdminController {
 int MyId => int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var i) ? i : 0;
 Task<bool> OtherActiveAdmin(int id) => db.Staffs.AnyAsync(s => s.Id != id && s.Role == StaffRole.Admin && !s.IsLocked);

 [Authorize(Roles = StaffRole.Admin), HttpGet("staff")]
 public async Task<IActionResult> StaffList(string? q, string? role, string? status) {
  var all = await db.Staffs.AsNoTracking().OrderBy(s => s.Id).ToListAsync();
  ViewBag.Q = q; ViewBag.Role = role; ViewBag.Status = status; ViewBag.Me = MyId;
  return View(all.Where(s => (Has(s.FullName, q) || Has(s.Username, q) || Has(s.Phone, q)) && (string.IsNullOrEmpty(role) || s.Role == role) && (string.IsNullOrEmpty(status) || s.IsLocked == (status == "locked"))).ToList());
 }

 [Authorize(Roles = StaffRole.Admin), HttpGet("staff/create")]
 public IActionResult CreateStaff() => View("StaffForm", new Staff());

 [Authorize(Roles = StaffRole.Admin), HttpPost("staff/create"), ValidateAntiForgeryToken]
 public async Task<IActionResult> CreateStaff([Bind("FullName,Username,Phone,Role")] Staff s, string? password) {
  s.Username = s.Username?.Trim().ToLowerInvariant() ?? ""; ModelState.Remove(nameof(Staff.Username)); TryValidateModel(s);
  if (string.IsNullOrEmpty(password)) ModelState.AddModelError("password", "Hãy nhập mật khẩu.");
  else if (password.Length is < 6 or > 64) ModelState.AddModelError("password", "Mật khẩu dài 6–64 ký tự.");
  if (!StaffRole.All.Contains(s.Role)) ModelState.AddModelError(nameof(Staff.Role), "Hãy chọn vai trò.");
  if (await db.Staffs.AnyAsync(x => x.Username == s.Username)) ModelState.AddModelError(nameof(Staff.Username), "Tên đăng nhập đã tồn tại.");
  if (!ModelState.IsValid) return View("StaffForm", s);
  s.PasswordHash = Pw.Hash(password!);
  db.Staffs.Add(s); await db.SaveChangesAsync();
  TempData["Msg"] = $"Đã thêm nhân sự “{s.FullName}” ({s.Username}).";
  return RedirectToAction(nameof(StaffList));
 }

 [Authorize(Roles = StaffRole.Admin), HttpGet("staff/edit/{id:int}")]
 public async Task<IActionResult> EditStaff(int id) {
  var s = await db.Staffs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
  return s is null ? NotFound() : View("StaffForm", s);
 }

 [Authorize(Roles = StaffRole.Admin), HttpPost("staff/edit/{id:int}"), ValidateAntiForgeryToken]
 public async Task<IActionResult> EditStaff(int id, [Bind("FullName,Phone,Role")] Staff input, string? password) {
  var s = await db.Staffs.FindAsync(id);
  if (s is null) return NotFound();
  ModelState.Remove(nameof(Staff.Username));
  if (!string.IsNullOrEmpty(password) && password.Length is < 6 or > 64) ModelState.AddModelError("password", "Mật khẩu dài 6–64 ký tự.");
  if (!StaffRole.All.Contains(input.Role)) ModelState.AddModelError(nameof(Staff.Role), "Hãy chọn vai trò.");
  else if (s.Role == StaffRole.Admin && input.Role != StaffRole.Admin && !await OtherActiveAdmin(id)) ModelState.AddModelError(nameof(Staff.Role), "Phải còn ít nhất một người quản trị đang hoạt động.");
  if (!ModelState.IsValid) { input.Id = id; input.Username = s.Username; input.IsLocked = s.IsLocked; return View("StaffForm", input); }
  (s.FullName, s.Phone, s.Role) = (input.FullName, input.Phone, input.Role);
  if (!string.IsNullOrEmpty(password)) s.PasswordHash = Pw.Hash(password);
  await db.SaveChangesAsync();
  TempData["Msg"] = $"Đã cập nhật “{s.FullName}”{(string.IsNullOrEmpty(password) ? "" : " và đổi mật khẩu")}.";
  return RedirectToAction(nameof(StaffList));
 }

 [Authorize(Roles = StaffRole.Admin), HttpPost("staff/lock/{id:int}"), ValidateAntiForgeryToken]
 public async Task<IActionResult> ToggleStaffLock(int id) {
  var s = await db.Staffs.FindAsync(id);
  if (s is null) return NotFound();
  if (!s.IsLocked && id == MyId) TempData["Msg"] = "Bạn không thể tự khóa tài khoản đang đăng nhập.";
  else if (!s.IsLocked && s.Role == StaffRole.Admin && !await OtherActiveAdmin(id)) TempData["Msg"] = "Phải còn ít nhất một người quản trị đang hoạt động.";
  else { s.IsLocked = !s.IsLocked; await db.SaveChangesAsync(); TempData["Msg"] = s.IsLocked ? $"Đã khóa tài khoản “{s.FullName}”." : $"Đã mở khóa “{s.FullName}”."; }
  return RedirectToAction(nameof(StaffList));
 }
}

