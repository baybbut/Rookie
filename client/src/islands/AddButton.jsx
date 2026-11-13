import { useState } from 'react';
import { cartApi, toast } from '../api.js';

// Nút "+" thêm một chiếc bánh vào giỏ, không tải lại trang. Chưa đăng nhập thì api() tự chuyển sang trang đăng nhập.
export default function AddButton({ id, name }) {
  const [busy, setBusy] = useState(false);
  const add = async () => {
    setBusy(true);
    try { await cartApi.add(+id); toast(`Đã thêm ${name} vào giỏ ♡`); }
    catch (e) { if (e.status !== 401) toast(e.message); }
    finally { setBusy(false); }
  };
  return <button className="add" type="button" disabled={busy} aria-label={`Thêm ${name} vào giỏ`} onClick={add}>+</button>;
}
