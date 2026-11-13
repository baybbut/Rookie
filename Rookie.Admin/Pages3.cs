using System.Drawing.Printing;
using Microsoft.EntityFrameworkCore;
using Rookie.Models;
namespace Rookie.Admin;

// Quản lý kho: phiếu nhập từ xưởng bánh (xác nhận mới cộng tồn) · tồn kho · bánh hủy.
class WarehousePage : Page {
 readonly TabControl tabs = new() { Dock = DockStyle.Fill, Font = Ui.Bold, Padding = new Point(18, 5) }; readonly Page[] subs = [new PurchasesSub(), new StockSub(), new DiscardsSub()];
 public WarehousePage() : base("Warehouse", "Quản lý kho") {
  foreach (var (s, t) in subs.Zip(new[] { "Phiếu nhập", "Tồn kho", "Bánh hủy" })) { var tp = new TabPage(t) { BackColor = Ui.Cream, Padding = new Padding(0, 6, 0, 0) }; tp.Controls.Add(s); tabs.TabPages.Add(tp); }
  tabs.SelectedIndexChanged += (_, _) => subs[tabs.SelectedIndex].OnShow(); Body(tabs);
 }
 public override void OnShow() { base.OnShow(); }
 public override void Reload() => subs[Math.Max(0, tabs.SelectedIndex)].OnShow();
}

