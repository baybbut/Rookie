using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rookie.Data;
using Rookie.Models;
namespace Rookie.Controllers.Api;
public record CartLine(int ProductId, [Range(1, 99)] int Quantity);
public record CheckoutReq([Required, MinLength(1), MaxLength(50)] List<CartLine> Items, [StringLength(500)] string? Note);

// Cho website khách (React): xem bánh công khai, khách đã đăng nhập thì đặt hàng / xem đơn của mình.
[ApiController, Route("api")]
public class ShopApiController(ShopDbContext db) : ControllerBase {
 IQueryable<Product> Visible => db.Products.AsNoTracking().Where(p => !db.Categories.Any(c => c.Name == p.Category && c.IsHidden));

 [HttpGet("categories")]
 public Task<List<string>> Categories() => db.Categories.AsNoTracking().Where(c => !c.IsHidden).OrderBy(c => c.Name).Select(c => c.Name).ToListAsync();

 [HttpGet("products")]
 public Task<List<Product>> Products(string? category) => Visible.Where(p => category == null || p.Category == category).OrderBy(p => p.Id).ToListAsync();

 [HttpGet("products/{id:int}")]
 public async Task<IActionResult> Product(int id) => await Visible.FirstOrDefaultAsync(p => p.Id == id) is { } p ? Ok(p) : NotFound();

 [Authorize(AuthenticationSchemes = Roles.Scheme, Roles = Roles.Customer), HttpPost("orders")]
 public async Task<IActionResult> Checkout(CheckoutReq r) {
  var (o, err) = await BuildOrder(db, await db.Customers.FindAsync(CustomerId), r.Items, r.Note, Visible);
  if (o is null) return BadRequest(new { error = err });
  db.Orders.Add(o); await db.SaveChangesAsync();
  return CreatedAtAction(nameof(MyOrder), new { id = o.Id }, o);
 }

 [Authorize(AuthenticationSchemes = Roles.Scheme, Roles = Roles.Customer), HttpGet("orders/mine")]
 public Task<List<Order>> MyOrders() => db.Orders.AsNoTracking().Include(o => o.Items).Where(o => o.CustomerId == CustomerId).OrderByDescending(o => o.CreatedAt).ToListAsync();

 [Authorize(AuthenticationSchemes = Roles.Scheme, Roles = Roles.Customer), HttpGet("orders/mine/{id:int}")]
 public async Task<IActionResult> MyOrder(int id) => await db.Orders.AsNoTracking().Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id && o.CustomerId == CustomerId) is { } o ? Ok(o) : NotFound();

 // Khách chỉ được hủy đơn khi cửa hàng chưa bắt đầu làm.
 [Authorize(AuthenticationSchemes = Roles.Scheme, Roles = Roles.Customer), HttpPost("orders/mine/{id:int}/cancel")]
 public async Task<IActionResult> CancelMyOrder(int id) {
  var o = await db.Orders.FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == CustomerId);
  if (o is null) return NotFound();
  if (o.Status != OrderStatus.New) return Conflict(new { error = $"Đơn đang ở trạng thái “{o.Status}”, không hủy được." });
  o.Status = OrderStatus.Cancelled; await db.SaveChangesAsync();
  return NoContent();
 }

 int CustomerId => int.Parse(User.FindFirstValue("sub")!);

 // Nghiệp vụ tạo đơn dùng chung cho khách và admin: chụp tên/giá tại thời điểm đặt, gộp dòng trùng, tối đa 99/sp.
 internal static async Task<(Order?, string?)> BuildOrder(ShopDbContext db, Customer? customer, List<CartLine> items, string? note, IQueryable<Product> source) {
  if (customer is null) return (null, "Không tìm thấy khách hàng.");
  if (customer.IsLocked) return (null, "Tài khoản khách này đang bị khóa.");
  var ids = items.Select(i => i.ProductId).Distinct().ToList();
  var products = await source.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
  var o = new Order { CustomerId = customer.Id, Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim() };
  foreach (var g in items.GroupBy(i => i.ProductId)) {
   if (!products.TryGetValue(g.Key, out var p)) return (null, $"Sản phẩm #{g.Key} không tồn tại hoặc đã ngừng bán.");
   o.Items.Add(new OrderItem { ProductId = p.Id, ProductName = p.Name, UnitPrice = p.Price, Quantity = Math.Clamp(g.Sum(i => i.Quantity), 1, 99) });
  }
  return o.Items.Count == 0 ? (null, "Hãy chọn ít nhất một chiếc bánh.") : (o, null);
 }
}
