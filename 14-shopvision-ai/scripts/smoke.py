import argparse, json, time, urllib.request, uuid
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--base',default='http://127.0.0.1:8080');p.add_argument('--live',action='store_true');args=p.parse_args()
def request(path,body=None,headers=None):
 req=urllib.request.Request(args.base+path,data=body,headers=headers or {})
 with urllib.request.urlopen(req,timeout=30) as response:return json.load(response)
def post(path,obj):return request(path,json.dumps(obj).encode(),{'Content-Type':'application/json'})
request('/api/health/ready')
image=(Path(__file__).parent.parent/'fixtures/black-usb-adapter.png').read_bytes()
boundary='shopvision-'+uuid.uuid4().hex
body=(f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="black-usb-adapter.png"\r\nContent-Type: image/png\r\n\r\n').encode()+image+f'\r\n--{boundary}--\r\n'.encode()
doc=request('/api/documents',body,{'Content-Type':f'multipart/form-data; boundary={boundary}'})
search=post('/api/visual/searches',{'imageDocumentId':doc['documentId'],'question':'Ce adaptoare similare sunt disponibile?','catalogVersion':'seed-v1'})
post('/api/visual/searches/'+search['id']+'/analyze',{'revision':search['revision'],'requestId':str(uuid.uuid4())})
for _ in range(150):
 result=request('/api/visual/searches/'+search['id'])
 if result['state'] in ['Completed','NeedsInput','Failed']:break
 time.sleep(1)
assert result['state']=='Completed',result
assert len(result['result']['candidates'])>=2,result
if args.live:assert result['result']['demonstration'] is False,'Live test must use Foundry'
else:assert result['result']['demonstration'] is True,'Demo test must be explicitly enabled'
for c in result['result']['candidates']:
 assert isinstance(c['price'],str)
 assert 'catalog:'+c['sku'] in c['sourceIds']
 request(c['sheetUrl']);request(c['sheetUrl']+'/stock')
answer=post('/api/visual/searches/'+search['id']+'/questions',{'question':'Ce stoc a fost consultat?','requestId':str(uuid.uuid4())})
assert answer['sourceIds']
print(json.dumps({'searchId':search['id'],'state':result['state'],'candidateSkus':[c['sku'] for c in result['result']['candidates']],'demonstration':result['result']['demonstration'],'inputTokens':result['inputTokens'],'outputTokens':result['outputTokens']},indent=2))
