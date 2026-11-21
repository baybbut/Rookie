using System.Drawing.Printing;
using Rookie.Models;
namespace Rookie.Admin;

// Quản lý kho: phiếu nhập (xác nhận mới cộng tồn) · tồn kho · phiếu hủy (quản trị duyệt) · bánh hủy (đã duyệt) · kiểm kê cuối ngày · nhà cung cấp.
class WarehousePage : Page {
 readonly TabControl tabs = new() { Dock = DockStyle.Fill, Font = Ui.Bold, Padding = new Point(18, 5) }; readonly Page[] subs = [new PurchasesSub(), new StockSub(), new DiscardRequestsSub(), new DiscardsSub(), new DailySub(), new SuppliersSub()];
 public WarehousePage() : base("Warehouse", "Quản lý kho") {
  foreach (var (s, t) in subs.Zip(new[] { "Phiếu nhập", "Tồn kho", "Phiếu hủy", "Bánh hủy", "Kiểm kê cuối ngày", "Nhà cung cấp" })) { var tp = new TabPage(t) { BackColor = Ui.Cream, Padding = new Padding(0, 6, 0, 0) }; tp.Controls.Add(s); tabs.TabPages.Add(tp); }
  tabs.SelectedIndexChanged += (_, _) => subs[tabs.SelectedIndex].OnShow(); Body(tabs);
 }
 public override void OnShow() { base.OnShow(); }
 public override void Reload() => subs[Math.Max(0, tabs.SelectedIndex)].OnShow();
}