class PurchasesSub : Page {
 readonly TextBox q, year; readonly ComboBox day, month, status; readonly Button bReceive, bCancel;
 public PurchasesSub() : base(null, null) {
  q = Add("Tìm phiếu nhập", new TextBox(), 220); day = Add("Ngày", Combo("Tất cả", Enumerable.Range(1, 31).Select(i => i.ToString())), 80); month = Add("Tháng", Combo("Tất cả", Enumerable.Range(1, 12).Select(i => $"Tháng {i}")), 100); year = Add("Năm", new TextBox(), 70); status = Add("Trạng thái", Combo("Tất cả", PurchaseStatus.All)); ClearButton();
  G.Setup("Phiếu|0.5", "Xưởng bánh|1.6", "Ngày lập|1", "Bánh nhập|1.3", ">Tổng tiền|1.1", "Trạng thái|1.1");
  Act("Tạo phiếu nhập", Create, primary: true); bReceive = Act("Xác nhận nhập kho", Receive); bCancel = Act("Hủy phiếu", Cancel, danger: true); Act("Chi tiết", Detail, reload: false); G.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) Detail(); }; G.SelectionChanged += (_, _) => Buttons();
 }
 void Buttons() { var pending = G.Pick<Purchase>()?.Status == PurchaseStatus.Pending; bReceive.Enabled = bCancel.Enabled = pending; }
 public override void Reload() {
  using var db = Db.New(); var t = q.Text.Trim().TrimStart('#'); int? d = day.SelectedIndex > 0 ? day.SelectedIndex : null, m = month.SelectedIndex > 0 ? month.SelectedIndex : null; var st = Sel(status);
  var list = db.Purchases.AsNoTracking().Include(p => p.Items).OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id).ToList()
   .Where(p => Svc.InDate(p.CreatedAt, d, m, Num(year.Text)) && (st is null || p.Status == st) && (t == "" || p.Id.ToString() == t || Svc.Has(p.Supplier, t) || p.Items.Any(i => Svc.Has(i.ItemName, t)))).ToList();
  G.Load(list, p => [$"#{p.Id}", p.Supplier, Fmt.Day(p.CreatedAt), $"{p.Items.Sum(i => i.Quantity)} bánh · {p.Items.Count} loại", Fmt.Vnd(p.Total), p.Status], (r, p) => Grid.Tint(r, 5, Ui.PurchaseColor(p.Status))); Note($"{list.Count} phiếu"); Buttons();
 }
 void Receive() {
  if (G.Pick<Purchase>() is not { } sel) return; using var db = Db.New(); var p = db.Purchases.Include(x => x.Items).First(x => x.Id == sel.Id);
  if (p.Status != PurchaseStatus.Pending) { Ui.Err($"Phiếu #{p.Id} đang ở trạng thái “{p.Status}”, không xác nhận được."); return; }
  if (!Ui.Ask($"Xác nhận nhập kho phiếu #{p.Id}?\n{p.Items.Sum(i => i.Quantity)} bánh sẽ được cộng vào tồn kho.")) return;
  (p.Status, p.ReceivedAt, p.ReceivedBy) = (PurchaseStatus.Received, DateTime.Now, Session.Name); db.SaveChanges(); Note($"{Session.Name} đã xác nhận nhập kho phiếu #{p.Id}.");
 }
 void Cancel() {
  if (G.Pick<Purchase>() is not { } sel) return; using var db = Db.New(); var p = db.Purchases.Find(sel.Id)!;
  if (p.Status != PurchaseStatus.Pending) { Ui.Err($"Phiếu #{p.Id} đang ở trạng thái “{p.Status}”, không hủy được."); return; }
  if (!Ui.Ask($"Hủy phiếu nhập #{p.Id}? Không thể mở lại.")) return; p.Status = PurchaseStatus.Cancelled; db.SaveChanges(); Note($"Đã hủy phiếu #{p.Id}.");
 }
 void Detail() {
  if (G.Pick<Purchase>() is not { } p) return;
  using var f = new Form { Text = $"Phiếu nhập #{p.Id}", Font = Ui.Base, BackColor = Ui.Cream, StartPosition = FormStartPosition.CenterParent, Size = new Size(640, 460), MinimizeBox = false, Padding = new Padding(20) };
  var g = new Grid().Setup("Bánh", ">Số lượng", ">Đơn giá nhập", ">Thành tiền"); g.Load(p.Items, i => [i.ItemName, $"{i.Quantity} {i.Unit}", $"{Fmt.Vnd(i.UnitCost)} / {i.Unit}", Fmt.Vnd(i.Subtotal)]);
  var info = Ui.Lbl($"{p.Supplier}\nNgày lập {Fmt.Day(p.CreatedAt)}{(p.CreatedBy is null ? "" : " · lập bởi " + p.CreatedBy)}\nTrạng thái: {p.Status}{(p.ReceivedBy is null ? "" : $"  ·  nhập kho bởi {p.ReceivedBy}{(p.ReceivedAt is { } t ? " lúc " + Fmt.Date(t) : "")}")}{(string.IsNullOrEmpty(p.Note) ? "" : "\nGhi chú: " + p.Note)}\n"); info.Dock = DockStyle.Top;
  var total = Ui.Lbl($"Tổng cộng: {Fmt.Vnd(p.Total)}", Ui.Big); total.Dock = DockStyle.Bottom; total.AutoSize = false; total.Height = 40; total.TextAlign = ContentAlignment.MiddleRight;
  f.Controls.Add(g); f.Controls.Add(total); f.Controls.Add(info); f.ShowDialog(this);
 }
 void Create() {
  using var db = Db.New(); var suppliers = db.Suppliers.AsNoTracking().OrderBy(s => s.Name).Select(s => s.Name).ToList(); var products = db.Products.AsNoTracking().OrderBy(p => p.Id).ToList();
  using var f = new FieldForm("Tạo phiếu nhập", "Lưu phiếu nhập", 470);
  var cSup = f.Add("Xưởng bánh", FieldForm.Combo(suppliers.Cast<object>())); var dDate = f.Add("Ngày lập phiếu", new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", MaxDate = DateTime.Today, Value = DateTime.Today }); var tNote = f.Add("Ghi chú", FieldForm.Txt(null, 200));
  var grid = new TableLayoutPanel { AutoSize = true, ColumnCount = 4, Dock = DockStyle.Top, Margin = new Padding(0, 8, 0, 0) };
  foreach (var w in new[] { 210, 80, 60, 110 }) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, w));
  foreach (var h in new[] { "BÁNH", "SỐ LƯỢNG", "ĐƠN VỊ", "ĐƠN GIÁ NHẬP (₫)" }) grid.Controls.Add(Ui.Lbl(h, Ui.Small, Ui.Muted));
  var total = Ui.Lbl("Tổng tiền: 0 ₫", Ui.Bold); var rows = new List<(ComboBox P, NumericUpDown Q, Label U, NumericUpDown C)>();
  void Sum() { var s = rows.Where(r => r.P.SelectedIndex > 0).Sum(r => r.Q.Value * r.C.Value); total.Text = $"Tổng tiền: {Fmt.Vnd(s)}"; }
  for (var i = 0; i < 6; i++) {
   var cp = FieldForm.Combo(new[] { "— Chọn bánh —" }.Concat(products.Select(p => $"{p.Code} · {p.Name}")).Cast<object>()); cp.Width = 200; var q = FieldForm.Num(0, 0, 100000); q.Width = 70; var u = Ui.Lbl("—"); u.Margin = new Padding(0, 8, 0, 0); var c = FieldForm.Num(0, 0, 100000000, 1000); c.Width = 100;
   cp.SelectedIndexChanged += (_, _) => { u.Text = cp.SelectedIndex > 0 ? products[cp.SelectedIndex - 1].Unit : "—"; Sum(); }; q.ValueChanged += (_, _) => Sum(); c.ValueChanged += (_, _) => Sum();
   grid.Controls.AddRange([cp, q, u, c]); rows.Add((cp, q, u, c));
  }
  f.AddWide(grid); f.AddWide(total); f.AddWide(Ui.Lbl("Phiếu mới ở trạng thái “Chờ nhập kho”: số bánh chỉ được cộng vào tồn kho sau khi bạn xác nhận.", Ui.Small, Ui.Muted));
  f.Check = () => {
   if (suppliers.Count == 0) return "Chưa có xưởng bánh nào."; if (rows.Any(r => r.Q.Value > 0 && r.P.SelectedIndex <= 0)) return "Có dòng đã nhập số lượng nhưng chưa chọn bánh.";
   if (!rows.Any(r => r.P.SelectedIndex > 0 && r.Q.Value > 0)) return "Hãy chọn ít nhất một loại bánh với số lượng > 0."; return null;
  };
  if (f.ShowDialog() != DialogResult.OK) return;
  var pu = new Purchase { Supplier = (string)cSup.SelectedItem!, CreatedAt = dDate.Value.Date, CreatedBy = Session.Name, Note = string.IsNullOrWhiteSpace(tNote.Text) ? null : tNote.Text.Trim() };
  foreach (var r in rows.Where(r => r.P.SelectedIndex > 0 && r.Q.Value > 0)) { var p = products[r.P.SelectedIndex - 1]; pu.Items.Add(new PurchaseItem { ProductId = p.Id, ItemName = p.Name, Unit = p.Unit, Quantity = (int)r.Q.Value, UnitCost = r.C.Value }); }
  db.Purchases.Add(pu); db.SaveChanges(); Note($"Đã lưu phiếu nhập #{pu.Id}. Hãy kiểm hàng và xác nhận để cộng vào tồn kho.");
 }
}

