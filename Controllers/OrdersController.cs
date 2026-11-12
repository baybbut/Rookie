using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rookie.Data;
using Rookie.Models;
using Rookie.Services;
namespace Rookie.Controllers;
[Authorize]
public class OrdersController(CommerceDbContext db,CartService cart):Controller {
 int UserId=>int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
 public async Task<IActionResult> Index()=>View(await db.Orders.Include(o=>o.Items).Where(o=>o.CustomerId==UserId).OrderByDescending(o=>o.Id).ToListAsync());
 public async Task<IActionResult> Details(int id){var order=await db.Orders.Include(o=>o.Items).SingleOrDefaultAsync(o=>o.Id==id&&o.CustomerId==UserId);return order==null?NotFound():View(order);}
 public async Task<IActionResult> Success(int id){var order=await db.Orders.Include(o=>o.Items).SingleOrDefaultAsync(o=>o.Id==id&&o.CustomerId==UserId);return order==null?NotFound():View(order);}
 [HttpGet] public async Task<IActionResult> Checkout(){var lines=await cart.Lines();if(lines.Count==0)return RedirectToAction("Cart","Shop");var c=await db.Customers.FindAsync(UserId);if(c==null)return Challenge();var key=HttpContext.Session.GetString("checkout-key")??Guid.NewGuid().ToString("N");HttpContext.Session.SetString("checkout-key",key);ViewBag.Lines=lines;return View(new CheckoutInput{Name=c.Name,Phone=c.Phone,Address=c.Address,RequestKey=key});}
 [HttpPost] public async Task<IActionResult> Checkout(CheckoutInput input){
 if(!string.IsNullOrEmpty(input.RequestKey)){var previous=await db.Orders.SingleOrDefaultAsync(o=>o.CustomerId==UserId&&o.RequestKey==input.RequestKey);if(previous!=null)return RedirectToAction("Success",new{id=previous.Id});}
 var lines=await cart.Lines();
 if(lines.Count==0)return RedirectToAction("Cart","Shop");
 if(input.RequestKey!=HttpContext.Session.GetString("checkout-key")||string.IsNullOrEmpty(input.RequestKey))ModelState.AddModelError("","Phiên đặt hàng đã hết hạn. Vui lòng mở lại trang đặt hàng.");
 if(!ModelState.IsValid){ViewBag.Lines=lines;return View(input);}
 var order=new Order{CustomerId=UserId,Name=input.Name.Trim(),Phone=input.Phone.Trim(),Address=input.Address.Trim(),Note=input.Note?.Trim()??"",Payment=input.Payment,RequestKey=input.RequestKey,Items=lines.Select(l=>new OrderItem{ProductId=l.Product.Id,Name=l.Product.Name,Price=l.Product.Price,Quantity=l.Quantity}).ToList()};
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
