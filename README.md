# Rookie Bakery
Website bán bánh bằng ASP.NET Core MVC, Razor và EF Core SQLite (.NET 8).

## Chạy
```sh
dotnet restore
dotnet run --urls http://0.0.0.0:5080
```
SQLite `rookie.db` tự tạo với 6 sản phẩm mẫu. Trang chủ truy vấn sản phẩm qua EF Core; lọc bánh bán chạy/bánh mới, tìm kiếm, giỏ hàng và cập nhật số lượng chạy trong trình duyệt. Giỏ hàng lưu localStorage. Chưa có thanh toán hoặc quản trị; đánh giá, thông tin liên hệ là nội dung minh họa. Ảnh từ Unsplash cần Internet.

WinForms chỉ chạy trên Windows và không dùng để render website. Có thể xây dựng ứng dụng quản trị WinForms riêng kết nối API trong giai đoạn sau.

## Web API (`/api`)
Xác thực bằng JWT: gửi header `Authorization: Bearer <token>`. Token hết hạn sau 8 giờ. Cấu hình `Jwt:Key` (≥ 32 ký tự) khi triển khai thật, nếu không token mất hiệu lực mỗi lần khởi động lại. Thêm cột `PasswordHash` nên cần xóa `rookie.db` cũ để tạo lại.

| Quyền | Endpoint |
|---|---|
| Công khai | `POST auth/register`, `POST auth/login` (khách), `POST auth/admin` (mật khẩu `Admin:Password`), `GET categories`, `GET products[?category=]`, `GET products/{id}` |
| Đã đăng nhập | `GET auth/me` |
| Customer | `POST orders` `{items:[{productId,quantity}],note}`, `GET orders/mine`, `GET orders/mine/{id}`, `POST orders/mine/{id}/cancel` (chỉ khi đơn còn “Mới”) |
| Admin | `GET admin/dashboard`, `GET admin/reports?days=7\|30\|90`; CRUD `admin/products`, `admin/categories`; `admin/customers` (+ `POST {id}/lock`); `admin/orders` (+ `PUT {id}/status`, `GET admin/order-statuses`); `admin/purchases` |

Lỗi nghiệp vụ trả `{ "error": "..." }`. API đăng nhập giới hạn 10 lần/phút mỗi IP. CORS mặc định cho `http://localhost:5173` và `:3000`, đổi bằng `Cors:Origins`.