class StockSub : Page {
 readonly TextBox q; readonly ComboBox level;
 public StockSub() : base(null, null) {
  q = Add("Tìm bánh", new TextBox(), 230); level = Add("Mức tồn", Combo("Tất cả", [$"Sắp hết (≤ {Svc.LowStock})"])); ClearButton();
  G.Setup("Mã|0.6", "Bánh|2.6", "Đơn vị|0.6", ">Đã nhập|0.8", ">Đã bán|0.8", ">Đã hủy|0.8", ">Tồn kho|0.8", "Tình trạng|1");
 }
 public override void Reload() {
  using var db = Db.New(); var rows = Svc.Stock(db).Where(s => (Svc.Has(s.Product.Name, q.Text) || Svc.Has(s.Product.Code, q.Text)) && (level.SelectedIndex == 0 || s.Left <= Svc.LowStock)).ToList();
  G.Load(rows, s => [s.Product.Code, $"{s.Product.Name}  ({s.Product.Category})", s.Product.Unit, s.In, s.Sold, s.Wasted, s.Left, s.Left < 0 ? "Âm kho" : s.Left == 0 ? "Hết hàng" : s.Left <= Svc.LowStock ? "Sắp hết" : "Còn hàng"],
   (r, s) => Grid.Tint(r, 7, s.Left <= 0 ? Color.FromArgb(246, 220, 217) : s.Left <= Svc.LowStock ? Color.FromArgb(247, 227, 181) : Color.FromArgb(200, 232, 205)));
  Note("Tồn = đã nhập kho (phiếu đã xác nhận) − đã bán (đơn chưa hủy) − bánh hủy.");
 }
}

