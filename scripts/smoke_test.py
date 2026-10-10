"""End-to-end checks against a running app; uses temporary accounts and removes uploaded files."""
import http.cookiejar, urllib.request, urllib.parse, urllib.error, re, uuid, sys
base = sys.argv[1] if len(sys.argv) > 1 else 'http://localhost:5080'

def client():
    """Creates an independent HTTP client with its own cookie jar to represent one user session."""
    return urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
def request(c, path, data=None, headers=None):
    """Sends a local HTTP request and returns status, response bytes, and final URL, including HTTP error responses."""
    try:
        r = c.open(urllib.request.Request(base + path, data=data, headers=headers or {}))
        return r.status, r.read(), r.url
    except urllib.error.HTTPError as e:
        return e.code, e.read(), e.url

def token(body):
    """Extracts the anti-forgery token from a rendered HTML form for subsequent POST requests."""
    return re.search(rb'name="__RequestVerificationToken"[^>]*value="([^"]+)"', body).group(1).decode()
def post(c, path, fields):
    """Fetches a form token and submits URL-encoded fields through the supplied user session."""
    _, page, _ = request(c, path)
    fields['__RequestVerificationToken'] = token(page)
    return request(c, path, urllib.parse.urlencode(fields).encode(), {'Content-Type':'application/x-www-form-urlencoded'})
def register(c):
    """Creates and signs in a uniquely named test account, verifies the dashboard, and returns its username."""
    suffix = uuid.uuid4().hex[:10]
    status, page, url = post(c, '/Account/Register', dict(DisplayName='Smoke Test', Username='test_' + suffix, Email=suffix+'@example.test', Password='TestPass123!', ConfirmPassword='TestPass123!'))
    assert status == 200 and b'<title>Dashboard' in page, (status, page.decode())
    return 'test_' + suffix

def upload_many(c, files):
    """Builds and submits a token-protected multipart upload from filename and content pairs."""
    _, page, _ = request(c, '/Files/Upload')
    boundary = '----test' + uuid.uuid4().hex
    data = f'--{boundary}\r\nContent-Disposition: form-data; name="__RequestVerificationToken"\r\n\r\n{token(page)}\r\n'.encode()
    for name, content in files:
        data += f'--{boundary}\r\nContent-Disposition: form-data; name="files"; filename="{name}"\r\nContent-Type: application/octet-stream\r\n\r\n'.encode() + content + b'\r\n'
    data += f'--{boundary}--\r\n'.encode()
    return request(c, '/Files/Upload', data, {'Content-Type': 'multipart/form-data; boundary=' + boundary})

def upload(c, name, content):
    """Uploads one file using the same multipart helper as batch uploads."""
    return upload_many(c, [(name, content)])

a, b = client(), client()
status, _, url = request(a, '/Files/Index')
assert '/Account/Login' in url, 'Anonymous files must require login'
username = register(a)
register(b)
content = b'Private file content\n'
status, page, _ = upload(a, 'sample.txt', content)
assert status == 200 and b'sample.txt' in page
file_id = re.search(rb'/Files/Download/(\d+)', page).group(1).decode()
assert request(a, '/Files/Download/'+file_id)[1] == content
assert request(b, '/Files/Download/'+file_id)[0] == 404
_, bpage, _ = request(b, '/Files/Index')
assert b'sample.txt' not in bpage
status, _, _ = request(b, '/Files/Delete/'+file_id, urllib.parse.urlencode({'__RequestVerificationToken':token(bpage)}).encode(), {'Content-Type':'application/x-www-form-urlencoded'})
assert status == 404, 'Another user must not delete files'
assert request(a, '/Files/Delete/'+file_id, b'')[0] == 400, 'CSRF must be rejected'
assert b'sample.txt' in request(a, '/Files/Index?search=sample')[1]
assert b'sample.txt' not in request(a, '/Files/Index?search=missing')[1]
assert b'sample.txt' not in request(a, '/Files/Index?type=.pdf')[1]
assert b'This file type is not allowed' in upload(a, 'bad.exe', b'bad')[1]
assert b'Choose a non-empty file' in upload(a, 'empty.txt', b'')[1]
assert b'Files must be' in upload(a, 'large.txt', b'x'*(25*1024*1024+1))[1]
_, page, _ = request(a, '/Files/Index')
status, body, _ = request(a, '/Files/Delete/'+file_id, urllib.parse.urlencode({'__RequestVerificationToken':token(page)}).encode(), {'Content-Type':'application/x-www-form-urlencoded'})
assert status == 200 and b'sample.txt deleted' in body
assert request(a, '/Files/Download/'+file_id)[0] == 404
# A mixed-validity selection must save neither file.
assert b'This file type is not allowed' in upload_many(a, [('reject-batch.txt', b'ok'), ('bad.exe', b'bad')])[1]
assert b'reject-batch.txt' not in request(a, '/Files/Index')[1]
assert b'Choose at most' in upload_many(a, [('count.txt', b'x')] * 21)[1]
assert b'Selected files must total' in upload_many(a, [('total.txt', b'x' * (20*1024*1024+1024))] * 5)[1]
status, page, _ = upload_many(a, [('batch.txt', b'first'), ('batch.txt', b'second'), ('third.csv', b'a,b')])
assert status == 200 and b'3 files uploaded successfully' in page
ids = re.findall(rb'/Files/Download/(\d+)', page)
assert len(ids) == 3, 'All selected files must be saved, including duplicate names'
assert sorted(request(a, '/Files/Download/'+i.decode())[1] for i in ids) == sorted([b'first', b'second', b'a,b'])
for i in ids:
    assert request(b, '/Files/Download/'+i.decode())[0] == 404
    _, current, _ = request(a, '/Files/Index')
    assert request(a, '/Files/Delete/'+i.decode(), urllib.parse.urlencode({'__RequestVerificationToken':token(current)}).encode(), {'Content-Type':'application/x-www-form-urlencoded'})[0] == 200
_, page, _ = request(a, '/Files/Dashboard')
request(a, '/Account/Logout', urllib.parse.urlencode({'__RequestVerificationToken':token(page)}).encode(), {'Content-Type':'application/x-www-form-urlencoded'})
assert '/Account/Login' in request(a, '/Files/Index')[2]
assert b'<title>Dashboard' in post(a, '/Account/Login', dict(Username=username, Password='TestPass123!'))[1]
assert b'Incorrect username or password' in post(client(), '/Account/Login', dict(Username=username, Password='wrong'))[1]
print('PASS: registration, login, logout, single/batch upload, batch rejection, duplicate names, download, search, filter, deletion, size/type validation, CSRF, anonymous protection, and cross-user isolation.')
print('Two test accounts remain in the test database; uploaded files were deleted.')
