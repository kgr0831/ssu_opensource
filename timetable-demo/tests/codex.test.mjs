import test from 'node:test';
import assert from 'node:assert/strict';
import {CodexBridge,openBrowser} from '../codex.mjs';

function mockBridge(browser=async()=>{}){
  const bridge=new CodexBridge({browser});bridge.start=async()=>{};
  const calls=[];
  bridge.rpc=async(method,params)=>{
    calls.push({method,params});
    if(method==='account/read')return {account:{type:'chatgpt'}};
    if(method==='account/login/start')return {loginId:'test-login',authUrl:'https://auth.openai.com/test-login'};
    if(method==='account/login/cancel')return {};
    throw new Error('Unexpected RPC');
  };
  return {bridge,calls};
}
test('새 계정 승인 대기 중에는 기존 계정을 연결 완료로 표시하지 않는다',async()=>{
  let opened=0;const {bridge,calls}=mockBridge(async()=>{opened++});
  assert.equal((await bridge.status()).connected,true);
  await Promise.all([bridge.connect(),bridge.connect()]);await bridge.connect();
  assert.equal(opened,1);
  assert.equal((await bridge.status()).connected,false);
  assert.equal((await bridge.status()).loginState,'waiting');
  await bridge.cancelLogin();
  assert.equal((await bridge.status()).connected,true);
  assert.ok(calls.some(c=>c.method==='account/login/cancel'&&c.params.loginId==='test-login'));
  assert.ok(calls.every(c=>c.method!=='account/logout'));
});
test('브라우저 열기 실패 시 진행 중인 로그인을 취소한다',async()=>{
  const {bridge,calls}=mockBridge(async()=>{throw new Error('browser failure')});
  await assert.rejects(bridge.connect(),/browser failure/);
  assert.equal(bridge.login.state,'idle');
  assert.ok(calls.some(c=>c.method==='account/login/cancel'));
});
test('AI 실행 중 계정 변경과 중복 추천을 막는다',async()=>{
  const {bridge}=mockBridge();bridge.busy=true;
  await assert.rejects(bridge.connect(),/추천이 끝난 뒤/);
  await assert.rejects(bridge.recommend({},{}),/추천을 생성 중/);
});
test('인증 브라우저는 공식 호스트와 로컬 앱 주소만 허용한다',()=>{
  for(const url of ['https://example.com','https://auth.openai.com.evil.test','file:///C:/Windows','javascript:alert(1)'])assert.throws(()=>openBrowser(url),/허용되지/);
});