class DiscardsSub : Page {
 readonly TextBox q, year; readonly ComboBox day, month;
 public DiscardsSub() : base(null, null) {
  q = Add("Tìm", new TextBox(), 220); day = Add("Ngày", Combo("Tất cả", Enumerable.Range(1, 31).Select(i => i.ToString())), 80); month = Add("Tháng", Combo("Tất cả", Enumerable.Range(1, 12).Select(i => $"Tháng {i}")), 100); year = Add("Năm", new TextBox(), 70); ClearButton();
  G.Setup("Ngày hủy", "Bánh", ">Số lượng", "Lý do", ">Giá trị", "Người ghi"); Act("Ghi nhận bánh hủy", Create, primary: true);
 }
 public override void Reload() {
  using var db = Db.New(); int? d = day.SelectedIndex > 0 ? day.SelectedIndex : null, m = month.SelectedIndex > 0 ? month.SelectedIndex : null;
  var list = db.Discards.AsNoTracking().OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).ToList().Where(x => Svc.InDate(x.CreatedAt, d, m, Num(year.Text)) && (Svc.Has(x.ProductName, q.Text) || Svc.Has(x.Reason, q.Text))).ToList();
  G.Load(list, x => [Fmt.Day(x.CreatedAt), x.ProductName, $"{x.Quantity} {x.Unit}", x.Reason, Fmt.Vnd(x.Value), x.CreatedBy]); Note($"{list.Count} lượt hủy · {list.Sum(x => x.Quantity)} bánh · {Fmt.Vnd(list.Sum(x => x.Value))}");
 }
 void Create() {
  using var db = Db.New(); var stock = Svc.Stock(db);
  using var f = new FieldForm("Ghi nhận bánh hủy", "Ghi nhận", 340);
  var cProd = f.Add("Bánh", FieldForm.Combo(stock.Select(s => (object)$"{s.Product.Code} · {s.Product.Name} (tồn {s.Left} {s.Product.Unit})"))); var nQty = f.Add("Số lượng", FieldForm.Num(1, 1, 100000));
  var cReason = f.Add("Lý do", FieldForm.Combo(DiscardReasons.All)); var dDate = f.Add("Ngày hủy", new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", MaxDate = DateTime.Today, Value = DateTime.Today }); var tNote = f.Add("Ghi chú", FieldForm.Txt(null, 100));
  f.Check = () => { if (cProd.SelectedIndex < 0) return "Hãy chọn loại bánh."; var s = stock[cProd.SelectedIndex]; return nQty.Value > s.Left ? $"Chỉ còn {s.Left} {s.Product.Unit} “{s.Product.Name}” trong kho." : null; };
  if (f.ShowDialog() != DialogResult.OK) return;
  var s = stock[cProd.SelectedIndex]; var day = dDate.Value.Date; var n = string.IsNullOrWhiteSpace(tNote.Text) ? null : tNote.Text.Trim(); var reason = (string)cReason.SelectedItem!;
  db.Discards.Add(new Discard { ProductId = s.Product.Id, ProductName = s.Product.Name, Unit = s.Product.Unit, Quantity = (int)nQty.Value, UnitPrice = s.Product.Price, Reason = n is null ? reason : $"{reason}: {n}", CreatedAt = day == DateTime.Today ? DateTime.Now : day.AddHours(12), CreatedBy = Session.Name });
  db.SaveChanges(); Note($"Đã ghi nhận hủy {(int)nQty.Value} {s.Product.Unit} “{s.Product.Name}”.");
 }
}

