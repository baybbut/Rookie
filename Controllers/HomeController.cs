using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rookie.Data;
namespace Rookie.Controllers;
public class HomeController(ShopDbContext db) : Controller {
 public async Task<IActionResult> Index() => View(await db.Products.AsNoTracking().Where(p => !db.Categories.Any(c => c.Name == p.Category && c.IsHidden)).OrderBy(p=>p.Id).ToListAsync());
}