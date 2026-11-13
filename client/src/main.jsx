import { createRoot } from 'react-dom/client';
import Products from './islands/Products.jsx';
import AddButton from './islands/AddButton.jsx';
import Cart from './islands/Cart.jsx';

// React islands: trang vẫn do Razor (C#) dựng; chỗ nào có <div data-island="tên"> thì React gắn component tương ứng vào.
// Thuộc tính data-* của thẻ đó được truyền làm props, ví dụ <span data-island="add-button" data-id="1" data-name="…">.
const islands = { products: Products, 'add-button': AddButton, cart: Cart };
document.querySelectorAll('[data-island]').forEach(el => {
  const Component = islands[el.dataset.island];
  if (!Component) { console.warn('Không có island tên', el.dataset.island); return; }
  const { island, ...props } = el.dataset;
  el.replaceChildren(); // bỏ nội dung dự phòng (noscript/đang tải) do Razor đặt sẵn
  createRoot(el).render(<Component {...props} />);
});

// Số bánh trên header cập nhật khi island đổi giỏ (sự kiện phát ra từ cartApi).
window.addEventListener('rookie:cart', e => { const c = document.querySelector('#cartCount'); if (c) c.textContent = e.detail.count; });

// Nút kính lúp trên header: ở trang chủ thì nhảy tới ô tìm kiếm, trang khác thì về /#cakes.
document.querySelector('#searchToggle')?.addEventListener('click', () => {
  const search = document.querySelector('#search');
  if (search) { document.querySelector('#cakes')?.scrollIntoView(); search.focus({ preventScroll: true }); }
  else location.href = '/#cakes';
});
