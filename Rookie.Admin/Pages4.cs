using System.Text.RegularExpressions;
using Rookie.Models;
namespace Rookie.Admin;

// Quản lý thanh toán (quản trị + nhân viên): Chưa thanh toán → (ghi nhận) Chờ xác nhận → (xác nhận) Đã thanh toán → (hoàn tiền) Đã hoàn tiền.
// Server kiểm tra đúng bước và ghi tên người thao tác theo token.
class PaymentsPage : Page {
 readonly TextBox q; readonly DateTimePicker from, to; readonly ComboBox status; readonly Button bRecord, bConfirm, bRefund;
 public PaymentsPage() : base("Payments", "Quản lý thanh toán") {
  q = Add("Tìm đơn / khách", new TextBox(), 220); from = Add("Từ ngày", new DateTimePicker(), 130); to = Add("Đến ngày", new DateTimePicker(), 130); status = Add("Thanh toán", Combo("Tất cả", PaymentStatus.All), 140); ClearButton();
  G.Setup("Mã|0.45", "Khách hàng|1.6", "Ngày đặt|1.1", ">Số tiền|0.9", "Trạng thái đơn|1", "Thanh toán|1.1", "Hình thức|1.1", "Ghi nhận bởi|1.1", "Xác nhận bởi|1.1");
  bRecord = Act("Ghi nhận", Record, primary: true); bConfirm = Act("Xác nhận", Confirm); bRefund = Act("Hoàn tiền", Refund, danger: true); Act("Chi tiết", Detail, reload: false);
  G.SelectionChanged += (_, _) => Buttons(); G.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) Detail(); };
 }
 void Buttons() { var o = G.Pick<Order>(); bRecord.Enabled = o is not null && o.PaymentStatus == PaymentStatus.Unpaid && o.Status != OrderStatus.Cancelled; bConfirm.Enabled = o?.PaymentStatus == PaymentStatus.Pending; bRefund.Enabled = o?.PaymentStatus == PaymentStatus.Paid; }
 public override void Reload() {
  IEnumerable<Order> r = Api.Get<List<Order>>("api/admin/orders").OrderByDescending(o => o.Id).ToList();
  if (!string.IsNullOrWhiteSpace(q.Text)) { var t = q.Text.Trim().TrimStart('#'); r = r.Where(o => o.Id.ToString() == t || Svc.Has(o.Customer?.FullName, t) || Svc.Has(o.Customer?.Phone, t)); }
  if (Pick(from) is { } a) r = r.Where(o => o.CreatedAt >= a); if (Pick(to) is { } b) r = r.Where(o => o.CreatedAt < b.AddDays(1)); if (Sel(status) is { } s) r = r.Where(o => o.PaymentStatus == s);
  var list = r.ToList();
  G.Load(list, o => [$"#{o.Id}", $"{o.Customer?.FullName}  ({o.Customer?.Phone})", Fmt.Date(o.CreatedAt), Fmt.Vnd(o.Total), o.Status, o.PaymentStatus, o.PaymentMethod ?? "—", o.PaymentRecordedBy ?? "—", o.PaymentConfirmedBy ?? "—"],
   (row, o) => { Grid.Tint(row, 4, Ui.OrderColor(o.Status)); Grid.Tint(row, 5, Ui.PaymentColor(o.PaymentStatus)); });
  decimal Sum(string st) => list.Where(o => o.PaymentStatus == st).Sum(o => o.Total);
  Note($"Đã thu {Fmt.Vnd(Sum(PaymentStatus.Paid))} · chờ xác nhận {Fmt.Vnd(Sum(PaymentStatus.Pending))} · chưa thu {Fmt.Vnd(list.Where(o => o.PaymentStatus == PaymentStatus.Unpaid && o.Status != OrderStatus.Cancelled).Sum(o => o.Total))} (không tính đơn hủy) · đã hoàn {Fmt.Vnd(Sum(PaymentStatus.Refunded))}"); Buttons();
 }
 void Record() {
  if (G.Pick<Order>() is not { } o) return;
  using var f = new FieldForm($"Ghi nhận thanh toán — đơn #{o.Id}", "Ghi nhận", 320);
  f.Add("Số tiền", Ui.Lbl(Fmt.Vnd(o.Total), Ui.Bold)); var cMethod = f.Add("Hình thức", FieldForm.Combo(PaymentStatus.Methods)); var tNote = f.Add("Ghi chú", FieldForm.Txt(null, 200));
  f.AddWide(Ui.Lbl("Sau khi ghi nhận, thanh toán ở trạng thái “Chờ xác nhận”; kiểm tra tiền đã vào rồi bấm “Xác nhận thanh toán”.", Ui.Small, Ui.Muted));
  f.Check = () => Ui.Try(() => Api.Post<Order>($"api/admin/orders/{o.Id}/payment/record", new { method = (string)cMethod.SelectedItem!, note = tNote.Text.Trim() }));
  if (f.ShowDialog() == DialogResult.OK) Note($"Đã ghi nhận thanh toán đơn #{o.Id}, chờ xác nhận.");
 }
 void Confirm() {
  if (G.Pick<Order>() is not { } o) return;
  if (!Ui.Ask($"Xác nhận đã nhận đủ {Fmt.Vnd(o.Total)} cho đơn #{o.Id} ({o.PaymentMethod})?")) return;
  var x = Api.Post<Order>($"api/admin/orders/{o.Id}/payment/confirm"); Note($"{x.PaymentConfirmedBy} đã xác nhận thanh toán đơn #{x.Id}.");
 }
 void Refund() {
  if (G.Pick<Order>() is not { } o) return;
  using var f = new FieldForm($"Hoàn tiền — đơn #{o.Id}", "Hoàn tiền", 320);
  f.Add("Số tiền hoàn", Ui.Lbl(Fmt.Vnd(o.Total), Ui.Bold)); var tNote = f.Add("Lý do hoàn tiền", FieldForm.Txt(null, 200));
  f.Check = () => tNote.Text.Trim() == "" ? "Hãy nhập lý do hoàn tiền." : Ui.Try(() => Api.Post<Order>($"api/admin/orders/{o.Id}/payment/refund", new { note = tNote.Text.Trim() }));
  if (f.ShowDialog() == DialogResult.OK) Note($"Đã hoàn tiền đơn #{o.Id}.");
 }
 void Detail() {
  if (G.Pick<Order>() is not { } o) return;
  static string? By(string? who, DateTime? at) => who is null ? null : $"{who}{(at is { } t ? " lúc " + Fmt.Date(t) : "")}";
  var g = new Grid().Setup("Sản phẩm", ">Đơn giá", ">SL", ">Thành tiền"); g.Load(o.Items, i => [i.ProductName, Fmt.Vnd(i.UnitPrice), i.Quantity, Fmt.Vnd(i.Subtotal)]);
  Viewer.Show(this, $"Thanh toán — đơn #{o.Id}", [("Khách hàng", $"{o.Customer?.FullName} · {o.Customer?.Phone}"), ("Ngày đặt", Fmt.Date(o.CreatedAt)), ("Trạng thái đơn", o.Status), ("Số tiền", Fmt.Vnd(o.Total)),
   ("Thanh toán", o.PaymentStatus), ("Hình thức", o.PaymentMethod), ("Ghi nhận bởi", By(o.PaymentRecordedBy, o.PaymentRecordedAt)), ("Xác nhận bởi", By(o.PaymentConfirmedBy, o.PaymentConfirmedAt)), ("Hoàn tiền bởi", By(o.RefundedBy, o.RefundedAt)), ("Ghi chú", o.PaymentNote)], grid: g);
 }
}

