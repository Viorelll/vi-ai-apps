#!/usr/bin/env python3
"""Exercise the public workflow without exposing credentials or submitted content in logs."""
import argparse,hashlib,json,os,struct,subprocess,time,urllib.request,urllib.error,uuid,zlib
from pathlib import Path
root=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser();parser.add_argument('--restart',action='store_true');parser.add_argument('--live',action='store_true');args=parser.parse_args()
values=dict(line.split('=',1) for line in (root/'.env').read_text().splitlines() if line and not line.startswith('#'))
base=os.environ.get('MODERATEAI_BASE_URL',f"http://127.0.0.1:{values.get('WEB_PORT','8088')}")
def request(path,body=None,method=None,expected=200,headers=None):
 data=json.dumps(body).encode() if body is not None else None
 req=urllib.request.Request(base+path,data=data,method=method,headers=headers or ({'Content-Type':'application/json'} if data else {}))
 try:
  with urllib.request.urlopen(req,timeout=15) as response:
   assert response.status==expected,(path,response.status)
   payload=response.read()
   return json.loads(payload) if response.headers.get_content_type()=='application/json' else payload
 except urllib.error.HTTPError as error:
  if error.code==expected:return json.load(error)
  raise AssertionError(f'{path}: unexpected HTTP {error.code}') from None
def create(text,image=None):return request('/api/moderation/items',{'requestId':str(uuid.uuid4()),'text':text,'imageDocumentId':image},expected=201)['id']
def analyze(id):
 request(f'/api/moderation/items/{id}/analyze',{},expected=202)
 for _ in range(90):
  item=request(f'/api/moderation/items/{id}')
  if item['state'] not in ('Submitted','Analyzing'):
   assert item['state']!='Failed',item.get('errorCode');return item
  time.sleep(1)
 raise AssertionError('Analysis deadline exceeded')
health=request('/api/health');assert health['mode']==('Foundry' if args.live else 'Demo')
checks=[]
if args.live:
 id=create('This is a synthetic product documentation test.');item=analyze(id);assert item['analysis']['complete'];assert item['analysis']['provider']=='AzureContentSafety';assert item['explanation'];checks.append('Live Content Safety text + Foundry explanation')
else:
 ids={}
 for name,state in [('allow','Allowed'),('review','Review'),('block','Blocked')]:
  id=create('[demo:'+name+']');ids[name]=id;item=analyze(id);assert item['state']==state;assert item['analysis']['outputScale']=='FourSeverityLevels';assert len(item['analysis']['scores'])==4;assert item['text'] is None;checks.append('Fixture '+name)
 id=ids['review'];item=request(f'/api/moderation/items/{id}');revision=item['revision']
 assert any(i['id']==id for i in request('/api/moderation/review')['items'])
 reviewed=request(f'/api/moderation/items/{id}/review',{'revision':revision,'decision':'allow','note':'Synthetic smoke review'})
 assert reviewed['state']=='Allowed' and reviewed['policy']['decision']=='review' and len(reviewed['reviews'])==1
 request(f'/api/moderation/items/{id}/review',{'revision':revision,'decision':'block'},expected=409);checks.append('Versioned override + conflict')
 unknown=analyze(create('Arbitrary text not classified by Demo'));assert unknown['state']=='Review' and not unknown['analysis']['complete'];checks.append('Demo fail-closed')
 exported=request(f'/api/moderation/items/{id}/export');assert exported['policy']['policyVersion']=='policy-four-v1';assert exported['reviews'][0]['decision']=='allow';checks.append('Export preserves raw policy and override')
# Real PNG upload, hash deduplication, content stream, analysis and durable deletion.
def chunk(kind,data):return struct.pack('>I',len(data))+kind+data+struct.pack('>I',zlib.crc32(kind+data)&0xffffffff)
color=uuid.uuid4().bytes[:3]
png=b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',80,80,8,2,0,0,0))+chunk(b'IDAT',zlib.compress(b''.join(b'\0'+color*80 for _ in range(80))))+chunk(b'IEND',b'')
def upload(data,name,expected):
 boundary='moderateai-'+uuid.uuid4().hex
 body=(f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="{name}"\r\nContent-Type: image/png\r\n\r\n').encode()+data+f'\r\n--{boundary}--\r\n'.encode()
 req=urllib.request.Request(base+'/api/documents',data=body,headers={'Content-Type':'multipart/form-data; boundary='+boundary},method='POST')
 try:
  with urllib.request.urlopen(req) as response:assert response.status==expected;return json.load(response)
 except urllib.error.HTTPError as error:
  assert error.code==expected;return json.load(error)
image=upload(png,'synthetic.png',201);assert image['sha256']==hashlib.sha256(png).hexdigest();assert upload(png,'duplicate.png',200)['id']==image['id'];assert request('/api/documents/'+image['id']+'/content')==png;upload(b'invalid png','fake.png',415)
imageItem=analyze(create(None,image['id']));assert imageItem['analysis']['provider'].startswith('Azure' if args.live else 'Demo');assert imageItem['state']=='Review' if not args.live else imageItem['analysis']['complete'];checks.append('S3 image upload + hash + dedup + analysis')
request('/api/documents/'+image['id'],method='DELETE',expected=202);assert request('/api/moderation/items/'+imageItem['id'])['stale'];request('/api/documents/'+image['id']+'/content',expected=404);checks.append('Deletion invalidates source')
if args.restart:
 assert not args.live,'Live restart smoke is separate to avoid repeating paid calls.'
 before=request('/api/moderation/items/'+ids['allow'])
 subprocess.run(['docker','compose','-f','compose.yaml','-f','compose.cloud.yaml','restart','api','ai'],cwd=root,check=True)
 subprocess.run(['python3','scripts/readiness.py'],cwd=root,check=True)
 after=request('/api/moderation/items/'+ids['allow']);assert before==after;checks.append('API/AI restart persistence')
out=root/'output';out.mkdir(exist_ok=True)
(out/('smoke-live.json' if args.live else 'smoke.json')).write_text(json.dumps({'mode':health['mode'],'passed':len(checks),'checks':checks},indent=2))
print(f'{len(checks)} functional smoke checks passed ({health["mode"]}).')
