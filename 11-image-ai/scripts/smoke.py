#!/usr/bin/env python3
"""Functional image pipeline checks against real PostgreSQL and MinIO; no Python dependencies."""
import argparse, hashlib, json, struct, time, urllib.request, urllib.error, uuid, zlib
parser=argparse.ArgumentParser()
parser.add_argument('--base',default='http://127.0.0.1:5080')
parser.add_argument('--live',action='store_true',help='Explicit opt-in to potentially billable Foundry generation and edit')
parser.add_argument('--verify-project',help='Verify previously saved project and content after service restart')
parser.add_argument('--report',default='/tmp/imageai-smoke.json')
args=parser.parse_args()
checks=[]
def check(condition,name):
 assert condition,name
 checks.append(name)
 print('PASS',name)
def call(path,body=None,method=None,expected=200,headers=None):
 data=json.dumps(body).encode() if body is not None else None
 req=urllib.request.Request(args.base+path,data=data,headers=headers or ({'Content-Type':'application/json'} if data else {}),method=method)
 try:
  with urllib.request.urlopen(req,timeout=160) as response:status=response.status;raw=response.read()
 except urllib.error.HTTPError as e:status=e.code;raw=e.read()
 assert status==expected,(path,status,raw[:500])
 return json.loads(raw) if raw[:1] in [b'{',b'['] else raw
def png(w,h,mask=False):
 def chunk(kind,data):return struct.pack('!I',len(data))+kind+data+struct.pack('!I',zlib.crc32(kind+data)&0xffffffff)
 row=b'\xff\xff\xff\xff'*w
 pixels=b''.join(b'\0'+(b'\0\0\0\0'+row[4:] if mask and y==0 else row) for y in range(h))
 return b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('!2I5B',w,h,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(pixels))+chunk(b'IEND',b'')
def upload(bytes,name='input.png',expected=200):
 boundary=uuid.uuid4().hex
 body=(f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="{name}"\r\nContent-Type: image/png\r\n\r\n').encode()+bytes+f'\r\n--{boundary}--\r\n'.encode()
 req=urllib.request.Request(args.base+'/api/documents',data=body,headers={'Content-Type':f'multipart/form-data; boundary={boundary}'})
 try:
  with urllib.request.urlopen(req,timeout=30) as r:status=r.status;raw=r.read()
 except urllib.error.HTTPError as e:status=e.code;raw=e.read()
 assert status==expected,(status,raw[:500])
 return json.loads(raw)
def ready(id):
 for _ in range(140):
  p=call('/api/images/projects/'+id)
  if p['state'] not in ['Queued','Generating','Editing','RetryWaiting']:return p
  time.sleep(1)
 raise AssertionError('job timed out')
config=call('/api/config')
if args.verify_project:
 p=call('/api/images/projects/'+args.verify_project)
 check(p['state']=='Ready' and len(p['assets'])>=2,'project versions survive restart')
 for a in p['assets']:check(hashlib.sha256(call('/api/images/assets/'+a['id']+'/content')).hexdigest()==a['sha256'],'immutable content survives restart')
 print(json.dumps({'projectId':p['id'],'checks':checks}));raise SystemExit(0)
if config['providerMode']!='Demo' and not args.live:raise SystemExit('Refusing Azure calls without --live')
check(call('/health/ready')['status']=='ready','database migrations and object storage ready')
call('/api/images/projects',{'brief':'x'},expected=400)
check(True,'short brief rejected')
fixture=png(1024,1024)
doc=upload(fixture)
check(doc['width']==1024 and doc['height']==1024,'real PNG upload and dimension validation')
check(upload(fixture)['id']==doc['id'],'duplicate upload deduplicates by SHA256')
check(call('/api/documents/'+doc['id']+'/content')==fixture,'S3 preserves original bytes')
upload(b'not an image',expected=400)
check(True,'forged MIME rejected')
p=call('/api/images/projects',{'brief':'Premium product on a warm minimalist background'},expected=201)
pid=p['id'];path='/api/images/projects/'+pid
call(path+'/generate',{'requestId':str(uuid.uuid4()),'revision':0,'size':'9999x9999'},expected=400)
check(True,'unsupported size rejected')
request={'requestId':str(uuid.uuid4()),'revision':0,'size':'1024x1024','quality':'medium','inputDocumentId':doc['id']}
q=call(path+'/generate',request,expected=202)
check(call(path+'/generate',request,expected=202)['runId']==q['runId'],'request id safely deduplicates generation')
conflict={**request,'quality':'high'}
call(path+'/generate',conflict,expected=409)
check(True,'request id cannot be reused for a different payload')
p=ready(pid)
check(p['state']=='Ready' and len(p['assets'])==1,'durable generation produces one persisted asset')
a=p['assets'][0];original=call('/api/images/assets/'+a['id']+'/content')
check(hashlib.sha256(original).hexdigest()==a['sha256'],'generated content hash matches persisted metadata')
mask=upload(png(1024,1024,True),'mask.png')
wrong=upload(png(32,32,True),'wrong.png')
body={'requestId':str(uuid.uuid4()),'revision':p['revision'],'inputAssetId':a['id'],'maskDocumentId':wrong['id'],'prompt':'Change background to blue, preserving product','quality':'medium'}
call(path+'/edits',body,expected=400)
check(True,'wrong mask dimensions rejected')
call(path+'/edits',{**body,'maskDocumentId':doc['id']},expected=400)
check(True,'empty mask selection rejected')
p2=call('/api/images/projects',{'brief':'Another independent project'},expected=201)
call('/api/images/projects/'+p2['id']+'/edits',{**body,'revision':0,'maskDocumentId':mask['id']},expected=404)
check(True,'cross-project asset rejected')
body['maskDocumentId']=mask['id']
call(path+'/edits',{**body,'revision':0},expected=409)
check(True,'stale revision rejected')
call(path+'/edits',body,expected=202)
p=ready(pid)
check(p['state']=='Ready' and len(p['assets'])==2,'durable edit produces second immutable version')
b=p['assets'][1]
check(b['parentAssetId']==a['id'],'edit preserves parent asset lineage')
check(call('/api/images/assets/'+a['id']+'/content')==original,'editing does not overwrite original')
call('/api/documents/'+mask['id'],method='DELETE',expected=409)
check(True,'immutable run retains referenced mask')
call('/api/documents/'+wrong['id'],method='DELETE',expected=204)
call('/api/documents/'+wrong['id'],expected=404)
check(True,'unused document deleted and metadata hidden')
report={'providerMode':config['providerMode'],'projectId':pid,'checks':checks,'passed':len(checks),'azureVerified':args.live}
with open(args.report,'w') as f:json.dump(report,f,indent=2)
print(json.dumps(report))
