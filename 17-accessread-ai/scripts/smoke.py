#!/usr/bin/env python3
"""Real PostgreSQL/S3/HTTP acceptance check; no external AI calls in demo mode."""
import concurrent.futures
import json
import time
import urllib.error
import urllib.request
import uuid
from pathlib import Path

BASE='http://127.0.0.1:8080'
ROOT=Path(__file__).resolve().parent.parent

def request(path, method='GET', payload=None, raw=None, headers=None):
    data=raw if raw is not None else json.dumps(payload).encode() if payload is not None else None
    h=headers or ({'Content-Type':'application/json'} if payload is not None else {})
    req=urllib.request.Request(BASE+path,data=data,method=method,headers=h)
    with urllib.request.urlopen(req,timeout=90) as response:
        body=response.read()
        return json.loads(body) if response.headers.get('Content-Type','').startswith('application/json') else body

def upload(data,name='acceptance.txt'):
    boundary='accessread-'+uuid.uuid4().hex
    body=(f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="{name}"\r\nContent-Type: text/plain\r\n\r\n'.encode()+data+f'\r\n--{boundary}--\r\n'.encode())
    return request('/api/documents','POST',raw=body,headers={'Content-Type':f'multipart/form-data; boundary={boundary}'})

def wait(path, states=('Ready',), timeout=60):
    deadline=time.monotonic()+timeout
    while time.monotonic()<deadline:
        value=request(path)
        if value['state'] in states:return value
        if value['state']=='Failed':raise AssertionError(value)
        time.sleep(.4)
    raise AssertionError('Timed out: '+path)

def expect_status(status,fn):
    try:fn()
    except urllib.error.HTTPError as error:
        assert error.code==status,(error.code,error.read())
    else:raise AssertionError(f'Expected HTTP {status}')

def main():
    assert request('/api/health')['status']=='ok'
    original=(ROOT/'fixtures/reading.txt').read_bytes()
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        uploads=list(pool.map(lambda _:upload(original),range(4)))
    assert len({u['documentId'] for u in uploads})==1,'Concurrent upload deduplication failed'
    document_id=uploads[0]['documentId']
    wait(f'/api/documents/{document_id}')
    assert request(f'/api/documents/{document_id}/content')==original,'S3 original changed'
    expect_status(415,lambda:upload(b'fake PDF','fake.pdf'))
    expect_status(400,lambda:request('/api/readings','POST',{'documentId':document_id,'targetLanguage':'xx','sourceLanguage':'ro'}))
    reading=request('/api/readings','POST',{'documentId':document_id,'targetLanguage':'en','sourceLanguage':'ro'})
    reading_id=reading['id']
    expect_status(409,lambda:request(f'/api/readings/{reading_id}/generate','POST',{'revision':999}))
    request(f'/api/readings/{reading_id}/generate','POST',{'revision':reading['revision'],'simplify':True,'translate':True,'narrate':True})
    # A repeated generate with the same revision cannot duplicate work.
    expect_status(409,lambda:request(f'/api/readings/{reading_id}/generate','POST',{'revision':reading['revision']}))
    result=wait(f'/api/readings/{reading_id}',('Ready','NeedsReview'))
    assert result['state']=='Ready',result
    assert len(result['blocks'])==3
    assert [b['ordinal'] for b in result['blocks']]==[0,1,2]
    first=result['blocks'][0]
    assert first['original']=='Nu depășiți 10 unități.'
    translation=next(v for v in first['variants'] if v['kind']=='translated')
    assert translation['text']=='Do not exceed 10 units.'
    assert translation['narration'],'No narration'
    segment=translation['narration'][0]
    audio=request(segment['audioUrl'])
    assert audio[:4]==b'RIFF' and len(audio)>1000,'Narration is not a real WAV'
    assert request(f'/api/readings/{reading_id}/export')['documentId']==document_id
    expect_status(422,lambda:request(f'/api/readings/{reading_id}/variants','PATCH',{'revision':result['revision'],'variantId':translation['id'],'text':'Exceed 11 units.'}))
    request(f'/api/readings/{reading_id}/variants','PATCH',{'revision':result['revision'],'variantId':translation['id'],'text':'Do not exceed 10 units. Read carefully.'})
    expect_status(410,lambda:request(segment['audioUrl']))
    corrected=wait(f'/api/readings/{reading_id}',('Ready','NeedsReview'))
    new_variant=next(v for v in corrected['blocks'][0]['variants'] if v['id']==translation['id'])
    assert new_variant['text']=='Do not exceed 10 units. Read carefully.'
    assert new_variant['narration'] and new_variant['narration'][0]['id']!=segment['id']
    answer=request('/api/chat','POST',{'requestId':str(uuid.uuid4()),'conversationId':None,'question':'unități','documentIds':[document_id]})
    assert answer['citations'] and answer['citations'][0]['documentId']==document_id
    absent=request('/api/chat','POST',{'requestId':str(uuid.uuid4()),'conversationId':None,'question':'astronaut galaxie','documentIds':[document_id]})
    assert not absent['citations']
    # Do not delete the shared immutable demo document: create a separate cleanup fixture.
    cleanup=upload(('Nu depășiți 10 unități.\n\ncleanup-'+uuid.uuid4().hex).encode())['documentId']
    wait(f'/api/documents/{cleanup}')
    request(f'/api/documents/{cleanup}','DELETE')
    for _ in range(40):
        try:request(f'/api/documents/{cleanup}')
        except urllib.error.HTTPError as error:
            if error.code==404:break
            assert error.code==410
        time.sleep(.25)
    else:raise AssertionError('Cleanup did not remove the document')
    expect_status(404,lambda:request(f'/api/documents/{cleanup}/content'))
    print('Smoke passed: concurrent deduplication, PostgreSQL persistence, S3 originals, 3 aligned blocks, meaning checks, WAV narration, revision conflicts, correction/audio invalidation, citations, no-evidence answer and deletion.')
    return document_id,reading_id

if __name__=='__main__':
    ids=main()
    (ROOT/'artifacts').mkdir(exist_ok=True)
    (ROOT/'artifacts/smoke-result.json').write_text(json.dumps({'documentId':ids[0],'readingId':ids[1],'passed':True}))
