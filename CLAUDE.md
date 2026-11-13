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

A bakery storefront plus an admin back office (all UI text is Vietnamese).

- **Startup** — `Program.cs` registers MVC, cookie auth (`rookie-admin` cookie, login path `/admin/login`) and `ShopDbContext` (SQLite; connection string `ConnectionStrings:Shop`, default `Data Source=rookie.db`). On startup it calls `Database.EnsureCreated()` then `DemoData.Seed(db)`. There are **no EF migrations**, so any model change needs `rookie.db` deleted before it takes effect.
- **Data** — `Products` and `Categories` are seeded via `HasData` in `Data/ShopDbContext.cs`. `Data/DemoData.cs` fills in sample customers, 30 days of orders and purchases, but only when those tables are empty, so the dashboard and reports have numbers. `Product.Category` is a **string matching `Category.Name`**, not a foreign key. Renaming a category in admin rewrites the products' strings with `ExecuteUpdateAsync`, and a hidden category (`IsHidden`) removes its products from the storefront in `HomeController.Index`. `Product.Image` holds just an Unsplash photo ID (images need Internet).
- **Sales models** (`Models/Sales.cs`) — `Customer`, `Order`/`OrderItem`, `Purchase`/`PurchaseItem` (ingredient stock-in), plus the view models (`DashboardVm`, `ReportVm`) and the `Fmt` helpers (VND/date formatting, status CSS class). `OrderStatus` uses Vietnamese string constants that are stored as-is in the DB. `OrderItem` snapshots `ProductName`/`UnitPrice` at order time. `Total`/`Subtotal` are computed properties, so aggregates load entities and sum in memory.
- **Storefront** — `HomeController.Index` → `Views/Home/Index.cshtml`; `wwwroot/js/site.js` is vanilla JS driving filtering, search and the cart client-side, working off the server-rendered DOM (card `data-name`, flag attributes read as `p.dataset[filter]`, `.add` buttons with `data-id/name/price`). The cart persists in `localStorage` under `rookie-cart` (validated on load, qty capped at 99). site.js queries elements (`#search`, `#cartDialog`, `#cartItems`, `#cartTotal`, `#toast`, …) without null checks, so when changing the view's markup keep those IDs and data attributes in sync. There is no customer checkout: storefront carts never become `Order`s.
- **Admin** — `AdminController` is a `partial` class split over `AdminController.cs` (login, dashboard, products, categories) and `AdminController.Sales.cs` (customers, orders, purchases, reports). It uses attribute routing under `[Authorize, Route("admin")]`, not the conventional route. Every POST uses `[ValidateAntiForgeryToken]`, and model binding is restricted with `[Bind]`. There is a single shared password from config `Admin:Password`, defaulting to `admin` only in Development. Outside Development with no password set, login is disabled. Views use `Views/Shared/_AdminLayout.cshtml` (via `Views/Admin/_ViewStart.cshtml`) and `wwwroot/css/admin.css`. Charts are server-rendered HTML/CSS (`_RevenueChart.cshtml`), with no JS chart library. Flash messages go through `TempData["Msg"]`.
- **Web API** (`Controllers/Api/`, routes under `/api`) — for the planned React storefront and WinForms admin. JWT bearer auth (scheme `Roles.Scheme` = "Bearer", added alongside the cookie scheme, which stays the default for MVC). Signing key from `Jwt:Key` (≥ 32 chars); if missing a random key is generated at startup, so tokens die on restart. Claims are short names (`sub`, `name`, `role`, `MapInboundClaims = false`). Two roles: `Admin` (login with the same `Admin:Password` via `POST /api/auth/admin`) and `Customer` (`register`/`login`, password stored in `Customer.PasswordHash` with `PasswordHasher`; customers created by admin have no hash and cannot log in). `ShopApiController` = public catalog (hidden categories filtered) + customer checkout/own orders; `AdminApiController` = everything the `/admin` pages do. Dashboard/report numbers come from `AdminController.BuildDashboard`/`BuildReport`, and order creation from `ShopApiController.BuildOrder`, shared with the API. Auth endpoints are rate-limited (`"auth"` policy, 10/min per IP); CORS origins from `Cors:Origins`. JSON uses `IncludeFields` because the view models use fields; `PasswordHash` and `Customer.Orders` are `[JsonIgnore]`.
- Reviews and contact info on the storefront are placeholder content.

## Conventions

Existing code is written in a very compact style (primary constructors, one-line statements, dense JS, single-space indentation in C#). Match it when editing nearby code.

Luôn trả lời bằng tiếng Việt. Giữ nguyên tên biến, lệnh và thuật ngữ kỹ thuật bằng tiếng Anh.