class PurchasesSub : Page {
 readonly TextBox q, year; readonly ComboBox day, month, status; readonly Button bReceive, bCancel;
 public PurchasesSub() : base(null, null) {
  q = Add("Tìm phiếu nhập", new TextBox(), 220); day = Add("Ngày", Combo("Tất cả", Enumerable.Range(1, 31).Select(i => i.ToString())), 80); month = Add("Tháng", Combo("Tất cả", Enumerable.Range(1, 12).Select(i => $"Tháng {i}")), 100); year = Add("Năm", new TextBox(), 70); status = Add("Trạng thái", Combo("Tất cả", PurchaseStatus.All)); ClearButton();
  G.Setup("Phiếu|0.5", "Nhà cung cấp|1.6", "Ngày lập|1", "Bánh nhập|1.3", ">Tổng tiền|1.1", "Trạng thái|1.1");
  Act("Tạo phiếu nhập", Create, primary: true); bReceive = Act("Xác nhận nhập kho", Receive); bCancel = Act("Hủy phiếu", Cancel, danger: true); Act("Chi tiết", Detail, reload: false); G.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) Detail(); }; G.SelectionChanged += (_, _) => Buttons();
 }
 void Buttons() { var pending = G.Pick<Purchase>()?.Status == PurchaseStatus.Pending; bReceive.Enabled = bCancel.Enabled = pending; }
 public override void Reload() {
  var t = q.Text.Trim().TrimStart('#'); int? d = day.SelectedIndex > 0 ? day.SelectedIndex : null, m = month.SelectedIndex > 0 ? month.SelectedIndex : null; var st = Sel(status);
  var list = Api.Get<List<Purchase>>("api/admin/purchases")
   .Where(p => Svc.InDate(p.CreatedAt, d, m, Num(year.Text)) && (st is null || p.Status == st) && (t == "" || p.Id.ToString() == t || Svc.Has(p.Supplier, t) || p.Items.Any(i => Svc.Has(i.ItemName, t)))).ToList();
  G.Load(list, p => [$"#{p.Id}", p.Supplier, Fmt.Day(p.CreatedAt), $"{p.Items.Sum(i => i.Quantity)} bánh · {p.Items.Count} loại", Fmt.Vnd(p.Total), p.Status], (r, p) => Grid.Tint(r, 5, Ui.PurchaseColor(p.Status))); Note($"{list.Count} phiếu"); Buttons();
 }
 // Server chỉ cho xác nhận/hủy phiếu "Chờ nhập kho" và ghi tên người xác nhận theo token.
 void Receive() {
  if (G.Pick<Purchase>() is not { } p) return;
  if (!Ui.Ask($"Xác nhận nhập kho phiếu #{p.Id}?\n{p.Items.Sum(i => i.Quantity)} bánh sẽ được cộng vào tồn kho.")) return;
  var x = Api.Post<Purchase>($"api/admin/purchases/{p.Id}/receive"); Note($"{x.ReceivedBy} đã xác nhận nhập kho phiếu #{x.Id}.");
 }
 void Cancel() {
  if (G.Pick<Purchase>() is not { } p) return;
  if (!Ui.Ask($"Hủy phiếu nhập #{p.Id}? Không thể mở lại.")) return; Api.Post($"api/admin/purchases/{p.Id}/cancel"); Note($"Đã hủy phiếu #{p.Id}.");
 }
 // Chi tiết phiếu nhập (chỉ xem) + nút "In phiếu".
 void Detail() {
  if (G.Pick<Purchase>() is not { } p) return;
  var g = new Grid().Setup("Bánh", ">Số lượng", ">Đơn giá nhập", ">Thành tiền"); g.Load(p.Items, i => [i.ItemName, $"{i.Quantity} {i.Unit}", $"{Fmt.Vnd(i.UnitCost)} / {i.Unit}", Fmt.Vnd(i.Subtotal)]);
  Viewer.Show(this, $"Phiếu nhập #{p.Id}", [("Nhà cung cấp", p.Supplier), ("Ngày lập", $"{Fmt.Day(p.CreatedAt)}{(p.CreatedBy is null ? "" : " · lập bởi " + p.CreatedBy)}"), ("Trạng thái", $"{p.Status}{(p.ReceivedBy is null ? "" : $" · nhập kho bởi {p.ReceivedBy}{(p.ReceivedAt is { } t ? " lúc " + Fmt.Date(t) : "")}")}"),
   ("Ghi chú", p.Note), ("Tổng cộng", $"{Fmt.Vnd(p.Total)} · {p.Items.Sum(i => i.Quantity)} bánh")], grid: g, print: () => PrintSlip(p));
 }
 static System.Drawing.Printing.PrintDocument PrintSlip(Purchase p) {
  var sup = Api.Get<List<Supplier>>("api/admin/suppliers").FirstOrDefault(s => s.Name == p.Supplier);
  var L = new List<PLine> { new("Rookie Bakery — PHIẾU NHẬP KHO", Style: 2), new($"Số phiếu: #{p.Id}", "", $"Ngày lập: {Fmt.Day(p.CreatedAt)}", 1), new($"Nhà cung cấp: {p.Supplier}"), new($"Điện thoại: {sup?.Phone ?? "—"}   ·   Địa chỉ: {sup?.Address ?? "—"}"),
   new($"Người lập: {p.CreatedBy ?? "—"}", "", $"Trạng thái: {p.Status}"), new(p.ReceivedBy is null ? "" : $"Nhập kho bởi {p.ReceivedBy}{(p.ReceivedAt is { } t ? " lúc " + Fmt.Date(t) : "")}"), new(""),
   new("Danh sách bánh nhập", Style: 3), new("Bánh", "Số lượng × đơn giá", "Thành tiền", 1) };
  L.AddRange(p.Items.Select(i => new PLine(i.ItemName, $"{i.Quantity} {i.Unit} × {Fmt.Vnd(i.UnitCost)}", Fmt.Vnd(i.Subtotal))));
  L.Add(new("TỔNG CỘNG", $"{p.Items.Sum(i => i.Quantity)} bánh", Fmt.Vnd(p.Total), 1)); if (!string.IsNullOrEmpty(p.Note)) L.Add(new($"Ghi chú: {p.Note}"));
  L.AddRange([new(""), new(""), new("Người lập phiếu", "Người giao hàng", "Thủ kho", 1), new("(ký, ghi rõ họ tên)", "(ký, ghi rõ họ tên)", "(ký, ghi rõ họ tên)"), new($"In lúc {Fmt.Date(DateTime.Now)} bởi {Session.Name}")]);
  return PrintDoc.Build($"Phiếu nhập #{p.Id}", L);
 }
 void Create() {
  // Chỉ nhà cung cấp đang hợp tác; chọn bánh thì đơn giá nhập tự điền theo giá nhập của bánh (sửa được).
  var suppliers = Api.Get<List<Supplier>>("api/admin/suppliers").Where(s => s.IsActive).Select(s => s.Name).ToList(); var products = Api.Get<List<Product>>("api/admin/products"); Purchase? saved = null;
  using var f = new FieldForm("Tạo phiếu nhập", "Lưu phiếu nhập", 470);
  var cSup = f.Add("Nhà cung cấp", FieldForm.Combo(suppliers.Cast<object>())); var dDate = f.Add("Ngày lập phiếu", new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", MaxDate = DateTime.Today, Value = DateTime.Today }); var tNote = f.Add("Ghi chú", FieldForm.Txt(null, 200));
  var grid = new TableLayoutPanel { AutoSize = true, ColumnCount = 4, Dock = DockStyle.Top, Margin = new Padding(0, 8, 0, 0) };
  foreach (var w in new[] { 210, 80, 60, 110 }) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, w));
  foreach (var h in new[] { "BÁNH", "SỐ LƯỢNG", "ĐƠN VỊ", "ĐƠN GIÁ NHẬP (₫)" }) grid.Controls.Add(Ui.Lbl(h, Ui.Small, Ui.Muted));
  // Đơn giá nhập lấy cố định theo bảng giá của xưởng đang chọn (không nhập tay); server cũng lấy lại giá từ bảng giá khi lưu.
  var quotes = Api.Get<List<SupplierPriceRow>>("api/admin/supplier-prices");
  decimal? Cost(int productId) => quotes.FirstOrDefault(x => x.Supplier == (string?)cSup.SelectedItem && x.ProductId == productId)?.Price;
  var total = Ui.Lbl("Tổng tiền: 0 ₫", Ui.Bold); var rows = new List<(ComboBox P, NumericUpDown Q, Label U, Label C)>();
  void Sum() {
   foreach (var r in rows) r.C.Text = r.P.SelectedIndex > 0 ? (Cost(products[r.P.SelectedIndex - 1].Id) is { } c ? Fmt.Vnd(c) : "chưa có giá") : "—";
   total.Text = $"Tổng tiền: {Fmt.Vnd(rows.Where(r => r.P.SelectedIndex > 0).Sum(r => r.Q.Value * (Cost(products[r.P.SelectedIndex - 1].Id) ?? 0)))}";
  }
  for (var i = 0; i < 6; i++) {
   var cp = FieldForm.Combo(new[] { "— Chọn bánh —" }.Concat(products.Select(p => $"{p.Code} · {p.Name}")).Cast<object>()); cp.Width = 200; var q = FieldForm.Num(0, 0, 100000); q.Width = 70; var u = Ui.Lbl("—"); u.Margin = new Padding(0, 8, 0, 0); var c = Ui.Lbl("—", Ui.Bold); c.Margin = new Padding(0, 8, 0, 0);
   cp.SelectedIndexChanged += (_, _) => { u.Text = cp.SelectedIndex > 0 ? products[cp.SelectedIndex - 1].Unit : "—"; Sum(); }; q.ValueChanged += (_, _) => Sum();
   grid.Controls.AddRange([cp, q, u, c]); rows.Add((cp, q, u, c));
  }
  cSup.SelectedIndexChanged += (_, _) => Sum();
  f.AddWide(grid); f.AddWide(total); f.AddWide(Ui.Lbl("Đơn giá nhập theo bảng giá cố định của xưởng (Quản lý kho → Nhà cung cấp → Bảng giá). Phiếu mới ở trạng thái “Chờ nhập kho”: số bánh chỉ được cộng vào tồn kho sau khi bạn xác nhận.", Ui.Small, Ui.Muted));
  f.Check = () => {
   if (suppliers.Count == 0) return "Chưa có nhà cung cấp nào đang hợp tác (thêm ở tab “Nhà cung cấp”)."; if (rows.Any(r => r.Q.Value > 0 && r.P.SelectedIndex <= 0)) return "Có dòng đã nhập số lượng nhưng chưa chọn bánh.";
   if (!rows.Any(r => r.P.SelectedIndex > 0 && r.Q.Value > 0)) return "Hãy chọn ít nhất một loại bánh với số lượng > 0.";
   if (rows.FirstOrDefault(r => r.P.SelectedIndex > 0 && r.Q.Value > 0 && Cost(products[r.P.SelectedIndex - 1].Id) is null) is { P: { } missing }) return $"“{cSup.SelectedItem}” chưa có giá cho “{products[missing.SelectedIndex - 1].Name}”.";
   var items = rows.Where(r => r.P.SelectedIndex > 0 && r.Q.Value > 0).Select(r => new PurchaseRow { ProductId = products[r.P.SelectedIndex - 1].Id, Quantity = (int)r.Q.Value, UnitCost = Cost(products[r.P.SelectedIndex - 1].Id) ?? 0 }).ToList();
   return Ui.Try(() => saved = Api.Post<Purchase>("api/admin/purchases", new { supplier = (string)cSup.SelectedItem!, date = dDate.Value.Date, note = string.IsNullOrWhiteSpace(tNote.Text) ? null : tNote.Text.Trim(), items }));
  };
  if (f.ShowDialog() == DialogResult.OK && saved is not null) Note($"Đã lưu phiếu nhập #{saved.Id}. Hãy kiểm hàng và xác nhận để cộng vào tồn kho.");
 }
}

