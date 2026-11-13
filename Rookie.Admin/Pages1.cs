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
 // Số liệu do server tính (GET /api/admin/dashboard), cùng công thức với trang /admin.
 public override void Reload() {
  var d = Api.Get<DashboardVm>("api/admin/dashboard");
  vals["rev"].Text = Fmt.Vnd(d.RevenueToday); vals["month"].Text = Fmt.Vnd(d.RevenueMonth); vals["buy"].Text = Fmt.Vnd(d.PurchaseMonth);
  vals["new"].Text = d.NewOrders.ToString(); vals["open"].Text = d.OpenOrders.ToString(); vals["cus"].Text = d.Customers.ToString(); vals["prod"].Text = d.Products.ToString();
  chart.Set(d.Days.Select(p => (Fmt.Day(p.Day), p.Revenue, $"{Fmt.Day(p.Day)} · {Fmt.Vnd(p.Revenue)} · {p.Orders} đơn")));
  top.Load(d.TopProducts, t => [t.Name, t.Count]);
  recent.Load(d.Recent, o => [$"#{o.Id}", o.Customer?.FullName, Fmt.Date(o.CreatedAt), Fmt.Vnd(o.Total), o.Status], (r, o) => Grid.Tint(r, 4, Ui.OrderColor(o.Status)));
 }
}

class CategoriesPage : Page {
 public CategoriesPage() : base("Categories", "Quản lý danh mục") {
  G.Setup("Tên danh mục", ">Số sản phẩm", "Trên trang bán bánh");
  Act("Thêm danh mục", Create, primary: true); Act("Đổi tên", Rename); Act("Ẩn / hiện", Toggle); Act("Xóa", Delete, danger: true);
  Note("Đổi tên áp dụng cho mọi bánh trong danh mục. Chỉ xóa được danh mục không còn bánh. Danh mục bị ẩn sẽ không hiện bánh của nó trên trang bán bánh.");
 }
 public override void Reload() =>
  G.Load(Api.Get<List<CategoryRow>>("api/admin/categories"), c => [c.Name, c.Count, c.Hidden ? "Đang ẩn" : "Hiển thị"], (r, c) => { if (c.Hidden) r.DefaultCellStyle.ForeColor = Ui.Muted; });
 static string? CheckName(string? n) { n = n?.Trim(); return string.IsNullOrEmpty(n) || n.Length > 40 ? "Tên danh mục không hợp lệ (1–40 ký tự)." : null; }
 void Create() {
  using var f = new FieldForm("Thêm danh mục", "Thêm"); var t = f.Add("Tên danh mục", FieldForm.Txt(null, 40));
  f.Check = () => CheckName(t.Text) ?? Ui.Try(() => Api.Post("api/admin/categories", new { name = t.Text.Trim(), isHidden = false }));
  if (f.ShowDialog() == DialogResult.OK) Note($"Đã thêm danh mục “{t.Text.Trim()}”.");
 }
 void Rename() {
  if (G.Pick<CategoryRow>() is not { } c) return; using var f = new FieldForm("Đổi tên danh mục"); var t = f.Add("Tên danh mục", FieldForm.Txt(c.Name, 40));
  f.Check = () => CheckName(t.Text) ?? (t.Text.Trim() == c.Name ? null : Ui.Try(() => Api.Put<Category>($"api/admin/categories/{c.Id}", new { name = t.Text.Trim(), isHidden = c.Hidden })));
  if (f.ShowDialog() == DialogResult.OK && t.Text.Trim() != c.Name) Note($"Đã đổi “{c.Name}” thành “{t.Text.Trim()}” ({c.Count} sản phẩm).");
 }
 void Toggle() {
  if (G.Pick<CategoryRow>() is not { } c) return;
  var x = Api.Put<Category>($"api/admin/categories/{c.Id}", new { name = c.Name, isHidden = !c.Hidden });
  Note(x.IsHidden ? $"Đã ẩn “{x.Name}” khỏi trang bán bánh." : $"Đã hiện lại “{x.Name}”.");
 }
 void Delete() {
  if (G.Pick<CategoryRow>() is not { } c) return;
  if (c.Count > 0) { Ui.Err($"Không xóa được “{c.Name}” vì vẫn còn bánh trong danh mục này."); return; }
  if (!Ui.Ask($"Xóa danh mục “{c.Name}”?")) return; Api.Delete($"api/admin/categories/{c.Id}"); Note($"Đã xóa danh mục “{c.Name}”.");
 }
}

