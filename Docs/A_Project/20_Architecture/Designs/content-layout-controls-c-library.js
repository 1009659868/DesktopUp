/* Review-only template and interface palettes: C adapter: memory only; schema imports handled by the C runtime; no scripts imported. */
(() => {
  'use strict';
  const $=id=>document.getElementById(id), core=()=>window.bpCore;
  const templates=[
    {id:'night',name:'夜航',group:'builtin',bg:'#202e2a',card:'#2d433a',heading:true,favorite:true},
    {id:'box',name:'清简',group:'builtin',bg:'#2b3035',card:'#353c43',heading:false,favorite:true},
    {id:'ocean',name:'深海',group:'builtin',bg:'#1d2a3d',card:'#2b425c',heading:true},
    {id:'paper',name:'纸间',group:'builtin',bg:'#eee8dc',card:'#ded3bf',heading:true},
    {id:'violet',name:'暮紫',group:'builtin',bg:'#302739',card:'#473652',heading:true},
    {id:'sand',name:'沙丘',group:'builtin',bg:'#443c30',card:'#5b4b36',heading:true},
    {id:'fog',name:'薄雾',group:'builtin',bg:'#dee8e7',card:'#c6d8d4',heading:false},
    {id:'mono',name:'黑白',group:'builtin',bg:'#222325',card:'#3a3c40',heading:false},
    {id:'mine-moss',name:'我的苔原',group:'mine',bg:'#25372e',card:'#3e5142',heading:true,favorite:true},
    {id:'mine-plum',name:'周末收藏',group:'mine',bg:'#302a35',card:'#463b4e',heading:true},
    {id:'mine-cream',name:'书桌留白',group:'mine',bg:'#e8dfcf',card:'#cfc4b0',heading:false}
  ];
  window.bpTemplates=templates;
  const palettes=[
    {id:'ink',name:'墨色',bg:'#202326',accent:'#e9b871',group:'builtin'},
    {id:'light',name:'纸白',bg:'#f4f1e9',accent:'#755023',group:'builtin'},
    {id:'blue',name:'深海',bg:'#192738',accent:'#93b9e7',group:'builtin'},
    {id:'my-jade',name:'我的青岚',bg:'#20332e',accent:'#9dc7b7',group:'mine'}
  ];
  let palette=palettes[0],filter='all',serial=1,recent=['night','box','mine-moss'],lastPosterName='游戏收藏';
  const rgb=h=>[1,3,5].map(i=>parseInt(h.slice(i,i+2),16));
  const hex=v=>'#'+v.map(n=>Math.round(Math.max(0,Math.min(255,n))).toString(16).padStart(2,'0')).join('');
  const mix=(a,b,t)=>hex(rgb(a).map((v,i)=>v*(1-t)+rgb(b)[i]*t));
  const luminance=h=>rgb(h).map(v=>{v/=255;return v<=.04045?v/12.92:Math.pow((v+.055)/1.055,2.4);}).reduce((s,v,i)=>s+v*[.2126,.7152,.0722][i],0);
  const ink=h=>luminance(h)>.179?'#111416':'#f3f4f0';
  const contrast=(a,b)=>(Math.max(luminance(a),luminance(b))+.05)/(Math.min(luminance(a),luminance(b))+.05);
  const artMarkup='<span class="art-heading"></span><span class="art-cells"><i></i><i></i><i></i><i></i><i></i></span>';
  function paintArt(el,t){el.dataset.effect=t.effect||'minimal';el.style.setProperty('--art-bg',t.bg);el.style.setProperty('--art-card',t.card);el.style.setProperty('--art-text',ink(t.bg));el.innerHTML=artMarkup;el.querySelector('.art-heading').style.visibility=t.heading?'visible':'hidden';}
  function paletteVars(p){
    const light=luminance(p.bg)>.179,text=ink(p.bg),sidebar=mix(p.bg,'#000000',light?.055:.19),raised=mix(p.bg,light?'#000000':'#ffffff',light?.035:.045),muted=mix(p.bg,text,.73),accentInk=ink(p.accent);
    return {page:mix(p.bg,light?'#ffffff':'#000000',light?.38:.48),panel:p.bg,sidebar,raised,border:mix(p.bg,text,.2),text,muted,quiet:mix(p.bg,text,.68),accent:p.accent,accentink:accentInk,focus:mix(p.accent,text,.35),hover:mix(p.bg,text,.085),selected:mix(p.bg,p.accent,.12),'selected-border':mix(p.bg,p.accent,.6),track:mix(p.bg,text,.27),subtle:sidebar,scheme:light?'light':'dark'};
  }
  function paintPaletteSample(el,p){const v=paletteVars(p);for(const [k,val] of Object.entries({surface:v.panel,sidebar:v.sidebar,text:v.text,border:v.border,accent:v.accent,ink:v.accentink}))el.style.setProperty('--sw-'+k,val);}
  function applyPalette(p){palette=p;const v=paletteVars(p);for(const [k,val] of Object.entries(v))if(k!=='scheme')document.documentElement.style.setProperty('--'+k,val);document.documentElement.style.colorScheme=v.scheme;$('palette-open').setAttribute('aria-label','主题，当前'+p.name);window.bpActivePalette=structuredClone(p);}
  window.bpPaintPoster=t=>{
    const el=$('poster'),text=ink(t.bg),itemText=ink(t.card);
    for(const [key,value] of Object.entries({poster:t.bg,card:t.card,'poster-text':text,'poster-muted':mix(t.bg,text,.72),'poster-line':mix(t.bg,text,.14),'item-text':itemText,'item-muted':mix(t.card,itemText,.78)}))el.style.setProperty('--'+key,value);
  };
  function templateById(id){return templates.find(t=>t.id===id)||templates[0];}
  window.bpRender=(state,poster)=>{
    lastPosterName=poster.name;const t=templateById(state.theme);
    $('current-template-name').textContent=t.name;$('current-template-meta').textContent=(t.group==='builtin'?'内置':'我的')+' · '+(t.family||(t.heading?'海报式':'简洁式'));paintArt($('current-mini'),t);
    $('library-count').textContent=templates.length+' 个模板';
    $('template-shortcuts').replaceChildren(...recent.slice(0,3).map(id=>{const a=templateById(id),b=document.createElement('button');b.type='button';b.textContent=a.name;b.setAttribute('aria-pressed',String(a.id===state.theme));b.addEventListener('click',()=>chooseTemplate(a.id));return b;}));
  };
  function chooseTemplate(id){core().hideTip();core().state().theme=id;recent=[id,...recent.filter(x=>x!==id)].slice(0,3);core().render();if($('library-dialog').open)$('library-dialog').close();core().notify('已切换演示外观 · '+lastPosterName);}
  function openDialog(id){core().hideTip();$(id).showModal();}
  function libraryHome(){ $('template-form').hidden=true;$('library-browser').hidden=false;renderLibrary();}
  function renderLibrary(){
    const query=$('template-search').value.trim().toLocaleLowerCase(),results=templates.filter(t=>(filter==='all'||filter==='favorite'&&t.favorite||t.group===filter)&&t.name.toLocaleLowerCase().includes(query));
    $('library-target').textContent='当前海报 · '+lastPosterName;document.querySelectorAll('[data-filter]').forEach(b=>b.setAttribute('aria-pressed',String(b.dataset.filter===filter)));
    $('library-grid').replaceChildren(...results.map(t=>{
      const wrap=document.createElement('div');wrap.className='template-card';
      const pick=document.createElement('button');pick.type='button';pick.className='template-pick';pick.dataset.template=t.id;pick.setAttribute('aria-label','使用模板 '+t.name);pick.setAttribute('aria-pressed',String(t.id===core().state().theme));
      const art=document.createElement('span');art.className='template-art';art.setAttribute('aria-hidden','true');paintArt(art,t);
      const info=document.createElement('span');info.className='template-card-info';const name=document.createElement('strong');name.textContent=t.name;const meta=document.createElement('small');meta.textContent=(t.group==='builtin'?'内置':'我的')+' · '+(t.family||(t.heading?'海报式':'简洁式'));info.append(name,meta);pick.append(art,info);pick.addEventListener('click',()=>chooseTemplate(t.id));wrap.append(pick);
      if(t.id===core().state().theme){const tag=document.createElement('span');tag.className='selected-tag';tag.textContent='使用中';wrap.append(tag);}
      const star=document.createElement('button');star.type='button';star.className='favorite-template';star.dataset.favorite=t.id;star.textContent=t.favorite?'★':'☆';star.setAttribute('aria-label',(t.favorite?'取消收藏 ':'收藏模板 ')+t.name);star.setAttribute('aria-pressed',String(!!t.favorite));star.addEventListener('click',()=>{t.favorite=!t.favorite;renderLibrary();const next=document.querySelector('[data-favorite="'+t.id+'"]');(next||document.querySelector('[data-filter="favorite"]')).focus();});wrap.append(star);return wrap;
    }));
    $('library-empty').hidden=results.length!==0;$('library-result').textContent=results.length+' 个模板 · 本页样例';
  }
  $('library-open').addEventListener('click',()=>{libraryHome();openDialog('library-dialog');});
  document.querySelectorAll('[data-filter]').forEach(b=>b.addEventListener('click',()=>{filter=b.dataset.filter;renderLibrary();}));
  $('template-search').addEventListener('input',renderLibrary);$('clear-search').addEventListener('click',()=>{filter='all';$('template-search').value='';renderLibrary();$('template-search').focus();});
  window.CLibrary={home:libraryHome,render:renderLibrary,paintArt,showMine(){filter="mine";$("template-search").value="";libraryHome();}};
  function paletteHome(){ $('palette-form').hidden=true;$('palette-browser').hidden=false;renderPalettes();}
  const sampleMarkup='<span class="palette-sample-sidebar"></span><span class="palette-sample-main"><span class="sample-line"></span><span class="sample-line short"></span><span class="sample-action"></span></span>';
  function renderPalettes(){
    $('palette-grid').replaceChildren(...palettes.filter(p=>p.group==='builtin').map(p=>{const b=document.createElement('button');b.type='button';b.className='palette-choice';b.dataset.palette=p.id;b.setAttribute('aria-pressed',String(p.id===palette.id));const sample=document.createElement('span');sample.className='palette-preview';sample.setAttribute('aria-hidden','true');sample.innerHTML=sampleMarkup;paintPaletteSample(sample,p);const name=document.createElement('span');name.textContent=p.name;b.append(sample,name);b.addEventListener('click',()=>{applyPalette(p);renderPalettes();document.querySelector('[data-palette="'+p.id+'"]').focus();});return b;}));
    $('custom-palettes').replaceChildren(...palettes.filter(p=>p.group==='mine').map(p=>{const b=document.createElement('button');b.type='button';b.className='custom-palette';b.dataset.palette=p.id;b.setAttribute('aria-pressed',String(p.id===palette.id));paintPaletteSample(b,p);const dot=document.createElement('span');dot.className='palette-dot';dot.setAttribute('aria-hidden','true');b.append(dot,document.createTextNode(p.name));b.addEventListener('click',()=>{applyPalette(p);renderPalettes();document.querySelector('[data-palette="'+p.id+'"]').focus();});return b;}));
  }
  $('palette-open').addEventListener('click',()=>{paletteHome();openDialog('palette-dialog');});
  function draftPalette(){return {name:$('palette-name').value.trim(),bg:$('palette-bg').value,accent:$('palette-accent').value};}
  $('palette-new').addEventListener('click',()=>{$('palette-name').value='我的主题';$('palette-bg').value=palette.bg;$('palette-accent').value=palette.accent;$('palette-error').hidden=true;$('palette-browser').hidden=true;$('palette-form').hidden=false;paintPaletteSample($('palette-draft'),draftPalette());$('palette-name').focus();});
  ['palette-bg','palette-accent'].forEach(id=>$(id).addEventListener('input',()=>{const p=draftPalette();paintPaletteSample($('palette-draft'),p);const low=contrast(p.bg,p.accent)<4.5;$('palette-error').hidden=!low;$('palette-error').textContent=low?'强调色与底色过于接近，请调整后再保存。':'';}));
  ['palette-back','palette-cancel'].forEach(id=>$(id).addEventListener('click',()=>{paletteHome();$('palette-new').focus();}));
  $('palette-form').addEventListener('submit',e=>{e.preventDefault();const p=draftPalette(),error=$('palette-error');if(contrast(p.bg,p.accent)<4.5){error.textContent='强调色与底色过于接近，请调整后再保存。';error.hidden=false;$('palette-accent').focus();return;}if(!p.name||palettes.some(x=>x.name.toLocaleLowerCase()===p.name.toLocaleLowerCase())){error.textContent=!p.name?'请填写主题名称。':'已有同名主题，请换个名称。';error.hidden=false;$('palette-name').focus();return;}const saved={...p,id:'palette-'+serial++,group:'mine'};palettes.push(saved);applyPalette(saved);paletteHome();core().notify('已保存主题样例 · 刷新后复位');});
  document.querySelectorAll('[data-close]').forEach(b=>b.addEventListener('click',()=>$(b.dataset.close).close()));
  for(const id of ['library-dialog','palette-dialog'])$(id).addEventListener('click',e=>{const r=e.currentTarget.getBoundingClientRect();if(e.target===e.currentTarget&&(e.clientX<r.left||e.clientX>r.right||e.clientY<r.top||e.clientY>r.bottom))e.currentTarget.close();});
  window.bpChooseTemplate=chooseTemplate;
  window.bpCatalogSnapshot=()=>({templates:structuredClone(templates),palettes:structuredClone(palettes),palette:palette.id});
  applyPalette(palettes.find(p=>p.id===document.body.dataset.startTheme)||palette);
})();
