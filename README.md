# Rookie Bakery
Website bán bánh bằng ASP.NET Core MVC, Razor và EF Core SQLite (.NET 8).

## Chạy
```sh
dotnet restore
dotnet run --urls http://localhost:5080
```

WinForms chỉ chạy trên Windows và không dùng để render website. Có thể xây dựng ứng dụng quản trị WinForms riêng kết nối API trong giai đoạn sau.

## Ứng dụng quản trị WinForms
`Rookie.Admin/` là app quản trị WinForms (Windows, .NET 8) dùng chung model và file `rookie.db` với web, nên dữ liệu hai bên luôn khớp. Web admin (`/admin`) vẫn còn nguyên.
```sh
dotnet run --project Rookie.Admin
```
Đăng nhập `admin` / `admin` (đặt biến môi trường `Admin__Password` để đổi mật khẩu cho lần tạo DB đầu tiên). App tự tìm `rookie.db` ở thư mục dự án web; đặt `ROOKIE_DB` để chỉ file khác. Nếu chưa có DB, app tạo mới kèm dữ liệu mẫu. Xuất Excel ghi file .xlsx; xuất PDF dùng máy in "Microsoft Print to PDF" của Windows.
- `/`: bánh bán chạy/bánh mới; “Xem tất cả bánh” mở `/Shop`.
- `/Shop`: trang riêng với tìm kiếm, bộ lọc loại bánh/bánh mới/bán chạy và sắp xếp giá.
- `/Shop/Details/1`: chi tiết bánh và chọn số lượng.
- `/Shop/Cart`: giỏ hàng của tài khoản đăng nhập, cập nhật số lượng hoặc xóa bánh. Khách chưa đăng nhập được chuyển đến trang đăng nhập khi thêm bánh.
- `/Account/Register`, `/Account/Login`: đăng ký (tên tài khoản, họ tên, email, mật khẩu, xác nhận), đăng nhập bằng tên tài khoản hoặc email; đăng nhập thành công mở hồ sơ, trừ khi đang trở lại một trang được bảo vệ.
- `/Account/Profile`: hồ sơ, số điện thoại và địa chỉ giao hàng mặc định.
- `/Orders/Checkout`: nhập người nhận, địa chỉ, ghi chú và chọn COD (cần đăng nhập).
- `/Orders/Success/{id}`: thông báo đã nhận đơn, đang chờ xác nhận.
- `/Orders`: lịch sử; `/Orders/Details/{id}`: chi tiết đơn của tài khoản đang đăng nhập.

## Dữ liệu và bảo mật
Khách hàng, đơn hàng, bánh, kho và nhân viên đều nằm chung trong `rookie.db` (gitignore), dùng chung giữa trang khách, trang `/admin` và app WinForms: đơn khách đặt trên web hiện ngay ở trạng thái “Mới” để nhân viên xác nhận. Đường dẫn cấu hình qua `ConnectionStrings:Shop`. Khi đổi model, `DbInit.SchemaVersion` tăng và `rookie.db` cũ được đổi tên thành `rookie.db.bak-*` rồi tạo lại kèm dữ liệu mẫu (khách mẫu `khach1@example.com` … mật khẩu `123456`). File `rookie-commerce.db` của bản cũ không còn được dùng; tài khoản/đơn trong đó không tự chuyển sang. Đơn lưu bản chụp tên/giá/số lượng và thông tin nhận hàng tại thời điểm đặt.

Mật khẩu khách và nhân viên được băm PBKDF2 (`Data/Pw.cs`); phiên khách và phiên nhân viên là hai cookie tách riêng; tài khoản khách bị khóa không đăng nhập hay đặt hàng được; chống CSRF cho POST, giới hạn thử đăng nhập/đăng ký theo IP. Đơn được kiểm tra quyền sở hữu. Giá được truy vấn lại từ database; yêu cầu checkout có khóa chống tạo trùng. Cookie xác thực không chứa mật khẩu. Khi triển khai thực tế cần HTTPS, lưu Data Protection keys an toàn và cấu hình forwarded headers/rate limiting đúng cho reverse proxy.

Giỏ hàng lưu trong session phía server, tách theo tài khoản và dùng chung giữa các trang; session hiện dùng memory cache và hết hạn sau 8 giờ không hoạt động hoặc khi server khởi động lại. Giỏ localStorage phiên bản cũ không được nhập sang phiên bản này. Hồ sơ, địa chỉ và đơn tồn tại trong SQLite sau khi khởi động lại. Chỉ có một địa chỉ mặc định trong hồ sơ; có thể sửa địa chỉ cho từng đơn khi checkout.

