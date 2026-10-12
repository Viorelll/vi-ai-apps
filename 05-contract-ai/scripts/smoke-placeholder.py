"""Container smoke check for .env.example placeholders only; no Azure model is called."""
import json,time,urllib.request,urllib.error
from uuid import uuid4
base='http://127.0.0.1:8080'
def call(path,body=None,kind='application/json',method=None):
 data=json.dumps(body).encode() if body is not None and kind=='application/json' else body
 request=urllib.request.Request(base+path,data,{'Content-Type':kind},method=method)
 try:
  with urllib.request.urlopen(request,timeout=15) as r:return r.status,r.read()
 except urllib.error.HTTPError as e:return e.code,e.read()
def wait_document(id):
 for _ in range(60):
  status,raw=call('/api/documents/'+id);assert status==200
  doc=json.loads(raw)
  if doc['status']=='Ready':return doc
  assert doc['status']!='Failed',doc.get('errorCode')
  time.sleep(.2)
 raise TimeoutError(id)
status,html=call('/');assert status==200 and b'ContractAI' in html
status,_=call('/health/live');assert status==200
status,_=call('/health/ready');assert status==503 # Explicit placeholder configuration.
text=f'Beneficiarul plătește în 15 zile de la recepție. Test {uuid4()}'.encode()
boundary='contractai-'+str(uuid4())
body=f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="container-seed.txt"\r\nContent-Type: text/plain\r\n\r\n'.encode()+text+f'\r\n--{boundary}--\r\n'.encode()
status,raw=call('/api/documents',body,'multipart/form-data; boundary='+boundary);assert status==202,raw
doc=json.loads(raw);wait_document(doc['documentId'])
status,original=call('/api/documents/'+doc['documentId']+'/content');assert status==200 and original==text
status,raw=call('/api/contracts/versions',{'documentId':doc['documentId'],'contractKey':'container-'+str(uuid4()),'versionLabel':'V1','parentVersionId':None});assert status==201,raw
version=json.loads(raw)
status,raw=call(f'/api/contracts/versions/{version["id"]}/analyze',{'revision':version['revision']});assert status==202
job=json.loads(raw)
for _ in range(60):
 status,raw=call('/api/jobs/'+job['jobId']);assert status==200
 result=json.loads(raw)
 if result['status']=='Failed':assert result['errorCode']=='foundry_not_configured';break
 time.sleep(.2)
else:raise TimeoutError(job)
status,_=call('/api/documents/'+doc['documentId'],method='DELETE');assert status==202
print(json.dumps({'web':'passed','migration':'passed','uploadAndExtraction':'passed','originalStream':'passed','missingFoundryError':'passed','readinessWithPlaceholders':503}))
