using Microsoft.EntityFrameworkCore;
using Rookie.Data;
using Rookie.Models;
namespace Rookie.Admin;

class DashboardPage : Page {
 readonly FlowLayoutPanel stats = new() { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Margin = new Padding(0, 0, 0, 12) };
 readonly Dictionary<string, Label> vals = [];
 readonly BarChart chart = new() { Dock = DockStyle.Fill }; readonly Grid top = new(), recent = new();
 public DashboardPage() : base("Dashboard", "Tiệm bánh hôm nay") {
  foreach (var (k, t, c) in new[] { ("rev", "Doanh thu hôm nay", 0), ("month", "Doanh thu tháng này", 1), ("buy", "Chi nhập hàng tháng này", 2), ("new", "Đơn mới chờ xử lý", 0), ("open", "Đơn đang xử lý", 1), ("cus", "Khách hàng", 2), ("prod", "Sản phẩm", 0) }) {
   var card = new Panel { Width = 190, Height = 82, Margin = new Padding(0, 0, 12, 12), BackColor = new[] { Color.FromArgb(238, 229, 216), Ui.Pink, Ui.Mint }[c] };
   var v = Ui.Lbl("—", Ui.Big); v.Left = 14; v.Top = 40; v.AutoSize = false; v.Width = 168; v.Height = 30; var s = Ui.Lbl(t, new Font("Segoe UI", 8.5f), Ui.Muted); s.Left = 14; s.Top = 14;
   card.Controls.AddRange([s, v]); vals[k] = v; stats.Controls.Add(card);
  }
  var mid = new TableLayoutPanel { Dock = DockStyle.Top, Height = 230, ColumnCount = 2, Margin = new Padding(0, 0, 0, 12) }; mid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58)); mid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
  mid.Controls.Add(Box("Doanh thu 7 ngày qua", chart), 0, 0); top.Setup("Bánh bán chạy (30 ngày)", ">Số lượng"); mid.Controls.Add(Box("Bán chạy (30 ngày)", top), 1, 0); top.ColumnHeadersVisible = false;
  recent.Setup("Mã", "Khách", "Thời gian", ">Tổng", "Trạng thái");
  var body = new TableLayoutPanel { ColumnCount = 1, RowCount = 3 }; body.RowStyles.Add(new RowStyle(SizeType.AutoSize)); body.RowStyles.Add(new RowStyle(SizeType.Absolute, 242)); body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
  body.Controls.Add(stats, 0, 0); body.Controls.Add(mid, 0, 1); body.Controls.Add(Box("Đơn hàng gần đây", recent), 0, 2); Body(body);
 }
 static Panel Box(string title, Control c) { var p = new Panel { Dock = DockStyle.Fill, BackColor = Ui.Card, Padding = new Padding(14, 40, 14, 10), Margin = new Padding(0, 0, 12, 0) }; var h = Ui.Lbl(title, new Font("Georgia", 12.5f, FontStyle.Bold)); h.Left = 14; h.Top = 10; c.Dock = DockStyle.Fill; p.Controls.Add(c); p.Controls.Add(h); return p; }
 public override void Reload() {
  using var db = Db.New(); var today = DateTime.Today; var month = new DateTime(today.Year, today.Month, 1);
  var orders = db.Orders.AsNoTracking().Include(o => o.Items).Include(o => o.Customer).ToList(); var live = orders.Where(o => o.Status != OrderStatus.Cancelled).ToList();
  var buy = db.Purchases.AsNoTracking().Include(p => p.Items).Where(p => p.Status == PurchaseStatus.Received).ToList().Where(p => p.CreatedAt >= month).Sum(p => p.Total);
  vals["rev"].Text = Fmt.Vnd(live.Where(o => o.CreatedAt >= today).Sum(o => o.Total)); vals["month"].Text = Fmt.Vnd(live.Where(o => o.CreatedAt >= month).Sum(o => o.Total)); vals["buy"].Text = Fmt.Vnd(buy);
  vals["new"].Text = orders.Count(o => o.Status == OrderStatus.New).ToString(); vals["open"].Text = orders.Count(o => o.Status is OrderStatus.New or OrderStatus.Confirmed or OrderStatus.Preparing or OrderStatus.Shipping).ToString();
  vals["cus"].Text = db.Customers.Count().ToString(); vals["prod"].Text = db.Products.Count().ToString();
  chart.Set(Enumerable.Range(0, 7).Select(i => { var d = today.AddDays(i - 6); var l = live.Where(o => o.CreatedAt.Date == d).ToList(); return (Fmt.Day(d), l.Sum(o => o.Total), $"{Fmt.Day(d)} · {Fmt.Vnd(l.Sum(o => o.Total))} · {l.Count} đơn"); }));
  top.Load(Svc.TopProducts(live.Where(o => o.CreatedAt >= today.AddDays(-29)), 5), t => [t.Name, t.Count]);
  recent.Load(orders.OrderByDescending(o => o.CreatedAt).Take(6), o => [$"#{o.Id}", o.Customer?.FullName, Fmt.Date(o.CreatedAt), Fmt.Vnd(o.Total), o.Status], (r, o) => Grid.Tint(r, 4, Ui.OrderColor(o.Status)));
 }
}