class StockSub : Page {
 readonly TextBox q; readonly ComboBox level;
 public StockSub() : base(null, null) {
  q = Add("Tìm bánh", new TextBox(), 230); level = Add("Mức tồn", Combo("Tất cả", [$"Sắp hết (≤ {Svc.LowStock})"])); ClearButton();
  G.Setup("Mã|0.6", "Bánh|2.6", "Đơn vị|0.6", ">Đã nhập|0.8", ">Đã bán|0.8", ">Đã hủy|0.8", ">Tồn kho|0.8", "Tình trạng|1");
 }
 public override void Reload() {
  var rows = Api.Get<List<StockRow>>("api/admin/stock").Where(s => (Svc.Has(s.Product.Name, q.Text) || Svc.Has(s.Product.Code, q.Text)) && (level.SelectedIndex == 0 || s.Left <= Svc.LowStock)).ToList();
  G.Load(rows, s => [s.Product.Code, $"{s.Product.Name}  ({s.Product.Category})", s.Product.Unit, s.In, s.Sold, s.Wasted, s.Left, s.Left < 0 ? "Âm kho" : s.Left == 0 ? "Hết hàng" : s.Left <= Svc.LowStock ? "Sắp hết" : "Còn hàng"],
   (r, s) => Grid.Tint(r, 7, s.Left <= 0 ? Color.FromArgb(246, 220, 217) : s.Left <= Svc.LowStock ? Color.FromArgb(247, 227, 181) : Color.FromArgb(200, 232, 205)));
  Note("Tồn = đã nhập kho (phiếu đã xác nhận) − đã bán (đơn chưa hủy) − bánh hủy.");
 }
}

