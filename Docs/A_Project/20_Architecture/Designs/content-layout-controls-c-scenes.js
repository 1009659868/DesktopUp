/* Original, bounded CSS scene study inspired by the sources credited in CONTENT_TEMPLATE_SCENES_REVIEW.md.
   No imported code/assets, WebGL, pointer capture, layout solver, file IO or persistence. */
(() => {
  'use strict';
  const $=id=>document.getElementById(id),poster=$('poster'),prefs=new Map(),reduced=matchMedia('(prefers-reduced-motion: reduce)');
  const layer=document.createElement('div');layer.className='scene-surface';layer.setAttribute('aria-hidden','true');
  layer.innerHTML='<div class="scene-light"><div class="light-cone"></div><div class="light-pool"></div><div class="hanging-lamp"><i class="lamp-cord"></i><i class="lamp-shade"></i></div></div><div class="scene-weather"><div class="rain-trails"></div><div class="storm-cloud"><svg viewBox="0 0 360 126" preserveAspectRatio="none"><defs><linearGradient id="rain-cloud-tone" x1=".2" y1="0" x2=".7" y2="1"><stop offset="0" stop-color="#70869b"/><stop offset=".5" stop-color="#3f576d"/><stop offset="1" stop-color="#243b50"/></linearGradient><filter id="c-cloud-feather" x="-12%" y="-30%" width="124%" height="160%"><feGaussianBlur stdDeviation="1.1"/></filter><linearGradient id="c-cloud-mist" x2="0" y2="1"><stop stop-color="#dfeaf1" stop-opacity=".32"/><stop offset="1" stop-color="#dfeaf1" stop-opacity="0"/></linearGradient></defs><g class="cloud-vapor" filter="url(#c-cloud-feather)" fill="url(#rain-cloud-tone)"><path d="M12 83C30 69 47 73 63 61C86 38 119 41 141 49C165 26 195 31 214 49C241 36 260 53 279 59C304 61 331 70 346 88C320 104 294 108 268 105C237 116 203 108 175 111C143 120 117 108 91 111C59 112 25 101 12 83Z" opacity=".32"/></g><g class="cloud-core" filter="url(#c-cloud-feather)"><path d="M35 94C16 93 11 78 22 67C28 59 40 58 47 60C44 41 60 29 78 35C90 12 127 11 145 29C166 9 204 14 216 38C239 24 268 36 272 57C291 47 314 58 316 75C337 72 345 90 327 99C310 110 288 103 268 105C248 113 228 107 208 109C184 118 167 109 147 112C124 116 103 110 83 112C67 111 45 108 35 94Z" fill="url(#rain-cloud-tone)"/><path d="M47 76C72 75 77 49 103 50C127 54 142 31 162 34C186 32 198 53 215 49C238 43 249 69 270 67C284 65 303 75 312 86C282 80 267 85 246 83C216 84 207 78 180 86C157 84 143 91 120 87C97 91 81 80 47 76Z" fill="url(#c-cloud-mist)"/></g><path class="cloud-wisp" d="M35 95C87 91 104 108 152 102S232 98 277 101C244 115 215 112 181 116C124 120 74 109 35 95Z" fill="url(#rain-cloud-tone)" opacity=".24" filter="url(#c-cloud-feather)"/></svg></div><div class="storm-glow"></div><div class="storm-bolt"></div></div>';
  poster.append(layer);
  const glyphs={al:'<path d="m24 5 4 13 13 6-13 5-4 14-5-14-12-5 12-6Z"/><path d="m38 6 1.5 5 4.5 2-4.5 2-1.5 5-2-5-5-2 5-2Z"/>',gs:'<path d="M32 7C18 5 8 17 11 30c3 12 17 15 27 8-13 0-21-12-17-22 2-4 5-7 11-9Z"/><path d="m35 12 2 5 5 2-5 2-2 5-2-5-5-2 5-2Z"/>',iv:'<path d="m5 37 14-25 10 16 7-10 8 19Z"/><path d="m14 22 5 3 5-3M19 12l10 25"/>',sr:'<circle cx="24" cy="24" r="12"/><ellipse cx="24" cy="24" rx="23" ry="6" transform="rotate(-28 24 24)"/>',lo:'<circle cx="24" cy="24" r="15"/><path d="M9 24h30M24 9c-10 8-10 22 0 30 10-8 10-22 0-30Z"/>',hc:'<path d="M11 32V17l8 6 5-13 5 13 8-6v15M11 37h26M15 32h18"/>'};
  function paintRainGlyphs(){poster.querySelectorAll('.tile').forEach(tile=>{tile.style.setProperty('--rain-glyph-size',Math.min(72,tile.clientWidth*.54,Math.max(24,tile.clientHeight-52))+'px');if(tile.querySelector('.rain-glyph'))return;const glyph=document.createElement('span');glyph.className='rain-glyph';glyph.setAttribute('aria-hidden','true');glyph.innerHTML='<svg viewBox="0 0 48 48" fill="none" stroke="currentColor" stroke-width="1.4" stroke-linecap="round" stroke-linejoin="round">'+(glyphs[tile.dataset.item]||glyphs.lo)+'</svg>';tile.prepend(glyph);});}
  const prefKey=()=>key+'|'+current().theme;
  // A fixed pool across the transparent content backdrop, behind the real items.
  for(let i=0;i<64;i++){
    const drop=document.createElement('i');drop.className='rain-drop';
    drop.style.setProperty('--drop-x',(6+(i*37)%89)+'%');drop.style.setProperty('--drop-length',(17+(i*7)%23)+'px');
    drop.style.setProperty('--duration',(1.5+(i%7)*.17)+'s');drop.style.setProperty('--delay',(-i*.31)+'s');drop.style.setProperty('--still-y',(24+(i*31)%345)+'px');
    layer.querySelector('.rain-trails').append(drop);
  }
  let scene='minimal',key='',running=false,inView=true,pageLive=true,frame=0,pointer=null,drawCount=0,values={};
  const current=()=>window.bpCore.state(),selected=()=>window.bpTemplates.find(t=>t.id===current().theme);
  function cancel(){if(frame)cancelAnimationFrame(frame);frame=0;}
  function paintLight(){
    frame=0;if(scene!=='lamp')return;
    const bounds=poster.getBoundingClientRect(),point=running&&pointer?pointer:{x:bounds.width*.55,y:bounds.height*.47};
    const x=Math.max(8,Math.min(bounds.width-8,point.x)),y=Math.max(66,Math.min(bounds.height-16,point.y));
    poster.style.setProperty('--light-x',x+'px');poster.style.setProperty('--light-y',y+'px');
    poster.style.setProperty('--lamp-angle',Math.max(-13,Math.min(13,(x-bounds.width*.75)/15))+'deg');
    const reach=65+(values["lamp.softness"]??60);
    layer.querySelector('.light-cone').style.clipPath='polygon(72% 57px,78% 57px,'+(x+reach)+'px '+(y+40)+'px,'+(x-reach)+'px '+(y+40)+'px)';
    poster.querySelectorAll('.tile').forEach(tile=>{
      const r=tile.getBoundingClientRect(),tx=x-(r.left-bounds.left),ty=y-(r.top-bounds.top),distance=Math.hypot(tx-r.width/2,ty-r.height/2),power=Math.max(0,1-distance/(150+(values["lamp.softness"]??60)*2));
      tile.style.setProperty('--tile-light-x',tx+'px');tile.style.setProperty('--tile-light-y',ty+'px');
      tile.style.setProperty('--tile-light',String((.025+power*.3)*(values['lamp.intensity']??70)/70));tile.style.setProperty('--edge-light',String((.1+power*.35)*(values['lamp.intensity']??70)/70));
      tile.style.setProperty('--shadow-x',(Math.max(-5,Math.min(5,(r.width/2-tx)/28)))+'px');tile.style.setProperty('--shadow-y',(Math.max(1,Math.min(6,(r.height/2-ty)/25+3)))+'px');
    });
    drawCount++;
  }
  function queue(){if(scene==='lamp'&&running&&!frame)frame=requestAnimationFrame(paintLight);}
  function update(){
    cancel();
    const state=current(),t=selected();scene=t?.effect||'minimal';key=window.uiReviewBPlusSnapshot.active;
    values=window.CModel.resolve(key,state.theme);
    const bg=values['surface.background']??t.bg,card=values['item.color']??t.card;
    window.bpPaintPoster({...t,bg,card});
    const rgb=hex=>[1,3,5].map(i=>parseInt(hex.slice(i,i+2),16));
    const lampColor=values['lamp.color']??'#ffdc99';
    poster.style.setProperty('--surface-fill',bg+Math.round((values['surface.opacity']??100)*2.55).toString(16).padStart(2,'0'));
    poster.style.setProperty('--lamp-rgb',rgb(lampColor).join(' '));
    poster.style.setProperty('--lamp-color',lampColor);
    poster.style.setProperty('--lamp-strength',String((values['lamp.intensity']??70)/100));
    poster.style.setProperty('--lamp-radius',(70+(values['lamp.softness']??60)*1.5)+'px');
    poster.style.setProperty('--rain-color',values['rain.color']??'#93c9e9');
    const cloud=values['cloud.color']??'#3f576d',cloudRGB=rgb(cloud),light=cloudRGB.reduce((s,x,i)=>s+x*[.2126,.7152,.0722][i],0)>155;
    poster.style.setProperty('--cloud-ink',light?'#16232c':'#f2f7fa');
    const stops=layer.querySelectorAll('#rain-cloud-tone stop');
    [.22,0,-.24].forEach((amount,i)=>stops[i].setAttribute('stop-color','#'+cloudRGB.map(v=>Math.round(amount>0?v+(255-v)*amount:v*(1+amount)).toString(16).padStart(2,'0')).join('')));
    layer.querySelectorAll('.rain-drop').forEach((drop,i)=>{drop.hidden=i>=(values['rain.count']??28);const speed=values['rain.speed']??1;drop.style.setProperty('--duration',((1.5+(i%7)*.17)/speed)+'s');drop.style.setProperty('--delay',(-i*.31/speed)+'s');});
    layer.querySelector('.storm-bolt').hidden=values['rain.lightning']===false;
    layer.querySelector('.storm-glow').hidden=values['rain.lightning']===false;
    const wind=values['cloud.wind']??35;
    poster.style.setProperty('--cloud-drift',(wind*.12)+'px');poster.style.setProperty('--cloud-rise',(wind*.04)+'px');poster.style.setProperty('--cloud-tilt',(wind*.012)+'deg');poster.style.setProperty('--rain-slant',(-wind*.035)+'deg');
    poster.dataset.windActive=String(wind>0);
    const enabled=prefs.get(prefKey())!==false;
    running=scene!=='minimal'&&enabled&&!state.editing&&!reduced.matches&&!document.hidden&&inView&&pageLive;
    poster.dataset.scene=scene;poster.dataset.fxRunning=String(running);poster.dataset.fxEnabled=String(enabled);poster.dataset.fxEditing=String(state.editing);
    poster.dataset.rainCaption=String(t?.heading!==false);
    $('scene-option').hidden=scene==='minimal';$('scene-motion').checked=enabled;
    $('scene-status').textContent=!enabled?'已关闭 · 保留静态材质':state.editing?'整理中 · 暂缓光影':reduced.matches?'减少动效 · 静态呈现':scene==='lamp'?'移动鼠标，照亮桌上的收藏':'云下落雨 · 透明背景';
    if(scene!=='minimal'){
      const caption=$('poster-caption');caption.querySelector('small').textContent=scene==='lamp'?'A LITTLE LIGHT, JUST FOR YOU':'A SMALL CLOUD OF YOUR OWN';
      $('poster-subtitle').textContent=scene==='lamp'?'The night desk.':'雨声收藏';
      // The cloud has a fixed chrome slot; hiding its subtitle must not put items into the cloud.
      if(scene==='rain'){caption.hidden=false;paintRainGlyphs();}
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
  $('scene-motion').addEventListener('change',e=>{prefs.set(prefKey(),e.target.checked);update();});
  reduced.addEventListener('change',update);document.addEventListener('visibilitychange',update);
  window.addEventListener('pagehide',()=>{pageLive=false;cancel();update();});window.addEventListener('pageshow',()=>{pageLive=true;update();});
  new IntersectionObserver(entries=>{inView=entries[0].isIntersecting;update();}).observe(poster);
  // Test/inspection surface contains only local visual state, not product state.
  window.sceneSnapshot=()=>({scene,key,running,reduced:reduced.matches,pendingFrame:!!frame,drawCount,particles:layer.querySelectorAll('.rain-drop:not([hidden])').length,pool:64,values:structuredClone(values),enabled:prefs.get(prefKey())!==false});
  window.CScenes={refresh:update};
  current().theme='lamp';window.bpCore.render();
})();
