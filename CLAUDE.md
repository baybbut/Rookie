# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

Single ASP.NET Core MVC project (.NET 8, EF Core SQLite). No test project or linter is configured.

```sh
dotnet restore
dotnet build
dotnet run --urls http://0.0.0.0:5080   # README's documented run command
```

## Architecture

A single-page bakery storefront (UI text is Vietnamese). Server and client split:

- **Server** — `Program.cs` registers MVC and `ShopDbContext` (SQLite; connection string `ConnectionStrings:Shop`, defaulting to `Data Source=rookie.db`). On startup it calls `Database.EnsureCreated()` — there are **no EF migrations**, so schema changes to `Models/Product.cs` require deleting `rookie.db` to take effect. Only route is `{controller=Home}/{action=Index}/{id?}`; `HomeController.Index` returns all `Product`s to `Views/Home/Index.cshtml`.
- **Seed data** — products are seeded via `HasData` in `Data/ShopDbContext.cs`. `Image` holds just an Unsplash photo ID (not a full URL); images need Internet.
- **Client** — `wwwroot/js/site.js` is vanilla JS driving everything interactive, working off the server-rendered DOM: product cards expose `data-name`, `data-best`/`data-new`-style flags (read as `p.dataset[filter]`), and `.add` buttons carry `data-id/name/price`. Filtering, search, cart and quantity updates all happen client-side; the cart persists in `localStorage` under `rookie-cart` (validated on load, qty capped at 99). When changing product markup in the view, keep these data attributes and element IDs (`#search`, `#cartDialog`, `#cartItems`, `#cartTotal`, `#toast`, etc.) in sync with `site.js`, which queries them unguarded.
- No checkout, admin, or API exists yet; reviews/contact info are placeholder content.

## Conventions

Existing code is written in a very compact style (primary constructors, one-line statements, dense JS). Match it when editing nearby code.
