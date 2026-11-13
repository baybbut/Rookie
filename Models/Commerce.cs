using System.ComponentModel.DataAnnotations;
namespace Rookie.Models;
// Form nhập liệu của trang khách. Khách hàng / đơn hàng dùng chung model trong Sales.cs (rookie.db).
public class RegisterInput {
 [Required(ErrorMessage="Nhập tên tài khoản"),RegularExpression(@"^[a-zA-Z0-9_]{3,40}$",ErrorMessage="Tên tài khoản gồm 3–40 chữ cái, số hoặc dấu gạch dưới")] public string UserName {get;set;} = "";
 [Required(ErrorMessage="Nhập họ tên"),StringLength(80)] public string Name {get;set;} = "";
 [Required,EmailAddress,StringLength(254)] public string Email {get;set;} = "";
 [Required,MinLength(8,ErrorMessage="Mật khẩu cần ít nhất 8 ký tự"),MaxLength(128)] public string Password {get;set;} = "";
 [Compare(nameof(Password),ErrorMessage="Mật khẩu xác nhận chưa khớp")] public string ConfirmPassword {get;set;} = "";
}
public class LoginInput {
 [Required] public string Email {get;set;} = "";
 [Required] public string Password {get;set;} = "";
}
public class ProfileInput {
 [Required,StringLength(80)] public string Name {get;set;} = "";
 [StringLength(20)] public string Phone {get;set;} = "";
 [StringLength(200)] public string Address {get;set;} = "";
}
public class CheckoutInput {
 [Required(ErrorMessage="Nhập tên người nhận"),StringLength(100)] public string Name {get;set;} = "";
 [Required(ErrorMessage="Nhập số điện thoại"),Phone,StringLength(30)] public string Phone {get;set;} = "";
 [Required(ErrorMessage="Nhập địa chỉ giao bánh"),StringLength(500)] public string Address {get;set;} = "";
 [StringLength(1000)] public string? Note {get;set;}
 [Required,RegularExpression("^COD$",ErrorMessage="Hiện chỉ hỗ trợ thanh toán khi nhận hàng")] public string Payment {get;set;} = "COD";
 public string RequestKey {get;set;} = "";
}
public record CartLine(Product Product,int Quantity);
