using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Rookie.Models;
using Rookie.Data;
using Rookie.Services;
namespace Rookie.Controllers;
public class ShopController(ShopDbContext db,CartService cart):Controller {
 public async Task<IActionResult> Index(string? q,string? category,string? kind,string? sort) {
 var query=db.Products.AsNoTracking().AsQueryable();

 if(!string.IsNullOrWhiteSpace(category))query=query.Where(p=>p.Category==category);
 if(kind=="new")query=query.Where(p=>p.IsNew);else if(kind=="best")query=query.Where(p=>p.IsBestSeller);
 var products=await query.ToListAsync();
 if(!string.IsNullOrWhiteSpace(q))products=products.Where(p=>p.Name.Contains(q.Trim(),StringComparison.OrdinalIgnoreCase)||p.Description.Contains(q.Trim(),StringComparison.OrdinalIgnoreCase)).ToList();
 products=sort switch {"price-asc"=>products.OrderBy(p=>p.Price).ToList(),"price-desc"=>products.OrderByDescending(p=>p.Price).ToList(),_=>products.OrderBy(p=>p.Id).ToList()};
 ViewBag.Categories=await db.Products.Select(p=>p.Category).Distinct().ToListAsync();
 ViewBag.Query=q;ViewBag.Category=category;ViewBag.Kind=kind;ViewBag.Sort=sort;
 return View(products);
 }
 public async Task<IActionResult> Details(int id){var p=await db.Products.FindAsync(id);return p==null?NotFound():View(p);}
 [Authorize] public async Task<IActionResult> Cart()=>View(await cart.Lines());
 [Authorize,HttpPost] public async Task<IActionResult> Add(int id,int quantity=1){if(quantity<1||!await cart.Change(id,quantity,true))return BadRequest();if(Request.Headers.Accept.ToString().Contains("application/json"))return Json(new{count=(await cart.Lines()).Sum(l=>l.Quantity)});return RedirectToAction("Cart");}
 [Authorize,HttpPost] public async Task<IActionResult> Update(int id,int quantity){if(!await cart.Change(id,quantity))return BadRequest();return RedirectToAction("Cart");}
}
