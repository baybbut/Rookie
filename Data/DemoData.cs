using Microsoft.EntityFrameworkCore;
using Rookie.Models;
namespace Rookie.Data;
// Dữ liệu MẪU để các trang quản trị có số liệu khi chạy lần đầu. Xóa rookie.db hoặc bỏ lời gọi trong Program.cs nếu không muốn.
public static class DemoData {
 // Tài khoản quản trị đầu tiên: tên đăng nhập "admin"; mật khẩu null (không cấu hình) thì không tạo và trang đăng nhập báo lỗi cấu hình.
 public static void SeedStaff(ShopDbContext db, string? adminPassword, bool demo) {
  if (db.Staffs.Any()) return;
  if (adminPassword is not null) db.Staffs.Add(new Staff { FullName = "Quản trị viên", Username = "admin", Role = StaffRole.Admin, Phone = "0900000001", PasswordHash = Pw.Hash(adminPassword) });
  if (adminPassword is not null && demo) { var h = Pw.Hash("nhanvien"); db.Staffs.AddRange(new Staff { FullName = "Lê Thu Hà", Username = "thuha", Role = StaffRole.Staff, Phone = "0900000002", PasswordHash = h }, new Staff { FullName = "Phạm Quốc Việt", Username = "quocviet", Role = StaffRole.Staff, Phone = "0900000003", PasswordHash = h }); }
  db.SaveChanges();
 }

 public static void Seed(ShopDbContext db) {
  if (!db.Suppliers.Any()) { db.Suppliers.AddRange(new Supplier { Name = "Xưởng bánh Hòa Bình", Phone = "02838000001", Address = "12 Nguyễn Trãi, Q.5, TP.HCM" }, new Supplier { Name = "Xưởng bánh Bơ Vàng", Phone = "02838000002", Address = "45 Lê Văn Việt, TP. Thủ Đức" }, new Supplier { Name = "Xưởng bánh Sài Gòn Xưa", Phone = "02838000003", Address = "8 Hoàng Hoa Thám, Q. Bình Thạnh" }); db.SaveChanges(); }
  if (db.Customers.Any() || db.Orders.Any() || db.Purchases.Any()) return;
  var rnd = new Random(42);
  var products = db.Products.AsNoTracking().ToList();
  var staff = db.Staffs.AsNoTracking().Select(s => s.FullName).ToList(); if (staff.Count == 0) staff.Add("Quản trị viên");
  string[] names = ["Nguyễn Minh Anh", "Trần Thảo Nhi", "Lê Hoàng Nam", "Phạm Gia Hân", "Võ Quốc Bảo", "Đặng Ngọc Mai"];
  var pass = Pw.Hash("123456");
  var customers = names.Select((n, i) => new Customer { FullName = n, Email = $"khach{i + 1}@example.com", Phone = $"09000000{i + 10}", Address = $"{10 + i} Đường Số {i + 1}, TP.HCM", PasswordHash = pass, CreatedAt = DateTime.Today.AddDays(-40 + i * 3) }).ToList();
  customers[5].IsLocked = true;
  db.Customers.AddRange(customers); db.SaveChanges();
  if (products.Count == 0) return;
  for (var d = 29; d >= 0; d--) {
   for (var k = rnd.Next(0, 4); k > 0; k--) {
    var o = new Order { CustomerId = customers[rnd.Next(5)].Id, CreatedAt = DateTime.Today.AddDays(-d).AddHours(8 + rnd.Next(12)), CreatedBy = staff[rnd.Next(staff.Count)] };
    foreach (var p in products.OrderBy(_ => rnd.Next()).Take(rnd.Next(1, 4))) o.Items.Add(new OrderItem { ProductId = p.Id, ProductName = p.Name, UnitPrice = p.Price, Quantity = rnd.Next(1, 4) });
    o.Status = d > 2 ? (rnd.Next(10) == 0 ? OrderStatus.Cancelled : OrderStatus.Done) : OrderStatus.All[rnd.Next(5)];
    if (o.Status != OrderStatus.New && o.Status != OrderStatus.Cancelled) (o.ConfirmedBy, o.ConfirmedAt) = (staff[rnd.Next(staff.Count)], o.CreatedAt.AddMinutes(10 + rnd.Next(50)));
    db.Orders.Add(o);
   }
  }
  // Phiếu nhập: các xưởng bánh giao bánh thành phẩm; 4 phiếu đã nhập kho và 1 phiếu đang chờ xác nhận.
  var suppliers = db.Suppliers.AsNoTracking().Select(s => s.Name).ToList();
  for (var i = 0; i < 5; i++) {
   var pu = new Purchase { Supplier = suppliers[i % suppliers.Count], CreatedAt = DateTime.Today.AddDays(i == 4 ? 0 : -i * 7 - 1), CreatedBy = staff[0] };
   foreach (var p in products) pu.Items.Add(new PurchaseItem { ProductId = p.Id, ItemName = p.Name, Unit = p.Unit, Quantity = rnd.Next(15, 31), UnitCost = Math.Round(p.Price * 0.55m / 1000) * 1000 });
   if (i < 4) (pu.Status, pu.ReceivedAt, pu.ReceivedBy) = (PurchaseStatus.Received, pu.CreatedAt.AddHours(15), staff[0]);
   db.Purchases.Add(pu);
  }
  string[] reasons = ["Hết hạn", "Hỏng / rơi vỡ", "Làm lỗi", "Ế cuối ngày"];
  for (var d = 0; d < 9; d++) { var p = products[rnd.Next(products.Count)]; db.Discards.Add(new Discard { ProductId = p.Id, ProductName = p.Name, Unit = p.Unit, Quantity = rnd.Next(1, 4), UnitPrice = p.Price, Reason = reasons[rnd.Next(reasons.Length)], CreatedAt = DateTime.Today.AddDays(-d * 3).AddHours(18), CreatedBy = staff[rnd.Next(staff.Count)] }); }
  db.SaveChanges();
 }
}
