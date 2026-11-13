using System.ComponentModel.DataAnnotations;
namespace Rookie.Storefront.Models;
public class Customer {
 [StringLength(40)] public string? UserName {get;set;}
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