// Bánh hủy: CHỈ các phiếu hủy đã được quản trị duyệt (đã trừ tồn kho).
class DiscardsSub : Page {
 readonly TextBox q, year; readonly ComboBox day, month;
 public DiscardsSub() : base(null, null) {
  q = Add("Tìm", new TextBox(), 220); day = Add("Ngày", Combo("Tất cả", Enumerable.Range(1, 31).Select(i => i.ToString())), 80); month = Add("Tháng", Combo("Tất cả", Enumerable.Range(1, 12).Select(i => $"Tháng {i}")), 100); year = Add("Năm", new TextBox(), 70); ClearButton();
  G.Setup("Phiếu|0.5", "Ngày hủy|0.9", "Bánh|1.6", ">Số lượng|0.8", "Lý do|1.6", ">Giá trị|0.9", "Người lập|1", "Người duyệt|1");
 }
 public override void Reload() {
  int? d = day.SelectedIndex > 0 ? day.SelectedIndex : null, m = month.SelectedIndex > 0 ? month.SelectedIndex : null;
  var list = Api.Get<List<Discard>>("api/admin/discards").Where(x => x.Status == DiscardStatus.Approved && Svc.InDate(x.CreatedAt, d, m, Num(year.Text)) && (Svc.Has(x.ProductName, q.Text) || Svc.Has(x.Reason, q.Text))).ToList();
  G.Load(list, x => [$"#{x.SlipId}", Fmt.Day(x.CreatedAt), x.ProductName, $"{x.Quantity} {x.Unit}", x.Reason, Fmt.Vnd(x.Value), x.CreatedBy, x.ReviewedBy]); Note($"{list.Count} phiếu đã duyệt · {list.Sum(x => x.Quantity)} bánh · {Fmt.Vnd(list.Sum(x => x.Value))}");
 }
}

