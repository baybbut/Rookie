using System.ComponentModel.DataAnnotations;
namespace Rookie.Models;
public static class StaffRole {
 public const string Admin = "Quản trị", Staff = "Nhân viên";
 public static readonly string[] All = [Admin, Staff];
}
public class Staff {
 public int Id { get; set; }
 [Required, StringLength(80)] public string FullName { get; set; } = "";
 [Required, RegularExpression("^[a-z0-9._]{3,30}$", ErrorMessage = "Tên đăng nhập 3–30 ký tự: chữ thường, số, dấu . hoặc _")] public string Username { get; set; } = "";
 [Phone, StringLength(20)] public string? Phone { get; set; }
 [Required] public string Role { get; set; } = StaffRole.Staff;
 public string? PasswordHash { get; set; }
 public bool IsLocked { get; set; }
 public DateTime CreatedAt { get; set; } = DateTime.Now;
}
