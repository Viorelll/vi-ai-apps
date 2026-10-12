#!/usr/bin/env python3
"""Offline functional acceptance test against real API, PostgreSQL and S3. No Azure calls."""
import hashlib,json,os,time,urllib.request,urllib.error,uuid
base=os.environ.get("API_URL","http://127.0.0.1:5080")
checks=[]
def check(condition,name):
 if not condition: raise AssertionError(name)
 checks.append(name)
def request(path,method="GET",body=None,expected=200,headers=None,raw=None):
 data=raw if raw is not None else (json.dumps(body).encode() if body is not None else None)
 h={"Content-Type":"application/json"} if body is not None else {}
 h.update(headers or {})
 try:
  with urllib.request.urlopen(urllib.request.Request(base+path,data,headers=h,method=method),timeout=60) as r: status=r.status;payload=r.read();rh=r.headers
 except urllib.error.HTTPError as e: status=e.code;payload=e.read();rh=e.headers
 check(status==expected,f"{method} {path}: {status} == {expected}; {payload[:250] if status!=expected else ''}")
 return (json.loads(payload) if 'json' in rh.get('Content-Type','') and payload else payload),rh
def project(id):return request('/api/video/projects/'+id)[0]
def wait_project(id,count=0,story=False):
 deadline=time.monotonic()+120
 while time.monotonic()<deadline:
  p=project(id)
  if any(j['state'] in ['Failed','Uncertain','NeedsReview'] for j in p['jobs']):raise AssertionError(p)
  if (story and p['storyboard']) or (not story and len(p['clips'])==count):return p
  time.sleep(1)
 raise AssertionError('Timed out waiting for project')
def upload(data,name,expected=202):
 boundary='test-'+str(uuid.uuid4());body=(f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="{name}"\r\nContent-Type: application/octet-stream\r\n\r\n').encode()+data+f'\r\n--{boundary}--\r\n'.encode()
 return request('/api/documents','POST',expected=expected,raw=body,headers={'Content-Type':'multipart/form-data; boundary='+boundary})[0]
def wait_doc(id):
 for _ in range(60):
  d=request('/api/documents/'+id)[0]
  if d['status']=='Ready':return d
  if d['status']=='Failed':raise AssertionError(d)
  time.sleep(1)
 raise AssertionError('Extraction timeout')
request('/health/ready');caps=request('/api/video/capabilities')[0];check(caps['mode']=='Demo','Explicit Demo mode')
request('/api/video/projects','POST',{'brief':''},400)
p=request('/api/video/projects','POST',{'brief':'A warm cafe at sunrise. Synthetic acceptance brief.'},201)[0];id=p['id']
request('/api/video/projects/'+id+'/generate','POST',{'revision':0,'requestId':str(uuid.uuid4()),'sceneId':'scene-1','seconds':8,'size':'1280x720'},409)
rid=str(uuid.uuid4());body={'revision':0,'requestId':rid};a=request('/api/video/projects/'+id+'/storyboard','POST',body,202)[0];b=request('/api/video/projects/'+id+'/storyboard','POST',body,202)[0];check(a==b,'Storyboard request deduplicated')
p=wait_project(id,story=True)
request('/api/video/projects/'+id+'/storyboard','PATCH',{'revision':0,'scenes':p['storyboard']['scenes']},409)
p=request('/api/video/projects/'+id+'/storyboard','PATCH',{'revision':p['revision'],'scenes':p['storyboard']['scenes']})[0]
check(p['confirmed'],'Storyboard explicitly confirmed')
body={'revision':p['revision'],'requestId':str(uuid.uuid4()),'sceneId':'scene-1','seconds':9,'size':'1280x720'}
request('/api/video/projects/'+id+'/generate','POST',body,400)
body.update(seconds=8,requestId=str(uuid.uuid4()))
a=request('/api/video/projects/'+id+'/generate','POST',body,202)[0];b=request('/api/video/projects/'+id+'/generate','POST',body,202)[0];check(a==b,'Video request deduplicated')
p=wait_project(id,1);clip=p['clips'][0];content,h=request(clip['contentUrl']);check(content[4:8]==b'ftyp','Actual MP4 file');check(hashlib.sha256(content).hexdigest()==clip['sha256'],'Persisted SHA-256 matches downloaded bytes');check(h['Content-Type']=='video/mp4','Correct content type')
part,h=request(clip['contentUrl'],expected=206,headers={'Range':'bytes=0-31'});check(part==content[:32],'Range content matches original')
request('/api/video/clips/'+clip['id']+'/remix','POST',{'revision':p['revision'],'requestId':str(uuid.uuid4()),'prompt':'Change the palette to teal.'},202)
p=wait_project(id,2);check(p['clips'][1]['sourceClipId']==clip['id'],'Remix points to original');check(p['clips'][0]['sha256']==clip['sha256'],'Original clip immutable');request(p['clips'][1]['contentUrl'])
text=('Synthetic video brief '+str(uuid.uuid4())+': coffee, warm daylight, ceramic cup.').encode();d=upload(text,'brief.txt');dup=upload(text,'brief.txt',200);check(d['id']==dup['id'],'Duplicate upload reused');wait_doc(d['id']);original,_=request(d['contentUrl']);check(original==text,'S3 document round trip')
answer=request('/api/chat','POST',{'question':'What should appear in the video?','documentIds':[d['id']]})[0];check(len(answer['citations'])>0,'Grounded response has citations');check(all(c['documentId']==d['id'] for c in answer['citations']),'Citations remain in allowed document scope')
request('/api/chat','POST',{'question':'question','documentIds':[str(uuid.uuid4())]},409)
upload(bytes([0,1,2,3]),'invalid.pdf',415)
request('/api/documents/'+d['id'],'DELETE',expected=202);request('/api/documents/'+d['id'],expected=404);request('/api/chat','POST',{'question':'question','documentIds':[d['id']]},409)
report={'mode':'Demo','passed':len(checks),'projectId':id,'clipId':clip['id'],'sha256':clip['sha256'],'checks':checks,'liveFoundry':'not run: requires real credentials and budget'}
path=os.environ.get('SMOKE_REPORT','test-results/smoke.json');os.makedirs(os.path.dirname(path) or '.',exist_ok=True);open(path,'w').write(json.dumps(report,indent=2)+'\n');print(json.dumps({k:v for k,v in report.items() if k!='checks'},indent=2))
