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

- **Server** — `Program.cs` registers MVC and `ShopDbContext` (SQLite; connection string `ConnectionStrings:Shop`, defaulting to `Data Source=rookie.db`). On startup it calls `Database.EnsureCreated()` — there are **no EF migrations**, so schema changes require bumping `SchemaVersion` in `Data/DbInit.cs` (on startup an older `rookie.db` is renamed to `rookie.db.bak-*` and recreated with seed/demo data). Only route is `{controller=Home}/{action=Index}/{id?}`; `HomeController.Index` returns all `Product`s to `Views/Home/Index.cshtml`.
- **Seed data** — products are seeded via `HasData` in `Data/ShopDbContext.cs`. `Image` holds just an Unsplash photo ID (not a full URL); images need Internet.
- **Client (React islands)** — pages stay Razor; interactive parts are React components mounted on `<div data-island="name" data-*>` (data-* become props). Source in `client/src` (`main.jsx` mounts islands, `islands/Products.jsx` = home cake grid with tabs/search from `GET /api/products`, `islands/AddButton.jsx` = "+" buttons on `/Shop`, `islands/Cart.jsx` = cart page). Vite builds ONE file `wwwroot/js/islands/islands.js`, which is committed so `dotnet run` works without Node; after editing `client/src` run `npm run build` (or `npm run watch`) in `client/` and commit the rebuilt file. Islands call `/api/cart` (`CartApiController`) with the customer cookie, not JWT; because it is cookie-authenticated it keeps the global antiforgery check, and `client/src/api.js` sends the token from `_Layout`'s `#csrf` in the `RequestVerificationToken` header. Cart contents still live in the server session via `CartService`, so Razor checkout reads the same cart. `main.jsx` also updates `#cartCount` on the `rookie:cart` event and wires `#searchToggle`; islands reuse `#toast`. `client/` is excluded from the .NET project items (node_modules).
- Admin lives under `/admin` (`AdminController*.cs` partials: products/categories, customers, orders, warehouse = purchases/stock/discards, reports + Excel export, staff). Login is by `Staff` username/password (PBKDF2 in `Data/Pw.cs`); the first `admin` account is seeded from `Admin:Password` (default `admin` in Development). PDF export is the browser print dialog (`reports?print=true`); Excel is a hand-built .xlsx (`Data/Xlsx.cs`). Storefront accounts, cart (server session) and COD checkout are in `AccountController`/`ShopController`/`OrdersController` and write the same `Customer`/`Order` tables (`Models/Sales.cs`) the admin uses; `Models/Commerce.cs` holds only storefront form inputs. Two cookie schemes (`Auth` class in `AccountController.cs`): `Auth.Customer` ("Rookie", default) for the storefront and `Auth.Staff` for `/admin`, which must name it explicitly in `[Authorize]`/`SignInAsync`. Reviews/contact info are placeholder content.
- **Web API** (`Controllers/Api/`, routes under `/api`) for the planned React storefront and WinForms admin. JWT bearer scheme `Auth.Api`; signing key `Jwt:Key` (≥ 32 chars, otherwise random per start, so tokens die on restart). Claims are short names (`sub`, `name`, `role`; `MapInboundClaims = false`). Roles: `Roles.Customer` ("Customer") and the staff's own `Staff.Role` ("Quản trị"/"Nhân viên"); `Roles.Staff` allows both, staff management needs `StaffRole.Admin`. `OnTokenValidated` re-checks the DB on every request (locked/deleted account or changed staff role → 401). API controllers need `[IgnoreAntiforgeryToken]` because `Program.cs` adds `AutoValidateAntiforgeryToken` globally. Business rules are shared, not copied, where possible: `AdminController.BuildDashboard`/`BuildReport`/`StockRows` are `internal static` for the API, `ShopApiController.BuildOrder` builds orders for both customer and staff endpoints, and the API reuses `RegisterInput`/`LoginInput`/`ProfileInput`. `DashboardVm`/`ReportVm` use properties (not fields) so they serialize and show in Swagger; `PasswordHash` and `Customer.Orders` are `[JsonIgnore]`. Swagger UI (`/swagger`, Swashbuckle) is enabled only in Development and lists only `api/` routes. Note `dotnet run` applies `Properties/launchSettings.json` (Development, port 5000) and overrides `ASPNETCORE_ENVIRONMENT`; use `--no-launch-profile` to test Production behaviour.

## Conventions

Existing code is written in a very compact style (primary constructors, one-line statements, dense JS). Match it when editing nearby code.
