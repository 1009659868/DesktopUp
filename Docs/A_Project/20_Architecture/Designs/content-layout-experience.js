/* UI C companion: isolated experience trial. No filesystem, launch, network or persistent storage. */
(() => {
  'use strict';
  const M=window.ExperienceModel,$=id=>document.getElementById(id),STEP=40;
  const tasks={
    1:'找到「项目计划」并打开，然后返回。再选中「参考资料」，但不要打开它。',
    2:'把「参考资料」放到你想要的位置，再移到已有内容的位置。若不满意，恢复这次操作前的安排。',
    3:'让「归档」保持在比较靠前的位置。模拟一次热度顺序变化后找回它，再让它重新跟随自动排序。',
    4:'把海报收窄，再放宽。随后再次收窄，只调整「工具箱」的位置，然后放宽。操作前先写下你预计其他内容会怎样。',
    5:'观察「工具箱」的固定顺序和「参考资料」的大小。切到手动，改动参考资料的位置和大小，再切回自动、切回手动。先写下你预计哪些安排会保留。',
    6:'开始拖动后取消；关闭控制中心后再尝试拖动。最后模拟空间不足，判断是否知道如何安全继续。'
  };
  let doc=M.seed(),width=8,task=1,selected=null,history=[],redo=[],revision=0,opens=0;
  let drag=null,edge=null,preview=null,shadow=null,nonDragClicks=0,lastClickId=null,suppressClick=false;
  let lastGoodWidth=8,records=[],facts=[],pendingHeat=false;
  const tiles=new Map();
  const geometry=()=>M.layout(doc,width);
  const selectedItem=()=>M.items.find(i=>i.id===selected);
  function tell(text,warning=false){$('feedback').textContent=text;$('feedback').classList.toggle('warning',warning);}
  function fact(text){facts.push(text);$('facts').textContent=facts.slice(-30).join('\n');}
  function requireGesture(){if($('gesture').value)return true;tell('请先在试验条件中选择基础手势；这不是产品的整理前置。',true);$('gesture').focus();return false;}
  function markSelection(id){selected=id;render();}
  function activeView(){return preview?.ok?preview.view:geometry();}
  function render(){
    const view=activeView(),good=view.ok;
    $('poster').hidden=!good;$('restricted').hidden=good;
    $('poster').style.width=(width*STEP+24)+'px';
    $('viewport').style.width=width*STEP+'px';$('board').style.width=width*STEP+'px';
    $('board').style.height=(good?Math.max(270,view.extent*STEP):270)+'px';
    $('board').classList.toggle('grid',$('show-grid').checked);
    for(const member of M.items){
      const tile=tiles.get(member.id),r=view.items.find(i=>i.id===member.id);
      tile.hidden=!r;if(!r)continue;
      Object.assign(tile.style,{left:(r.x*STEP+4)+'px',top:(r.y*STEP+4)+'px',width:(r.w*STEP-8)+'px',height:(r.h*STEP-8)+'px'});
      tile.classList.toggle('compact',r.h===1);tile.classList.toggle('drag-source',!!(drag?.started&&drag.id===r.id));
      tile.setAttribute('aria-pressed',String(selected===r.id));
      const pin=doc.mode==='auto'?doc.pins[r.id]:null;
      tile.setAttribute('aria-label',member.name+(pin?'，固定第'+pin+'位':''));
      tile.querySelector('.span').textContent=r.w+'×'+r.h;tile.querySelector('.span').hidden=!$('show-span').checked;
      tile.querySelector('.pin').textContent=pin?'钉 '+pin:'';tile.querySelector('.pin').hidden=!pin;
    }
    const chosen=selectedItem(),r=view.items.find(i=>i.id===selected);
    $('selection-name').textContent=chosen?.name||'未选中';$('size').disabled=!r||!!drag||!good;
    $('size').value=r?r.w+'x'+r.h:'';
    $('toggle-pin').disabled=!r||doc.mode!=='auto'||!!drag;
    $('toggle-pin').textContent=doc.pins[selected]?'解除固定顺序':'固定当前顺序';
    $('mode-manual').setAttribute('aria-pressed',String(doc.mode==='manual'));
    $('mode-auto').setAttribute('aria-pressed',String(doc.mode==='auto'));
    $('mode-manual').disabled=!!drag||!good;$('mode-auto').disabled=!!drag||!good;
    $('mode-badge').textContent=doc.mode==='manual'?'手动布局':'热度自动';
    $('mode-note').textContent=doc.mode==='manual'?'当前位置由你安排，热度不改变布局。':'未固定项跟随热度；拖动后固定目标顺序位。';
    $('undo').disabled=!history.length||!!drag;$('redo').disabled=!redo.length||!!drag;$('heat').disabled=!!drag;
    $('poster-state').textContent=view.adapted?'适配显示 · 原构图保留':'页面内存 · 无文件操作';
    $('width').min=String(M.minWidth(doc));$('width').max=String(capacity());$('width').value=String(width);
    $('width-output').textContent=width+' 格';
    window.experienceSnapshot={doc:M.clone(doc),width,task,selected,history:history.length,redo:redo.length,revision,opens,view:M.clone(view),dragging:!!drag?.started,pendingHeat,records:M.clone(records)};
  }
  function commit(p,label){
    if(!p.ok){tell(p.reason,true);fact('拒绝：'+p.reason);render();return false;}
    if(!p.changed){tell('没有布局变化，未保存。');render();return false;}
    history.push(M.clone(doc));if(history.length>30)history.shift();redo=[];doc=M.clone(p.doc);revision++;
    tell(label);fact('提交 '+revision+'：'+label);render();return true;
  }
  function openItem(id){
    if(!requireGesture()||drag?.started)return;
    selected=id;opens++;$('open-title').textContent=selectedItem().name;
    $('context').hidden=true;$('mock-open').showModal();fact('模拟打开 '+selected+'：累计 '+opens+' 次');render();
  }
  function hideMenu(){$('context').hidden=true;}
  function context(id,x,y){
    if(!requireGesture())return;selected=id;render();
    $('context-pin').hidden=doc.mode!=='auto';$('context-pin').textContent=doc.pins[id]?'解除固定顺序':'固定当前顺序';
    $('context').hidden=false;
    $('context').style.left=Math.max(8,Math.min(x,innerWidth-$('context').offsetWidth-8))+'px';
    $('context').style.top=Math.max(8,Math.min(y,innerHeight-$('context').offsetHeight-8))+'px';
  }
  for(const i of M.items){
    const tile=document.createElement('button');tile.type='button';tile.className='trial-tile';tile.dataset.id=i.id;tile.draggable=false;
    for(const [cls,text]of[['span',''],['pin',''],['mark',i.mark],['name',i.name]]){const span=document.createElement('span');span.className=cls;span.textContent=text;tile.append(span);}
    tile.addEventListener('click',()=>{
      if(suppressClick){suppressClick=false;return;}if(!requireGesture())return;
      nonDragClicks=lastClickId===i.id?nonDragClicks+1:1;lastClickId=i.id;
      markSelection(i.id);if($('gesture').value==='single'&&!$('mock-open').open)openItem(i.id);
    });
    tile.addEventListener('dblclick',e=>{e.preventDefault();if($('gesture').value==='double'&&nonDragClicks>=2&&!$('mock-open').open){openItem(i.id);nonDragClicks=0;}});
    tile.addEventListener('contextmenu',e=>{e.preventDefault();cancelDrag('右键中止未提交拖动');context(i.id,e.clientX,e.clientY);});
    tile.addEventListener('keydown',e=>{
      if(e.key==='Enter'&&!e.repeat){e.preventDefault();openItem(i.id);}
      if(e.key==='F10'&&e.shiftKey){e.preventDefault();const r=tile.getBoundingClientRect();context(i.id,r.left,r.bottom);}
    });
    tile.addEventListener('pointerdown',e=>{
      if(e.button!==0||drag||!requireGesture())return;
      suppressClick=false;hideMenu();const r=tile.getBoundingClientRect(),view=geometry();
      if(!view.ok)return;
      selected=i.id;drag={pointerId:e.pointerId,id:i.id,owner:tile,startX:e.clientX,startY:e.clientY,grabX:e.clientX-r.left,grabY:e.clientY-r.top,started:false,base:M.clone(doc),view:M.clone(view),last:e};
      // Stable source owns capture and remains the click target; rendering never replaces it.
      tile.setPointerCapture(e.pointerId);render();
    });
    tiles.set(i.id,tile);$('board').append(tile);
  }
  function targetFor(e){
    const rect=$('board').getBoundingClientRect(),vp=$('viewport').getBoundingClientRect();
    if(e.clientX<vp.left||e.clientX>vp.right||e.clientY<vp.top||e.clientY>vp.bottom)return null;
    if(drag.base.mode==='manual')return {x:Math.round((e.clientX-rect.left-drag.grabX-4)/STEP),y:Math.round((e.clientY-rect.top-drag.grabY-4)/STEP)};
    const x=(e.clientX-rect.left)/STEP,y=(e.clientY-rect.top)/STEP;
    let nearest=drag.view.items[0],distance=Infinity;
    for(const r of drag.view.items){const d=Math.hypot(x-r.x-r.w/2,y-r.y-r.h/2);if(d<distance){nearest=r;distance=d;}}
    return {rank:drag.view.items.findIndex(r=>r.id===nearest.id)+1};
  }
  function updateDrag(e){
    if(!drag||e.pointerId!==drag.pointerId)return;
    drag.last={pointerId:e.pointerId,clientX:e.clientX,clientY:e.clientY};
    if(!drag.started&&Math.hypot(e.clientX-drag.startX,e.clientY-drag.startY)<5)return; // Trial threshold, not production contract.
    if(!drag.started){
      drag.started=true;nonDragClicks=0;lastClickId=null;
      shadow=document.createElement('div');shadow.className='drag-shadow';shadow.textContent=selectedItem().name;
      const r=drag.view.items.find(i=>i.id===drag.id);shadow.style.width=(r.w*STEP-8)+'px';shadow.style.height=(r.h*STEP-8)+'px';document.body.append(shadow);
    }
    shadow.style.left=(e.clientX-drag.grabX)+'px';shadow.style.top=(e.clientY-drag.grabY)+'px';
    const target=targetFor(e);
    preview=target?M.move(drag.base,width,drag.id,target):{ok:false,reason:'已离开本海报：本试验不执行跨海报或系统拖放，松手取消'};
    if(!preview.ok)tell(preview.reason,true);
    else if(!preview.changed)tell('返回原位置，松手不保存。');
    else if(doc.mode==='auto')tell('松手固定到第 '+preview.rank+' 位；其他内容仍自动。');
    else if(preview.adopted)tell('松手将采用整张当前排列，并完成本次移动；'+preview.adoptedIds.length+' 项适配位置将保存，可一次撤销。',true);
    else tell('松手移动所选内容'+(preview.affected.length?'，另有 '+preview.affected.length+' 项局部让位':'')+'；可一次撤销。');
    render();
  }
  function finishDrag(e){
    if(!drag||e.pointerId!==drag.pointerId)return;
    const moved=drag.started;if(moved)updateDrag(e);
    const p=preview,id=drag.id,pointerId=drag.pointerId,owner=drag.owner;drag=null;preview=null;shadow?.remove();shadow=null;
    if(owner.hasPointerCapture(pointerId))owner.releasePointerCapture(pointerId);
    if(moved){suppressClick=true;nonDragClicks=0;
      if(p?.ok)commit(p,p.adopted?'已采用当前整张构图并移动 '+M.items.find(i=>i.id===id).name+'；可撤销。':doc.mode==='auto'?'已固定到第 '+p.rank+' 位。':'已移动内容，受影响项一起保存。');
      else {tell(p?.reason||'取消本次拖动',true);fact('拖动取消：无布局保存');}
    } else {markSelection(id);}
    render();
  }
  function cancelDrag(reason){
    if(!drag)return;
    const pointerId=drag.pointerId,moved=drag.started,owner=drag.owner;drag=null;preview=null;shadow?.remove();shadow=null;
    if(owner.hasPointerCapture(pointerId))owner.releasePointerCapture(pointerId);
    if(moved){suppressClick=true;nonDragClicks=0;}
    tell(reason);fact('取消：'+reason+'；未保存');render();
  }
  $('board').addEventListener('pointermove',updateDrag);
  $('board').addEventListener('pointerup',finishDrag);
  $('board').addEventListener('pointercancel',()=>cancelDrag('输入取消，保留原布局'));
  $('board').addEventListener('lostpointercapture',()=>{if(drag)cancelDrag('输入捕获中断，保留原布局');});
  $('viewport').addEventListener('scroll',()=>{if(drag?.started)updateDrag(drag.last);});
  window.addEventListener('blur',()=>cancelDrag('窗口失活，取消未提交编辑'));
  for(const mode of ['manual','auto'])$('mode-'+mode).addEventListener('click',()=>{
    if(!requireGesture())return;
    const p=M.setMode(doc,width,mode,$('exit-policy').value);
    commit(p,mode==='manual'?'已从当前完整画面进入手动，可撤销。':$('exit-policy').value==='retain-auto'?'候选 A：已恢复原自动图钉和尺寸覆盖；手动构图可撤销恢复。':'候选 B：已恢复自动图钉，并带入手动阶段明确改过的尺寸；可撤销。');
  });
  $('size').addEventListener('change',()=>{
    if(!selected||!requireGesture())return;
    const [w,h]=$('size').value.split('x').map(Number);
    commit(M.resize(doc,width,selected,w,h),'已调整所选内容跨度'+(geometry().adapted?'并采用当前构图':'')+'，可撤销。');
  });
  function togglePin(){if(selected&&requireGesture())commit(M.pin(doc,width,selected),'已更新所选内容的固定顺序，可撤销。');hideMenu();}
  $('toggle-pin').addEventListener('click',togglePin);$('context-pin').addEventListener('click',togglePin);
  $('context-open').addEventListener('click',()=>openItem(selected));
  document.addEventListener('pointerdown',e=>{if(!e.target.closest('#context'))hideMenu();});
  function undoAction(forward=false){
    if(drag)return;
    const from=forward?redo:history,to=forward?history:redo;if(!from.length)return;
    const latestHeatOrder=[...doc.order];to.push(M.clone(doc));doc=from.pop();doc.order=latestHeatOrder;revision++;pendingHeat=false;
    tell(forward?'已重做完整操作。':'已撤销完整操作；容器宽度不回退。');fact((forward?'重做':'撤销')+'：revision '+revision);render();
  }
  $('undo').addEventListener('click',()=>undoAction());$('redo').addEventListener('click',()=>undoAction(true));
  document.addEventListener('keydown',e=>{
    const typing=e.target.matches('input,textarea,select');
    if(e.key==='Escape'){if(drag){e.preventDefault();cancelDrag('已取消拖动，保留原布局');}else hideMenu();}
    if(!typing&&e.ctrlKey&&e.key.toLowerCase()==='z'){e.preventDefault();undoAction(e.shiftKey);}
  });
  ['show-grid','show-span'].forEach(id=>$(id).addEventListener('change',render));
  $('close-center').addEventListener('click',()=>{cancelDrag('中心关闭，取消未提交拖动');$('center').hidden=true;$('open-center').hidden=false;document.querySelector('.trial-workspace').style.gridTemplateColumns='1fr';render();});
  $('open-center').addEventListener('click',()=>{$('center').hidden=false;$('open-center').hidden=true;document.querySelector('.trial-workspace').style.gridTemplateColumns='';render();});
  function capacity(){return Math.min(8,Math.max(4,Math.floor(($('stage').clientWidth-60)/STEP)));}
  function setWidth(value){
    cancelDrag('空间变化，取消未提交拖动');
    width=Math.max(M.minWidth(doc),Math.min(capacity(),Number(value)));lastGoodWidth=width;render();
  }
  $('width').addEventListener('input',()=>setWidth($('width').value));
  $('resize-edge').addEventListener('pointerdown',e=>{
    if(e.button!==0)return;cancelDrag('开始调整容器');edge={id:e.pointerId,start:e.clientX,width};e.currentTarget.setPointerCapture(e.pointerId);
  });
  $('resize-edge').addEventListener('pointermove',e=>{if(edge?.id===e.pointerId){width=Math.max(M.minWidth(doc),Math.min(capacity(),edge.width+Math.round((e.clientX-edge.start)/STEP)));lastGoodWidth=width;render();}});
  $('resize-edge').addEventListener('pointerup',()=>{edge=null;});
  $('resize-edge').addEventListener('pointercancel',()=>{if(edge){width=edge.width;edge=null;render();}});
  $('small-space').addEventListener('click',()=>{cancelDrag('模拟被动空间不足');lastGoodWidth=width;width=M.minWidth(doc)-1;fact('模拟不足：作者布局未改；产品恢复策略待决定');tell('本场景只暴露未闭合的边界，不代表已经设计好降级体验。',true);render();});
  $('restore-space').addEventListener('click',()=>{width=lastGoodWidth;render();tell('已还原试验空间；没有修改作者布局。');});
  $('heat').addEventListener('click',()=>{
    if(drag)return; // Injection disabled during a gesture; native scheduling is outside this trial.
    const p=M.heat(doc,width);doc=p.doc;fact('模拟热度源变化：不进入人工撤销栈');render();tell(doc.mode==='manual'?'热度源已变，但手动构图不变。':'未固定内容按样例热度重排；图钉与尺寸覆盖保留。');
  });
  function loadTask(n){
    cancelDrag('切换试验任务');hideMenu();if($('mock-open').open)$('mock-open').close();
    task=n;doc=M.seed([3,5].includes(n)?'auto':'manual');width=capacity();lastGoodWidth=width;selected=null;history=[];redo=[];revision=0;opens=0;facts=[];pendingHeat=false;nonDragClicks=0;suppressClick=false;
    $('task-intent').textContent=tasks[n];$('prediction').value='';$('observation').value='';$('verdict').value='';$('record-state').textContent='';$('facts').textContent='';
    document.querySelectorAll('[data-task]').forEach(b=>b.setAttribute('aria-pressed',String(Number(b.dataset.task)===n)));
    $('viewport').scrollTop=0;tell('请按任务意图尝试；本页不会自动判定体验通过。');render();
  }
  document.querySelectorAll('[data-task]').forEach(b=>b.addEventListener('click',()=>loadTask(Number(b.dataset.task))));
  $('restart-task').addEventListener('click',()=>loadTask(task));
  $('gesture').addEventListener('change',()=>{loadTask(task);fact('手势试验条件：'+$('gesture').selectedOptions[0].text);});
  $('exit-policy').addEventListener('change',()=>{loadTask(task);fact('退出策略试验条件：'+$('exit-policy').selectedOptions[0].text);});
  $('record-result').addEventListener('click',()=>{
    if(!$('verdict').value){$('record-state').textContent='请先填写操作后的判断。';return;}
    records.push({task,gesture:$('gesture').value,exitPolicy:$('exit-policy').value,prediction:$('prediction').value,verdict:$('verdict').value,observation:$('observation').value});
    $('records').textContent=records.map((r,n)=>(n+1)+'. 任务 '+r.task+' / '+r.gesture+' / '+r.exitPolicy+'\n预期：'+r.prediction+'\n判断：'+r.verdict+'\n观察：'+r.observation).join('\n\n');
    $('record-state').textContent='已记在本页内存；刷新会清空。';render();
  });
  // No external drop: do not read or enumerate real files.
  window.addEventListener('dragover',e=>e.preventDefault());
  window.addEventListener('drop',e=>{e.preventDefault();tell('本试验不接收真实文件，不执行系统拖放。',true);});
  new ResizeObserver(()=>{if(width>=M.minWidth(doc)&&width>capacity())setWidth(capacity());}).observe($('stage'));
  loadTask(1);
})();
