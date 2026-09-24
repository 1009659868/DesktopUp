/* Alternate UI presentation only: browse sample pages without applying a template. */
(() => {
  'use strict';
  const $=id=>document.getElementById(id),baseRender=window.bpRender;
  let start=0,lastId=null,lastWidth=0;
  const preferred=['night','lamp','rain','paper','violet','box','ocean','sand','fog','mono','mine-moss','mine-plum','mine-cream'];
  function ordered(){return [...window.bpTemplates].sort((a,b)=>{const ia=preferred.indexOf(a.id),ib=preferred.indexOf(b.id);return(ia<0?1000:ia)-(ib<0?1000:ib);});}
  function pageSize(){return ($('sample-deck').clientWidth||lastWidth||340)<310?2:3;}
  function paint(){
    const all=ordered(),n=pageSize(),active=window.bpCore.state().theme,focused=document.activeElement?.dataset.sample;
    if(lastId!==active){const at=all.findIndex(x=>x.id===active);start=Math.floor(Math.max(0,at)/n)*n;lastId=active;}
    start=Math.max(0,Math.min(start,Math.floor((all.length-1)/n)*n));
    $('sample-deck').style.gridTemplateColumns='repeat('+n+',minmax(0,1fr))';
    $('sample-deck').replaceChildren(...all.slice(start,start+n).map((t,index)=>{
      const b=document.createElement('button');b.type='button';b.className='sample-card';b.dataset.sample=t.id;b.setAttribute('aria-label','使用模板 '+t.name);b.setAttribute('aria-pressed',String(active===t.id));
      const picture=document.createElement('span');picture.className='sample-picture';picture.setAttribute('aria-hidden','true');picture.style.setProperty('--art-bg',t.bg);picture.style.setProperty('--art-card',t.card);
      picture.dataset.effect=t.effect||'minimal';
      const r=[1,3,5].map(i=>parseInt(t.bg.slice(i,i+2),16)/255).map(v=>v<=.04045?v/12.92:Math.pow((v+.055)/1.055,2.4)),l=r[0]*.2126+r[1]*.7152+r[2]*.0722;picture.style.setProperty('--art-text',l>.179?'#111416':'#f3f4f0');
      const word=document.createElement('span');word.className='sample-word';word.textContent=t.effect==='lamp'?'AFTER\nDARK.':t.effect==='rain'?'RAIN CLOUD':({night:'NIGHT\nPLAY.',paper:'LESS,\nBUT GOOD.',violet:'SLOW\nHOURS.',ocean:'DEEP\nBLUE.',box:'MAKE\nROOM.',sand:'WARM\nDAYS.'})[t.id]||'MY\nCOLLECTION.';
      const shapes=document.createElement('span');shapes.className='sample-shapes';for(let i=0;i<3;i++)shapes.append(document.createElement('i'));picture.append(word,shapes);
      const caption=document.createElement('span');caption.className='sample-card-caption';const name=document.createElement('strong');name.textContent=t.name;const number=document.createElement('small');number.textContent=String(start+index+1).padStart(2,'0');caption.append(name,number);
      const state=document.createElement('span');state.className='sample-card-state';state.textContent=active===t.id?'✓ 使用中':t.family||(t.group==='mine'?'我的模板':'内置模板');
      b.append(picture,caption,state);b.addEventListener('click',()=>window.bpChooseTemplate(t.id));return b;
    }));
    $('sample-range').textContent=(start+1)+'–'+Math.min(start+n,all.length)+' / '+all.length;$('sample-prev').disabled=start===0;$('sample-next').disabled=start+n>=all.length;
    window.studioSnapshot={start,pageSize:n,total:all.length,active};
    $('folder-current').textContent=all.find(t=>t.id===active)?.name||'';
    if(focused)document.querySelector('[data-sample="'+focused+'"]')?.focus({preventScroll:true});
  }
  window.bpRender=(state,poster)=>{baseRender(state,poster);paint();};
  $('sample-prev').addEventListener('click',()=>{start-=pageSize();paint();});
  $('sample-next').addEventListener('click',()=>{start+=pageSize();paint();});
  $('folder-toggle').addEventListener('click',()=>{const open=$('folder-body').hidden;$('folder-body').hidden=!open;$('folder-toggle').setAttribute('aria-expanded',String(open));$('folder-action').textContent=open?'收起样片 ⌃':'展开样片 ⌄';window.bpCore.hideTip();if(open)paint();});
  new ResizeObserver(()=>{const w=$('sample-deck').clientWidth;if(w>0&&Math.abs(w-lastWidth)>1){lastWidth=w;paint();}}).observe($('sample-deck'));
  paint();
})();