class ReportsPage : Page {
 readonly ComboBox type; readonly DateTimePicker from, to, month; readonly NumericUpDown year;
 readonly BarChart chart = new() { Dock = DockStyle.Fill }; readonly Grid revenue = new(), top = new(), custs = new(), waste = new();
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
  revenue.Setup("Ngày", ">Số đơn", ">Doanh thu"); top.Setup("Bánh", ">Số lượng", ">Doanh thu"); custs.Setup("Khách hàng", ">Số đơn", ">Chi tiêu"); waste.Setup("Ngày", ">Số bánh hủy", ">Giá trị");
  var tabs = new TabControl { Dock = DockStyle.Fill, Font = Ui.Bold, Padding = new Point(16, 5) };
  foreach (var (t, c) in new (string, Control)[] { ("Doanh thu chi tiết", revenue), ("Bánh bán chạy", top), ("Khách hàng chi nhiều nhất", custs), ("Bánh hủy", waste) }) { var tp = new TabPage(t) { BackColor = Ui.Cream, Padding = new Padding(0, 6, 0, 0) }; tp.Controls.Add(c); tabs.TabPages.Add(tp); }
  var chartBox = new Panel { Dock = DockStyle.Top, Height = 200, BackColor = Ui.Card, Padding = new Padding(10), Margin = new Padding(0, 0, 0, 10) }; chartBox.Controls.Add(chart);
  var body = new TableLayoutPanel { ColumnCount = 1, RowCount = 4 }; body.RowStyles.Add(new RowStyle(SizeType.AutoSize)); body.RowStyles.Add(new RowStyle(SizeType.AutoSize)); body.RowStyles.Add(new RowStyle(SizeType.Absolute, 210)); body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
  period.Margin = new Padding(0, 0, 0, 8); body.Controls.Add(period, 0, 0); body.Controls.Add(stats, 0, 1); body.Controls.Add(chartBox, 0, 2); body.Controls.Add(tabs, 0, 3); Body(body);
 }
 void Quick(int days) { to.Value = DateTime.Today; from.Value = DateTime.Today.AddDays(-(days - 1)); type.SelectedIndex = 0; Reload(); }
 string Kind => type.SelectedIndex == 1 ? "month" : type.SelectedIndex == 2 ? "year" : "range";
 public override void Reload() {
  using var db = Db.New(); var r = cur = Svc.Report(db, Kind, from.Value.Date, to.Value.Date, month.Value, (int)year.Value); var unit = r.PeriodUnit == "tháng" ? "Tháng" : "Ngày";
  period.Text = $"Kỳ báo cáo: {r.Title}"; vals["rev"].Text = Fmt.Vnd(r.Revenue); vals["cnt"].Text = r.OrderCount.ToString(); vals["avg"].Text = Fmt.Vnd(r.AvgOrder); vals["w"].Text = r.Wasted.ToString(); vals["wv"].Text = Fmt.Vnd(r.WastedValue);
  chart.Set(r.Series.Select(d => (d.Label ?? Fmt.Day(d.Day), d.Revenue, $"{d.Label} · {Fmt.Vnd(d.Revenue)} · {d.Orders} đơn")));
  revenue.Columns[0].HeaderText = unit.ToUpperInvariant(); waste.Columns[0].HeaderText = unit.ToUpperInvariant();
  revenue.Load(r.Series.Append(new DayPoint(r.To, r.Revenue, r.OrderCount, "TỔNG")), d => [d.Label, d.Orders, Fmt.Vnd(d.Revenue)], (row, d) => { if (d.Label == "TỔNG") row.DefaultCellStyle.Font = Ui.Bold; else if (d.Orders == 0) row.DefaultCellStyle.ForeColor = Ui.Muted; });
  top.Load(r.TopProducts, t => [t.Name, t.Count, Fmt.Vnd(t.Value)]); custs.Load(r.TopCustomers, t => [t.Name, t.Count, Fmt.Vnd(t.Value)]); waste.Load(r.Discards, d => [d.Label, d.Quantity, Fmt.Vnd(d.Value)]);
  Note("");
 }
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

