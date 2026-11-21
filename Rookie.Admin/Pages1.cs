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
  G.Setup("Mã|0.6", "Tên bánh|2.6", "Danh mục|1", ">Giá nhập|0.9", ">Lãi|0.5", ">Giá bán|0.9", "Đơn vị|0.6", "Trạng thái|1.1", "Trang bán|0.8");
  Act("Thêm bánh mới", () => Edit(null), primary: true); Act("Xem", View, reload: false); Act("Sửa", () => { if (G.Pick<Product>() is { } p) Edit(p); }); Act("Ẩn / hiện", Hide); Act("Xóa", Delete, danger: true);
  G.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) View(); };
 }
 // Xem thông tin bánh (chỉ xem): ảnh, giá, lợi nhuận, tồn kho.
 void View() {
  if (G.Pick<Product>() is not { } p) return; var s = Api.Get<List<StockRow>>("api/admin/stock").FirstOrDefault(x => x.Product.Id == p.Id);
  var quotes = Api.Get<List<SupplierPriceRow>>($"api/admin/supplier-prices?productId={p.Id}");
  var g = new Grid().Setup("Xưởng|2", ">Giá nhập|1", "|1"); g.Load(quotes, q => [q.Supplier, Fmt.Vnd(q.Price), q.IsMain ? "Xưởng chính" : q.SupplierActive ? "" : "Ngừng hợp tác"], (r, q) => { if (q.IsMain) r.DefaultCellStyle.Font = Ui.Bold; });
  Viewer.Show(this, $"Bánh {p.Code} — {p.Name}", [("Mã bánh", p.Code), ("Tên bánh", p.Name), ("Mô tả", p.Description), ("Danh mục", p.Category), ("Đơn vị bán", p.Unit),
   ("Xưởng chính", quotes.FirstOrDefault(q => q.IsMain)?.Supplier), ("Giá nhập", Fmt.Vnd(p.CostPrice)), ("Tỉ lệ lợi nhuận", $"{p.Margin:0.##} %"), ("Giá bán", Fmt.Vnd(p.Price)), ("Lãi mỗi " + p.Unit, Fmt.Vnd(p.Price - p.CostPrice)),
   ("Nhãn", Label(p)), ("Trang bán bánh", p.IsHidden ? "Đang ẩn" : "Đang hiển thị"), ("Tồn kho", s is null ? null : $"{s.Left} {p.Unit} (đã nhập {s.In} · đã bán {s.Sold} · đã hủy {s.Wasted})")], Viewer.ImageUrl(p), g);
 }
 void Hide() {
  if (G.Pick<Product>() is not { } p) return;
  if (!Ui.Ask(p.IsHidden ? $"Hiện lại “{p.Name}” trên trang bán bánh?" : $"Ẩn “{p.Name}” khỏi trang bán bánh?\nKhách sẽ không thấy và không đặt được bánh này; đơn, phiếu cũ vẫn giữ nguyên.")) return;
  var x = Api.Post<Product>($"api/admin/products/{p.Id}/hide"); Note(x.IsHidden ? $"Đã ẩn “{x.Name}”." : $"Đã hiện lại “{x.Name}”.");
 }
 static List<string> Cats() => Api.Get<List<CategoryRow>>("api/admin/categories").Select(c => c.Name).ToList();
 public override void Reload() {
  var st = status.SelectedIndex <= 0 ? null : ProductStatus.Options[status.SelectedIndex - 1].Key; var sel = Sel(cat);
  var list = Api.Get<List<Product>>("api/admin/products").Where(p => Svc.Has(p.Code, code.Text) && Svc.Has(p.Name, name.Text) && (sel is null || p.Category == sel) && (st is null || ProductStatus.Key(p) == st)).ToList();
  G.Load(list, p => [p.Code, $"{p.Name}  —  {p.Description}", p.Category, Fmt.Vnd(p.CostPrice), $"{p.Margin:0.##}%", Fmt.Vnd(p.Price), p.Unit, Label(p), p.IsHidden ? "Đang ẩn" : "Hiển thị"],
   (r, p) => { if (p.IsHidden) r.DefaultCellStyle.ForeColor = Ui.Muted; }); Note($"{list.Count} sản phẩm · {list.Count(p => p.IsHidden)} đang ẩn");
 }
 static string Label(Product p) => p.IsBestSeller && p.IsNew ? "Bán chạy · Mới" : p.IsBestSeller ? "Bán chạy nhất" : p.IsNew ? "Mới ra lò" : "Bình thường";
 void Edit(Product? cur) {
  var p = cur ?? new Product(); var isNew = cur is null; Product? saved = null;
  using var f = new FieldForm(isNew ? "Thêm bánh mới" : $"Sửa bánh — {p.Name}", isNew ? "Thêm bánh" : "Lưu thay đổi", 340);
  var tCode = f.Add("Mã bánh", FieldForm.Txt(p.Code, 20)); var tName = f.Add("Tên bánh", FieldForm.Txt(p.Name, 80)); var tDesc = f.Add("Mô tả ngắn", FieldForm.Txt(p.Description, 200));
  var cCat = f.Add("Danh mục", FieldForm.Combo(Cats().Cast<object>(), p.Category)); var cSt = f.Add("Trạng thái", FieldForm.Combo(ProductStatus.Options.Select(o => (object)o.Label), ProductStatus.Options.First(o => o.Key == ProductStatus.Key(p)).Label));
  // Giá nhập cố định, CHỈ HIỂN THỊ (lấy từ danh sách bánh + bảng giá của xưởng).
  // Bánh mới: chọn xưởng → chọn bánh trong danh sách của xưởng (bánh tiệm chưa bán). Sửa bánh: chỉ chọn xưởng chính trong số xưởng đã có giá.
  var quotes = Api.Get<List<SupplierPriceRow>>(isNew ? "api/admin/supplier-prices?unlinked=true" : $"api/admin/supplier-prices?productId={p.Id}").Where(q => !isNew || q.SupplierActive).ToList();
  var cSup = f.Add(isNew ? "Xưởng cung cấp" : "Xưởng chính", FieldForm.Combo(isNew ? quotes.Select(q => (object)q.Supplier).Distinct() : quotes.Select(q => (object)$"{q.Supplier} — {Fmt.Vnd(q.Price)}"), isNew ? null : quotes.Where(q => q.IsMain).Select(q => $"{q.Supplier} — {Fmt.Vnd(q.Price)}").FirstOrDefault()));
  var cItem = isNew ? f.Add("Bánh của xưởng", FieldForm.Combo(Array.Empty<object>())) : null; List<SupplierPriceRow> items = [];
  var lCost = f.Add("Giá nhập", Ui.Lbl("", Ui.Bold));
  var nMargin = f.Add("Tỉ lệ lợi nhuận (%)", FieldForm.Num(isNew ? 50 : p.Margin, 0, 1000));
  var lPrice = f.Add("Giá bán", Ui.Lbl("", Ui.Bold));
  var cUnit = f.Add("Đơn vị bán", FieldForm.Combo(ProductStatus.Units, p.Unit));
  SupplierPriceRow? Pick() => cItem is not null ? (cItem.SelectedIndex >= 0 ? items[cItem.SelectedIndex] : null) : cSup.SelectedIndex >= 0 ? quotes[cSup.SelectedIndex] : null;
  decimal Cost() => Pick()?.Price ?? (isNew ? 0 : p.CostPrice);
  void Price() {
   lCost.Text = isNew && Pick() is null ? (quotes.Count == 0 ? "Chưa có bánh nào trong danh sách của xưởng" : "Chọn bánh của xưởng") : $"{Fmt.Vnd(Cost())}  (giá xưởng, cố định)";
   lPrice.Text = Pick() is null && isNew ? "—" : $"{Fmt.Vnd(ProductStatus.PriceOf(Cost(), nMargin.Value))}   (= giá nhập × (1 + tỉ lệ), làm tròn 1.000 ₫)";
  }
  // Chọn bánh của xưởng: điền sẵn tên và đơn vị theo xưởng (tên vẫn sửa được).
  var autoName = "";
  void Items() { items = quotes.Where(q => q.Supplier == (string?)cSup.SelectedItem).ToList(); cItem!.Items.Clear(); cItem.Items.AddRange(items.Select(q => (object)$"{q.Product} ({q.Unit}) — {Fmt.Vnd(q.Price)}").ToArray()); if (items.Count > 0) cItem.SelectedIndex = 0; Price(); }
  if (cItem is not null) {
   cItem.SelectedIndexChanged += (_, _) => { if (Pick() is { } q) { if (tName.Text.Trim() == "" || tName.Text == autoName) tName.Text = autoName = q.Product; cUnit.SelectedItem = q.Unit; } Price(); };
   cSup.SelectedIndexChanged += (_, _) => Items(); Items();
  } else cSup.SelectedIndexChanged += (_, _) => Price();
  nMargin.ValueChanged += (_, _) => Price(); Price();
  var tImg = f.Add("Ảnh bánh", FieldForm.Txt(p.Image, 120));
  f.Add("", Ui.Btn("Chọn ảnh từ máy…", () => {
   using var dlg = new OpenFileDialog { Title = "Chọn ảnh bánh", Filter = "Ảnh (*.jpg;*.jpeg;*.png;*.webp;*.gif)|*.jpg;*.jpeg;*.png;*.webp;*.gif" };
   if (dlg.ShowDialog() != DialogResult.OK) return;
   try { if (Ui.Try(() => tImg.Text = Api.Upload(dlg.FileName)) is { } e) Ui.Err(e); }
   catch (IOException e) { Ui.Err($"Không đọc được file ảnh: {e.Message}"); }
  }));
  f.AddWide(Ui.Lbl("Để trống mã bánh để tự tạo (VD: SP007). Ảnh: bấm “Chọn ảnh từ máy…” (JPG/PNG/WEBP/GIF, tối đa 5 MB) để tải lên, hoặc nhập mã ảnh Unsplash (VD photo-1464305795204-6f5bbfc7fb81).", Ui.Small, Ui.Muted));
  f.Check = () => {
   if (tName.Text.Trim() == "") return "Hãy nhập tên bánh."; if (tDesc.Text.Trim() == "") return "Hãy nhập mô tả ngắn."; if (cCat.SelectedItem is null) return "Hãy chọn danh mục."; if (tImg.Text.Trim() == "") return "Hãy nhập mã ảnh.";
   if (isNew && Pick() is null) return "Hãy chọn bánh trong danh sách của xưởng (thêm ở Quản lý kho → Nhà cung cấp → Bảng giá).";
   // Mã trống thì server tự sinh SPxxx; trùng mã, sai danh mục/đơn vị do server kiểm tra. Giá nhập server lấy từ bảng giá xưởng, giá bán server tính lại.
   var body = new Product { Code = tCode.Text.Trim(), Name = tName.Text.Trim(), Description = tDesc.Text.Trim(), Category = (string)cCat.SelectedItem!, SupplierId = Pick()?.SupplierId, CostPrice = Cost(), Margin = nMargin.Value, Price = Math.Max(1000, ProductStatus.PriceOf(Cost(), nMargin.Value)), Unit = (string)cUnit.SelectedItem!, Image = tImg.Text.Trim() };
   ProductStatus.Apply(body, ProductStatus.Options[cSt.SelectedIndex].Key);
   return Ui.Try(() => saved = isNew ? Api.Post<Product>($"api/admin/products?priceId={Pick()!.Id}", body) : Api.Put<Product>($"api/admin/products/{p.Id}", body));
  };
  if (f.ShowDialog() == DialogResult.OK && saved is not null) Note($"Đã lưu “{saved.Name}” (mã {saved.Code}).");
 }
 void Delete() { if (G.Pick<Product>() is not { } p) return; if (!Ui.Ask($"Xóa “{p.Name}”? Không hoàn tác được.")) return; Api.Delete($"api/admin/products/{p.Id}"); Note($"Đã xóa “{p.Name}”."); }
}
