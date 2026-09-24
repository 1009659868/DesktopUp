/* Original, bounded CSS scene study inspired by the sources credited in CONTENT_TEMPLATE_SCENES_REVIEW.md.
   No imported code/assets, WebGL, pointer capture, layout solver, file IO or persistence. */
(() => {
  'use strict';
  const $=id=>document.getElementById(id),poster=$('poster'),prefs=new Map(),reduced=matchMedia('(prefers-reduced-motion: reduce)');
  window.bpTemplates.forEach(t=>t.family='极简主义');
  window.bpTemplates.push(
    {id:'lamp',name:'深夜书桌',family:'交互光影',effect:'lamp',group:'builtin',bg:'#17221e',card:'#354237',heading:true},
    {id:'rain',name:'雨夜窗边',family:'天气场景',effect:'rain',group:'builtin',bg:'#152337',card:'#253d53',heading:true}
  );
  const layer=document.createElement('div');layer.className='scene-surface';layer.setAttribute('aria-hidden','true');
  layer.innerHTML='<div class="scene-light"><div class="light-cone"></div><div class="light-pool"></div><div class="hanging-lamp"><i class="lamp-cord"></i><i class="lamp-shade"></i></div></div><div class="scene-weather"><div class="rain-trails"></div><div class="storm-cloud"><svg viewBox="0 0 360 126" preserveAspectRatio="none"><defs><linearGradient id="rain-cloud-tone" x1="0" y1="0" x2="0.2" y2="1"><stop offset="0" stop-color="#70869b"/><stop offset=".42" stop-color="#3f576d"/><stop offset="1" stop-color="#243b50"/></linearGradient></defs><path d="M30 113C10 113 2 100 4 86C5 72 16 64 29 65C23 44 39 29 58 30C73 9 102 5 124 21C141-1 181 1 197 24C219 10 251 18 260 38C286 24 319 37 321 61C340 56 357 70 356 88C358 104 342 115 327 114C307 121 282 118 266 118C244 125 221 120 202 120C180 127 156 120 139 120C116 126 97 119 81 119C62 123 44 119 30 113Z" fill="url(#rain-cloud-tone)"/><path d="M38 67C26 48 46 29 61 36M130 24C150 8 176 9 190 25M263 41C288 32 308 43 313 61" fill="none" stroke="#b7cbd7" stroke-opacity=".2" stroke-width="1.3" stroke-linecap="round"/></svg></div><div class="storm-glow"></div><div class="storm-bolt"></div></div>';
  poster.append(layer);
  // A fixed pool across the transparent content backdrop, behind the real items.
  for(let i=0;i<28;i++){
    const drop=document.createElement('i');drop.className='rain-drop';
    drop.style.setProperty('--drop-x',(6+(i*37)%89)+'%');drop.style.setProperty('--drop-length',(17+(i*7)%23)+'px');
    drop.style.setProperty('--duration',(1.5+(i%7)*.17)+'s');drop.style.setProperty('--delay',(-i*.31)+'s');drop.style.setProperty('--still-y',(24+(i*31)%345)+'px');
    layer.querySelector('.rain-trails').append(drop);
  }
  let scene='minimal',key='',running=false,inView=true,pageLive=true,frame=0,pointer=null,drawCount=0;
  const current=()=>window.bpCore.state(),selected=()=>window.bpTemplates.find(t=>t.id===current().theme);
  function cancel(){if(frame)cancelAnimationFrame(frame);frame=0;}
  function paintLight(){
    frame=0;if(scene!=='lamp')return;
    const bounds=poster.getBoundingClientRect(),point=running&&pointer?pointer:{x:bounds.width*.55,y:bounds.height*.47};
    const x=Math.max(8,Math.min(bounds.width-8,point.x)),y=Math.max(66,Math.min(bounds.height-16,point.y));
    poster.style.setProperty('--light-x',x+'px');poster.style.setProperty('--light-y',y+'px');
    poster.style.setProperty('--lamp-angle',Math.max(-13,Math.min(13,(x-bounds.width*.75)/15))+'deg');
    const reach=100;
    layer.querySelector('.light-cone').style.clipPath='polygon(72% 57px,78% 57px,'+(x+reach)+'px '+(y+40)+'px,'+(x-reach)+'px '+(y+40)+'px)';
    poster.querySelectorAll('.tile').forEach(tile=>{
      const r=tile.getBoundingClientRect(),tx=x-(r.left-bounds.left),ty=y-(r.top-bounds.top),distance=Math.hypot(tx-r.width/2,ty-r.height/2),power=Math.max(0,1-distance/230);
      tile.style.setProperty('--tile-light-x',tx+'px');tile.style.setProperty('--tile-light-y',ty+'px');
      tile.style.setProperty('--tile-light',String(.05+power*.24));tile.style.setProperty('--edge-light',String(.16+power*.32));
      tile.style.setProperty('--shadow-x',(Math.max(-5,Math.min(5,(r.width/2-tx)/28)))+'px');tile.style.setProperty('--shadow-y',(Math.max(1,Math.min(6,(r.height/2-ty)/25+3)))+'px');
    });
    drawCount++;
  }
  function queue(){if(scene==='lamp'&&running&&!frame)frame=requestAnimationFrame(paintLight);}
  function update(){
    cancel();
    const state=current(),t=selected();scene=t?.effect||'minimal';key=window.uiReviewBPlusSnapshot.active;
    const enabled=prefs.get(key)!==false;
    running=scene!=='minimal'&&enabled&&!state.editing&&!reduced.matches&&!document.hidden&&inView&&pageLive;
    poster.dataset.scene=scene;poster.dataset.fxRunning=String(running);poster.dataset.fxEnabled=String(enabled);poster.dataset.fxEditing=String(state.editing);
    poster.dataset.rainCaption=String(t?.heading!==false);
    $('scene-option').hidden=scene==='minimal';$('scene-motion').checked=enabled;
    $('scene-status').textContent=!enabled?'已关闭 · 保留静态材质':state.editing?'整理中 · 暂缓光影':reduced.matches?'减少动效 · 静态呈现':scene==='lamp'?'移动鼠标，照亮桌上的收藏':'云下落雨 · 透明背景';
    if(scene!=='minimal'){
      const caption=$('poster-caption');caption.querySelector('small').textContent=scene==='lamp'?'A LITTLE LIGHT, JUST FOR YOU':'A SMALL CLOUD OF YOUR OWN';
      $('poster-subtitle').textContent=scene==='lamp'?'The night desk.':'RAIN CLOUD';
      // The cloud has a fixed chrome slot; hiding its subtitle must not put items into the cloud.
      if(scene==='rain')caption.hidden=false;
      if(!state.editing)poster.querySelectorAll('.tile').forEach(tile=>delete tile.dataset.tip);
    }else $('poster-caption').querySelector('small').textContent='PERSONAL COLLECTION';
    poster.style.setProperty('--rain-travel',(poster.clientHeight-60)+'px');
    if(!running)cancel();
    if(scene==='lamp')paintLight();
  }
  const base=window.bpRender;
  window.bpRender=(state,target)=>{const old=key,oldScene=scene;base(state,target);if(old!==window.uiReviewBPlusSnapshot.active||oldScene!==(selected()?.effect||'minimal'))pointer=null;cancel();update();};
  poster.addEventListener('pointermove',e=>{if(!running||scene!=='lamp')return;const r=poster.getBoundingClientRect();pointer={x:e.clientX-r.left,y:e.clientY-r.top};queue();},{passive:true});
  poster.addEventListener('pointerleave',()=>{pointer=null;queue();},{passive:true});
  poster.addEventListener('focusin',e=>{if(!running||scene!=='lamp'||!e.target.matches('.tile'))return;const r=poster.getBoundingClientRect(),t=e.target.getBoundingClientRect();pointer={x:t.left-r.left+t.width/2,y:t.top-r.top+t.height/2};queue();});
  $('viewport').addEventListener('scroll',queue,{passive:true});
  $('scene-motion').addEventListener('change',e=>{prefs.set(key,e.target.checked);update();});
  reduced.addEventListener('change',update);document.addEventListener('visibilitychange',update);
  window.addEventListener('pagehide',()=>{pageLive=false;cancel();update();});window.addEventListener('pageshow',()=>{pageLive=true;update();});
  new IntersectionObserver(entries=>{inView=entries[0].isIntersecting;update();}).observe(poster);
  // Test/inspection surface contains only local visual state, not product state.
  window.sceneSnapshot=()=>({scene,key,running,reduced:reduced.matches,pendingFrame:!!frame,drawCount,particles:layer.querySelectorAll('.rain-drop').length,enabled:prefs.get(key)!==false});
  current().theme='lamp';window.bpCore.render();
})();