// Dàn trang báo cáo để in: mỗi dòng gồm ba cột (trái · giữa căn phải · phải căn phải), tự sang trang khi hết chỗ.
static class ReportDoc {
 record Line(string A, string B, string C, int Style); // 0 thường · 1 đậm · 2 tiêu đề lớn · 3 đầu mục
 public static PrintDocument Build(ReportVm r, string who) {
  var unit = r.PeriodUnit == "tháng" ? "Tháng" : "Ngày"; var L = new List<Line> { new("Rookie Bakery — Báo cáo & thống kê", "", "", 2), new($"Kỳ báo cáo: {r.Title}", "", "", 1), new($"Xuất bởi {who} lúc {Fmt.Date(DateTime.Now)}", "", "", 0), new("", "", "", 0),
   new("Tổng quan", "", "", 3), new("Doanh thu", "", Fmt.Vnd(r.Revenue), 0), new("Số đơn (không tính đã hủy)", "", r.OrderCount.ToString(), 0), new("Giá trị trung bình / đơn", "", Fmt.Vnd(r.AvgOrder), 0), new("Bánh hủy", "", $"{r.Wasted} bánh · {Fmt.Vnd(r.WastedValue)}", 0), new("", "", "", 0),
   new($"Doanh thu theo {r.PeriodUnit}", "", "", 3), new(unit, "Số đơn", "Doanh thu", 1) };
  L.AddRange(r.Series.Select(d => new Line(d.Label ?? Fmt.Day(d.Day), d.Orders.ToString(), Fmt.Vnd(d.Revenue), 0))); L.Add(new("Tổng", r.OrderCount.ToString(), Fmt.Vnd(r.Revenue), 1)); L.Add(new("", "", "", 0));
  L.Add(new("Bánh bán chạy", "", "", 3)); L.Add(new("Bánh", "Số lượng", "Doanh thu", 1)); L.AddRange(r.TopProducts.Select(t => new Line(t.Name, t.Count.ToString(), Fmt.Vnd(t.Value), 0))); L.Add(new("", "", "", 0));
  L.Add(new("Khách hàng chi nhiều nhất", "", "", 3)); L.Add(new("Khách hàng", "Số đơn", "Chi tiêu", 1)); L.AddRange(r.TopCustomers.Select(t => new Line(t.Name, t.Count.ToString(), Fmt.Vnd(t.Value), 0))); L.Add(new("", "", "", 0));
  L.Add(new($"Bánh hủy theo {r.PeriodUnit}", "", "", 3)); if (r.Discards.Count == 0) L.Add(new("Không có bánh hủy trong kỳ này.", "", "", 0)); else { L.Add(new(unit, "Số bánh hủy", "Giá trị", 1)); L.AddRange(r.Discards.Select(d => new Line(d.Label, d.Quantity.ToString(), Fmt.Vnd(d.Value), 0))); }
  var doc = new PrintDocument { DocumentName = "Báo cáo Rookie" }; var i = 0; doc.DefaultPageSettings.Margins = new Margins(60, 60, 60, 60);
  var fonts = new[] { new Font("Segoe UI", 10f), new Font("Segoe UI", 10f, FontStyle.Bold), new Font("Georgia", 18f, FontStyle.Bold), new Font("Segoe UI", 12f, FontStyle.Bold) };
  doc.PrintPage += (_, e) => {
   var g = e.Graphics!; var m = e.MarginBounds; float y = m.Top;
   while (i < L.Count) {
    var ln = L[i]; var f = fonts[ln.Style]; var hgt = f.GetHeight(g) + (ln.Style == 3 ? 8 : 4); if (y + hgt > m.Bottom) { e.HasMorePages = true; return; }
    g.DrawString(ln.A, f, Brushes.Black, m.Left, y + (ln.Style == 3 ? 6 : 0));
    var right = new StringFormat { Alignment = StringAlignment.Far }; if (ln.B != "") g.DrawString(ln.B, f, Brushes.Black, m.Left + m.Width * 0.62f, y, right); if (ln.C != "") g.DrawString(ln.C, f, Brushes.Black, m.Right, y, right);
    if (ln.Style == 3) g.DrawLine(Pens.Gray, m.Left, y + hgt - 1, m.Right, y + hgt - 1);
    y += hgt; i++;
   }
   e.HasMorePages = false; i = 0;
  };
  return doc;
 }
}