// Phiếu hủy: một phiếu gồm nhiều loại bánh. Nhân viên lập (chờ duyệt) → quản trị duyệt / từ chối cả phiếu. Quản trị tự lập thì duyệt luôn.
class DiscardRequestsSub : Page {
 // Một dòng trên bảng = một phiếu (các dòng Discard cùng SlipId).
 record Slip(int Id, List<Discard> Lines) {
  public Discard First => Lines[0]; public int Qty => Lines.Sum(l => l.Quantity); public decimal Value => Lines.Sum(l => l.Value);
  public string Items => string.Join(", ", Lines.Select(l => $"{l.ProductName} ×{l.Quantity}"));
 }
 readonly TextBox q; readonly ComboBox status; readonly Button? bApprove, bReject;
 public DiscardRequestsSub() : base(null, null) {
  q = Add("Tìm", new TextBox(), 220); status = Add("Trạng thái", Combo("Tất cả", DiscardStatus.All), 130); ClearButton();
  G.Setup("Phiếu|0.5", "Ngày lập|1", "Bánh hủy|2.6", ">Số bánh|0.6", ">Giá trị|0.9", "Người lập|1", "Trạng thái|0.9", "Người duyệt|1");
  Act(Session.IsAdmin ? "Ghi nhận bánh hủy" : "Lập phiếu hủy", Create, primary: true);
  if (Session.IsAdmin) { bApprove = Act("Duyệt hủy", () => Review(true)); bReject = Act("Từ chối", () => Review(false), danger: true); }
  Act("Chi tiết", Detail, reload: false); G.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) Detail(); }; G.SelectionChanged += (_, _) => Buttons();
 }
 void Buttons() { var pending = G.Pick<Slip>()?.First.Status == DiscardStatus.Pending; if (bApprove is not null) bApprove.Enabled = bReject!.Enabled = pending; }
 public override void Reload() {
  var st = Sel(status); var t = q.Text.Trim().TrimStart('#');
  var list = Api.Get<List<Discard>>("api/admin/discards").GroupBy(d => d.SlipId).Select(g => new Slip(g.Key, g.OrderBy(d => d.Id).ToList()))
   .Where(s => (st is null || s.First.Status == st) && (s.Id.ToString() == t || Svc.Has(s.First.CreatedBy, t) || s.Lines.Any(l => Svc.Has(l.ProductName, t) || Svc.Has(l.Reason, t))))
   .OrderByDescending(s => s.First.Status == DiscardStatus.Pending).ThenByDescending(s => s.First.CreatedAt).ToList();
  G.Load(list, s => [$"#{s.Id}", Fmt.Date(s.First.CreatedAt), s.Items, s.Qty, Fmt.Vnd(s.Value), s.First.CreatedBy, s.First.Status, s.First.ReviewedBy ?? "—"], (r, s) => Grid.Tint(r, 6, Ui.DiscardColor(s.First.Status)));
  var pending = list.Count(s => s.First.Status == DiscardStatus.Pending); Note(pending == 0 ? $"{list.Count} phiếu" : $"{list.Count} phiếu · {pending} phiếu đang chờ quản trị duyệt"); Buttons();
 }
 // Server chỉ cho quản trị duyệt; lúc duyệt kiểm tra lại tồn kho từng bánh.
 void Review(bool ok) {
  if (G.Pick<Slip>() is not { } s) return;
  if (!Ui.Ask(ok ? $"Duyệt phiếu hủy #{s.Id}: {s.Items}?\n{s.Qty} bánh sẽ bị trừ khỏi tồn kho." : $"Từ chối phiếu hủy #{s.Id}?")) return;
  Api.Post<List<Discard>>($"api/admin/discards/slips/{s.Id}/{(ok ? "approve" : "reject")}"); Note($"Phiếu hủy #{s.Id}: {(ok ? DiscardStatus.Approved : DiscardStatus.Rejected)}.");
 }
 void Detail() {
  if (G.Pick<Slip>() is not { } s) return; var d = s.First;
  var g = new Grid().Setup("Bánh|1.6", ">Số lượng|0.8", ">Giá bán|0.9", ">Giá trị|0.9", "Lý do|1.6"); g.Load(s.Lines, l => [l.ProductName, $"{l.Quantity} {l.Unit}", Fmt.Vnd(l.UnitPrice), Fmt.Vnd(l.Value), l.Reason]);
  Viewer.Show(this, $"Phiếu hủy #{s.Id}", [("Ngày lập", Fmt.Date(d.CreatedAt)), ("Người lập", d.CreatedBy), ("Trạng thái", d.Status), ("Người duyệt", d.ReviewedBy is null ? null : $"{d.ReviewedBy}{(d.ReviewedAt is { } t ? " lúc " + Fmt.Date(t) : "")}"),
   ("Tổng cộng", $"{s.Lines.Count} loại · {s.Qty} bánh · {Fmt.Vnd(s.Value)}")], grid: g, print: () => PrintSlip(s));
 }
 static System.Drawing.Printing.PrintDocument PrintSlip(Slip s) {
  var d = s.First;
  var L = new List<PLine> { new("Rookie Bakery — PHIẾU HỦY BÁNH", Style: 2), new($"Số phiếu: #{s.Id}", "", $"Ngày: {Fmt.Date(d.CreatedAt)}", 1), new($"Người lập: {d.CreatedBy ?? "—"}"),
   new($"Trạng thái: {d.Status}{(d.ReviewedBy is null ? "" : $" · {d.ReviewedBy}{(d.ReviewedAt is { } t ? " lúc " + Fmt.Date(t) : "")}")}"), new(""), new("Danh sách bánh hủy", Style: 3), new("Bánh · lý do", "Số lượng × giá bán", "Giá trị", 1) };
  L.AddRange(s.Lines.Select(l => new PLine($"{l.ProductName} · {l.Reason}", $"{l.Quantity} {l.Unit} × {Fmt.Vnd(l.UnitPrice)}", Fmt.Vnd(l.Value))));
  L.AddRange([new("TỔNG CỘNG", $"{s.Qty} bánh", Fmt.Vnd(s.Value), 1), new(""), new(""), new("Người lập phiếu", "", "Quản trị duyệt", 1), new("(ký, ghi rõ họ tên)", "", "(ký, ghi rõ họ tên)"), new($"In lúc {Fmt.Date(DateTime.Now)} bởi {Session.Name}")]);
  return PrintDoc.Build($"Phiếu hủy #{s.Id}", L);
 }
 // Lập phiếu nhiều dòng: mỗi dòng chọn bánh, số lượng, lý do; ngày hủy và ghi chú chung cho cả phiếu.
 void Create() {
  var stock = Api.Get<List<StockRow>>("api/admin/stock"); var saved = new List<Discard>();
  using var f = new FieldForm(Session.IsAdmin ? "Ghi nhận bánh hủy" : "Lập phiếu hủy (gửi quản trị duyệt)", Session.IsAdmin ? "Ghi nhận" : "Gửi phiếu", 580);
  var dDate = f.Add("Ngày hủy", new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", MaxDate = DateTime.Today, Value = DateTime.Today }); var tNote = f.Add("Ghi chú chung", FieldForm.Txt(null, 100));
  var grid = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, Dock = DockStyle.Top, Margin = new Padding(0, 8, 0, 0) };
  foreach (var w in new[] { 330, 80, 160 }) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, w));
  foreach (var h in new[] { "BÁNH (TỒN HIỆN TẠI)", "SỐ LƯỢNG", "LÝ DO" }) grid.Controls.Add(Ui.Lbl(h, Ui.Small, Ui.Muted));
  var total = Ui.Lbl("", Ui.Bold); var rows = new List<(ComboBox P, NumericUpDown Q, ComboBox R)>();
  void Sum() { var on = rows.Where(r => r.P.SelectedIndex > 0 && r.Q.Value > 0).ToList(); total.Text = $"{on.Count} dòng · {on.Sum(r => r.Q.Value):0} bánh · {Fmt.Vnd(on.Sum(r => r.Q.Value * stock[r.P.SelectedIndex - 1].Product.Price))} (theo giá bán)"; }
  for (var i = 0; i < 8; i++) {
   var cp = FieldForm.Combo(new[] { "— Chọn bánh —" }.Concat(stock.Select(s => $"{s.Product.Code} · {s.Product.Name} (tồn {s.Left} {s.Product.Unit})")).Cast<object>()); cp.Width = 320;
   var q = FieldForm.Num(0, 0, 100000); q.Width = 70; var r = FieldForm.Combo(DiscardReasons.All); r.Width = 150;
   cp.SelectedIndexChanged += (_, _) => { if (cp.SelectedIndex > 0 && q.Value == 0) q.Value = 1; Sum(); }; q.ValueChanged += (_, _) => Sum();
   grid.Controls.AddRange([cp, q, r]); rows.Add((cp, q, r));
  }
  Sum(); f.AddWide(grid); f.AddWide(total);
  f.AddWide(Ui.Lbl(Session.IsAdmin ? "Quản trị ghi nhận thì phiếu được duyệt luôn và trừ tồn kho ngay." : "Phiếu ở trạng thái “Chờ duyệt”; chỉ khi quản trị duyệt, số bánh mới bị trừ khỏi tồn kho.", Ui.Small, Ui.Muted));
  // Server kiểm tra lại số tồn lúc ghi (có thể vừa thay đổi) và ghi tên người lập theo token.
  f.Check = () => {
   var on = rows.Where(r => r.P.SelectedIndex > 0 && r.Q.Value > 0).ToList();
   if (on.Count == 0) return "Hãy chọn ít nhất một loại bánh với số lượng > 0."; if (rows.Any(r => r.Q.Value > 0 && r.P.SelectedIndex <= 0)) return "Có dòng đã nhập số lượng nhưng chưa chọn bánh.";
   if (on.GroupBy(r => r.P.SelectedIndex).Select(g => (S: stock[g.Key - 1], Q: g.Sum(r => r.Q.Value))).FirstOrDefault(x => x.Q > x.S.Left) is { S: { } s }) return $"Chỉ còn {s.Left} {s.Product.Unit} “{s.Product.Name}” trong kho.";
   var items = on.Select(r => new { productId = stock[r.P.SelectedIndex - 1].Product.Id, quantity = (int)r.Q.Value, reason = (string)r.R.SelectedItem! }).ToList();
   return Ui.Try(() => saved = Api.Post<List<Discard>>("api/admin/discards", new { items, note = string.IsNullOrWhiteSpace(tNote.Text) ? null : tNote.Text.Trim(), date = dDate.Value.Date }));
  };
  if (f.ShowDialog() != DialogResult.OK || saved.Count == 0) return;
  Note(Session.IsAdmin ? $"Đã ghi nhận phiếu hủy #{saved[0].SlipId}: {saved.Sum(d => d.Quantity)} bánh." : $"Đã gửi phiếu hủy #{saved[0].SlipId} ({saved.Sum(d => d.Quantity)} bánh), chờ quản trị duyệt.");
 }
}

