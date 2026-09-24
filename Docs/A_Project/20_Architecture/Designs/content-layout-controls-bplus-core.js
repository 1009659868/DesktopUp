(() => {
  'use strict';
  const root=document.getElementById('review-bplus'), $=id=>document.getElementById(id);
  const seed=[{id:'al',name:'Afterlight',symbol:'AL',w:3,h:2},{id:'gs',name:'Ghost Signal',symbol:'GS',w:2,h:3},{id:'iv',name:'Iron Veil',symbol:'IV',w:2,h:2},{id:'sr',name:'Sable Run',symbol:'SR',w:1,h:2},{id:'lo',name:'Last Orbit',symbol:'LO',w:1,h:1},{id:'hc',name:'Hollow Crown',symbol:'HC',w:1,h:1}];
  const posters=[{id:'games',name:'游戏收藏',subtitle:'AFTER HOURS',color:'#202e2a',card:'#2d433a'},{id:'work',name:'工作文件',subtitle:'IN PROGRESS',color:'#232e37',card:'#34424e'},{id:'ideas',name:'灵感收集',subtitle:'OPEN MIND',color:'#302a35',card:'#463b4e'}];
  const settings=Object.fromEntries(posters.map(p=>[p.id,{axis:'vertical',cross:288,scale:1,gap:10,theme:p.id==='games'?'night':p.id==='work'?'box':'mine-plum',loop:false,auto:false,delay:1,editing:false,selected:null,grid:true,spans:true,items:structuredClone(seed),history:[],offset:0}]));
  let active='games',tab='appearance',layout=null,rendering=false,scrollTimer,toastTimer,focusTimer,resizeDrag=null,thumbDrag=null;
  const s=()=>settings[active],cell=48;
  const dimensions=(items,scale,gap)=>items.map((item,index)=>({...item,index,width:item.w*cell*scale+(item.w-1)*gap,height:item.h*cell*scale+(item.h-1)*gap}));
  // UI-only shelf layout. Input is the already-resolved order; no area/heat/pin sorting here.
  function solve(items,axis,cross,scale,gap){
    const vertical=axis==='vertical',dim=dimensions(items,scale,gap),minimum=Math.max(1,...dim.map(i=>vertical?i.width:i.height)),available=Math.max(minimum,cross),single=available<=minimum+.001;
    let across=0,along=0,band=0;
    const tiles=dim.map(item=>{const c=vertical?item.width:item.height,m=vertical?item.height:item.width;if(across&&(single||across+c>available+.001)){along+=band+gap;across=0;band=0;}const t={...item,x:vertical?across:along,y:vertical?along:across};across+=c+gap;band=Math.max(band,m);return t;});
    return{tiles,minimum,cross:available,single,extent:along+band};
  }
  function rangeFill(input){input.style.setProperty('--value',((Number(input.value)-Number(input.min))/(Number(input.max)-Number(input.min)||1)*100)+'%');}
  function toast(text){clearTimeout(toastTimer);$('toast').textContent=text;$('toast').hidden=false;toastTimer=setTimeout(()=>$('toast').hidden=true,1800);}
  function updateScroll(){
    const v=$('viewport'),vertical=s().axis==='vertical',view=vertical?v.clientHeight:v.clientWidth,extent=vertical?v.scrollHeight:v.scrollWidth,offset=vertical?v.scrollTop:v.scrollLeft,track=$('scrolltrack'),thumb=$('scrollthumb');
    track.hidden=extent<=view+1;if(track.hidden){track.classList.remove('shown');return;}
    const length=Math.max(22,view*view/extent),pos=(view-length)*offset/(extent-view);
    Object.assign(thumb.style,vertical?{height:length+'px',top:pos+'px',left:'',width:''}:{width:length+'px',left:pos+'px',top:'',height:''});
  }
  function revealScroll(){updateScroll();if($('scrolltrack').hidden)return;clearTimeout(scrollTimer);$('scrolltrack').classList.add('shown');if(!thumbDrag)scrollTimer=setTimeout(()=>{if(document.activeElement!==$('scrollthumb'))$('scrolltrack').classList.remove('shown');},1000);}
  function render(){
    rendering=true;const state=s(),poster=posters.find(p=>p.id===active),vertical=state.axis==='vertical',available=Math.max(224,$('stage').clientWidth-62),minimum=Math.max(...dimensions(state.items,state.scale,state.gap).map(i=>vertical?i.width:i.height)),max=Math.max(minimum,vertical?Math.min(360,available):290);
    state.cross=Math.max(minimum,Math.min(max,state.cross));layout=solve(state.items,state.axis,state.cross,state.scale,state.gap);const main=vertical?246:Math.min(360,available);
    $('target-name').textContent=poster.name;$('target-id').textContent='POSTER 0'+(posters.indexOf(poster)+1);$('poster-name').textContent=poster.name;$('poster-ordinal').textContent='0'+(posters.indexOf(poster)+1);$('poster-subtitle').textContent=poster.subtitle;
    root.querySelectorAll('[data-poster]').forEach(b=>b.setAttribute('aria-pressed',String(b.dataset.poster===active)));
    root.querySelectorAll('[data-theme]').forEach(b=>b.setAttribute('aria-pressed',String(b.dataset.theme===state.theme)));
    root.querySelectorAll('[data-axis]').forEach(b=>b.setAttribute('aria-pressed',String(b.dataset.axis===state.axis)));
    root.querySelectorAll('[data-tab]').forEach(b=>{b.setAttribute('aria-selected',String(b.dataset.tab===tab));b.tabIndex=b.dataset.tab===tab?0:-1;});
    $('appearance-panel').hidden=tab!=='appearance';$('behavior-panel').hidden=tab!=='behavior';$('poster').classList.toggle('vertical',vertical);$('poster').classList.toggle('horizontal',!vertical);
    const skin=window.bpTemplates.find(t=>t.id===state.theme)||window.bpTemplates[0];window.bpPaintPoster(skin);$('poster-caption').hidden=!skin.heading;
    $('poster').style.width=(vertical?state.cross+30:main+30)+'px';$('viewport').style.width=(vertical?state.cross:main)+'px';$('viewport').style.height=(vertical?main:state.cross)+'px';
    $('content').style.width=(vertical?state.cross:layout.extent)+'px';$('content').style.height=(vertical?layout.extent:state.cross)+'px';$('content').className='content'+(state.editing&&state.grid?' grid-on':'');$('content').style.backgroundSize=(cell*state.scale+state.gap)+'px '+(cell*state.scale+state.gap)+'px';
    $('content').replaceChildren(...layout.tiles.map(item=>{
      const b=document.createElement('button');b.type='button';b.className='tile'+(item.width<75||item.height<68?' compact':'')+(state.editing&&state.selected===item.id?' selected':'');b.dataset.item=item.id;b.setAttribute('aria-label',item.name+'，'+item.w+'乘'+item.h+'格');
      b.dataset.tip=state.editing?'选择 '+item.name+'，调整单项跨度。':'演示 Item：'+item.name+'。不执行真实打开。';Object.assign(b.style,{left:item.x+'px',top:item.y+'px',width:item.width+'px',height:item.height+'px'});
      const a=document.createElement('span');a.className='spanhint';a.textContent=item.w+'×'+item.h;a.hidden=!(state.editing&&state.spans);
      const n=document.createElement('span');n.className='symbol';n.textContent=item.symbol;const label=document.createElement('span');label.className='item-name';label.textContent=item.name;
      b.append(a,n,label);b.addEventListener('click',()=>{hideTip();if(state.editing){state.selected=item.id;render();}else toast('演示 Item · 未执行打开');});return b;
    }));
    if(vertical){$('viewport').scrollLeft=0;$('viewport').scrollTop=state.offset;}else{$('viewport').scrollTop=0;$('viewport').scrollLeft=state.offset;}
    $('scale').value=state.scale*100;$('scale-value').textContent=state.scale.toFixed(2)+'×';$('spacing').value=state.gap;$('spacing-value').textContent=state.gap===10?'标准':state.gap+' px';
    $('cross').min=minimum;$('cross').max=max;$('cross').value=state.cross;$('cross-label').textContent=vertical?'内容宽度':'内容高度';$('cross-value').textContent=Math.round(state.cross)+' px'+(layout.single?' · 最小':'');
    $('loop').checked=state.loop;$('loop-status').textContent=state.loop?'偏好已开启':'关闭';$('auto-posture').checked=state.auto;$('delay').disabled=!state.auto;$('delay').value=state.delay;$('delay-value').textContent=state.delay.toFixed(1)+' s';
    $('toolbar').hidden=!state.editing;$('arrange').textContent=state.editing?'正在整理':'整理内容 ↗';$('arrange').disabled=state.editing;$('poster-mode').textContent=state.editing?'ARRANGING':'BROWSE';$('grid').setAttribute('aria-pressed',String(state.grid));$('spans').setAttribute('aria-pressed',String(state.spans));$('undo').disabled=!state.history.length;
    const chosen=state.items.find(i=>i.id===state.selected);$('itemtools').hidden=!state.editing||!chosen;if(chosen){$('selected-name').textContent=chosen.name;$('item-size').value=chosen.w===chosen.h?String(chosen.w):'original';}
    root.querySelectorAll('input[type=range]').forEach(rangeFill);updateScroll();rendering=false;window.uiReviewBPlusSnapshot={active,tab,state:structuredClone(state),layout:structuredClone(layout),main};window.bpRender(state,poster);
  }
  root.querySelectorAll('[data-poster]').forEach(b=>b.addEventListener('click',()=>{hideTip();clearTimeout(scrollTimer);$('scrolltrack').classList.remove('shown');active=b.dataset.poster;render();}));
  root.querySelectorAll('[data-tab]').forEach(b=>{b.addEventListener('click',()=>{hideTip();tab=b.dataset.tab;render();});b.addEventListener('keydown',e=>{if(['ArrowLeft','ArrowRight'].includes(e.key)){e.preventDefault();tab=tab==='appearance'?'behavior':'appearance';render();$(tab+'-tab').focus();}});});
  root.querySelectorAll('[data-theme]').forEach(b=>b.addEventListener('click',()=>{s().theme=b.dataset.theme;render();}));
  root.querySelectorAll('[data-axis]').forEach(b=>b.addEventListener('click',()=>{s().axis=b.dataset.axis;s().cross=s().axis==='vertical'?288:220;s().offset=0;clearTimeout(scrollTimer);$('scrolltrack').classList.remove('shown');render();}));
  function scale(v){s().scale=v/100;render();}function gap(v){s().gap=v;render();}
  $('scale').addEventListener('input',e=>scale(Number(e.target.value)));$('spacing').addEventListener('input',e=>gap(Number(e.target.value)));
  root.querySelectorAll('[data-scale]').forEach(b=>b.addEventListener('click',()=>scale(Number(b.dataset.scale))));root.querySelectorAll('[data-gap]').forEach(b=>b.addEventListener('click',()=>gap(Number(b.dataset.gap))));
  $('cross').addEventListener('input',e=>{s().cross=Number(e.target.value);render();});$('loop').addEventListener('change',e=>{s().loop=e.target.checked;render();toast('循环偏好已更新 · 原型仍为有限滚动');});
  $('auto-posture').addEventListener('change',e=>{s().auto=e.target.checked;render();});$('delay').addEventListener('input',e=>{s().delay=Number(e.target.value);render();});
  $('arrange').addEventListener('click',()=>{hideTip();s().editing=true;render();});$('done').addEventListener('click',()=>{hideTip();s().editing=false;s().selected=null;render();});['grid','spans'].forEach(id=>$(id).addEventListener('click',()=>{s()[id]=!s()[id];render();}));
  function size(value){const state=s(),item=state.items.find(i=>i.id===state.selected);if(!item)return;const next=value==='original'?seed.find(i=>i.id===item.id):{w:Number(value),h:Number(value)};if(item.w===next.w&&item.h===next.h)return;state.history.push(structuredClone(state.items));item.w=next.w;item.h=next.h;render();}
  $('item-size').addEventListener('change',e=>size(e.target.value));$('restore-size').addEventListener('click',()=>size('original'));$('undo').addEventListener('click',()=>{if(s().history.length){s().items=s().history.pop();render();}});
  $('viewport').addEventListener('scroll',()=>{s().offset=s().axis==='vertical'?$('viewport').scrollTop:$('viewport').scrollLeft;updateScroll();if(!rendering)revealScroll();},{passive:true});
  $('viewport').addEventListener('wheel',e=>{const vertical=s().axis==='vertical',factor=e.deltaMode===1?16:e.deltaMode===2?(vertical?e.currentTarget.clientHeight:e.currentTarget.clientWidth):1,delta=(vertical?e.deltaY:(e.deltaX||e.deltaY))*factor;if(delta){e.preventDefault();if(vertical)e.currentTarget.scrollTop+=delta;else e.currentTarget.scrollLeft+=delta;revealScroll();}},{passive:false});
  function navigate(e){const state=s(),v=$('viewport'),vertical=state.axis==='vertical',view=vertical?v.clientHeight:v.clientWidth;let value=null;const current=vertical?v.scrollTop:v.scrollLeft;if(e.key==='Home')value=0;else if(e.key==='End')value=vertical?v.scrollHeight:v.scrollWidth;else if(e.key==='PageDown')value=current+view*.85;else if(e.key==='PageUp')value=current-view*.85;else if(e.key===(vertical?'ArrowDown':'ArrowRight'))value=current+40;else if(e.key===(vertical?'ArrowUp':'ArrowLeft'))value=current-40;if(value!==null){e.preventDefault();if(vertical)v.scrollTop=value;else v.scrollLeft=value;revealScroll();}}
  $('viewport').addEventListener('keydown',navigate);$('scrollthumb').addEventListener('keydown',navigate);$('scrollthumb').addEventListener('focus',revealScroll);$('scrollthumb').addEventListener('blur',revealScroll);
  $('viewportwrap').addEventListener('pointermove',e=>{const r=e.currentTarget.getBoundingClientRect();if(s().axis==='vertical'?r.right-e.clientX<12:r.bottom-e.clientY<12)revealScroll();});
  $('scrollthumb').addEventListener('pointerdown',e=>{const v=$('viewport'),vertical=s().axis==='vertical';thumbDrag={id:e.pointerId,start:vertical?e.clientY:e.clientX,offset:vertical?v.scrollTop:v.scrollLeft,extent:vertical?v.scrollHeight:v.scrollWidth,view:vertical?v.clientHeight:v.clientWidth,length:vertical?e.currentTarget.offsetHeight:e.currentTarget.offsetWidth};e.currentTarget.setPointerCapture(e.pointerId);clearTimeout(scrollTimer);});
  $('scrollthumb').addEventListener('pointermove',e=>{if(!thumbDrag||e.pointerId!==thumbDrag.id)return;const vertical=s().axis==='vertical',delta=(vertical?e.clientY:e.clientX)-thumbDrag.start,value=thumbDrag.offset+delta*(thumbDrag.extent-thumbDrag.view)/Math.max(1,thumbDrag.view-thumbDrag.length);if(vertical)$('viewport').scrollTop=value;else $('viewport').scrollLeft=value;});
  ['pointerup','pointercancel','lostpointercapture'].forEach(t=>$('scrollthumb').addEventListener(t,()=>{thumbDrag=null;revealScroll();}));
  $('edge').addEventListener('pointerdown',e=>{hideTip();resizeDrag={id:e.pointerId,start:s().axis==='vertical'?e.clientX:e.clientY,cross:s().cross};e.currentTarget.setPointerCapture(e.pointerId);});
  $('edge').addEventListener('pointermove',e=>{if(resizeDrag&&resizeDrag.id===e.pointerId){s().cross=resizeDrag.cross+(s().axis==='vertical'?e.clientX:e.clientY)-resizeDrag.start;render();}});['pointerup','pointercancel','lostpointercapture'].forEach(t=>$('edge').addEventListener(t,()=>resizeDrag=null));
  $('edge').addEventListener('keydown',e=>{if(['ArrowLeft','ArrowUp','ArrowRight','ArrowDown'].includes(e.key)){e.preventDefault();s().cross+=['ArrowLeft','ArrowUp'].includes(e.key)?-4:4;render();}});
  $('locate').addEventListener('click',()=>{clearTimeout(focusTimer);$('poster').classList.add('focused');$('poster').scrollIntoView({block:'nearest',behavior:'auto'});focusTimer=setTimeout(()=>$('poster').classList.remove('focused'),1200);});$('about').addEventListener('click',()=>{hideTip();$('about-dialog').showModal();});
  let tipTarget=null,tipDescriptionTarget=null,tipTimer,tipHideTimer;
  function hideTip(){clearTimeout(tipTimer);clearTimeout(tipHideTimer);$('tooltip').hidden=true;if(tipDescriptionTarget)tipDescriptionTarget.removeAttribute('aria-describedby');tipTarget=null;tipDescriptionTarget=null;}
  function showTip(el,immediate=false){if(!el||!el.dataset.tip)return;clearTimeout(tipHideTimer);if(tipTarget===el&&!$('tooltip').hidden&&!immediate)return;hideTip();tipTarget=el;tipTimer=setTimeout(()=>{if(!el.isConnected)return;const tip=$('tooltip');tip.textContent=el.dataset.tip;tip.hidden=false;tipDescriptionTarget=el.contains(document.activeElement)?document.activeElement:el;tipDescriptionTarget.setAttribute('aria-describedby','tooltip');const r=el.getBoundingClientRect(),w=tip.offsetWidth,h=tip.offsetHeight;tip.style.left=Math.max(8,Math.min(innerWidth-w-8,r.left+r.width/2-w/2))+'px';tip.style.top=(r.top>h+12?r.top-h-8:Math.min(innerHeight-h-8,r.bottom+8))+'px';},immediate?0:400);}
  root.addEventListener('pointerover',e=>{const el=e.target.closest('[data-tip]');if(el)showTip(el);});root.addEventListener('pointerout',e=>{const el=e.target.closest('[data-tip]');if(el&&!el.contains(e.relatedTarget)&&e.relatedTarget!==$('tooltip'))tipHideTimer=setTimeout(hideTip,130);});root.addEventListener('focusin',e=>showTip(e.target.closest('[data-tip]'),true));root.addEventListener('focusout',()=>tipHideTimer=setTimeout(hideTip,100));$('tooltip').addEventListener('pointerenter',()=>clearTimeout(tipHideTimer));$('tooltip').addEventListener('pointerleave',hideTip);root.addEventListener('keydown',e=>{if(e.key==='Escape')hideTip();});window.addEventListener('scroll',hideTip,true);
  let lastWidth=0;new ResizeObserver(()=>{const w=$('stage').clientWidth;if(w!==lastWidth){lastWidth=w;render();}}).observe($('stage'));
  window.bpCore={state:s,render,notify:toast,hideTip};window.uiReviewBPlusSolve=solve;window.uiReviewBPlusSeed=structuredClone(seed);render();
})();
