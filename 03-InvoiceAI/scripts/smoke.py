#!/usr/bin/env python3
"""Functional HTTP acceptance test. Uses artificial fixtures and cleans up its documents."""
import argparse, hashlib, json, time, urllib.request, urllib.error, uuid
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--base-url',default='http://127.0.0.1:5080');p.add_argument('--keep',action='store_true');args=p.parse_args()
root=Path(__file__).resolve().parents[1];base=args.base_url.rstrip('/');checks=[]
def call(path,method='GET',body=None,expected=200,headers=None):
 data=None if body is None else json.dumps(body).encode();h={'Content-Type':'application/json',**(headers or {})}
 req=urllib.request.Request(base+path,data=data,method=method,headers=h)
 try:
  with urllib.request.urlopen(req,timeout=30) as r:status=r.status;content=r.read()
 except urllib.error.HTTPError as e:status=e.code;content=e.read()
 assert status==expected,(method,path,status,content.decode()[:300])
 return json.loads(content) if content else None

def upload(kind):
 data=(root/'samples'/f'{kind}.pdf').read_bytes();boundary=uuid.uuid4().hex
 body=(f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="{kind}.pdf"\r\nContent-Type: application/pdf\r\n\r\n'.encode()+data+f'\r\n--{boundary}--\r\n'.encode())
 req=urllib.request.Request(base+'/api/documents',data=body,headers={'Content-Type':'multipart/form-data; boundary='+boundary})
 with urllib.request.urlopen(req) as r:return json.load(r),data

def wait(id):
 end=time.monotonic()+75
 while time.monotonic()<end:
  i=call('/api/invoices/'+id)
  if i['state'] not in ['Uploaded','Extracting']:assert i['state']!='Failed',i['errorCode'];return i
  time.sleep(.4)
 raise AssertionError('processing timeout')

def check(label):checks.append(label);print('PASS',label,flush=True)
assert call('/api/config')['mode']=='Demo', 'Run this acceptance test in explicit Demo mode'
call('/api/health/ready');check('PostgreSQL, S3 and AI readiness')
created=[]
try:
 for kind in ['invoice','receipt']:
  doc,data=upload(kind);created.append(doc['documentId']);duplicate,_=upload(kind);assert duplicate['duplicate'] and duplicate['documentId']==doc['documentId'];check(kind+': hash deduplication')
  i=call('/api/invoices','POST',{'documentId':doc['documentId'],'kind':kind},201);id=i['id']
  a=call(f'/api/invoices/{id}/analyze','POST',expected=202);b=call(f'/api/invoices/{id}/analyze','POST',expected=202);assert a['jobId']==b['jobId'];check(kind+': durable analysis idempotency')
  i=wait(id);assert i['currency']=='MDL' and len(i['lines'])==1
  assert all(f['evidence'] for f in i['fields']);check(kind+': fields, lines and page evidence')
  if kind=='invoice':
   assert any(x['code']=='TOTAL_MISMATCH' for x in i['issues']);assert i['total']==119
   call(f'/api/invoices/{id}/export',expected=409);check('100 + 20 != 119 blocks export')
   revision=i['revision'];i=call(f'/api/invoices/{id}/fields','PATCH',{'revision':revision,'corrections':{'total':120}})
   assert i['state']=='Validated' and i['total']==120
   call(f'/api/invoices/{id}/fields','PATCH',{'revision':revision,'corrections':{'total':121}},409);check('explicit correction and stale revision rejection')
  export=call(f'/api/invoices/{id}/export');again=call(f'/api/invoices/{id}/export');assert export==again;assert export['amounts']['total']==120
  if kind=='invoice':assert export['corrections']['total']==120 and next(x for x in export['evidence'] if x['path']=='total')['value']==119
  check(kind+': idempotent versioned JSON export preserves raw evidence')
  answer=call('/api/chat','POST',{'invoiceId':id,'question':'Care sunt sumele și sursele?','conversationId':None});assert answer['citations'];check(kind+': scoped chat and citations')
  content=urllib.request.urlopen(base+f'/api/documents/{doc["documentId"]}/content').read();assert hashlib.sha256(content).digest()==hashlib.sha256(data).digest();check(kind+': original stream integrity')
 if not args.keep:
  for doc in created:
   job=call('/api/documents/'+doc,'DELETE',expected=202);call('/api/documents/'+doc,expected=404)
   end=time.monotonic()+30
   while time.monotonic()<end:
    state=call('/api/jobs/'+job['jobId'])
    if state['status'] in ['Completed','Failed']:break
    time.sleep(.2)
   assert state['status']=='Completed',state
  check('deletion hides originals immediately')
finally:
 report={'checks':checks,'passed':len(checks),'mode':'deterministic-demo','azureLiveValidated':False}
 print(json.dumps(report,indent=2))