class CategoriesPage : Page {
 public CategoriesPage() : base("Categories", "Quản lý danh mục") {
  G.Setup("Tên danh mục", ">Số sản phẩm", "Trên trang bán bánh");
  Act("Thêm danh mục", Create, primary: true); Act("Đổi tên", Rename); Act("Ẩn / hiện", Toggle); Act("Xóa", Delete, danger: true);
  Note("Đổi tên áp dụng cho mọi bánh trong danh mục. Chỉ xóa được danh mục không còn bánh. Danh mục bị ẩn sẽ không hiện bánh của nó trên trang bán bánh.");
 }
 public override void Reload() {
  using var db = Db.New(); var counts = db.Products.AsNoTracking().GroupBy(p => p.Category).Select(g => new { g.Key, N = g.Count() }).ToDictionary(x => x.Key, x => x.N);
  G.Load(db.Categories.AsNoTracking().OrderBy(c => c.Name).ToList(), c => [c.Name, counts.GetValueOrDefault(c.Name), c.IsHidden ? "Đang ẩn" : "Hiển thị"], (r, c) => { if (c.IsHidden) r.DefaultCellStyle.ForeColor = Ui.Muted; });
 }
 static string? CheckName(string? n, ShopDbContext db, int self = 0) { n = n?.Trim(); return string.IsNullOrEmpty(n) || n.Length > 40 ? "Tên danh mục không hợp lệ (1–40 ký tự)." : db.Categories.Any(c => c.Name == n && c.Id != self) ? $"Danh mục “{n}” đã tồn tại." : null; }
 void Create() {
  using var db = Db.New(); using var f = new FieldForm("Thêm danh mục", "Thêm"); var t = f.Add("Tên danh mục", FieldForm.Txt(null, 40)); f.Check = () => CheckName(t.Text, db);
  if (f.ShowDialog() != DialogResult.OK) return; db.Categories.Add(new Category { Name = t.Text.Trim() }); db.SaveChanges(); Note($"Đã thêm danh mục “{t.Text.Trim()}”.");
 }
 void Rename() {
  if (G.Pick<Category>() is not { } c) return; using var db = Db.New(); using var f = new FieldForm("Đổi tên danh mục"); var t = f.Add("Tên danh mục", FieldForm.Txt(c.Name, 40)); f.Check = () => CheckName(t.Text, db, c.Id);
  if (f.ShowDialog() != DialogResult.OK || t.Text.Trim() == c.Name) return; var nn = t.Text.Trim();
  var cat = db.Categories.Find(c.Id)!; var old = cat.Name; cat.Name = nn; var n = db.Products.Where(p => p.Category == old).ExecuteUpdate(s => s.SetProperty(p => p.Category, nn)); db.SaveChanges(); Note($"Đã đổi “{old}” thành “{nn}” ({n} sản phẩm).");
 }
 void Toggle() { if (G.Pick<Category>() is not { } c) return; using var db = Db.New(); var cat = db.Categories.Find(c.Id)!; cat.IsHidden = !cat.IsHidden; db.SaveChanges(); Note(cat.IsHidden ? $"Đã ẩn “{cat.Name}” khỏi trang bán bánh." : $"Đã hiện lại “{cat.Name}”."); }
 void Delete() {
  if (G.Pick<Category>() is not { } c) return; using var db = Db.New();
  if (db.Products.Any(p => p.Category == c.Name)) { Ui.Err($"Không xóa được “{c.Name}” vì vẫn còn bánh trong danh mục này."); return; }
  if (!Ui.Ask($"Xóa danh mục “{c.Name}”?")) return; db.Categories.Remove(db.Categories.Find(c.Id)!); db.SaveChanges(); Note($"Đã xóa danh mục “{c.Name}”.");
 }
}

