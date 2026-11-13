import { useEffect, useState } from 'react';
import { cartApi, img, toast, vnd } from '../api.js';

// Gõ tự do, chỉ gửi lên server khi rời ô hoặc nhấn Enter (tránh gửi một request cho mỗi phím).
function QtyInput({ value, disabled, onCommit }) {
  const [draft, setDraft] = useState(String(value));
  useEffect(() => setDraft(String(value)), [value]);
  const commit = () => { const v = parseInt(draft, 10); if (Number.isNaN(v) || v === value) setDraft(String(value)); else onCommit(v); };
  return <div className="quantity-form"><label>Số lượng<input type="number" min="0" max="99" value={draft} disabled={disabled}
    onChange={e => setDraft(e.target.value)} onBlur={commit} onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); commit(); } }} /></label></div>;
}

// Trang giỏ hàng: đổi số lượng / xóa bánh, tổng tiền cập nhật ngay mà không tải lại trang.
// Giá và tổng do server tính (GET/PUT /api/cart), island chỉ hiển thị.
export default function Cart() {
  const [cart, setCart] = useState(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(0);
  useEffect(() => { cartApi.get().then(setCart).catch(e => setError(e.message)); }, []);

  const set = async (id, qty) => {
    setBusy(id);
    try { setCart(await cartApi.set(id, Math.max(0, Math.min(99, qty)))); if (qty === 0) toast('Đã xóa bánh khỏi giỏ.'); }
    catch (e) { if (e.status !== 401) toast(e.message); }
    finally { setBusy(0); }
  };

  if (error) return <p className="validation" role="alert">{error}</p>;
  if (!cart) return <p className="muted" role="status">Đang tải giỏ bánh…</p>;
  if (cart.lines.length === 0) return <div className="empty-card"><h2>Chưa có chiếc bánh nào ở đây.</h2><p>Thêm chút ngọt ngào cho ngày của bạn nhé.</p><a className="btn" href="/#cakes">Khám phá tiệm bánh ↗</a></div>;

  return <div className="checkout-grid">
    <div className="form-card">{cart.lines.map(l =>
      <div className="basket-row" key={l.productId}>
        <img src={img(l.image, 180).replace('q=85', 'q=70')} alt={l.name} />
        <div>
          <a href={'/Shop/Details/' + l.productId}><h3>{l.name}</h3></a>
          <p>{vnd(l.price)} / {l.unit}</p>
          <QtyInput value={l.quantity} disabled={busy === l.productId} onCommit={q => set(l.productId, q)} />
          <button type="button" className="remove" disabled={busy === l.productId} onClick={() => set(l.productId, 0)}>Xóa khỏi giỏ</button>
        </div>
        <b>{vnd(l.subtotal)}</b>
      </div>)}
    </div>
    <aside className="form-card summary">
      <h2>Hộp bánh của bạn</h2>
      <div className="total">Tạm tính <b>{vnd(cart.total)}</b></div>
      <p>Giao hàng: miễn phí</p>
      <a className="btn" href="/Orders/Checkout">Tiến hành đặt hàng ↗</a>
      <a href="/#cakes">Tiếp tục chọn bánh</a>
    </aside>
  </div>;
}
