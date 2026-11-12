# Rookie Bakery
Website bán bánh bằng ASP.NET Core MVC, Razor và EF Core SQLite (.NET 8).

## Chạy
```sh
dotnet restore
dotnet run --urls http://localhost:5080
```

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
`rookie.db` giữ nguyên dữ liệu bánh hiện có; `rookie-commerce.db` tự tạo riêng cho khách hàng, đơn và các dòng sản phẩm. Không xóa database bánh cũ để nâng cấp. Khi khởi động, ứng dụng kiểm tra schema và thêm cột UserName/index nếu database tài khoản cũ chưa có; tài khoản cũ vẫn đăng nhập bằng email. Cả hai file đều được gitignore. Có thể cấu hình đường dẫn qua `ConnectionStrings:Shop` và `ConnectionStrings:Commerce`. Đơn lưu bản chụp tên/giá/số lượng và thông tin nhận hàng tại thời điểm đặt.

Mật khẩu được hash bằng ASP.NET Core PasswordHasher; xác thực cookie, chống CSRF cho POST, giới hạn thử đăng nhập/đăng ký theo IP. Đơn được kiểm tra quyền sở hữu. Giá được truy vấn lại từ database; yêu cầu checkout có khóa chống tạo trùng. Cookie xác thực không chứa mật khẩu. Khi triển khai thực tế cần HTTPS, lưu Data Protection keys an toàn và cấu hình forwarded headers/rate limiting đúng cho reverse proxy.

Giỏ hàng lưu trong session phía server, tách theo tài khoản và dùng chung giữa các trang; session hiện dùng memory cache và hết hạn sau 8 giờ không hoạt động hoặc khi server khởi động lại. Giỏ localStorage phiên bản cũ không được nhập sang phiên bản này. Hồ sơ, địa chỉ và đơn tồn tại trong SQLite sau khi khởi động lại. Chỉ có một địa chỉ mặc định trong hồ sơ; có thể sửa địa chỉ cho từng đơn khi checkout.

COD là phương thức duy nhất đang hoạt động; chưa tích hợp cổng thanh toán, email xác nhận, xác minh email, quên mật khẩu, quản trị trạng thái đơn, tồn kho hay phí giao hàng thực tế. Miễn phí giao hàng trong bản thử nghiệm; đặt hàng chưa trừ tiền. Mã giảm giá trên banner cũ chưa áp dụng, vì vậy banner hiện không quảng cáo giảm giá. Không coi bản này là hệ thống sẵn sàng vận hành thương mại. Ảnh Unsplash và font Google cần Internet; đánh giá/thông tin liên hệ là minh họa.

## Kiểm thử tích hợp
Chạy server kiểm thử trong terminal riêng bằng database tạm, không dùng dữ liệu thật:
```sh
ConnectionStrings__Shop="Data Source=/tmp/rookie-smoke-products.db" ConnectionStrings__Commerce="Data Source=/tmp/rookie-smoke-commerce.db" dotnet run --urls http://127.0.0.1:5081
```
Terminal khác (Python 3, không cần thư viện ngoài):
```sh
ROOKIE_TEST_URL=http://127.0.0.1:5081 python3 tests/smoke.py
```
Kiểm thử tạo hai tài khoản và một đơn trên database tạm; kiểm tra CSRF, giỏ, đăng ký/đăng nhập, hồ sơ, checkout, giá phía server, gửi lại đơn và quyền truy cập giữa hai tài khoản.

WinForms chỉ chạy trên Windows; ứng dụng quản trị WinForms riêng chưa được xây dựng.
