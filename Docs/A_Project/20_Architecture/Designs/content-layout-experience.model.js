/* Experience review only. DEMO geometry; not the production solver or persistence contract. */
(function (root, factory) {
  const api = factory();
  if (typeof module === 'object' && module.exports) module.exports = api;
  else root.ExperienceModel = api;
})(typeof globalThis !== 'undefined' ? globalThis : this, function () {
  'use strict';
  const clone = value => JSON.parse(JSON.stringify(value));
  const items = [
    {id:'A',name:'项目计划',mark:'PLAN',w:4,h:2},
    {id:'B',name:'参考资料',mark:'REF',w:2,h:2},
    {id:'C',name:'工具箱',mark:'TOOL',w:2,h:1},
    {id:'D',name:'灵感集',mark:'IDEA',w:2,h:2},
    {id:'E',name:'归档',mark:'BOX',w:2,h:1},
    {id:'F',name:'速记',mark:'NOTE',w:2,h:1},
    {id:'G',name:'素材',mark:'ART',w:2,h:2},
    {id:'H',name:'备用',mark:'MORE',w:2,h:1}
  ];
  const byId = id => items.find(i=>i.id===id);
  const reading = (a,b) => a.y-b.y || a.x-b.x || a.id.localeCompare(b.id);
  const overlap = (a,b) => a.x<b.x+b.w && b.x<a.x+a.w && a.y<b.y+b.h && b.y<a.y+a.h;
  function seed(mode='manual') {
    const d={mode:'manual',order:items.map(i=>i.id),pins:{},sizes:{},manual:[],editedSizes:{}};
    const positions=[[0,0],[6,0],[0,4],[4,6],[0,9],[6,10],[0,12],[6,15]];
    d.manual=items.map((i,n)=>({id:i.id,x:positions[n][0],y:positions[n][1],w:i.w,h:i.h}));
    if(mode==='auto') {d.mode='auto';d.manual=[];d.pins={C:1};d.sizes={B:[3,2]};}
    return d;
  }
  function effectiveOrder(d) {
    const result=new Array(items.length);
    for(const [id,p] of Object.entries(d.pins)) {
      if(!byId(id)||!Number.isInteger(p)||p<1||p>items.length||result[p-1])throw Error('Invalid pin');
      result[p-1]=id;
    }
    const pool=d.order.filter(id=>!Object.hasOwn(d.pins,id));
    for(let n=0;n<result.length;n++)if(!result[n])result[n]=pool.shift();
    return result;
  }
  function spans(d) {
    return d.mode==='manual'?d.manual:items.map(i=>({id:i.id,w:d.sizes[i.id]?.[0]||i.w,h:d.sizes[i.id]?.[1]||i.h}));
  }
  const minWidth = d => Math.max(...spans(d).map(i=>i.w));
  function layout(d,width) {
    if(width<minWidth(d))return {ok:false,reason:'可用宽度小于最大内容，恢复方案仍待评审',items:[]};
    const result=[],single=width===minWidth(d);
    if(d.mode==='auto') {
      let x=0,y=0,height=0;
      for(const id of effectiveOrder(d)) {
        const i=spans(d).find(v=>v.id===id);
        if(result.length&&(single||x+i.w>width)){x=0;y+=height;height=0;}
        result.push({...i,x,y});x+=i.w;height=Math.max(height,i.h);
      }
    } else {
      let previous;
      for(const p of [...d.manual].sort(reading)) {
        const r={...p,x:single?0:Math.min(p.x,width-p.w)};
        if(previous) {
          r.y=Math.max(r.y,single?previous.y+previous.h:previous.y);
          if(!single&&r.y===previous.y&&r.x<previous.x)r.y=previous.y+previous.h;
        }
        let blocks;
        while((blocks=result.filter(b=>overlap(b,r))).length)r.y=Math.max(...blocks.map(b=>b.y+b.h));
        result.push(r);previous=r;
      }
    }
    return {ok:true,items:result,extent:Math.max(...result.map(r=>r.y+r.h)),adapted:d.mode==='manual'&&!sameGeometry(result,d.manual)};
  }
  function sameGeometry(a,b) {
    return a.length===b.length&&a.every(r=>{const s=b.find(i=>i.id===r.id);return s&&['x','y','w','h'].every(k=>r[k]===s[k]);});
  }
  function failure(reason) {return {ok:false,reason};}
  function proposal(before,after,width,extra={}) {
    const view=layout(after,width);
    if(!view.ok)return failure(view.reason);
    return {ok:true,changed:JSON.stringify(before)!==JSON.stringify(after),doc:after,view,...extra};
  }
  function move(d,width,id,target) {
    const current=layout(d,width);if(!current.ok)return failure(current.reason);
    const next=clone(d),source=current.items.find(i=>i.id===id);
    if(!source)return failure('目标不存在');
    if(d.mode==='auto') {
      const order=effectiveOrder(d),requested=target.rank;
      if(!Number.isInteger(requested)||requested<1||requested>items.length)return failure('落点超出顺序范围');
      if(order.indexOf(id)+1===requested)return proposal(d,next,width,{rank:requested});
      delete next.pins[id];let rank=requested;
      while(Object.values(next.pins).includes(rank))rank++;
      if(rank>items.length)return failure('该顺序位已固定，后方没有空位');
      next.pins[id]=rank;return proposal(d,next,width,{rank});
    }
    const {x,y}=target;
    if(!Number.isInteger(x)||!Number.isInteger(y)||x<0||y<0||x+source.w>width||y+source.h>256)return failure('落点超出可用网格');
    if(width===minWidth(d)&&x!==0)return failure('最窄布局只允许单列落点');
    if(source.x===x&&source.y===y)return proposal(d,next,width,{adopted:false,affected:[]});
    const placed=[{...source,x,y}],affected=[];
    for(const old of [...current.items].filter(i=>i.id!==id).sort(reading)) {
      const r={...old};let blocks;
      while((blocks=placed.filter(b=>overlap(b,r))).length) {
        r.y=Math.max(...blocks.map(b=>b.y+b.h));
        if(r.y+r.h>256)return failure('让位超出试验容量');
      }
      if(r.y!==old.y)affected.push(r.id);placed.push(r);
    }
    next.manual=placed.sort(reading);
    const adoptedIds=current.adapted?current.items.filter(r=>!sameGeometry([r],[d.manual.find(i=>i.id===r.id)])).map(r=>r.id):[];
    return proposal(d,next,width,{adopted:current.adapted,adoptedIds,affected});
  }
  function setMode(d,width,mode,exitPolicy='retain-auto') {
    if(mode===d.mode)return proposal(d,clone(d),width);
    const current=layout(d,width);if(!current.ok)return failure(current.reason);
    const next=clone(d);
    if(mode==='manual') {next.manual=clone(current.items);next.editedSizes={};}
    else {
      if(exitPolicy==='carry-edited-size')Object.assign(next.sizes,clone(next.editedSizes));
      else if(exitPolicy!=='retain-auto')return failure('未知试验策略');
      next.manual=[];next.editedSizes={};
    }
    next.mode=mode;return proposal(d,next,width,{modeChange:true});
  }
  function resize(d,width,id,w,h) {
    const current=layout(d,width),next=clone(d);
    if(!current.ok)return failure(current.reason);
    const old=current.items.find(i=>i.id===id);
    if(!old||w<1||h<1||w>width||w>4||h>4)return failure('尺寸超出当前可用范围');
    if(w===old.w&&h===old.h)return proposal(d,next,width);
    if(d.mode==='auto')next.sizes[id]=[w,h];
    else {
      next.manual=clone(current.items);next.editedSizes[id]=[w,h];
      const source=next.manual.find(i=>i.id===id);
      source.w=w;source.h=h;source.x=Math.min(source.x,width-w);
      const held=[source];
      for(const oldPeer of [...next.manual].filter(i=>i.id!==id).sort(reading)) {
        const r={...oldPeer};let blocks;
        while((blocks=held.filter(b=>overlap(b,r))).length)r.y=Math.max(...blocks.map(b=>b.y+b.h));
        if(r.y+r.h>256)return failure('尺寸调整超出试验容量');held.push(r);
      }
      next.manual=held.sort(reading);
    }
    return proposal(d,next,width,{adopted:current.adapted});
  }
  function pin(d,width,id) {
    if(d.mode!=='auto')return failure('手动模式不使用顺序图钉');
    const next=clone(d);
    if(next.pins[id])delete next.pins[id];else next.pins[id]=effectiveOrder(d).indexOf(id)+1;
    return proposal(d,next,width);
  }
  function heat(d,width) {
    const next=clone(d);next.order=[...next.order].reverse();
    return proposal(d,next,width); // Ranking fixture only; does not compute or collect heat.
  }
  return {clone,items,seed,reading,overlap,effectiveOrder,spans,minWidth,layout,sameGeometry,move,setMode,resize,pin,heat};
});
