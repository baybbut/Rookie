// Gọi Web API từ các island. Island chạy trên trang Razor nên dùng luôn cookie đăng nhập của khách (fetch tự gửi),
// không cần JWT; request ghi dữ liệu gửi kèm token chống CSRF mà _Layout.cshtml đặt trong #csrf.
const csrf = () => document.querySelector('#csrf input[name="__RequestVerificationToken"]')?.value ?? '';

export class ApiError extends Error { constructor(status, message) { super(message); this.status = status; } }

function messageOf(status, body) {
  if (body?.error) return body.error;
  if (body?.errors) return Object.values(body.errors).flat().join(' ');
  if (status === 429) return 'Bạn thao tác quá nhanh, vui lòng thử lại sau một phút.';
  return 'Có lỗi xảy ra. Vui lòng tải lại trang và thử lại.';
}

export async function api(path, { method = 'GET', body } = {}) {
  let res;
  try {
    res = await fetch('/api' + path, {
      method, credentials: 'same-origin',
      headers: { Accept: 'application/json', ...(body ? { 'Content-Type': 'application/json' } : {}), ...(method !== 'GET' ? { RequestVerificationToken: csrf() } : {}) },
      body: body ? JSON.stringify(body) : undefined,
    });
  } catch { throw new ApiError(0, 'Không kết nối được tới máy chủ.'); }
  // Chưa đăng nhập: về trang đăng nhập rồi quay lại đúng trang đang xem.
  if (res.status === 401) { location.href = '/Account/Login?returnUrl=' + encodeURIComponent(location.pathname + location.search); throw new ApiError(401, 'Vui lòng đăng nhập.'); }
  const data = await res.json().catch(() => null);
  if (!res.ok) throw new ApiError(res.status, messageOf(res.status, data));
  return data;
}

// Giỏ hàng: mỗi lần đổi thì phát sự kiện "rookie:cart" để số đếm trên header và các island khác cập nhật theo.
const announce = cart => { window.dispatchEvent(new CustomEvent('rookie:cart', { detail: cart })); return cart; };
export const cartApi = {
  get: () => api('/cart'),
  add: (productId, quantity = 1) => api('/cart', { method: 'POST', body: { productId, quantity } }).then(announce),
  set: (productId, quantity) => api('/cart', { method: 'PUT', body: { productId, quantity } }).then(announce),
};

let toastTimer;
// Dùng chung khung #toast của layout Razor.
export function toast(message) {
  const el = document.querySelector('#toast'); if (!el) return;
  el.textContent = message; el.classList.add('show');
  clearTimeout(toastTimer); toastTimer = setTimeout(() => el.classList.remove('show'), 3000);
}

export const vnd = n => Number(n).toLocaleString('vi-VN') + ' ₫';
export const img = (id, w) => `https://images.unsplash.com/${id}?auto=format&fit=crop&w=${w}&q=85`;