// Kiểm kê cuối ngày: tổng hợp theo từng bánh số nhập, bán, hủy trong ngày (đã duyệt / chờ duyệt) và tồn cuối ngày.
class DailySub : Page {
 readonly DateTimePicker day; readonly ComboBox show; List<DailyRow> rows = [];
 public DailySub() : base(null, null) {
  day = Add("Ngày kiểm kê", new DateTimePicker(), 130); day.ShowCheckBox = false; day.MaxDate = DateTime.Today; day.Value = DateTime.Today;
  show = Add("Hiển thị", Combo("Tất cả bánh", ["Chỉ bánh có hủy", "Chỉ bánh có phát sinh"]), 170);
  Filters.Controls.Add(new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 19, 0, 0), Controls = { Ui.Btn("Hôm nay", () => day.Value = DateTime.Today), Ui.Btn("Làm mới", Reload) } });
  G.Setup("Mã|0.6", "Bánh|2", "Đơn vị|0.6", ">Nhập trong ngày|0.9", ">Bán trong ngày|0.9", ">Hủy (đã duyệt)|0.9", ">Giá trị hủy|1", ">Hủy chờ duyệt|0.9", ">Tồn cuối ngày|0.9");
  Act("In bảng kiểm kê", Print, reload: false);
 }
 public override void Reload() {
  rows = Api.Get<List<DailyRow>>($"api/admin/daily?date={day.Value:yyyy-MM-dd}")
   .Where(r => show.SelectedIndex == 0 || (show.SelectedIndex == 1 ? r.Wasted + r.PendingWaste > 0 : r.In + r.Sold + r.Wasted + r.PendingWaste > 0)).ToList();
  G.Load(rows, r => [r.Product.Code, r.Product.Name, r.Product.Unit, r.In, r.Sold, r.Wasted, Fmt.Vnd(r.WastedValue), r.PendingWaste, r.Left],
   (row, r) => { if (r.Wasted > 0) Grid.Tint(row, 5, Color.FromArgb(246, 220, 217)); if (r.PendingWaste > 0) Grid.Tint(row, 7, Color.FromArgb(247, 227, 181)); });
  Note($"Ngày {Fmt.Day(day.Value)}: hủy {rows.Sum(r => r.Wasted)} bánh ({Fmt.Vnd(rows.Sum(r => r.WastedValue))}) đã duyệt · {rows.Sum(r => r.PendingWaste)} bánh chờ duyệt · nhập {rows.Sum(r => r.In)} · bán {rows.Sum(r => r.Sold)}");
 }
 void Print() {
  var L = new List<PLine> { new("Rookie Bakery — KIỂM KÊ CUỐI NGÀY", Style: 2), new($"Ngày {Fmt.Day(day.Value)}", "", $"Lập bởi {Session.Name}", 1), new(""), new("Bánh hủy trong ngày (đã duyệt)", Style: 3), new("Bánh", "Số lượng", "Giá trị", 1) };
  var w = rows.Where(r => r.Wasted > 0).ToList(); if (w.Count == 0) L.Add(new("Không có bánh hủy được duyệt trong ngày.")); else L.AddRange(w.Select(r => new PLine(r.Product.Name, $"{r.Wasted} {r.Product.Unit}", Fmt.Vnd(r.WastedValue))));
  L.Add(new("Tổng bánh hủy", rows.Sum(r => r.Wasted).ToString(), Fmt.Vnd(rows.Sum(r => r.WastedValue)), 1)); if (rows.Sum(r => r.PendingWaste) is var p and > 0) L.Add(new($"Còn {p} bánh trong phiếu hủy chờ quản trị duyệt (chưa trừ tồn)."));
  L.AddRange([new(""), new("Tồn kho cuối ngày", Style: 3), new("Bánh", "Nhập / bán / hủy", "Tồn cuối ngày", 1)]);
  L.AddRange(rows.Select(r => new PLine(r.Product.Name, $"+{r.In} / −{r.Sold} / −{r.Wasted}", $"{r.Left} {r.Product.Unit}")));
  L.AddRange([new(""), new(""), new("Nhân viên kiểm kê", "", "Quản trị", 1), new("(ký, ghi rõ họ tên)", "", "(ký, ghi rõ họ tên)")]);
  PrintDoc.Preview(this, PrintDoc.Build($"Kiểm kê {day.Value:yyyy-MM-dd}", L));
 }
}

