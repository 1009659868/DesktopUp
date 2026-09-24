/* UI C: one schema form factory for live parameters and template-copy drafts. */
(() => {
  'use strict';
  const $=id=>document.getElementById(id),model=window.CModel,core=window.bpCore;
  const active=()=>window.uiReviewBPlusSnapshot.active;
  const identity=()=>({target:active(),id:core.state().theme});
  const current=()=>model.get(core.state().theme);
  let formKey='',draft=null,pending=null,fileRead=0,copySerial=1;
  function element(tag,cls,value){const e=document.createElement(tag);if(cls)e.className=cls;if(value!==undefined)e.textContent=value;return e;}
  function form(m,values,mount,prefix,onChange){
    mount.replaceChildren();
    const groups=new Map();
    for(const p of m.properties){
      const group=p.group||'参数';
      if(!groups.has(group)){const fieldset=element('fieldset','schema-group');fieldset.append(element('legend','',group));mount.append(fieldset);groups.set(group,fieldset);}
      const wrap=element('div','schema-field schema-'+p.type),label=element('label','schema-label',p.label),id=prefix+'-'+p.key;
      label.htmlFor=id;
      const input=document.createElement(p.type==='select'?'select':'input');input.id=id;input.dataset.param=p.key;
      if(p.description){wrap.dataset.tip=p.description;input.title=p.description;}
      const output=element('output','schema-value');output.htmlFor=id;
      if(p.type==='range'){
        input.type='range';input.min=p.min;input.max=p.max;input.step=p.step;input.value=values[p.key];
        output.textContent=values[p.key]+(p.unit||'');
        const heading=element('div','schema-fieldhead');heading.append(label,output);wrap.append(heading,input);
      }else if(p.type==='color'){
        input.type='color';input.value=values[p.key];output.textContent=values[p.key].toUpperCase();
        const swatch=element('div','schema-swatch');swatch.append(input,output);wrap.append(label,swatch);
      }else if(p.type==='boolean'){
        input.type='checkbox';input.checked=values[p.key];input.setAttribute('role','switch');
        const shell=element('label','switch');shell.append(input,element('span','switch-track'));wrap.append(label,shell);
      }else {
        for(const [i,o]of p.options.entries()){const option=element('option','',o.label);option.value=String(i);option.selected=o.value===values[p.key];input.append(option);}
        wrap.append(label,input);
      }
      input.addEventListener('input',()=>{
        const value=p.type==='range'?Number(input.value):p.type==='boolean'?input.checked:p.type==='select'?p.options[Number(input.value)].value:input.value;
        onChange(p.key,value);
        if(p.type==='range')output.textContent=value+(p.unit||'');else if(p.type==='color')output.textContent=value.toUpperCase();
      });
      groups.get(group).append(wrap);
    }
    if(!m.properties.length)mount.append(element('p','muted-note','这张模板没有额外参数。'));
  }
  function paintPanel(force=false){
    const {target,id}=identity(),key=target+'|'+id;
    if(!force&&formKey===key)return;
    formKey=key;const m=model.get(id);
    $('template-settings-count').textContent=m.properties.length+' 项专属设置';
    $('property-title').textContent=m.name;$('property-title').title=m.id+' · v'+m.version;
    form(m,model.values(target,id),$('template-properties'),'live',(name,value)=>{
      if(active()!==target||core.state().theme!==id)return;
      model.set(target,id,name,value);window.CScenes.refresh();
      $('parameter-state').textContent='已预览 · 仅当前海报';
    });
    $('parameter-state').textContent='实时预览 · 仅当前海报';
  }
  const base=window.bpRender;
  window.bpRender=(state,poster)=>{base(state,poster);paintPanel();};
  $('parameters-reset').addEventListener('click',()=>{const {target,id}=identity();model.reset(target,id);window.CScenes.refresh();paintPanel(true);core.notify('已恢复这张海报的模板参数');});
  function draftArt(){
    const slots=model.resolve(draft.target,draft.source.id,draft.values);
    window.CLibrary.paintArt($('draft-template-art'),{effect:draft.source.renderer.split('@')[0],bg:slots['surface.background']??draft.source.base.background,card:slots['item.color']??draft.source.base.card,heading:draft.source.base.heading});
  }
  function showDraft(){
    const {target,id}=identity(),source=model.get(id);
    draft={target,source,values:model.values(target,id)};
    $('template-name').value=(source.name+' · 副本').slice(0,40);
    $('template-error').hidden=true;$('library-browser').hidden=true;$('template-form').hidden=false;
    form(source,draft.values,$('draft-properties'),'draft',(key,value)=>{draft.values[key]=value;draftArt();});
    draftArt();
    core.hideTip();if(!$('library-dialog').open)$('library-dialog').showModal();$('template-name').focus();
  }
  $('template-save-open').addEventListener('click',showDraft);$('library-save').addEventListener('click',showDraft);
  ['template-back','template-cancel'].forEach(id=>$(id).addEventListener('click',()=>{draft=null;window.CLibrary.home();$('library-save').focus();}));
  $('library-dialog').addEventListener('close',()=>{draft=null;});
  $('template-form').addEventListener('submit',e=>{
    e.preventDefault();if(!draft)return;
    const name=$('template-name').value.trim(),error=$('template-error');
    try{
      if(!name)throw Error('请输入模板名称');
      if(window.bpTemplates.some(t=>t.name.toLowerCase()===name.toLowerCase()))throw Error('模板名称已存在');
      const m=structuredClone(draft.source);m.id='copy.user-'+copySerial++;m.version=1;m.name=name;
      for(const p of m.properties)p.default=draft.values[p.key];
      // Keep the library thumbnail's base material in sync; scene properties remain in the schema.
      const slots=model.resolve(draft.target,draft.source.id,draft.values);
      m.base.background=slots['surface.background']??m.base.background;m.base.card=slots['item.color']??m.base.card;
      const id=model.add(m);draft=null;window.CLibrary.showMine();core.render();
      $('library-result').textContent='已另存默认参数；点选新样片后使用';
      document.querySelector('[data-template="'+id+'"]')?.focus();core.notify('已存入“我的” · 本页内存');
    }catch(e){error.textContent=e.message;error.hidden=false;}
  });
  function report(message,kind='idle'){$('import-result').textContent=message;$('import-result').dataset.kind=kind;}
  function invalidate(){pending=null;$('install-template').disabled=true;}
  function openImport(inspect=false){
    invalidate();fileRead++;core.hideTip();
    $('template-file').value='';$('template-json').value=inspect?JSON.stringify(current(),null,2):'';
    $('import-target').textContent='用于 '+$('target-name').textContent+' · 刷新复位';
    report(inspect?'这是模板定义，参数默认值不随本次微调改变。另存为新 ID 后可装入。':'先填入示例或选择 JSON，再检查配方。');
    $('import-dialog').showModal();
  }
  $('template-import-open').addEventListener('click',()=>openImport());
  $('show-definition').addEventListener('click',()=>openImport(true));
  $('load-example').addEventListener('click',()=>{fileRead++;invalidate();$('template-json').value=JSON.stringify(model.example(),null,2);report('示例带有新的参数名和「雨的节奏」下拉框。点击检查。');});
  $('inspect-current').addEventListener('click',()=>{fileRead++;invalidate();$('template-json').value=JSON.stringify(current(),null,2);report('当前 ID 已在模板册中。修改 ID 才能作为新样片装入。');});
  $('template-json').addEventListener('input',()=>{fileRead++;invalidate();report('配方已修改，请重新检查。');});
  $('import-dialog').addEventListener('close',()=>{fileRead++;invalidate();});
  $('template-file').addEventListener('change',async e=>{
    const file=e.target.files[0],ticket=++fileRead;invalidate();if(!file)return;
    try{
      if(file.size>65536)throw Error('文件超过 64 KiB，请使用较小的模板配方');
      const source=await file.text();
      if(ticket!==fileRead||!$('import-dialog').open)return;
      $('template-json').value=source;report('已读取 '+file.name+'，尚未装入。请检查配方。');
    }catch(e){if(ticket===fileRead)report(e.message,'error');}
  });
  $('validate-template').addEventListener('click',()=>{
    invalidate();
    try{
      const source=$('template-json').value,m=model.parse(source);
      pending={m,source,target:active(),theme:core.state().theme,count:model.count()};
      $('install-template').disabled=false;
      report('检查通过 · '+m.name+' · '+m.properties.length+' 个专属参数 · '+m.renderer+'。现有海报未改变。','ok');
    }catch(e){report(e.message,'error');}
  });
  $('install-template').addEventListener('click',()=>{
    if(!pending)return;
    try{
      if(pending.source!==$('template-json').value||pending.target!==active()||pending.theme!==core.state().theme||pending.count!==model.count())throw Error('目标或模板库已变化，请重新检查');
      const m=model.parse(pending.source),id=model.add(m);
      $('import-dialog').close();window.bpChooseTemplate(id);
      $('parameter-state').textContent='刚刚装入 · 无需刷新';
      core.notify('新模板已装入并使用 · '+m.name);
    }catch(e){invalidate();report(e.message,'error');}
  });
  // Leave the sample folder compact so the template-specific controls remain the focus.
  if($('folder-toggle').getAttribute('aria-expanded')==='true')$('folder-toggle').click();
  paintPanel();
})();
