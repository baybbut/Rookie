import { useEffect, useState } from 'react';
import { api, img, vnd } from '../api.js';
import AddButton from './AddButton.jsx';

const TABS = [['best', 'Bán chạy nhất ↗'], ['new', 'Bánh mới ra lò'], ['all', 'Tất cả bánh']];

// Mục "Hôm nay, bạn thèm gì?" ở trang chủ: lấy bánh từ GET /api/products, lọc theo tab và tìm kiếm ngay khi gõ.
export default function Products() {
  const [products, setProducts] = useState(null);
  const [error, setError] = useState('');
  const [filter, setFilter] = useState('best');
  const [term, setTerm] = useState('');
  useEffect(() => { api('/products').then(setProducts).catch(e => setError(e.message)); }, []);

  const t = term.trim().toLocaleLowerCase('vi');
  const shown = (products ?? []).filter(p => (filter === 'all' || (filter === 'best' ? p.isBestSeller : p.isNew)) && `${p.name} ${p.category}`.toLocaleLowerCase('vi').includes(t));

  return <>
    <div className="shop-controls">
      <div className="tabs" role="group" aria-label="Lọc bánh">{TABS.map(([k, label]) => <button key={k} type="button" className={filter === k ? 'active' : ''} aria-pressed={filter === k} onClick={() => setFilter(k)}>{label}</button>)}</div>
      <label className="search"><span>⌕</span><input id="search" type="search" placeholder="Tìm chiếc bánh bạn thích…" aria-label="Tìm kiếm bánh" value={term} onChange={e => setTerm(e.target.value)} /></label>
    </div>
    {error && <p className="validation" role="alert">{error}</p>}
    {!products && !error && <p className="muted" role="status">Đang tải bánh…</p>}
    <div className="product-grid">{shown.map(p =>
      <article className="product" key={p.id}>
        <div className="product-image"><span className="badge">{p.isNew ? 'JUST BAKED' : 'FAN FAVORITE'}</span><a href={'/Shop/Details/' + p.id}><img src={img(p.image, 700)} alt={p.name} loading="lazy" /></a><AddButton id={p.id} name={p.name} /></div>
        <div className="product-meta"><small>{p.category}</small><span>★★★★★</span></div>
        <div className="product-title"><h3><a href={'/Shop/Details/' + p.id}>{p.name}</a></h3><b>{vnd(p.price)}</b></div>
        <p>{p.description}</p>
      </article>)}</div>
    {products && shown.length === 0 && <p id="empty">Chưa tìm thấy bánh phù hợp. Thử một tên khác nhé!</p>}
  </>;
}