// Nhà cung cấp: thêm, sửa thông tin, ngừng / tiếp tục hợp tác (ngừng thì không chọn được khi lập phiếu nhập mới).
class SuppliersSub : Page {
 readonly TextBox q; readonly ComboBox status;
 public SuppliersSub() : base(null, null) {
  q = Add("Tìm nhà cung cấp", new TextBox(), 230); status = Add("Trạng thái", Combo("Tất cả", ["Đang hợp tác", "Ngừng hợp tác"]), 140); ClearButton();
  G.Setup("Nhà cung cấp|1.6", "Số điện thoại|1", "Địa chỉ|2.2", ">Phiếu nhập|0.7", ">Đã nhập kho|1", "Trạng thái|1");
  Act("Thêm nhà cung cấp", () => Edit(null), primary: true); Act("Sửa", () => { if (G.Pick<Supplier>() is { } s) Edit(s); }); Act("Ngừng / tiếp tục hợp tác", Toggle); Act("Bảng giá", Prices);
  G.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0 && G.Pick<Supplier>() is { } s) { Edit(s); Reload(); } };
 }
 public override void Reload() {
  var st = status.SelectedIndex; var purchases = Api.Get<List<Purchase>>("api/admin/purchases").GroupBy(p => p.Supplier).ToDictionary(g => g.Key, g => g.ToList());
  var list = Api.Get<List<Supplier>>("api/admin/suppliers").Where(s => (Svc.Has(s.Name, q.Text) || Svc.Has(s.Phone, q.Text) || Svc.Has(s.Address, q.Text)) && (st == 0 || s.IsActive == (st == 1))).ToList();
  G.Load(list, s => { var ps = purchases.GetValueOrDefault(s.Name) ?? []; return [s.Name, s.Phone ?? "—", s.Address ?? "—", ps.Count, Fmt.Vnd(ps.Where(p => p.Status == PurchaseStatus.Received).Sum(p => p.Total)), s.IsActive ? "Đang hợp tác" : "Ngừng hợp tác"]; },
   (r, s) => { Grid.Tint(r, 5, s.IsActive ? Color.FromArgb(200, 232, 205) : Color.FromArgb(246, 220, 217)); if (!s.IsActive) r.DefaultCellStyle.ForeColor = Ui.Muted; });
  Note($"{list.Count} nhà cung cấp · {list.Count(s => s.IsActive)} đang hợp tác");
 }
 void Edit(Supplier? cur) {
  var s = cur ?? new Supplier(); var isNew = cur is null;
  using var f = new FieldForm(isNew ? "Thêm nhà cung cấp" : $"Sửa nhà cung cấp — {s.Name}", isNew ? "Thêm" : "Lưu thay đổi", 320);
  var tName = f.Add("Tên nhà cung cấp", FieldForm.Txt(s.Name, 80)); var tPhone = f.Add("Số điện thoại", FieldForm.Txt(s.Phone, 20)); var tAddr = f.Add("Địa chỉ", FieldForm.Txt(s.Address, 200));
  if (!isNew) f.AddWide(Ui.Lbl("Đổi tên sẽ cập nhật tên trên các phiếu nhập cũ của nhà cung cấp này.", Ui.Small, Ui.Muted));
  f.Check = () => {
   if (tName.Text.Trim() == "") return "Hãy nhập tên nhà cung cấp."; if (!Regex.IsMatch(tPhone.Text.Trim(), @"^[0-9+()\-\s.]{8,20}$")) return "Số điện thoại không hợp lệ.";
   var body = new { name = tName.Text.Trim(), phone = tPhone.Text.Trim(), address = string.IsNullOrWhiteSpace(tAddr.Text) ? null : tAddr.Text.Trim() };
   return Ui.Try(() => { if (isNew) Api.Post<Supplier>("api/admin/suppliers", body); else Api.Put<Supplier>($"api/admin/suppliers/{s.Id}", body); });
  };
  if (f.ShowDialog() == DialogResult.OK) Note($"Đã lưu nhà cung cấp “{tName.Text.Trim()}”.");
 }
 // Danh sách bánh + bảng giá cố định của xưởng. Quản trị thêm / sửa / bỏ (server chặn nhân viên); nhân viên chỉ xem.
 // "Bánh mới của xưởng" (tiệm chưa bán) là nguồn để chọn khi "Thêm bánh mới". Đổi giá của xưởng chính thì giá nhập, giá bán của bánh đổi theo.
 void Prices() {
  if (G.Pick<Supplier>() is not { } s) return;
  using var f = new Form { Text = $"Danh sách bánh & bảng giá — {s.Name}", Font = Ui.Base, BackColor = Ui.Cream, StartPosition = FormStartPosition.CenterParent, Size = new Size(820, 560), MinimizeBox = false, Padding = new Padding(20, 16, 20, 12) };
  var g = new Grid().Setup("Mã|0.6", "Bánh|2", "Đơn vị|0.6", ">Giá nhập|1", "Tình trạng|1.6");
  var msg = Ui.Lbl("", Ui.Base, Ui.Muted); msg.Dock = DockStyle.Top; msg.Padding = new Padding(0, 0, 0, 8);
  void Load() {
   var rows = Api.Get<List<SupplierPriceRow>>($"api/admin/supplier-prices?supplierId={s.Id}");
   g.Load(rows, r => [r.Code ?? "—", r.Product, r.Unit, Fmt.Vnd(r.Price), r.ProductId is null ? "Tiệm chưa bán (chọn được khi thêm bánh mới)" : r.IsMain ? "Đang bán · xưởng chính" : "Đang bán"],
    (row, r) => { if (r.ProductId is null) Grid.Tint(row, 4, Color.FromArgb(247, 227, 181)); else if (r.IsMain) row.DefaultCellStyle.Font = Ui.Bold; });
   msg.Text = $"{rows.Count} bánh · {rows.Count(r => r.ProductId is null)} bánh tiệm chưa bán. Phiếu nhập từ xưởng này lấy đơn giá đúng theo bảng này.";
  }
  // Bánh mới của xưởng (tiệm chưa bán): tên, đơn vị, giá.
  void AddNew() {
   using var e = new FieldForm($"Thêm bánh của xưởng — {s.Name}", "Thêm", 280);
   var tName = e.Add("Tên bánh", FieldForm.Txt(null, 80)); var cUnit = e.Add("Đơn vị", FieldForm.Combo(ProductStatus.Units)); var n = e.Add("Giá nhập", FieldForm.Num(30000, 1000, 100000000, 1000));
   e.Check = () => tName.Text.Trim() == "" ? "Hãy nhập tên bánh." : Ui.Try(() => Api.Post<SupplierPrice>("api/admin/supplier-prices", new { supplierId = s.Id, name = tName.Text.Trim(), unit = (string)cUnit.SelectedItem!, price = n.Value }));
   if (e.ShowDialog(f) == DialogResult.OK) Load();
  }
  // Báo giá của xưởng cho một bánh tiệm đang bán (để nhập bánh đó từ xưởng này).
  void AddExisting() {
   var have = Api.Get<List<SupplierPriceRow>>($"api/admin/supplier-prices?supplierId={s.Id}").Where(r => r.ProductId is not null).Select(r => r.ProductId!.Value).ToHashSet();
   var list = Api.Get<List<Product>>("api/admin/products").Where(p => !have.Contains(p.Id)).ToList();
   if (list.Count == 0) { Ui.Info("Xưởng đã có giá cho mọi bánh tiệm đang bán."); return; }
   using var e = new FieldForm($"Thêm giá bánh đang bán — {s.Name}", "Thêm", 300);
   var cP = e.Add("Bánh", FieldForm.Combo(list.Select(p => (object)$"{p.Code} · {p.Name} ({p.Unit})"))); var n = e.Add("Giá nhập", FieldForm.Num(30000, 1000, 100000000, 1000));
   e.Check = () => Ui.Try(() => Api.Post<SupplierPrice>("api/admin/supplier-prices", new { supplierId = s.Id, productId = list[cP.SelectedIndex].Id, price = n.Value }));
   if (e.ShowDialog(f) == DialogResult.OK) Load();
  }
  void Edit() {
   if (g.Pick<SupplierPriceRow>() is not { } r) return; var free = r.ProductId is null;
   using var e = new FieldForm($"Sửa “{r.Product}” — {s.Name}", "Lưu", 280);
   var tName = free ? e.Add("Tên bánh", FieldForm.Txt(r.Product, 80)) : null; var cUnit = free ? e.Add("Đơn vị", FieldForm.Combo(ProductStatus.Units, r.Unit)) : null; var n = e.Add("Giá nhập", FieldForm.Num(r.Price, 1000, 100000000, 1000));
   if (r.IsMain) e.AddWide(Ui.Lbl("Đây là xưởng chính: giá nhập và giá bán của bánh sẽ tính lại theo tỉ lệ lợi nhuận.", Ui.Small, Ui.Muted));
   e.Check = () => free && tName!.Text.Trim() == "" ? "Hãy nhập tên bánh." : Ui.Try(() => Api.Put<SupplierPrice>($"api/admin/supplier-prices/{r.Id}", new { supplierId = s.Id, name = tName?.Text.Trim(), unit = (string?)cUnit?.SelectedItem, price = n.Value }));
   if (e.ShowDialog(f) == DialogResult.OK) Load();
  }
  void Remove() {
   if (g.Pick<SupplierPriceRow>() is not { } r) return; if (!Ui.Ask($"Bỏ “{r.Product}” khỏi danh sách của {s.Name}?")) return;
   Api.Delete($"api/admin/supplier-prices/{r.Id}"); Load();
  }
  var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 8, 0, 0) };
  bar.Controls.Add(Ui.Btn("Đóng", f.Close));
  if (Session.IsAdmin) { bar.Controls.Add(Ui.Btn("Bỏ", Remove, danger: true)); bar.Controls.Add(Ui.Btn("Sửa giá", Edit)); bar.Controls.Add(Ui.Btn("Thêm giá bánh đang bán", AddExisting)); bar.Controls.Add(Ui.Btn("Thêm bánh mới của xưởng", AddNew, primary: true)); g.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) Edit(); }; }
  Load(); f.Controls.Add(g); f.Controls.Add(bar); f.Controls.Add(msg); f.ShowDialog(this);
 }
 void Toggle() {
  if (G.Pick<Supplier>() is not { } s) return;
  if (!Ui.Ask(s.IsActive ? $"Ngừng hợp tác với “{s.Name}”?\nSẽ không chọn được nhà cung cấp này khi lập phiếu nhập mới; phiếu cũ giữ nguyên." : $"Tiếp tục hợp tác với “{s.Name}”?")) return;
  var x = Api.Post<Supplier>($"api/admin/suppliers/{s.Id}/toggle"); Note(x.IsActive ? $"Đã tiếp tục hợp tác với “{x.Name}”." : $"Đã ngừng hợp tác với “{x.Name}”.");
 }
}
