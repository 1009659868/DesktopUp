// Isolated UI C check: node content-layout-controls-c.check.cjs <playwright-module-path> [browser-executable]
// No production app, network dependencies or user configuration. Screenshots go to OS temporary directory.

const {chromium}=require(process.argv[2]||'playwright');
const assert=require('assert/strict');
(async()=>{
 const browser=await chromium.launch({...(process.argv[3]?{executablePath:process.argv[3]}:{}),headless:true});
 const page=await browser.newPage({viewport:{width:1475,height:1150}});
 const errors=[],passed=[];page.on('pageerror',e=>errors.push(e.message));
 const ok=(name,condition)=>{assert.ok(condition,name);passed.push(name);};
 const input=async(selector,value)=>page.locator(selector).evaluate((e,v)=>{if(e.type==='checkbox')e.checked=v;else e.value=String(v);e.dispatchEvent(new Event('input',{bubbles:true}));},value);
 const geo=()=>page.evaluate(()=>JSON.stringify({layout:uiReviewBPlusSnapshot.layout,items:bpCore.state().items,rect:[poster.offsetWidth,poster.offsetHeight],tiles:[...document.querySelectorAll('.tile')].map(e=>[e.dataset.item,e.offsetLeft,e.offsetTop,e.offsetWidth,e.offsetHeight])}));
 await page.goto(require('url').pathToFileURL(require('path').join(__dirname,'content-layout-controls-c.html')).href);
 await checkCRefinement(page,ok);
 await page.waitForTimeout(150);
 await page.locator('#folder-toggle').click();
 await page.locator('#template-settings summary').click();
 ok('initial schema forms',await page.locator('#template-properties [data-param]').count()===6);
 const before=await geo(),token=await page.evaluate(()=>window.loadSentinel=Math.random());
 await input('#live-lightColor','#aabaff');await input('#live-softness',90);await input('#live-opacity',35);
 ok('parameter painting without geometry changes',(await geo())===before);
 ok('lamp CSS bindings',await page.evaluate(()=>poster.style.getPropertyValue('--lamp-rgb')==='170 186 255'&&getComputedStyle(poster).backgroundColor==='rgba(23, 34, 30, 0.35)'));
 await page.locator('[data-poster=work]').click();await page.evaluate(()=>bpChooseTemplate('lamp'));
 ok('target isolation',await page.inputValue('#live-softness')==='60');
 await page.locator('[data-poster=games]').click();
 ok('instance values survive target switch',await page.inputValue('#live-softness')==='90');
 await page.locator('[data-sample=rain]').count().then(async n=>{if(n)await page.locator('[data-sample=rain]').click();else await page.evaluate(()=>bpChooseTemplate('rain'));});
 ok('rain form differs',await page.locator('#live-amount').count()===1&&await page.locator('#live-softness').count()===0);
 await input('#live-amount',48);await input('#live-cloudColor','#aacccc');await input('#live-rainColor','#bbff88');await input('#live-lightning',false);
 ok('rain bounded pool and cloud binding',await page.evaluate(()=>sceneSnapshot().particles===48&&sceneSnapshot().pool===64&&document.querySelectorAll('.rain-drop').length===64&&document.querySelector('.storm-bolt').hidden&&document.querySelectorAll('#rain-cloud-tone stop')[1].getAttribute('stop-color')==='#aacccc'));
 ok('transparent rain backdrop',await page.evaluate(()=>getComputedStyle(poster).backgroundColor==='rgba(0, 0, 0, 0)'&&getComputedStyle(poster).boxShadow==='none'));
 await input('#live-amount',0);
 ok('zero rain',await page.evaluate(()=>sceneSnapshot().particles===0));
 await page.locator('#parameters-reset').click();
 ok('reset only current',await page.inputValue('#live-amount')==='28');
 await page.evaluate(()=>bpChooseTemplate('lamp'));
 ok('other template params survive reset',await page.inputValue('#live-softness')==='90');
 await page.locator('#template-save-open').click();await input('#draft-softness',25);
 await page.locator('#template-cancel').click();await page.locator('[data-close=library-dialog]').click();
 ok('draft cancellation isolation',await page.inputValue('#live-softness')==='90');
 await page.locator('#template-save-open').click();await page.locator('#template-name').fill('我的蓝灯');await input('#draft-softness',25);await page.locator('#template-form button[type=submit]').click();
 const savedId=await page.evaluate(()=>bpTemplates.find(t=>t.name==='我的蓝灯').id);
 ok('copy defaults saved',await page.evaluate(id=>CModel.get(id).properties.find(p=>p.key==='softness').default===25,savedId));
 await page.locator('[data-template="'+savedId+'"]').click();
 ok('copy applies defaults',await page.inputValue('#live-softness')==='25');
 await page.evaluate(()=>bpChooseTemplate('lamp'));
 ok('source definition and live params preserved',await page.evaluate(()=>CModel.get('lamp').properties.find(p=>p.key==='softness').default===60&&CModel.values('games','lamp').softness===90));
 await page.locator('#template-import-open').click();await page.locator('#load-example').click();await page.locator('#validate-template').click();
 const count=await page.evaluate(()=>CModel.count());
 ok('prepare has no registry writes',count===14&&await page.locator('#install-template').isEnabled());
 await page.locator('[data-close=import-dialog]').click();
 ok('cancel prepared import',await page.evaluate(()=>CModel.count())===count);
 await page.locator('#template-import-open').click();await page.locator('#load-example').click();await page.locator('#validate-template').click();await page.locator('#install-template').click();
 ok('no reload / new schema generated',await page.evaluate(t=>window.loadSentinel===t&&bpCore.state().theme==='demo.mint-rain'&&CModel.count()===15,token));
 ok('new select type generated',await page.locator('#live-mood option').allTextContents().then(a=>a.join('|')==='慢慢落|轻快些'));
 await page.locator('#live-mood').selectOption('1');
 ok('new select binding rendered',await page.evaluate(()=>sceneSnapshot().values['rain.speed']===1.4));
 await page.locator('#template-import-open').click();await page.locator('#load-example').click();await page.locator('#validate-template').click();
 ok('duplicate rejected',await page.locator('#install-template').isDisabled()&&(await page.textContent('#import-result')).includes('ID 已存在'));
 await page.locator('#template-json').fill('{"broken":');
 await page.locator('#validate-template').click();
 ok('malformed import preserves active',await page.evaluate(()=>bpCore.state().theme==='demo.mint-rain'&&CModel.count()===15)&&await page.locator('#install-template').isDisabled());
 const modelTests=await page.evaluate(()=>{
   const failures=[],rejections=[];
   function reject(name,mutate){const m=CModel.example();m.id='new.test';mutate(m);try{CModel.prepare(m);failures.push(name);}catch{rejections.push(name);}}
   reject('script field',m=>m.script='alert(1)');
   reject('unknown renderer',m=>m.renderer='javascript@1');
   reject('unknown binding',m=>m.properties[0].bind='filesystem.read');
   reject('unsafe key',m=>m.properties[0].key='constructor');
   reject('duplicate key',m=>m.properties[1].key=m.properties[0].key);
   reject('duplicate binding',m=>m.properties[1].bind=m.properties[0].bind);
   reject('excessive particles',m=>m.properties[1].max=10000);
   reject('default outside range',m=>m.properties[1].default=100);
   reject('fractional count',m=>m.properties[1].step=.5);
   reject('unsafe color',m=>m.properties[0].default='url(https://invalid.test)');
   reject('invalid enum',m=>m.properties[3].options[0].value=99);
   reject('extra enum script',m=>m.properties[3].options[0].onclick='bad()');
   reject('too many params',m=>m.properties=Array(21).fill(m.properties[0]));
   reject('unknown schema',m=>m.schemaVersion=99);
   let large=false;try{CModel.parse(' '.repeat(65537));}catch{large=true;}
   const m=CModel.get('lamp');m.properties[0].default='#000000';
   const immutable=CModel.get('lamp').properties[0].default==='#ffdc99';
   return {failures,rejections,large,immutable};
 });
 ok('14 invalid descriptor cases',modelTests.failures.length===0&&modelTests.rejections.length===14);
 ok('64KiB and immutable definitions',modelTests.large&&modelTests.immutable);
 // Real file chooser upload path, supplied in memory to the browser; reads no user files.
 const next=await page.evaluate(()=>{const m=CModel.example();m.id='file.rain';m.name='文件雨云';return JSON.stringify(m);});
 await page.locator('#template-file').setInputFiles({name:'cloud.json',mimeType:'application/json',buffer:Buffer.from(next)});
 await page.waitForFunction(()=>document.querySelector('#template-json').value.includes('file.rain'));
 await page.locator('#validate-template').click();
 await page.locator('#template-json').fill(next.replace('文件雨云','改名雨云'));
 ok('editing invalidates prepared import',await page.locator('#install-template').isDisabled());
 await page.locator('#validate-template').click();await page.locator('#install-template').click();
 ok('file import without reload',await page.evaluate(t=>window.loadSentinel===t&&bpCore.state().theme==='file.rain',token));
 const postImportGeometry=await geo();
 await input('#live-mintCloud','#aaccdd');await input('#live-drizzle',40);
 ok('custom changes preserve layout',postImportGeometry===await geo());
 await page.locator('#scene-motion').uncheck();
 ok('effect off preserves cloud and stops rain',await page.evaluate(()=>getComputedStyle(document.querySelector('.storm-cloud')).display!=='none'&&getComputedStyle(document.querySelector('.rain-trails')).display==='none'));
 await page.locator('#scene-motion').check();
 await page.emulateMedia({reducedMotion:'reduce'});
 ok('reduced motion stops rain',await page.evaluate(()=>getComputedStyle(document.querySelector('.rain-drop')).animationName==='none'));
 await page.emulateMedia({reducedMotion:'no-preference'});
 await page.locator('#arrange').click();
 ok('editing pauses effects',await page.evaluate(()=>!sceneSnapshot().running));
 await page.locator('#done').click();
 for(const axis of ['vertical','horizontal']){
   await page.locator('[data-axis="'+axis+'"]').click();
   await page.locator('details.sim-controls').evaluate(e=>e.open=true);
   await page.locator('#cross').evaluate(e=>{e.value=e.min;e.dispatchEvent(new Event('input',{bubbles:true}));});
   const state=await page.evaluate(()=>({items:bpCore.state().items.map(x=>[x.id,x.w,x.h]),tiles:[...document.querySelectorAll('.tile')].map(e=>({x:e.offsetLeft,y:e.offsetTop}))}));
   ok(axis+' cross-axis minimum single line',state.tiles.every(t=>axis==='vertical'?t.x===state.tiles[0].x:t.y===state.tiles[0].y));
 }
 await page.locator('[data-axis=vertical]').click();await input('#cross',288);
 await page.evaluate(()=>bpChooseTemplate('rain'));
 if(await page.locator('#folder-toggle').getAttribute('aria-expanded')==='true')await page.locator('#folder-toggle').click();
 await page.screenshot({path:require('os').tmpdir()+'/desktopup-c-rain.png',fullPage:true});
 await page.locator('#palette-open').click();await page.locator('[data-palette=ink]').click();await page.locator('[data-close=palette-dialog]').click();
 await page.screenshot({path:require('os').tmpdir()+'/desktopup-c-dark.png',fullPage:true});
 for(const width of [1242,1120,736,375,320]){
   await page.setViewportSize({width,height:1000});
   // Core handles the resize event asynchronously; assert the settled viewport, not its old-size frame.
   await page.waitForFunction(()=>document.documentElement.scrollWidth<=innerWidth);
   ok(width+' no page overflow',await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
 }
 await page.screenshot({path:require('os').tmpdir()+'/desktopup-c-narrow.png',fullPage:true});
 await page.setViewportSize({width:1242,height:1100});
 await page.locator('#template-import-open').click();await page.locator('#load-example').click();
 await page.screenshot({path:require('os').tmpdir()+'/desktopup-c-import.png',fullPage:true});

 const extra=await page.evaluate(()=>{
   const results={};
   const reject=f=>{try{f();return false;}catch{return true;}};
   const initial=CModel.count();
   const m=CModel.example();m.id='safe.label';m.name='<svg/onload=alert(1)>';m.properties[0].label='<img src=x>';
   CModel.add(m);bpChooseTemplate(m.id);
   results.textOnly=document.querySelector('#property-title').textContent===m.name&&!document.querySelector('#template-properties img');
   results.typeReject=reject(()=>CModel.set('games',m.id,'drizzle','18'));
   results.nanReject=reject(()=>CModel.set('games',m.id,'drizzle',NaN));
   const proto=JSON.parse(JSON.stringify(CModel.example()).replace('"format":','"__proto__":{},"format":'));
   results.prototypeReject=reject(()=>CModel.prepare(proto));
   while(CModel.count()<64){const n=CModel.example();n.id='capacity.'+CModel.count();CModel.add(n);}
   const n=CModel.example();n.id='over.limit';
   results.capacity=CModel.count()===64&&reject(()=>CModel.add(n));
   return results;
 });
 ok('escaped labels / typed values / prototype / bounded registry',Object.values(extra).every(Boolean));
 ok('no page errors',errors.length===0);
 console.log(JSON.stringify({passed:passed.length,cases:passed,modelTests,pageerrors:errors},null,2));
 await browser.close();
})().catch(e=>{console.error(e);process.exit(1)});

async function checkCRefinement(page,ok){
 const input=async(id,v)=>page.locator(id).evaluate((e,v)=>{e.value=String(v);e.dispatchEvent(new Event('input',{bubbles:true}));},v);
 const layout=()=>page.evaluate(()=>JSON.stringify({layout:uiReviewBPlusSnapshot.layout,items:bpCore.state().items,tiles:[...document.querySelectorAll('.tile')].map(e=>[e.offsetLeft,e.offsetTop,e.offsetWidth,e.offsetHeight])}));
 ok('template domain nested',await page.evaluate(()=>['scene-option','template-properties'].every(id=>document.getElementById('folder-body').contains(document.getElementById(id)))));
 ok('template controls initially folded',!(await page.locator('#scene-option').isVisible())&&!(await page.locator('#template-settings').isVisible()));
 await page.locator('#folder-toggle').focus();await page.keyboard.press('Enter');
 ok('outer folder keyboard opens',await page.locator('#template-settings').isVisible()&&!(await page.locator('#scene-option').isVisible()));
 await page.locator('#template-settings summary').focus();await page.keyboard.press('Enter');
 ok('inner settings keyboard opens',await page.locator('#scene-option').isVisible());
 await page.locator('[data-sample=rain]').click();
 ok('rain schema includes wind',await page.inputValue('#live-wind')==='35');
 const before=await layout(),state=await page.evaluate(()=>JSON.stringify(CModel.values('games','rain')));
 await page.locator('#template-settings summary').click();await page.locator('#folder-toggle').click();
 ok('folding retains params and scene',await page.evaluate(s=>JSON.stringify(CModel.values('games','rain'))===s&&sceneSnapshot().enabled,state));
 await page.locator('#folder-toggle').click();await page.locator('#template-settings summary').click();
 await input('#live-wind',100);
 const measurement=await page.evaluate(()=>{
   const cloud=document.querySelector('.storm-cloud'),animation=cloud.getAnimations()[0],view=document.querySelector('#viewportwrap');
   const tiles=()=>[...document.querySelectorAll('.tile')].map(e=>[e.offsetLeft,e.offsetTop,e.offsetWidth,e.offsetHeight]);
   const stable=JSON.stringify(tiles()),frames=[];
   animation.pause();
   for(const time of [0,4750,9500,14250,18999]){animation.currentTime=time;const c=cloud.getBoundingClientRect(),p=poster.getBoundingClientRect();frames.push({time,gap:view.getBoundingClientRect().top-c.bottom,inside:c.left>=p.left&&c.right<=p.right,stable:JSON.stringify(tiles())===stable,transform:getComputedStyle(cloud).transform});}
   animation.play();return frames;
 });
 ok('wind keyframes move cloud only',new Set(measurement.map(x=>x.transform)).size>1&&measurement.every(x=>x.stable));
 ok('wind envelope keeps cloud clear of items',measurement.every(x=>x.gap>=20&&x.inside));
 ok('wind does not change item solver',await layout()===before);
 console.log('Cloud frame envelope: '+JSON.stringify(measurement));
 await page.locator('#scene-motion').uncheck();
 ok('disabled cloud animation paused',await page.evaluate(()=>getComputedStyle(document.querySelector('.storm-cloud')).animationPlayState==='paused'));
 await page.evaluate(()=>bpChooseTemplate('lamp'));
 ok('motion enabled state isolated by template',await page.locator('#scene-motion').isChecked());
 await page.evaluate(()=>bpChooseTemplate('rain'));
 ok('rain disabled state retained',!(await page.locator('#scene-motion').isChecked()));
 await page.locator('#scene-motion').check();await input('#live-wind',0);
 ok('zero wind no cloud animation',await page.evaluate(()=>getComputedStyle(document.querySelector('.storm-cloud')).animationName==='none'));
 await input('#live-wind',35);
 await page.emulateMedia({reducedMotion:'reduce'});
 ok('reduced motion stops cloud and wisps',await page.evaluate(()=>['.storm-cloud','.cloud-vapor','.cloud-wisp'].every(s=>getComputedStyle(document.querySelector(s)).animationName==='none')));
 await page.emulateMedia({reducedMotion:'no-preference'});
 await page.locator('#arrange').click();
 ok('editing pauses cloud',await page.evaluate(()=>getComputedStyle(document.querySelector('.storm-cloud')).animationPlayState==='paused'));
 await page.locator('[data-item=al]').click();
 ok('glyph click retains item identity',await page.evaluate(()=>bpCore.state().selected==='al'));
 await page.locator('#done').click();
 const labels=await page.evaluate(()=>[...document.querySelectorAll('.tile:not(.compact)')].every(tile=>{const t=tile.getBoundingClientRect(),l=tile.querySelector('.item-name').getBoundingClientRect(),g=tile.querySelector('.rain-glyph').getBoundingClientRect();return l.height>=12&&l.top>=t.top&&l.bottom<=t.bottom&&g.bottom<=l.top;}));
 ok('rain labels fit tile without squeezing',labels);
 await page.evaluate(()=>window.dispatchEvent(new Event('pagehide')));
 ok('pagehide pauses cloud',await page.evaluate(()=>getComputedStyle(document.querySelector('.storm-cloud')).animationPlayState==='paused'&&!sceneSnapshot().running));
 await page.evaluate(()=>window.dispatchEvent(new Event('pageshow')));
 for(let i=0;i<8;i++){await page.evaluate(()=>bpChooseTemplate('lamp'));await page.evaluate(()=>bpChooseTemplate('rain'));}
 ok('switches keep bounded scene nodes',await page.evaluate(()=>document.querySelectorAll('.rain-drop').length===64&&document.querySelectorAll('.rain-glyph').length===6&&document.querySelectorAll('.scene-surface').length===1));
 await page.locator('#parameters-reset').click();
 await page.locator('#folder-toggle').click();
 await page.screenshot({path:require('os').tmpdir()+'/desktopup-c-refined-rain.png',fullPage:true});
 await page.locator('#palette-open').click();await page.locator('[data-palette=ink]').click();await page.locator('[data-close=palette-dialog]').click();
 await page.screenshot({path:require('os').tmpdir()+'/desktopup-c-refined-dark.png',fullPage:true});
 await page.locator('#folder-toggle').click();
 await page.screenshot({path:require('os').tmpdir()+'/desktopup-c-refined-folder.png',fullPage:true});
 for(const width of [375,320]){
   await page.setViewportSize({width,height:1000});await page.waitForFunction(()=>document.documentElement.scrollWidth<=innerWidth);
   ok(width+' expanded template settings fit',await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
 }
 await page.screenshot({path:require('os').tmpdir()+'/desktopup-c-refined-narrow.png',fullPage:true});
 await page.setViewportSize({width:1475,height:1150});
 await page.reload(); // Reset this independent refinement scenario before the existing import regressions.
}