COD là phương thức duy nhất đang hoạt động; chưa tích hợp cổng thanh toán, email xác nhận, xác minh email, quên mật khẩu hay phí giao hàng thực tế. Miễn phí giao hàng trong bản thử nghiệm; đặt hàng chưa trừ tiền. Mã giảm giá trên banner cũ chưa áp dụng, vì vậy banner hiện không quảng cáo giảm giá. Không coi bản này là hệ thống sẵn sàng vận hành thương mại. Ảnh Unsplash và font Google cần Internet; đánh giá/thông tin liên hệ là minh họa.

## Kiểm thử tích hợp
Chạy server kiểm thử trong terminal riêng bằng database tạm, không dùng dữ liệu thật:
```sh
ConnectionStrings__Shop="Data Source=/tmp/rookie-smoke.db" dotnet run --urls http://127.0.0.1:5081
```
Terminal khác (Python 3, không cần thư viện ngoài):
```sh
ROOKIE_TEST_URL=http://127.0.0.1:5081 python3 tests/smoke.py
```
Kiểm thử tạo hai tài khoản và một đơn trên database tạm; kiểm tra CSRF, giỏ, đăng ký/đăng nhập, hồ sơ, checkout, giá phía server, gửi lại đơn và quyền truy cập giữa hai tài khoản.

## Web API (`/api`)
Dành cho website khách viết bằng React và app quản trị WinForms. Hiện app WinForms vẫn đọc thẳng `rookie.db`, chưa gọi qua API. Xác thực bằng JWT: gửi header `Authorization: Bearer <token>`, token hết hạn sau 8 giờ. Mỗi request đều kiểm tra lại DB, nên tài khoản bị khóa/xóa hoặc nhân viên bị đổi vai trò thì token cũ bị từ chối ngay. Khi triển khai thật cần đặt `Jwt:Key` (≥ 32 ký tự), nếu không token mất hiệu lực mỗi lần khởi động lại.

**Thử API bằng Swagger** (chỉ bật ở môi trường Development, mặc định khi chạy `dotnet run`): mở http://localhost:5000/swagger (hoặc cổng bạn đặt bằng `--urls`). Gọi `POST /api/auth/staff` với `{"username":"admin","password":"admin"}`, hoặc `POST /api/auth/login` với `{"email":"khach1@example.com","password":"123456"}`. Sau đó chép `token`, bấm **Authorize**, dán token vào rồi gọi các API cần quyền. Tài khoản mẫu khi chạy Development: quản trị `admin`/`admin`, nhân viên `thuha`/`nhanvien`, khách `khach1@example.com`/`123456` (khách `khach6` đang bị khóa).

| Quyền | Endpoint |
|---|---|
| Công khai | `POST auth/register` (giống form đăng ký), `POST auth/login` (khách, email hoặc tên tài khoản), `POST auth/staff` (nhân viên, `{username,password}`), `GET categories`, `GET products[?category=]`, `GET products/{id}` |
| Đã đăng nhập | `GET auth/me` |
| Khách (`Customer`) | `GET/PUT me/profile`; `POST orders` `{items:[{productId,quantity}],name,phone,address,note,requestKey}`; `GET orders/mine`, `GET orders/mine/{id}`, `POST orders/mine/{id}/cancel` (chỉ khi đơn còn “Mới”) |
| Nhân viên (“Quản trị” hoặc “Nhân viên”) | `GET admin/dashboard`, `GET admin/reports?type=range&from=&to=` \| `type=month&month=yyyy-MM` \| `type=year&year=`; CRUD `admin/products`, `admin/categories`; `admin/customers` (+ `POST {id}/lock`); `admin/orders` (+ `PUT {id}/status` theo đúng quy trình, `GET admin/order-statuses`); `GET admin/suppliers`, `GET admin/stock`; `admin/purchases` (+ `POST {id}/receive`, `POST {id}/cancel`); `admin/discards`, `GET admin/discard-reasons` |
| Chỉ “Quản trị” | `GET/POST admin/staff`, `PUT admin/staff/{id}`, `POST admin/staff/{id}/lock` |

Lỗi nghiệp vụ trả về `{ "error": "..." }` (400/409), lỗi dữ liệu nhập trả về ProblemDetails (400). Các API đăng nhập/đăng ký giới hạn 10 lần/phút mỗi IP. CORS mặc định cho `http://localhost:5173` và `:3000`, đổi bằng `Cors:Origins`.
