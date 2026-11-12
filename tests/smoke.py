"""Integration smoke test. Start the app with temporary databases, then set ROOKIE_TEST_URL.
Example: ROOKIE_TEST_URL=http://127.0.0.1:5081 python3 tests/smoke.py
Never point this test at a production database: it registers two test accounts and places an order.
"""
import html
import http.cookiejar
import os
import re
import secrets
import urllib.error
import urllib.parse
import urllib.request

BASE = os.environ['ROOKIE_TEST_URL'].rstrip('/')
assert urllib.parse.urlsplit(BASE).hostname in ('localhost', '127.0.0.1'), 'Local test server only'

class Client:
    def __init__(self):
        self.http = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))

    def request(self, path, values=None):
        data = None if values is None else urllib.parse.urlencode(values).encode()
        try:
            with self.http.open(BASE + path, data=data) as response:
                return response.status, response.read().decode(), response.url
        except urllib.error.HTTPError as e:
            return e.code, e.read().decode(), e.url

    def post(self, path, values, form=None):
        status, body, _ = self.request(form or path)
        assert status == 200, (path, status)
        token = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', body)
        assert token, path
        return self.request(path, dict(values, __RequestVerificationToken=html.unescape(token[1])))

suffix=secrets.token_hex(5)
user=Client()
status,body,_=user.request('/')
assert status==200 and body.count('<article class="product"')==6
assert user.request('/Shop/Details/1')[0]==200
assert user.request('/Shop/Details/9999')[0]==404
assert '/Account/Login' in user.request('/Shop/Cart')[2]
assert '/Account/Login' in user.request('/Orders')[2]
assert '/Account/Login' in user.post('/Shop/Add', {'id':1,'quantity':2}, '/')[2]
status,body,_=user.request('/Shop?q=COOKIE&category=Cookies&kind=new&sort=price-asc')
assert status==200 and body.count('<article class="product"')==1 and 'Cookie cuddle' in body
assert user.request('/Shop?q=no-such-product')[1].count('<article class="product"')==0
email=f'smoke-{suffix}@example.test'
username=f'baker_{suffix}'
status,body,url=user.post('/Account/Register',{'UserName':username,'Name':'Smoke Baker','Email':email,'Password':'A-secure-test-123','ConfirmPassword':'A-secure-test-123'})
assert status==200 and '/Account/Profile' in url
assert username in body and email in body
assert user.request('/Shop/Add', {'id':1,'quantity':1})[0]==400, 'CSRF must fail'
assert user.post('/Shop/Add', {'id':1,'quantity':100}, '/')[0]==400
assert user.post('/Shop/Add', {'id':1,'quantity':2}, '/')[0]==200
assert user.post('/Shop/Add', {'id':6,'quantity':1}, '/')[0]==200
status,body,_=user.post('/Shop/Update',{'id':6,'quantity':0},'/Shop/Cart')
assert status==200 and 'Cookie cuddle' not in body
status,body,_=user.post('/Account/Profile',{'Name':'Smoke Baker','Phone':'0901234567','Address':'123 Test Street'})
assert status==200 and '123 Test Street' in body
status,body,_=user.request('/Orders/Checkout')
assert status==200 and '123 Test Street' in body
key=html.unescape(re.search(r'name="RequestKey"[^>]*value="([^"]+)"',body)[1])
values={'Name':'Smoke Baker','Phone':'0901234567','Address':'123 Test Street','Payment':'CARD','RequestKey':key}
token=html.unescape(re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"',body)[1])
status,body,_=user.request('/Orders/Checkout',dict(values,__RequestVerificationToken=token))
assert status==200 and 'validation-summary-errors' in body
# Use the exact valid form key from the failed response, without minting another key.
token=html.unescape(re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"',body)[1])
values.update(Payment='COD',__RequestVerificationToken=token,price='1')
status,body,url=user.request('/Orders/Checkout',values)
assert status==200 and '/Orders/Success/' in url, (status,url)
order_id=url.rsplit('/',1)[1]
assert '570,000' in body or '570.000' in body, 'Must calculate price from database'
assert 'Chưa thanh toán' in body
assert '0901234567' in body and '123 Test Street' in body
assert 'Chưa có chiếc bánh' in user.request('/Shop/Cart')[1]
assert user.request('/Orders/Details/'+order_id)[0]==200
assert '#RK' in user.request('/Orders')[1]
status,_,url=user.request('/Orders/Checkout',values)
assert status==200 and url.endswith('/Orders/Success/'+order_id), 'Retry must reuse same order'
other=Client()
status,_,_=other.post('/Account/Register',{'UserName':f'other_{suffix}','Name':'Other Baker','Email':f'other-{suffix}@example.test','Password':'A-secure-test-123','ConfirmPassword':'A-secure-test-123'})
assert status==200
assert other.request('/Orders/Details/'+order_id)[0]==404
assert other.request('/Orders/Success/'+order_id)[0]==404
status,_,_=user.post('/Account/Logout',{},'/Account/Profile')
assert status==200
status,body,_=user.post('/Account/Login',{'Email':email,'Password':'wrong-password'})
assert status==200 and 'validation-summary-errors' in body
status,_,url=user.post('/Account/Login',{'Email':username,'Password':'A-secure-test-123'})
assert status==200 and '/Account/Profile' in url
assert user.request('/Orders/Details/'+order_id)[0]==200
print('PASS: product details, cart add/update/remove/validation, CSRF, register/login/logout, profile/address, checkout validation, authoritative prices, COD order, cart cleared, idempotent retry, order history and cross-account isolation.')
