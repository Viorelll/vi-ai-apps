#!/usr/bin/env python3
"""Explicit opt-in: this submits one real clip and one real remix and can incur charges."""
import argparse,json,os,time,urllib.request,uuid
p=argparse.ArgumentParser();p.add_argument('--allow-cloud-cost',action='store_true');args=p.parse_args()
if not args.allow_cloud_cost:p.error('Real video generation can incur costs. Pass --allow-cloud-cost explicitly.')
base=os.environ.get('API_URL','http://127.0.0.1:8080')
def request(path,body=None,method=None):
 data=json.dumps(body).encode() if body is not None else None
 with urllib.request.urlopen(urllib.request.Request(base+path,data,headers={'Content-Type':'application/json'},method=method or ('POST' if body is not None else 'GET')),timeout=120) as r:
  return json.loads(r.read())
def wait(id,condition):
 deadline=time.monotonic()+1800
 while time.monotonic()<deadline:
  result=request('/api/video/projects/'+id)
  if any(j['state'] in ['Failed','Uncertain','NeedsReview','Canceled'] for j in result['jobs']):raise RuntimeError('Job ended without verified content: '+json.dumps(result['jobs']))
  if condition(result):return result
  time.sleep(5)
 raise TimeoutError('Job deadline exceeded; inspect existing provider ID, do not submit automatically.')
caps=request('/api/video/capabilities')
if caps['mode']!='Foundry':raise RuntimeError('This smoke test requires Foundry mode; Demo does not validate cloud access.')
project=request('/api/video/projects',{'brief':'An original stylized animated ceramic coffee cup with soft morning light, gentle camera motion, no brands, no people.'})
id=project['id'];request('/api/video/projects/'+id+'/storyboard',{'revision':project['revision'],'requestId':str(uuid.uuid4())})
project=wait(id,lambda r:r['storyboard'] is not None)
project=request('/api/video/projects/'+id+'/storyboard',{'revision':project['revision'],'scenes':project['storyboard']['scenes']},'PATCH')
request('/api/video/projects/'+id+'/generate',{'revision':project['revision'],'requestId':str(uuid.uuid4()),'sceneId':project['storyboard']['scenes'][0]['id'],'seconds':8 if 8 in caps['seconds'] else caps['seconds'][0],'size':caps['sizes'][0]})
project=wait(id,lambda r:len(r['clips'])==1)
request('/api/video/clips/'+project['clips'][0]['id']+'/remix',{'revision':project['revision'],'requestId':str(uuid.uuid4()),'prompt':'Shift the palette to teal and sand.'})
project=wait(id,lambda r:len(r['clips'])==2)
assert project['clips'][1]['sourceClipId']==project['clips'][0]['id']
for clip in project['clips']:
 with urllib.request.urlopen(base+clip['contentUrl'],timeout=120) as r:
  assert r.read(12)[4:8]==b'ftyp'
os.makedirs('test-results',exist_ok=True);open('test-results/live-smoke.json','w').write(json.dumps(project,indent=2)+'\n')
print('Live Foundry storyboard, clip, download and linked remix passed; project '+id)
