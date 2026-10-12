#!/usr/bin/env python3
"""Restart only the InvoiceAI Compose services and validate persisted artificial seed data."""
import argparse, hashlib, json, subprocess, time, urllib.request, urllib.error, uuid
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--cloud',action='store_true');args=p.parse_args()
root=Path(__file__).resolve().parents[1];base='http://127.0.0.1:8080'
compose=['docker','compose','-f','compose.yaml']+(['-f','.local/compose.cloud.yaml'] if args.cloud else [])
def call(path,body=None):
 req=urllib.request.Request(base+path,data=None if body is None else json.dumps(body).encode(),headers={'Content-Type':'application/json'})
 with urllib.request.urlopen(req,timeout=15) as r:return json.load(r)
def wait_ready():
 end=time.monotonic()+125
 while time.monotonic()<end:
  try:
   if call('/api/health/ready')['status']=='ready':return
  except (urllib.error.URLError,ConnectionError):pass
  time.sleep(.5)
 raise AssertionError('readiness did not recover')
def restart():
 subprocess.run(compose+['restart','-t','0','api','ai'],cwd=root,check=True)
 wait_ready()
assert call('/api/config')['mode']=='Demo'
data=(root/'samples/receipt.pdf').read_bytes();boundary=uuid.uuid4().hex
body=(f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="receipt.pdf"\r\nContent-Type: application/pdf\r\n\r\n'.encode()+data+f'\r\n--{boundary}--\r\n'.encode())
with urllib.request.urlopen(urllib.request.Request(base+'/api/documents',data=body,headers={'Content-Type':'multipart/form-data; boundary='+boundary})) as r:doc=json.load(r)['documentId']
i=call('/api/invoices',{'documentId':doc,'kind':'receipt'});id=i['id'];call(f'/api/invoices/{id}/analyze',{})
restart()
end=time.monotonic()+125
while time.monotonic()<end:
 i=call(f'/api/invoices/{id}')
 if i['state'] not in ['Uploaded','Extracting']:break
 time.sleep(.5)
assert i['state']=='Validated',i
print('PASS persisted analysis resumes after abrupt API/AI restart',flush=True)
export=call(f'/api/invoices/{id}/export');answer=call('/api/chat',{'invoiceId':id,'question':'Ce total are bonul?','conversationId':None});revision=i['revision']
restart()
current=call(f'/api/invoices/{id}');assert current['revision']==revision and current['state']=='Exported';assert current['total']==120
print('PASS structured result and revision survive restart',flush=True)
assert call(f'/api/invoices/{id}/export')==export
print('PASS export snapshot remains idempotent after restart',flush=True)
again=call('/api/chat',{'invoiceId':id,'question':'Care este pagina sursei?','conversationId':answer['conversationId']});assert again['conversationId']==answer['conversationId'] and again['citations']
print('PASS conversation and scoped citations survive restart',flush=True)
content=urllib.request.urlopen(base+f'/api/documents/{doc}/content').read();assert hashlib.sha256(content).digest()==hashlib.sha256(data).digest()
print('PASS original S3 object survives restart',flush=True)
with urllib.request.urlopen(urllib.request.Request(base+f'/api/documents/{doc}',method='DELETE')) as r:assert r.status==202
