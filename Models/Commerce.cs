using System.ComponentModel.DataAnnotations;
namespace Rookie.Models;
public class Customer {
 public int Id {get;set;}
 [Required, StringLength(100)] public string Name {get;set;} = "";
 [Required, EmailAddress, StringLength(254)] public string Email {get;set;} = "";
 public string PasswordHash {get;set;} = "";
 [StringLength(30)] public string Phone {get;set;} = "";
 [StringLength(500)] public string Address {get;set;} = "";
}
public class Order {
 public int Id {get;set;}
 public int CustomerId {get;set;}
 public DateTime CreatedAt {get;set;} = DateTime.UtcNow;
 public string Name {get;set;} = "";
 public string Phone {get;set;} = "";
 public string Address {get;set;} = "";
 public string Note {get;set;} = "";
 public string Payment {get;set;} = "COD";
 public string Status {get;set;} = "Chờ xác nhận";
 public string RequestKey {get;set;} = "";
 public List<OrderItem> Items {get;set;} = [];
 public decimal Total => Items.Sum(i=>i.Price*i.Quantity);
}
public class OrderItem {
 public int Id {get;set;}
 public int OrderId {get;set;}
 public int ProductId {get;set;}
 public string Name {get;set;} = "";
 public decimal Price {get;set;}
 public int Quantity {get;set;}
}
public class RegisterInput {
 [Required(ErrorMessage="Nhập họ tên"),StringLength(100)] public string Name {get;set;} = "";
 [Required,EmailAddress,StringLength(254)] public string Email {get;set;} = "";
 [Required,MinLength(8,ErrorMessage="Mật khẩu cần ít nhất 8 ký tự"),MaxLength(128)] public string Password {get;set;} = "";
 [Compare(nameof(Password),ErrorMessage="Mật khẩu xác nhận chưa khớp")] public string ConfirmPassword {get;set;} = "";
}
public class LoginInput {
 [Required,EmailAddress] public string Email {get;set;} = "";
 [Required] public string Password {get;set;} = "";
}
public class ProfileInput {
 [Required,StringLength(100)] public string Name {get;set;} = "";
 [StringLength(30)] public string Phone {get;set;} = "";
 [StringLength(500)] public string Address {get;set;} = "";
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