class ReportsPage : Page {
 readonly ComboBox type; readonly DateTimePicker from, to, month; readonly NumericUpDown year;
 readonly BarChart chart = new() { Dock = DockStyle.Fill }; readonly Grid revenue = new(), top = new(), custs = new(), waste = new(), orderStats = new();
 readonly FlowLayoutPanel stats = new() { Dock = DockStyle.Top, AutoSize = true, WrapContents = true }; readonly Dictionary<string, Label> vals = [];
 readonly Label period = Ui.Lbl("", Ui.Bold, Ui.Muted); ReportVm? cur;
 public ReportsPage() : base("Reports", "Báo cáo & thống kê") {
  type = Add("Kiểu kỳ báo cáo", Combo("Theo ngày", ["Theo tháng", "Theo năm"]), 130); from = Add("Từ ngày", new DateTimePicker(), 130); to = Add("Đến ngày", new DateTimePicker(), 130);
  month = Add("Tháng", new DateTimePicker(), 110); year = Add("Năm", new NumericUpDown { Minimum = 2000, Maximum = 2100, Value = DateTime.Today.Year }, 80);
  foreach (var d in new[] { from, to, month }) { d.ShowCheckBox = false; d.MaxDate = DateTime.Today; d.Value = DateTime.Today; } from.Value = DateTime.Today.AddDays(-29); month.Format = DateTimePickerFormat.Custom; month.CustomFormat = "MM/yyyy"; month.ShowUpDown = true;
  type.SelectedIndexChanged += (_, _) => { from.Parent!.Visible = to.Parent!.Visible = type.SelectedIndex == 0; month.Parent!.Visible = type.SelectedIndex == 1; year.Parent!.Visible = type.SelectedIndex == 2; }; month.Parent!.Visible = year.Parent!.Visible = false;
  Filters.Controls.Add(new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 19, 0, 0), Controls = { Ui.Btn("7 ngày", () => Quick(7)), Ui.Btn("30 ngày", () => Quick(30)), Ui.Btn("90 ngày", () => Quick(90)) } });
  Act("Xuất Excel", ExportXlsx, reload: false); Act("Xuất PDF", ExportPdf, reload: false);
  foreach (var (k, t) in new[] { ("rev", "Doanh thu"), ("cnt", "Số đơn (không tính đã hủy)"), ("avg", "Giá trị TB / đơn"), ("w", "Bánh hủy"), ("wv", "Giá trị bánh hủy") }) {
   var card = new Panel { Width = 210, Height = 74, Margin = new Padding(0, 0, 12, 10), BackColor = Color.FromArgb(238, 229, 216) }; var v = Ui.Lbl("—", Ui.Big); v.Left = 14; v.Top = 36; var s = Ui.Lbl(t, new Font("Segoe UI", 8.5f), Ui.Muted); s.Left = 14; s.Top = 12; card.Controls.AddRange([s, v]); vals[k] = v; stats.Controls.Add(card);
  }
  revenue.Setup("Ngày", ">Số đơn", ">Doanh thu"); top.Setup("Bánh", ">Số lượng", ">Doanh thu"); custs.Setup("Khách hàng", ">Số đơn", ">Chi tiêu"); waste.Setup("Ngày", ">Số bánh hủy", ">Giá trị"); orderStats.Setup("Nhóm|1", "Chỉ tiêu|1.6", ">Số đơn|0.7", ">Tỉ lệ|0.7", ">Tổng tiền|1");
  var tabs = new TabControl { Dock = DockStyle.Fill, Font = Ui.Bold, Padding = new Point(16, 5) };
  foreach (var (t, c) in new (string, Control)[] { ("Doanh thu chi tiết", revenue), ("Thống kê đơn hàng", orderStats), ("Bánh bán chạy", top), ("Khách hàng chi nhiều nhất", custs), ("Bánh hủy", waste) }) { var tp = new TabPage(t) { BackColor = Ui.Cream, Padding = new Padding(0, 6, 0, 0) }; tp.Controls.Add(c); tabs.TabPages.Add(tp); }
  var chartBox = new Panel { Dock = DockStyle.Top, Height = 200, BackColor = Ui.Card, Padding = new Padding(10), Margin = new Padding(0, 0, 0, 10) }; chartBox.Controls.Add(chart);
  var body = new TableLayoutPanel { ColumnCount = 1, RowCount = 4 }; body.RowStyles.Add(new RowStyle(SizeType.AutoSize)); body.RowStyles.Add(new RowStyle(SizeType.AutoSize)); body.RowStyles.Add(new RowStyle(SizeType.Absolute, 210)); body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
  period.Margin = new Padding(0, 0, 0, 8); body.Controls.Add(period, 0, 0); body.Controls.Add(stats, 0, 1); body.Controls.Add(chartBox, 0, 2); body.Controls.Add(tabs, 0, 3); Body(body);
 }
 void Quick(int days) { to.Value = DateTime.Today; from.Value = DateTime.Today.AddDays(-(days - 1)); type.SelectedIndex = 0; Reload(); }
 string Kind => type.SelectedIndex == 1 ? "month" : type.SelectedIndex == 2 ? "year" : "range";
 public override void Reload() {
  // Báo cáo do server tính (GET /api/admin/reports), cùng công thức với trang /admin.
  var r = cur = Api.Get<ReportVm>($"api/admin/reports?type={Kind}&from={from.Value:yyyy-MM-dd}&to={to.Value:yyyy-MM-dd}&month={month.Value:yyyy-MM}&year={(int)year.Value}"); var unit = r.PeriodUnit == "tháng" ? "Tháng" : "Ngày";
  period.Text = $"Kỳ báo cáo: {r.Title}"; vals["rev"].Text = Fmt.Vnd(r.Revenue); vals["cnt"].Text = r.OrderCount.ToString(); vals["avg"].Text = Fmt.Vnd(r.AvgOrder); vals["w"].Text = r.Wasted.ToString(); vals["wv"].Text = Fmt.Vnd(r.WastedValue);
  chart.Set(r.Series.Select(d => (d.Label ?? Fmt.Day(d.Day), d.Revenue, $"{d.Label} · {Fmt.Vnd(d.Revenue)} · {d.Orders} đơn")));
  revenue.Columns[0].HeaderText = unit.ToUpperInvariant(); waste.Columns[0].HeaderText = unit.ToUpperInvariant();
  revenue.Load(r.Series.Append(new DayPoint(r.To, r.Revenue, r.OrderCount, "TỔNG")), d => [d.Label, d.Orders, Fmt.Vnd(d.Revenue)], (row, d) => { if (d.Label == "TỔNG") row.DefaultCellStyle.Font = Ui.Bold; else if (d.Orders == 0) row.DefaultCellStyle.ForeColor = Ui.Muted; });
  top.Load(r.TopProducts, t => [t.Name, t.Count, Fmt.Vnd(t.Value)]); custs.Load(r.TopCustomers, t => [t.Name, t.Count, Fmt.Vnd(t.Value)]); waste.Load(r.Discards, d => [d.Label, d.Quantity, Fmt.Vnd(d.Value)]);
  orderStats.Load(OrderStats(r), x => [x.Group, x.Name, x.Count, x.Rate, x.Value], (row, x) => { if (x.Name == "Tổng số đơn") row.DefaultCellStyle.Font = Ui.Bold; });
  Note($"Trong kỳ có {r.AllOrders} đơn, {r.CancelledOrders} đơn đã hủy ({Pct(r.CancelledOrders, r.AllOrders)}).");
 }
 static string Pct(int n, int all) => all == 0 ? "0%" : $"{100.0 * n / all:0.#}%";
 // Thống kê đơn hàng (tính cả đơn hủy): theo trạng thái, theo thanh toán, theo nguồn đặt. Dùng chung cho bảng, PDF và Excel.
 public record Stat(string Group, string Name, int Count, string Rate, string Value);
 public static List<Stat> OrderStats(ReportVm r) =>
  [new("Tổng", "Tổng số đơn", r.AllOrders, "100%", Fmt.Vnd(r.OrdersByStatus.Sum(x => x.Value))),
   .. r.OrdersByStatus.Select(x => new Stat("Trạng thái đơn", x.Name, x.Count, Pct(x.Count, r.AllOrders), Fmt.Vnd(x.Value))),
   .. r.OrdersByPayment.Select(x => new Stat("Thanh toán", x.Name, x.Count, Pct(x.Count, r.AllOrders), Fmt.Vnd(x.Value))),
   .. r.OrdersBySource.Select(x => new Stat("Nguồn đặt", x.Name, x.Count, Pct(x.Count, r.AllOrders), Fmt.Vnd(x.Value)))];
 void ExportXlsx() {
  if (cur is null) return; using var sd = new SaveFileDialog { Filter = "Excel (*.xlsx)|*.xlsx", FileName = $"bao-cao-{cur.From:yyyyMMdd}-{cur.To:yyyyMMdd}.xlsx" }; if (sd.ShowDialog() != DialogResult.OK) return;
  File.WriteAllBytes(sd.FileName, Svc.ReportXlsx(cur, Session.Name)); if (Ui.Ask($"Đã lưu {sd.FileName}\nMở file ngay?")) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(sd.FileName) { UseShellExecute = true });
 }
 // Xuất PDF bằng máy in "Microsoft Print to PDF" của Windows; nếu máy không có thì mở bản xem trước để in/lưu thủ công.
 void ExportPdf() {
  if (cur is null) return; var doc = ReportDoc.Build(cur, Session.Name);
  using var sd = new SaveFileDialog { Filter = "PDF (*.pdf)|*.pdf", FileName = $"bao-cao-{cur.From:yyyyMMdd}-{cur.To:yyyyMMdd}.pdf" }; if (sd.ShowDialog() != DialogResult.OK) return;
  doc.PrinterSettings.PrinterName = "Microsoft Print to PDF";
  if (!doc.PrinterSettings.IsValid) { using var pv = new PrintPreviewDialog { Document = doc, WindowState = FormWindowState.Maximized }; pv.ShowDialog(this); return; }
  doc.PrinterSettings.PrintToFile = true; doc.PrinterSettings.PrintFileName = sd.FileName; doc.PrintController = new StandardPrintController(); doc.Print();
  if (File.Exists(sd.FileName) && Ui.Ask($"Đã lưu {sd.FileName}\nMở file ngay?")) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(sd.FileName) { UseShellExecute = true });
 }
}