class ProductsPage : Page {
 readonly TextBox code, name; readonly ComboBox cat, status;
 public ProductsPage() : base("Products", "Quản lý sản phẩm") {
  code = Add("Mã bánh", new TextBox()); name = Add("Tên bánh", new TextBox(), 200); cat = Add("Danh mục", Combo("Tất cả", Cats())); status = Add("Trạng thái", Combo("Tất cả", ProductStatus.Options.Select(o => o.Label))); ClearButton();
  G.Setup("Mã|0.6", "Tên bánh|3", "Danh mục|1", ">Giá|1", "Đơn vị|0.6", "Trạng thái|1.1");
  Act("Thêm bánh mới", () => Edit(null), primary: true); Act("Sửa", () => { if (G.Pick<Product>() is { } p) Edit(p); }); Act("Xóa", Delete, danger: true); G.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0 && G.Pick<Product>() is { } p) { Edit(p); Reload(); } };
 }
 static List<string> Cats() { using var db = Db.New(); return db.Categories.AsNoTracking().OrderBy(c => c.Name).Select(c => c.Name).ToList(); }
 public override void Reload() {
  using var db = Db.New(); var st = status.SelectedIndex <= 0 ? null : ProductStatus.Options[status.SelectedIndex - 1].Key; var sel = Sel(cat);
  var list = db.Products.AsNoTracking().OrderBy(p => p.Id).ToList().Where(p => Svc.Has(p.Code, code.Text) && Svc.Has(p.Name, name.Text) && (sel is null || p.Category == sel) && (st is null || ProductStatus.Key(p) == st)).ToList();
  G.Load(list, p => [p.Code, $"{p.Name}  —  {p.Description}", p.Category, Fmt.Vnd(p.Price), p.Unit, Label(p)]); Note($"{list.Count} sản phẩm");
 }
 static string Label(Product p) => p.IsBestSeller && p.IsNew ? "Bán chạy · Mới" : p.IsBestSeller ? "Bán chạy nhất" : p.IsNew ? "Mới ra lò" : "Bình thường";
 void Edit(Product? cur) {
  using var db = Db.New(); var p = cur is null ? new Product() : db.Products.Find(cur.Id)!; var isNew = cur is null;
  using var f = new FieldForm(isNew ? "Thêm bánh mới" : $"Sửa bánh — {p.Name}", isNew ? "Thêm bánh" : "Lưu thay đổi", 340);
  var tCode = f.Add("Mã bánh", FieldForm.Txt(p.Code, 20)); var tName = f.Add("Tên bánh", FieldForm.Txt(p.Name, 80)); var tDesc = f.Add("Mô tả ngắn", FieldForm.Txt(p.Description, 200));
  var cCat = f.Add("Danh mục", FieldForm.Combo(Cats().Cast<object>(), p.Category)); var cSt = f.Add("Trạng thái", FieldForm.Combo(ProductStatus.Options.Select(o => (object)o.Label), ProductStatus.Options.First(o => o.Key == ProductStatus.Key(p)).Label));
  var nPrice = f.Add("Giá (₫)", FieldForm.Num(p.Price == 0 ? 50000 : p.Price, 1000, 100000000, 1000)); var cUnit = f.Add("Đơn vị bán", FieldForm.Combo(ProductStatus.Units, p.Unit));
  var tImg = f.Add("Mã ảnh Unsplash", FieldForm.Txt(p.Image, 120)); f.AddWide(Ui.Lbl("Để trống mã bánh để tự tạo (VD: SP007). Mã ảnh là phần ID sau images.unsplash.com/, VD photo-1464305795204-6f5bbfc7fb81.", Ui.Small, Ui.Muted));
  f.Check = () => {
   if (tName.Text.Trim() == "") return "Hãy nhập tên bánh."; if (tDesc.Text.Trim() == "") return "Hãy nhập mô tả ngắn."; if (cCat.SelectedItem is null) return "Hãy chọn danh mục."; if (tImg.Text.Trim() == "") return "Hãy nhập mã ảnh.";
   var c = tCode.Text.Trim().ToUpperInvariant(); if (c != "" && db.Products.Any(x => x.Code == c && x.Id != p.Id)) return $"Mã “{c}” đã được dùng cho bánh khác."; return null;
  };
  if (f.ShowDialog() != DialogResult.OK) return;
  var code = tCode.Text.Trim().ToUpperInvariant(); if (code == "") { var n = db.Products.Max(x => (int?)x.Id) ?? 0; do code = $"SP{++n:000}"; while (db.Products.Any(x => x.Code == code && x.Id != p.Id)); }
  (p.Code, p.Name, p.Description, p.Category, p.Price, p.Unit, p.Image) = (code, tName.Text.Trim(), tDesc.Text.Trim(), (string)cCat.SelectedItem!, nPrice.Value, (string)cUnit.SelectedItem!, tImg.Text.Trim());
  ProductStatus.Apply(p, ProductStatus.Options[cSt.SelectedIndex].Key); if (isNew) db.Products.Add(p); db.SaveChanges(); Note($"Đã lưu “{p.Name}” (mã {p.Code}).");
 }
 void Delete() { if (G.Pick<Product>() is not { } p) return; if (!Ui.Ask($"Xóa “{p.Name}”? Không hoàn tác được.")) return; using var db = Db.New(); db.Products.Remove(db.Products.Find(p.Id)!); db.SaveChanges(); Note($"Đã xóa “{p.Name}”."); }
}

