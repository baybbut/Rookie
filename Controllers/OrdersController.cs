using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rookie.Data;
using Rookie.Models;
using Rookie.Services;
namespace Rookie.Controllers;
// Đơn khách đặt ghi thẳng vào rookie.db với trạng thái "Mới", nên hiện ngay trong trang quản trị và app WinForms để nhân viên xác nhận.
[Authorize(AuthenticationSchemes=Auth.Customer)]
public class OrdersController(ShopDbContext db,CartService cart):Controller {
 int UserId=>int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
 public async Task<IActionResult> Index()=>View(await db.Orders.Include(o=>o.Items).Where(o=>o.CustomerId==UserId).OrderByDescending(o=>o.Id).ToListAsync());
 public async Task<IActionResult> Details(int id){var order=await db.Orders.Include(o=>o.Items).SingleOrDefaultAsync(o=>o.Id==id&&o.CustomerId==UserId);return order==null?NotFound():View(order);}
 public async Task<IActionResult> Success(int id){var order=await db.Orders.Include(o=>o.Items).SingleOrDefaultAsync(o=>o.Id==id&&o.CustomerId==UserId);return order==null?NotFound():View(order);}
 [HttpGet] public async Task<IActionResult> Checkout(){var lines=await cart.Lines();if(lines.Count==0)return RedirectToAction("Cart","Shop");var c=await db.Customers.FindAsync(UserId);if(c==null)return Challenge();var key=HttpContext.Session.GetString("checkout-key")??Guid.NewGuid().ToString("N");HttpContext.Session.SetString("checkout-key",key);ViewBag.Lines=lines;return View(new CheckoutInput{Name=c.FullName,Phone=c.Phone,Address=c.Address??"",RequestKey=key});}
 [HttpPost] public async Task<IActionResult> Checkout(CheckoutInput input){
 if(!string.IsNullOrEmpty(input.RequestKey)){var previous=await db.Orders.SingleOrDefaultAsync(o=>o.CustomerId==UserId&&o.RequestKey==input.RequestKey);if(previous!=null)return RedirectToAction("Success",new{id=previous.Id});}
 var lines=await cart.Lines();
 if(lines.Count==0)return RedirectToAction("Cart","Shop");
 var customer=await db.Customers.FindAsync(UserId);if(customer==null)return Challenge();
 if(customer.IsLocked)ModelState.AddModelError("","Tài khoản này đang bị khóa nên chưa đặt được đơn. Vui lòng liên hệ tiệm.");
 if(input.RequestKey!=HttpContext.Session.GetString("checkout-key")||string.IsNullOrEmpty(input.RequestKey))ModelState.AddModelError("","Phiên đặt hàng đã hết hạn. Vui lòng mở lại trang đặt hàng.");
 if(!ModelState.IsValid){ViewBag.Lines=lines;return View(input);}
 var order=new Order{CustomerId=UserId,ShipName=input.Name.Trim(),ShipPhone=input.Phone.Trim(),ShipAddress=input.Address.Trim(),Note=string.IsNullOrWhiteSpace(input.Note)?null:input.Note.Trim(),RequestKey=input.RequestKey,Items=lines.Select(l=>new OrderItem{ProductId=l.Product.Id,ProductName=l.Product.Name,UnitPrice=l.Product.Price,Quantity=l.Quantity}).ToList()};
 db.Orders.Add(order);
 try { await db.SaveChangesAsync(); }
 catch(DbUpdateException) {
 db.ChangeTracker.Clear();
 var existing=await db.Orders.SingleOrDefaultAsync(o=>o.CustomerId==UserId&&o.RequestKey==input.RequestKey);
 if(existing==null)throw;
 cart.Clear();return RedirectToAction("Success",new{id=existing.Id});
 }
 cart.Clear();HttpContext.Session.Remove("checkout-key");return RedirectToAction("Success",new{id=order.Id});
 }
}
