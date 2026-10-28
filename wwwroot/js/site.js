"use strict";
const search=document.querySelector('#search');let filter='best';
function applyFilter(){if(!search)return;let count=0;const term=search.value.trim().toLocaleLowerCase('vi');document.querySelectorAll('.product').forEach(p=>{const match=(filter==='all'||p.dataset[filter]==='true')&&p.dataset.name.toLocaleLowerCase('vi').includes(term);p.hidden=!match;if(match)count++;});document.querySelector('#empty').hidden=count>0;document.querySelectorAll('[data-filter]').forEach(b=>{b.classList.toggle('active',b.dataset.filter===filter);b.setAttribute('aria-pressed',String(b.dataset.filter===filter));});}
document.querySelectorAll('[data-filter]').forEach(b=>b.onclick=()=>{filter=b.dataset.filter;applyFilter();});
search?.addEventListener('input',applyFilter);
document.querySelector('#seeAll')?.addEventListener('click',()=>{filter='all';search.value='';applyFilter();});
document.querySelector('#searchToggle')?.addEventListener('click',()=>{if(search){document.querySelector('#cakes').scrollIntoView();search.focus({preventScroll:true});}else location.href='/#cakes';});
let toastTimer;
function toast(message){const el=document.querySelector('#toast');el.textContent=message;el.classList.add('show');clearTimeout(toastTimer);toastTimer=setTimeout(()=>el.classList.remove('show'),3000);}
document.querySelectorAll('.add').forEach(b=>b.onclick=async()=>{b.disabled=true;try{const body=new URLSearchParams({id:b.dataset.id,quantity:'1',__RequestVerificationToken:document.querySelector('#csrf input').value});const res=await fetch('/Shop/Add',{method:'POST',headers:{Accept:'application/json'},body});if(!res.ok)throw Error();const result=await res.json();document.querySelector('#cartCount').textContent=result.count;toast('Đã thêm '+b.dataset.name+' vào giỏ ♡');}catch{toast('Chưa thêm được bánh. Vui lòng tải lại trang và thử lại.');}finally{b.disabled=false;}});
applyFilter();