class ProductsPage : Page {
 readonly TextBox code, name; readonly ComboBox cat, status;
 public ProductsPage() : base("Products", "Quản lý sản phẩm") {
  code = Add("Mã bánh", new TextBox()); name = Add("Tên bánh", new TextBox(), 200); cat = Add("Danh mục", Combo("Tất cả", Cats())); status = Add("Trạng thái", Combo("Tất cả", ProductStatus.Options.Select(o => o.Label))); ClearButton();
  G.Setup("Mã|0.6", "Tên bánh|3", "Danh mục|1", ">Giá|1", "Đơn vị|0.6", "Trạng thái|1.1");
  Act("Thêm bánh mới", () => Edit(null), primary: true); Act("Sửa", () => { if (G.Pick<Product>() is { } p) Edit(p); }); Act("Xóa", Delete, danger: true); G.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0 && G.Pick<Product>() is { } p) { Edit(p); Reload(); } };
 }
 static List<string> Cats() => Api.Get<List<CategoryRow>>("api/admin/categories").Select(c => c.Name).ToList();
 public override void Reload() {
  var st = status.SelectedIndex <= 0 ? null : ProductStatus.Options[status.SelectedIndex - 1].Key; var sel = Sel(cat);
  var list = Api.Get<List<Product>>("api/admin/products").Where(p => Svc.Has(p.Code, code.Text) && Svc.Has(p.Name, name.Text) && (sel is null || p.Category == sel) && (st is null || ProductStatus.Key(p) == st)).ToList();
  G.Load(list, p => [p.Code, $"{p.Name}  —  {p.Description}", p.Category, Fmt.Vnd(p.Price), p.Unit, Label(p)]); Note($"{list.Count} sản phẩm");
 }
 static string Label(Product p) => p.IsBestSeller && p.IsNew ? "Bán chạy · Mới" : p.IsBestSeller ? "Bán chạy nhất" : p.IsNew ? "Mới ra lò" : "Bình thường";
 void Edit(Product? cur) {
  var p = cur ?? new Product(); var isNew = cur is null; Product? saved = null;
  using var f = new FieldForm(isNew ? "Thêm bánh mới" : $"Sửa bánh — {p.Name}", isNew ? "Thêm bánh" : "Lưu thay đổi", 340);
  var tCode = f.Add("Mã bánh", FieldForm.Txt(p.Code, 20)); var tName = f.Add("Tên bánh", FieldForm.Txt(p.Name, 80)); var tDesc = f.Add("Mô tả ngắn", FieldForm.Txt(p.Description, 200));
  var cCat = f.Add("Danh mục", FieldForm.Combo(Cats().Cast<object>(), p.Category)); var cSt = f.Add("Trạng thái", FieldForm.Combo(ProductStatus.Options.Select(o => (object)o.Label), ProductStatus.Options.First(o => o.Key == ProductStatus.Key(p)).Label));
  var nPrice = f.Add("Giá (₫)", FieldForm.Num(p.Price == 0 ? 50000 : p.Price, 1000, 100000000, 1000)); var cUnit = f.Add("Đơn vị bán", FieldForm.Combo(ProductStatus.Units, p.Unit));
  var tImg = f.Add("Mã ảnh Unsplash", FieldForm.Txt(p.Image, 120)); f.AddWide(Ui.Lbl("Để trống mã bánh để tự tạo (VD: SP007). Mã ảnh là phần ID sau images.unsplash.com/, VD photo-1464305795204-6f5bbfc7fb81.", Ui.Small, Ui.Muted));
  f.Check = () => {
   if (tName.Text.Trim() == "") return "Hãy nhập tên bánh."; if (tDesc.Text.Trim() == "") return "Hãy nhập mô tả ngắn."; if (cCat.SelectedItem is null) return "Hãy chọn danh mục."; if (tImg.Text.Trim() == "") return "Hãy nhập mã ảnh.";
   // Mã trống thì server tự sinh SPxxx; trùng mã, sai danh mục/đơn vị do server kiểm tra.
   var body = new Product { Code = tCode.Text.Trim(), Name = tName.Text.Trim(), Description = tDesc.Text.Trim(), Category = (string)cCat.SelectedItem!, Price = nPrice.Value, Unit = (string)cUnit.SelectedItem!, Image = tImg.Text.Trim() };
   ProductStatus.Apply(body, ProductStatus.Options[cSt.SelectedIndex].Key);
   return Ui.Try(() => saved = isNew ? Api.Post<Product>("api/admin/products", body) : Api.Put<Product>($"api/admin/products/{p.Id}", body));
  };
  if (f.ShowDialog() == DialogResult.OK && saved is not null) Note($"Đã lưu “{saved.Name}” (mã {saved.Code}).");
 }
 void Delete() { if (G.Pick<Product>() is not { } p) return; if (!Ui.Ask($"Xóa “{p.Name}”? Không hoàn tác được.")) return; Api.Delete($"api/admin/products/{p.Id}"); Note($"Đã xóa “{p.Name}”."); }
}
