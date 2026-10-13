#!/usr/bin/env python3
"""Exercises the public API, answer-key isolation and reproducible adaptation.
Does not certify Foundry unless run with real configured deployments and demoMode=false.
"""
import json, os, time, urllib.request, urllib.error, uuid
BASE=os.environ.get('TUTORAI_URL','http://127.0.0.1:8080')
def request(path, body=None):
 data=None if body is None else json.dumps(body).encode()
 req=urllib.request.Request(BASE+path,data=data,headers={'Content-Type':'application/json'} if data else {})
 with urllib.request.urlopen(req,timeout=15) as r:return json.load(r)
def wait(id, states):
 for _ in range(180):
  s=request('/api/tutor/sessions/'+id)
  if s['state'] in states:return s
  if s['state'] in ['Failed','Stale']:raise RuntimeError(s.get('errorCode') or s['state'])
  time.sleep(.5)
 raise TimeoutError('Session did not become ready')
for _ in range(60):
 try:request('/api/status');break
 except (urllib.error.URLError,OSError):time.sleep(1)
else:raise SystemExit('API unavailable')
s=request('/api/tutor/sessions',{'topic':'RAG','difficulty':1});id=s['id']
request('/api/tutor/sessions/'+id+'/explain',{'question':'Explică RAG și rolul recuperării contextului.','revision':s['revision']})
s=wait(id,['Open']);assert s['explanation']['citations']
for index in range(2):
 request('/api/tutor/sessions/'+id+'/exercises',{'kind':'single_choice','revision':s['revision']})
 s=wait(id,['ExerciseReady']);exercise=request('/api/tutor/exercises/'+s['exercise']['id'])
 assert 'rubric' not in json.dumps(exercise).lower() and 'expectedAnswer' not in json.dumps(exercise)
 if not s['demoMode']:
  print(json.dumps({'sessionId':id,'demoMode':False,'liveExplanationAndExercise':'passed','note':'Live grading requires answering the generated exercise; seed answer is only valid in demo.'}));break
 body={'answer':'Retriever','requestId':str(uuid.uuid4()),'revision':s['revision']}
 a=request('/api/tutor/exercises/'+exercise['id']+'/attempts',body)
 duplicate=request('/api/tutor/exercises/'+exercise['id']+'/attempts',body);assert a['id']==duplicate['id']
 s=wait(id,['FeedbackReady']);assert s['feedback']['score']==1 and s['difficulty']==(1 if index==0 else 2)
else:
 print(json.dumps({'sessionId':id,'demoMode':True,'explanation':'passed','answerKeyIsolation':'passed','grading':'passed','idempotency':'passed','adaptation':'1 -> 2','liveFoundry':'not run'}))