// Báo cáo để in / xuất PDF (dàn trang bằng PrintDoc).
static class ReportDoc {
 public static PrintDocument Build(ReportVm r, string who) {
  var unit = r.PeriodUnit == "tháng" ? "Tháng" : "Ngày"; var L = new List<PLine> { new("Rookie Bakery — Báo cáo & thống kê", Style: 2), new($"Kỳ báo cáo: {r.Title}", Style: 1), new($"Xuất bởi {who} lúc {Fmt.Date(DateTime.Now)}"), new(""),
   new("Tổng quan", Style: 3), new("Doanh thu", "", Fmt.Vnd(r.Revenue)), new("Số đơn (không tính đã hủy)", "", r.OrderCount.ToString()), new("Giá trị trung bình / đơn", "", Fmt.Vnd(r.AvgOrder)), new("Bánh hủy", "", $"{r.Wasted} bánh · {Fmt.Vnd(r.WastedValue)}"), new(""),
   new($"Doanh thu theo {r.PeriodUnit}", Style: 3), new(unit, "Số đơn", "Doanh thu", 1) };
  L.AddRange(r.Series.Select(d => new PLine(d.Label ?? Fmt.Day(d.Day), d.Orders.ToString(), Fmt.Vnd(d.Revenue)))); L.Add(new("Tổng", r.OrderCount.ToString(), Fmt.Vnd(r.Revenue), 1)); L.Add(new(""));
  L.Add(new("Thống kê đơn hàng (tính cả đơn hủy)", Style: 3)); L.Add(new("Chỉ tiêu", "Số đơn (tỉ lệ)", "Tổng tiền", 1));
  L.AddRange(ReportsPage.OrderStats(r).Select(x => new PLine(x.Group == "Tổng" ? x.Name : $"{x.Group}: {x.Name}", $"{x.Count} ({x.Rate})", x.Value, x.Group == "Tổng" ? 1 : 0))); L.Add(new(""));
  L.Add(new("Bánh bán chạy", Style: 3)); L.Add(new("Bánh", "Số lượng", "Doanh thu", 1)); L.AddRange(r.TopProducts.Select(t => new PLine(t.Name, t.Count.ToString(), Fmt.Vnd(t.Value)))); L.Add(new(""));
  L.Add(new("Khách hàng chi nhiều nhất", Style: 3)); L.Add(new("Khách hàng", "Số đơn", "Chi tiêu", 1)); L.AddRange(r.TopCustomers.Select(t => new PLine(t.Name, t.Count.ToString(), Fmt.Vnd(t.Value)))); L.Add(new(""));
  L.Add(new($"Bánh hủy theo {r.PeriodUnit} (phiếu đã duyệt)", Style: 3)); if (r.Discards.Count == 0) L.Add(new("Không có bánh hủy trong kỳ này.")); else { L.Add(new(unit, "Số bánh hủy", "Giá trị", 1)); L.AddRange(r.Discards.Select(d => new PLine(d.Label, d.Quantity.ToString(), Fmt.Vnd(d.Value)))); }
  return PrintDoc.Build("Báo cáo Rookie", L);
 }
}
