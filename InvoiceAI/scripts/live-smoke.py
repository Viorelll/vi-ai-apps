#!/usr/bin/env python3
"""Opt-in Azure integration smoke. Never executed by default CI; uses only artificial seed data."""
import argparse, json, time, urllib.request, uuid
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--base-url',default='http://127.0.0.1:8080');p.add_argument('--accept-cloud-costs',action='store_true');args=p.parse_args()
if not args.accept_cloud_costs:p.error('Azure extraction/model calls incur costs; pass --accept-cloud-costs explicitly')
base=args.base_url.rstrip('/');root=Path(__file__).resolve().parents[1]
def call(path,body=None):
 req=urllib.request.Request(base+path,data=None if body is None else json.dumps(body).encode(),headers={'Content-Type':'application/json'})
 with urllib.request.urlopen(req,timeout=90) as r:return json.load(r)
assert call('/api/config')['mode']=='Azure', 'This smoke requires explicit Azure mode'
assert call('/api/health/ready')['status']=='ready'
boundary=uuid.uuid4().hex;data=(root/'samples/invoice.pdf').read_bytes()
body=(f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="invoice.pdf"\r\nContent-Type: application/pdf\r\n\r\n'.encode()+data+f'\r\n--{boundary}--\r\n'.encode())
with urllib.request.urlopen(urllib.request.Request(base+'/api/documents',data=body,headers={'Content-Type':'multipart/form-data; boundary='+boundary})) as r:doc=json.load(r)
assert not doc['duplicate'], 'Use a clean test database or explicitly delete an old seed first; existing review data is preserved'
i=call('/api/invoices',{'documentId':doc['documentId'],'kind':'invoice'});id=i['id'];call(f'/api/invoices/{id}/analyze',{})
end=time.monotonic()+300
while time.monotonic()<end:
 i=call(f'/api/invoices/{id}')
 if i['state'] not in ['Uploaded','Extracting']:break
 time.sleep(1)
assert i['state'] in ['NeedsReview','Validated'],i['errorCode']
assert i['fields'] and any(f['evidence'] for f in i['fields'])
assert i['explanation'] and i['inputTokens']>0 and i['outputTokens']>0
assert all(s['documentId']==doc['documentId'] for f in i['fields'] for s in f['evidence'])
assert i['total']==119 and any(x['code']=='TOTAL_MISMATCH' for x in i['issues']), 'Check actual OCR mapping and deterministic rule output'
print(json.dumps({'status':'passed','invoiceId':id,'documentId':doc['documentId'],'inputTokens':i['inputTokens'],'outputTokens':i['outputTokens'],'promptVersion':'invoice-explain-v1','providerApiVersion':'2024-11-30'},indent=2))
