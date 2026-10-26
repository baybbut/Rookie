using System.ComponentModel.DataAnnotations;
namespace Rookie.Models;
public static class OrderStatus {
 public const string New = "Mới", Preparing = "Đang làm", Shipping = "Đang giao", Done = "Hoàn thành", Cancelled = "Đã hủy";
 public static readonly string[] All = [New, Preparing, Shipping, Done, Cancelled];
}
public class Customer {
 public int Id { get; set; }
 [Required, StringLength(80)] public string FullName { get; set; } = "";
 [Required, EmailAddress, StringLength(120)] public string Email { get; set; } = "";
 [Required, Phone, StringLength(20)] public string Phone { get; set; } = "";
 [StringLength(200)] public string? Address { get; set; }
 public bool IsLocked { get; set; }
 public DateTime CreatedAt { get; set; } = DateTime.Now;
 public List<Order> Orders { get; set; } = [];
}
public class Order {
 public int Id { get; set; }
 public int CustomerId { get; set; }
 public Customer? Customer { get; set; }
 public DateTime CreatedAt { get; set; } = DateTime.Now;
 public string Status { get; set; } = OrderStatus.New;
 public string? Note { get; set; }
 public List<OrderItem> Items { get; set; } = [];
 public decimal Total => Items.Sum(i => i.Subtotal);
}
public class OrderItem {
 public int Id { get; set; }
 public int OrderId { get; set; }
 public int ProductId { get; set; }
 public string ProductName { get; set; } = "";
 public decimal UnitPrice { get; set; }
 public int Quantity { get; set; }
 public decimal Subtotal => UnitPrice * Quantity;
}
public class Purchase {
 public int Id { get; set; }
 public string Supplier { get; set; } = "";
 public DateTime CreatedAt { get; set; } = DateTime.Now;
 public string? Note { get; set; }
 public List<PurchaseItem> Items { get; set; } = [];
 public decimal Total => Items.Sum(i => i.Subtotal);
}
public class PurchaseItem {
 public int Id { get; set; }
 public int PurchaseId { get; set; }
 public string ItemName { get; set; } = "";
 public string Unit { get; set; } = "";
 public decimal Quantity { get; set; }
 public decimal UnitCost { get; set; }
 public decimal Subtotal => Quantity * UnitCost;
}
public class PurchaseRow { public string? ItemName { get; set; } public string? Unit { get; set; } public decimal Quantity { get; set; } public decimal UnitCost { get; set; } }

public record DayPoint(DateTime Day, decimal Revenue, int Orders);
public record NamedValue(string Name, decimal Value, int Count);
public class DashboardVm {
 public decimal RevenueToday, RevenueMonth, PurchaseMonth;
 public int NewOrders, OpenOrders, Customers, Products;
 public List<DayPoint> Days = [];
 public List<Order> Recent = [];
 public List<NamedValue> TopProducts = [];
}
public class ReportVm {
 public int Days;
 public decimal Revenue, Cost, Profit, AvgOrder;
 public int OrderCount;
 public List<DayPoint> Series = [];
 public List<NamedValue> TopProducts = [], TopCustomers = [], ByStatus = [], BySupplier = [];
}
public static class Fmt {
 static readonly System.Globalization.CultureInfo Vn = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");
 public static string Vnd(decimal v) => v.ToString("N0", Vn) + " ₫";
 public static string Date(DateTime d) => d.ToString("dd/MM/yyyy HH:mm", Vn);
 public static string StatusClass(string s) => s switch { OrderStatus.New => "st-new", OrderStatus.Preparing => "st-prep", OrderStatus.Shipping => "st-ship", OrderStatus.Done => "st-done", _ => "st-cancel" };
}
