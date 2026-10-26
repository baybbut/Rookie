using Microsoft.EntityFrameworkCore;
using Rookie.Models;
namespace Rookie.Data;
// Dữ liệu MẪU để dashboard/báo cáo có số liệu khi chạy lần đầu. Xóa rookie.db hoặc bỏ lời gọi trong Program.cs nếu không muốn.
public static class DemoData {
 public static void Seed(ShopDbContext db) {
  if (db.Customers.Any() || db.Orders.Any() || db.Purchases.Any()) return;
  var rnd = new Random(42);
  var products = db.Products.AsNoTracking().ToList();
  string[] names = ["Nguyễn Minh Anh", "Trần Thảo Nhi", "Lê Hoàng Nam", "Phạm Gia Hân", "Võ Quốc Bảo", "Đặng Ngọc Mai"];
  var customers = names.Select((n, i) => new Customer { FullName = n, Email = $"khach{i + 1}@example.com", Phone = $"09000000{i + 10}", Address = $"{10 + i} Đường Số {i + 1}, TP.HCM", CreatedAt = DateTime.Today.AddDays(-40 + i * 3) }).ToList();
  customers[5].IsLocked = true;
  db.Customers.AddRange(customers); db.SaveChanges();
  if (products.Count == 0) return;
  for (var d = 29; d >= 0; d--) {
   for (var k = rnd.Next(0, 4); k > 0; k--) {
    var o = new Order { CustomerId = customers[rnd.Next(5)].Id, CreatedAt = DateTime.Today.AddDays(-d).AddHours(8 + rnd.Next(12)) };
    foreach (var p in products.OrderBy(_ => rnd.Next()).Take(rnd.Next(1, 4))) o.Items.Add(new OrderItem { ProductId = p.Id, ProductName = p.Name, UnitPrice = p.Price, Quantity = rnd.Next(1, 4) });
    o.Status = d > 2 ? (rnd.Next(10) == 0 ? OrderStatus.Cancelled : OrderStatus.Done) : OrderStatus.All[rnd.Next(4)];
    db.Orders.Add(o);
   }
  }
  string[,] stock = { { "Bơ lạt", "kg" }, { "Bột mì", "kg" }, { "Đường", "kg" }, { "Trứng gà", "quả" }, { "Dâu tây", "kg" }, { "Socola", "kg" } };
  decimal[] cost = [190000, 22000, 24000, 3500, 160000, 210000];
  string[] suppliers = ["Bơ sữa Đà Lạt", "Kho nguyên liệu An Phát", "Nông trại Mộc Châu"];
  for (var i = 0; i < 4; i++) {
   var pu = new Purchase { Supplier = suppliers[i % 3], CreatedAt = DateTime.Today.AddDays(-i * 7 - 1) };
   for (var j = 0; j < 3; j++) { var s = (i + j * 2) % 6; pu.Items.Add(new PurchaseItem { ItemName = stock[s, 0], Unit = stock[s, 1], Quantity = rnd.Next(5, 30), UnitCost = cost[s] }); }
   db.Purchases.Add(pu);
  }
  db.SaveChanges();
 }
}
