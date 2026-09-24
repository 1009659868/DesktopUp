/* UI C declarative template model. No imported HTML/CSS/JS, URLs, file writes or persistence. */
(() => {
  'use strict';
  const registry=new Map(),instances=new Map(),clone=structuredClone;
  const fail=message=>{throw new Error(message);};
  const plain=o=>o!==null&&typeof o==='object'&&!Array.isArray(o)&&Object.getPrototypeOf(o)===Object.prototype;
  const text=(v,max=60)=>typeof v==='string'&&v.trim().length>0&&v.length<=max&&!/[\u0000-\u001f]/.test(v);
  const color=v=>typeof v==='string'&&/^#[0-9a-f]{6}$/i.test(v);
  const safeKey=v=>typeof v==='string'&&/^[a-z][a-zA-Z0-9._-]{0,47}$/.test(v)&&!['constructor','prototype','__proto__'].includes(v);
  const number=(min,max,integer=false)=>({type:'number',min,max,integer});
  const common={'item.color':{type:'color'}};
  const surfaces={'surface.background':{type:'color'},'surface.opacity':number(0,100)};
  const capabilities={
    'minimal@1':{...common,...surfaces},
    'lamp@1':{...common,...surfaces,'lamp.color':{type:'color'},'lamp.softness':number(0,100),'lamp.intensity':number(0,100)},
    'rain@1':{...common,'rain.count':number(0,64,true),'rain.color':{type:'color'},'cloud.color':{type:'color'},'cloud.wind':number(0,100),'rain.speed':number(.5,2),'rain.lightning':{type:'boolean'}}
  };
  function keys(o,allowed,where){
    if(!plain(o))fail(where+' 必须是对象');
    for(const k of Object.keys(o))if(!allowed.includes(k))fail(where+' 含不支持的字段：'+k);
  }
  function checkValue(v,cap){
    if(cap.type==='color')return color(v);
    if(cap.type==='boolean')return typeof v==='boolean';
    return typeof v==='number'&&Number.isFinite(v)&&v>=cap.min&&v<=cap.max&&(!cap.integer||Number.isInteger(v));
  }
  const onStep=(n,min,step)=>Math.abs((n-min)/step-Math.round((n-min)/step))<1e-7;
  function validProperty(p,value){
    if(p.type==='color')return color(value);
    if(p.type==='boolean')return typeof value==='boolean';
    if(p.type==='select')return p.options.some(o=>o.value===value);
    return typeof value==='number'&&Number.isFinite(value)&&value>=p.min&&value<=p.max&&onStep(value,p.min,p.step);
  }
  function validate(input){
    keys(input,['format','schemaVersion','id','version','name','family','renderer','base','properties'],'模板');
    if(input.format!=='desktopup.template.demo'||input.schemaVersion!==1)fail('不是受支持的模板格式 / schemaVersion');
    if(!safeKey(input.id)||!Number.isInteger(input.version)||input.version<1||input.version>999)fail('模板 ID 或版本无效');
    if(!text(input.name,40)||!text(input.family,24))fail('名称 / 分类无效或过长');
    if(!Object.hasOwn(capabilities,input.renderer))fail('未知渲染能力：'+String(input.renderer));
    keys(input.base,['background','card','heading'],'base');
    if(!color(input.base.background)||!color(input.base.card)||typeof input.base.heading!=='boolean')fail('base 颜色须为 #RRGGBB；heading 须为布尔值');
    if(!Array.isArray(input.properties)||input.properties.length>20)fail('properties 必须是数组，最多 20 项');
    const usedKeys=new Set(),usedBindings=new Set(),caps=capabilities[input.renderer];
    for(const p of input.properties){
      keys(p,['key','label','group','type','default','bind','description','unit','min','max','step','options'],'参数');
      if(!safeKey(p.key)||usedKeys.has(p.key))fail('参数 key 无效或重复');
      if(!text(p.label,30)||(p.group!==undefined&&!text(p.group,20))||(p.description!==undefined&&!text(p.description,160))||(p.unit!==undefined&&(!text(p.unit,8))))fail('参数标签 / 提示无效');
      if(typeof p.bind!=='string'||!Object.hasOwn(caps,p.bind)||usedBindings.has(p.bind))fail('不支持或重复的参数绑定：'+String(p.bind));
      const cap=caps[p.bind];
      if(!['color','boolean','range','select'].includes(p.type))fail('不支持的控件：'+String(p.type));
      if(p.type==='range'){
        if(cap.type!=='number'||![p.min,p.max,p.step].every(n=>typeof n==='number'&&Number.isFinite(n))||p.min>=p.max||p.min<cap.min||p.max>cap.max||p.step<=0||p.step>p.max-p.min||(p.max-p.min)/p.step>1000||!onStep(p.max,p.min,p.step)||(cap.integer&&(!Number.isInteger(p.step)||!Number.isInteger(p.min))))fail(p.label+' 的范围 / 步长越界');
        if(p.options!==undefined)fail('range 不能声明 options');
      }else if(p.type==='select'){
        if(!Array.isArray(p.options)||p.options.length<2||p.options.length>8)fail('select 需要 2–8 个选项');
        const seen=new Set();
        for(const o of p.options){
          keys(o,['label','value'],'选项');
          if(!text(o.label,20)||!checkValue(o.value,cap)||seen.has(o.value))fail('选项值无效或重复');
          seen.add(o.value);
        }
        if(['min','max','step'].some(k=>p[k]!==undefined))fail('select 不使用 range 范围字段');
      }else {
        if(p.type!==cap.type)fail('控件类型与能力类型不匹配');
        if(['min','max','step','options'].some(k=>p[k]!==undefined))fail('颜色 / 开关不能声明数值范围');
      }
      if(!checkValue(p.default,cap)||!validProperty(p,p.default))fail(p.label+' 的默认值无效');
      usedKeys.add(p.key);usedBindings.add(p.bind);
    }
    return clone(input);
  }
  function prepare(input){
    const m=validate(input);
    if(registry.has(m.id))fail('模板 ID 已存在；本稿不替换已有版本，请使用新的 ID');
    if(registry.size>=64)fail('本页最多装入 64 个模板');
    return m;
  }
  const frozen=o=>{Object.values(o).forEach(v=>{if(v&&typeof v==='object')frozen(v);});return Object.freeze(o);};
  function add(input,group='mine'){
    const m=prepare(input);
    registry.set(m.id,frozen(m));
    const existing=window.bpTemplates.find(t=>t.id===m.id);
    const projection={id:m.id,name:m.name,group,bg:m.base.background,card:m.base.card,heading:m.base.heading,effect:m.renderer.split('@')[0],family:m.family};
    if(existing)Object.assign(existing,projection);else window.bpTemplates.push(projection);
    return m.id;
  }
  function get(id){const m=registry.get(id);if(!m)fail('模板不存在：'+id);return clone(m);}
  const defaults=m=>Object.fromEntries(m.properties.map(p=>[p.key,p.default]));
  function state(target,id){
    if(!['games','work','ideas'].includes(target))fail('未知演示海报');
    const key=target+'|'+id;
    if(!instances.has(key))instances.set(key,defaults(get(id)));
    return instances.get(key);
  }
  function set(target,id,key,value){
    const p=get(id).properties.find(p=>p.key===key);
    if(!p||!validProperty(p,value))fail('参数值不符合模板声明');
    state(target,id)[key]=value;
  }
  function values(target,id){return clone(state(target,id));}
  function resolve(target,id,override){
    const m=get(id),v=override||state(target,id);
    return Object.fromEntries(m.properties.map(p=>[p.bind,v[p.key]??p.default]));
  }
  function reset(target,id){instances.set(target+'|'+id,defaults(get(id)));}
  function parse(source){
    if(typeof source!=='string'||new TextEncoder().encode(source).length>65536)fail('模板 JSON 不得超过 64 KiB');
    let raw;try{raw=JSON.parse(source);}catch{fail('JSON 格式错误，请检查逗号、引号和括号');}
    return prepare(raw);
  }
  const c=(key,label,bind,value,group='材质')=>({key,label,bind,type:'color',default:value,group});
  const r=(key,label,bind,value,min=0,max=100,step=1,unit='%',group='光影')=>({key,label,bind,type:'range',default:value,min,max,step,unit,group});
  const manifest=(t,renderer,properties)=>({format:'desktopup.template.demo',schemaVersion:1,id:t.id,version:1,name:t.name,family:t.family||'极简主义',renderer,base:{background:t.bg,card:t.card,heading:t.heading},properties});
  for(const t of [...window.bpTemplates]){
    add(manifest(t,'minimal@1',[c('background','背景颜色','surface.background',t.bg),c('card','卡片颜色','item.color',t.card),r('opacity','背景不透明度','surface.opacity',100,0,100,1,'%','材质')]),t.group);
  }
  add(manifest({id:'lamp',name:'深夜书桌',family:'交互光影',bg:'#17221e',card:'#354237',heading:true},'lamp@1',[
    {...c('lightColor','灯光颜色','lamp.color','#ffdc99','光影'),description:'只改变这盏灯的光色，不改变控制中心主题。'},
    r('softness','柔和度','lamp.softness',60),r('intensity','灯光强度','lamp.intensity',70),
    c('background','桌面底色','surface.background','#17221e'),c('card','卡片颜色','item.color','#354237'),
    {...r('opacity','背景不透明度','surface.opacity',100,0,100,1,'%','材质'),description:'仅影响海报背景；文字与 Item 不会一起变透明。'}
  ]),'builtin');
  add(manifest({id:'rain',name:'雨夜窗边',family:'天气场景',bg:'#152337',card:'#253d53',heading:true},'rain@1',[
    r('amount','雨量','rain.count',28,0,64,1,'滴','天气'),c('rainColor','雨的颜色','rain.color','#93c9e9','天气'),
    c('cloudColor','云的颜色','cloud.color','#3f576d','天气'),r('speed','落雨速度','rain.speed',1,.5,2,.1,'×','天气'),
    {...r('wind','风感','cloud.wind',35,0,100,1,'%','天气'),description:'轻缓移动云层与雨丝，Item 不移动；设为 0 时云层静止。'},
    {key:'lightning',label:'微光闪电',group:'天气',type:'boolean',default:true,bind:'rain.lightning',description:'低频局部微光；关闭动效或减少动效时不闪烁。'},
    c('card','卡片颜色','item.color','#253d53')
  ]),'builtin');
  const example={
    format:'desktopup.template.demo',schemaVersion:1,id:'demo.mint-rain',version:1,name:'薄荷细雨',family:'我的天气',renderer:'rain@1',
    base:{background:'#193b33',card:'#2f5b50',heading:true},
    properties:[
      c('mintCloud','云朵染色','cloud.color','#6f9f91','我的天气'),
      r('drizzle','细雨数量','rain.count',18,0,48,1,'滴','我的天气'),
      c('streak','雨丝颜色','rain.color','#9de7c5','我的天气'),
      {key:'mood',label:'雨的节奏',group:'我的天气',type:'select',default:.7,bind:'rain.speed',options:[{label:'慢慢落',value:.7},{label:'轻快些',value:1.4}],description:'这个下拉框也由 JSON 生成，UI 没有为它写专用分支。'},
      {key:'spark',label:'云间微光',group:'我的天气',type:'boolean',default:false,bind:'rain.lightning'},
      c('cards','收藏底色','item.color','#2f5b50')
    ]
  };
  window.CModel={get,values,set,resolve,reset,validate,prepare,parse,add,defaults,example:()=>clone(example),count:()=>registry.size};
})();
