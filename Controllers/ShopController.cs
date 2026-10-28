using Microsoft.AspNetCore.Mvc;
using Rookie.Data;
using Rookie.Services;
namespace Rookie.Controllers;
public class ShopController(ShopDbContext db,CartService cart):Controller {
 public async Task<IActionResult> Details(int id){var p=await db.Products.FindAsync(id);return p==null?NotFound():View(p);}
 public async Task<IActionResult> Cart()=>View(await cart.Lines());
 [HttpPost] public async Task<IActionResult> Add(int id,int quantity=1){if(quantity<1||!await cart.Change(id,quantity,true))return BadRequest();if(Request.Headers.Accept.ToString().Contains("application/json"))return Json(new{count=(await cart.Lines()).Sum(l=>l.Quantity)});return RedirectToAction("Cart");}
 [HttpPost] public async Task<IActionResult> Update(int id,int quantity){if(!await cart.Change(id,quantity))return BadRequest();return RedirectToAction("Cart");}
}
