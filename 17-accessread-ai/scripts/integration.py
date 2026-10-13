#!/usr/bin/env python3
"""PostgreSQL constraints, worker fencing/reclaim and restart persistence on real Compose services."""
import json
import subprocess
import time
import uuid
from smoke import ROOT, request, upload, wait, expect_status

def compose(*args):
    return subprocess.run(['docker','compose',*args],cwd=ROOT,check=True,capture_output=True,text=True).stdout

def sql(command):
    return compose('exec','-T','db','psql','-U','accessreadai','-d','accessreadai','-v','ON_ERROR_STOP=1','-At','-c',command).strip()

def ready():
    for _ in range(60):
        try:
            if request('/api/health')['status']=='ok':return
        except Exception:pass
        time.sleep(.5)
    raise AssertionError('Services did not recover')

def main():
    saved=json.loads((ROOT/'artifacts/smoke-result.json').read_text())
    document_id=saved['documentId'];reading_id=saved['readingId']
    before=request(f'/api/readings/{reading_id}')
    content=request(f'/api/documents/{document_id}/content')
    # Persist a queued job and expire its previous ownership while the worker is stopped.
    original=('Nu depășiți 10 unități.\n\nrestart-'+uuid.uuid4().hex).encode()
    pending=upload(original)['documentId']
    compose('stop','api')
    # The upload could already have completed; requeue only that test document's extraction.
    job_id=str(uuid.uuid4());old_token=str(uuid.uuid4())
    sql(f'''BEGIN;
UPDATE app."Documents" SET "State"='Processing',"ActiveGeneration"=0 WHERE "Id"='{pending}';
DELETE FROM app."DocumentChunks" WHERE "DocumentId"='{pending}';
DELETE FROM app."IngestionJobs" WHERE "DocumentId"='{pending}';
INSERT INTO app."IngestionJobs" ("Id","DocumentId","Kind","PipelineVersion","State","LeaseToken","LeaseUntilUtc","NextAttemptAtUtc","Attempt","InputJson") VALUES ('{job_id}','{pending}','Extract','reading-v1','Running','{old_token}',now()-interval '1 second',now(),0,'{{}}');
COMMIT;''')
    compose('restart','ai')
    compose('start','api')
    ready()
    wait(f'/api/documents/{pending}')
    assert sql(f'''SELECT "State" FROM app."IngestionJobs" WHERE "Id"='{job_id}';''')=='Completed'
    assert sql(f'''SELECT "LeaseToken"<>'{old_token}'::uuid FROM app."IngestionJobs" WHERE "Id"='{job_id}';''')=='t'
    assert sql(f'''UPDATE app."IngestionJobs" SET "State"='Failed' WHERE "Id"='{job_id}' AND "LeaseToken"='{old_token}' RETURNING "Id";''')=='UPDATE 0','Stale lease was allowed to write'
    assert request(f'/api/documents/{pending}/content')==original
    after=request(f'/api/readings/{reading_id}')
    assert before==after,'Reading changed after restart'
    assert request(f'/api/documents/{document_id}/content')==content
    translated=next(v for v in after['blocks'][0]['variants'] if v['kind']=='translated')
    assert request(translated['narration'][0]['audioUrl'])[:4]==b'RIFF'
    # Database-enforced unique business keys and optimistic revision updates.
    chunks=int(sql(f'''SELECT count(*) FROM app."DocumentChunks" WHERE "DocumentId"='{pending}';'''))
    assert chunks==2,'Lease recovery duplicated chunks'
    assert sql(f'''UPDATE app."Readings" SET "Revision"="Revision"+1 WHERE "Id"='{reading_id}' AND "Revision"=-1 RETURNING "Id";''')=='UPDATE 0'
    constraint=sql('''SELECT count(*) FROM pg_indexes WHERE schemaname='app' AND indexdef LIKE 'CREATE UNIQUE INDEX%' AND (tablename='Documents' OR tablename='TextVariants' OR tablename='DocumentChunks');''')
    assert int(constraint)>=6
    # Unclear source is explicitly reviewed, with no narration.
    unclear=upload('Nu depășiți 1? unități.'.encode())['documentId'];wait(f'/api/documents/{unclear}')
    reading=request('/api/readings','POST',{'documentId':unclear,'sourceLanguage':'ro','targetLanguage':'ro'})
    request(f"/api/readings/{reading['id']}/generate",'POST',{'revision':reading['revision']})
    reviewed=wait(f"/api/readings/{reading['id']}",('NeedsReview',))
    assert reviewed['blocks'][0]['needsReview']
    assert all(not v['narration'] for v in reviewed['blocks'][0]['variants'])
    # Fake scanned bytes never masquerade as OCR in demo mode.
    scan=upload(b'%PDF-1.7\nartificial fixture','demo.pdf')['documentId']
    scan_result=wait(f'/api/documents/{scan}',('Failed',))
    assert scan_result['errorCode']=='scan_requires_azure_layout'
    request(f'/api/documents/{pending}','DELETE')
    request(f'/api/documents/{unclear}','DELETE')
    request(f'/api/documents/{scan}','DELETE')
    print('Integration passed: PostgreSQL unique indexes/revision guard, expired-lease reclaim, stale-token fencing, no duplicate chunks, API/AI restart persistence, original/audio retrieval, NeedsReview narration suppression and explicit scan configuration failure.')

if __name__=='__main__':main()
