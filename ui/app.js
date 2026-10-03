'use strict';
const host=window.chrome?.webview;
const send=message=>host?.postMessage(message);
const $=id=>document.getElementById(id);
const icons=[
  '<path d="m5 3 14 9-7 1-3 7z"/>',
  '<path d="M3 15c4-17 9 14 18-6"/>',
  '<rect x="6" y="3" width="12" height="18" rx="6"/><path d="M12 3v7m-6 0h12"/>',
  '<path d="M3 7h6m4 0h8M3 17h10m4 0h4"/><circle cx="11" cy="7" r="2"/><circle cx="15" cy="17" r="2"/>'
];
const chevron='<svg viewBox="0 0 20 20"><path d="m6 8 4 4 4-4"/></svg>';
const check='<svg viewBox="0 0 20 20"><path d="m4 10 4 4 8-8"/></svg>';
let state,structure='',controls=new Map(),menu=null,canvas=null,pose=null;
function element(tag,className,text){const e=document.createElement(tag);if(className)e.className=className;if(text!=null)e.textContent=text;return e;}
function button(text,className,click){const e=element('button',className,text);e.type='button';e.onclick=click;return e;}
function closeMenu(){if(menu){menu.remove();menu=null;}}
function openMenu(control,trigger){
  closeMenu();const list=element('div','select-menu');list.setAttribute('role','listbox');list.setAttribute('aria-label',control.text);list.setAttribute('popover','auto');
  control.options.forEach((label,i)=>{const option=button(label,'option',()=>{send({action:'change',id:control.id,value:i});closeMenu();trigger.focus();});option.setAttribute('role','option');option.setAttribute('aria-selected',String(i===control.value));if(i===control.value)option.insertAdjacentHTML('beforeend',check);option.onkeydown=e=>{let next=i;if(e.key==='ArrowDown')next=Math.min(i+1,control.options.length-1);else if(e.key==='ArrowUp')next=Math.max(0,i-1);else if(e.key==='Home')next=0;else if(e.key==='End')next=control.options.length-1;else return;e.preventDefault();list.children[next].focus();};list.append(option);});
  document.body.append(list);menu=list;list.showPopover();const r=trigger.getBoundingClientRect();list.style.width=Math.max(r.width,220)+'px';list.style.left=Math.min(r.left,innerWidth-list.offsetWidth-12)+'px';list.style.top=Math.max(8,Math.min(r.bottom+5,innerHeight-list.offsetHeight-12))+'px';trigger.setAttribute('aria-expanded','true');list.addEventListener('toggle',e=>{if(e.newState==='closed'){trigger.setAttribute('aria-expanded','false');if(menu===list)menu=null;list.remove();}});list.children[Math.max(0,control.value)]?.focus();
}
function makeControl(c){
  let node,interactive;
  if(c.type==='toggle'){
    node=element('div','toggle-field');const label=element('label','',c.text);label.htmlFor=c.id;
    interactive=button('','switch',()=>send({action:'change',id:c.id,value:interactive.getAttribute('aria-checked')!=='true'}));interactive.id=c.id;interactive.setAttribute('role','switch');interactive.setAttribute('aria-label',c.text);node.append(label,interactive);
  }else if(c.type==='select'){
    node=element('div','select-wrap');interactive=button('','select-trigger',()=>openMenu(controls.get(c.id).model,interactive));interactive.id=c.id;interactive.append(element('span'));interactive.insertAdjacentHTML('beforeend',chevron);interactive.setAttribute('aria-haspopup','listbox');interactive.setAttribute('aria-expanded','false');interactive.setAttribute('aria-label',c.text);interactive.onkeydown=e=>{if(e.key==='ArrowDown'||e.key==='ArrowUp'){e.preventDefault();openMenu(controls.get(c.id).model,interactive);}};node.append(interactive);
  }else if(c.type==='range'){
    node=element('div','range-wrap');interactive=element('input');interactive.type='range';interactive.id=c.id;interactive.min=c.min;interactive.max=c.max;interactive.step=1;interactive.setAttribute('aria-label',c.text);const out=element('output');out.htmlFor=c.id;
    interactive.oninput=()=>{out.textContent=interactive.value+' px';interactive.style.setProperty('--progress',100*(interactive.value-c.min)/(c.max-c.min)+'%');send({action:'change',id:c.id,value:Number(interactive.value)});};node.append(interactive,out);
  }else if(c.type==='input'){
    node=interactive=element('input','text-input');interactive.type='text';interactive.id=c.id;interactive.maxLength=512;interactive.setAttribute('aria-label',c.text);interactive.oninput=()=>send({action:'change',id:c.id,value:interactive.value});
  }else{
    node=interactive=button('',c.type==='shortcut'?'keycaps':'button'+(c.primary?' primary':''),()=>send({action:'click',id:c.id}));interactive.id=c.id;
  }
  controls.set(c.id,{node,interactive,model:c});updateControl(c);return node;
}
function updateControl(c){
  const item=controls.get(c.id);if(!item)return;item.model=c;const e=item.interactive;e.disabled=!c.enabled;
  if(c.type==='toggle')e.setAttribute('aria-checked',String(c.value));
  else if(c.type==='select')e.querySelector('span').textContent=c.options[c.value]??'';
  else if(c.type==='range'){e.value=c.value;item.node.querySelector('output').textContent=c.value+' px';e.style.setProperty('--progress',100*(c.value-c.min)/(c.max-c.min)+'%');}
  else if(c.type==='input'){if(document.activeElement!==e)e.value=c.value;}
  else if(c.type==='shortcut'){
    e.setAttribute('aria-label',c.text);e.replaceChildren();if(c.text.includes('…'))e.textContent=c.text;else c.text.split(' + ').forEach(k=>e.append(element('kbd','',k)));
  }else e.textContent=c.text;
}
function render(next){
  state=next;document.documentElement.lang=state.language;document.documentElement.dataset.theme=state.dark?'dark':'light';
  const signature=state.language+':'+state.selected;
  $('settings-label').textContent=state.settings;$('navigation').setAttribute('aria-label',state.settings);$('version').textContent='v'+state.version;$('tray').title=$('tray').ariaLabel=state.tray;
  for(const b of document.querySelectorAll('[data-window]'))b.ariaLabel=state.window[b.dataset.window];
  if(signature!==structure){
    structure=signature;closeMenu();controls.clear();canvas=null;$('navigation').replaceChildren();
    state.pages.forEach((page,i)=>{const b=button('','nav-button',()=>send({action:'page',value:i}));b.innerHTML='<svg viewBox="0 0 24 24">'+icons[i]+'</svg>';b.append(element('span','',page.title));b.title=page.title;if(i===state.selected)b.setAttribute('aria-current','page');$('navigation').append(b);});
    const page=state.pages[state.selected];$('page-title').textContent=page.title;$('page-content').replaceChildren();$('page-scroll').scrollTop=0;
    page.rows.forEach(row=>{
      let node;
      if(row.type==='preview'){
        node=element('div','preview');canvas=element('canvas');canvas.ariaLabel=row.text;node.append(canvas,element('p','',row.text));node.onpointerdown=e=>{if(e.button===0||e.button===2)send({action:'preview',right:e.button===2});};node.oncontextmenu=e=>e.preventDefault();
      }else if(row.type==='heading')node=element('h2','section-heading',row.text);
      else if(row.type==='hint'){node=element('p','hint',row.text);node.dataset.row=String(page.rows.indexOf(row));}
      else{
        node=element('div','row');if(row.text)node.append(element('label','row-label',row.text));const group=element('div','row-controls');
        if(row.controls.length===1&&row.controls[0].type==='toggle')node.classList.add('toggle-only');
        if(row.controls.every(c=>c.type==='button'))node.classList.add('actions');
        row.controls.forEach(c=>group.append(makeControl(c)));node.append(group);
      }
      $('page-content').append(node);
    });
  }
  state.pages[state.selected].rows.forEach((r,i)=>{r.controls?.forEach(updateControl);const hint=document.querySelector('[data-row="'+i+'"]');if(hint)hint.textContent=r.text;});
  $('status').textContent=state.status;$('activation').textContent=state.toggle.text;$('activation').onclick=()=>send({action:'click',id:state.toggle.id});
  drawPose();
}
document.querySelectorAll('[data-window]').forEach(b=>b.onclick=()=>send({action:b.dataset.window}));$('tray').onclick=()=>send({action:'tray'});
document.addEventListener('keydown',e=>{
  if(!state?.recording)return;
  e.preventDefault();e.stopPropagation();
  if(e.key==='Escape'){send({action:'cancelShortcut'});return;}
  if(['Control','Alt','Shift','Meta'].includes(e.key))return;
  const modifiers=(e.altKey?1:0)|(e.ctrlKey?2:0)|(e.shiftKey?4:0);
  send({action:'shortcut',key:e.keyCode,modifiers});
},true);
window.addEventListener('blur',()=>{if(state?.recording)send({action:'cancelShortcut'});});
const artwork=new Image();artwork.src='cursor.png';artwork.onload=()=>drawPose();
function drawPose(){
  if(!canvas||!artwork.complete||!artwork.naturalWidth)return;
  const p=pose??{size:32,rotation:0,stretch:1,squash:1,axis:0,scale:1,leftRing:-1,rightRing:-1};
  const dpr=devicePixelRatio,w=canvas.clientWidth,h=canvas.clientHeight;
  if(canvas.width!==Math.round(w*dpr)||canvas.height!==Math.round(h*dpr)){canvas.width=Math.round(w*dpr);canvas.height=Math.round(h*dpr);}
  const ctx=canvas.getContext('2d');ctx.setTransform(dpr,0,0,dpr,0,0);ctx.clearRect(0,0,w,h);ctx.translate(w/2,(h-20)/2);
  function ring(t,double){if(t<0||t>=1)return;const r=p.size*(.12+.65*(1-(1-t)**2));ctx.globalAlpha=1-t;for(const k of double?[1,.68]:[1]){ctx.beginPath();ctx.arc(0,0,r*k,0,2*Math.PI);ctx.lineWidth=3.5;ctx.strokeStyle='#141414';ctx.stroke();ctx.lineWidth=1.5;ctx.strokeStyle='#fff';ctx.stroke();}ctx.globalAlpha=1;}
  ring(p.leftRing,false);ring(p.rightRing,true);
  const angle=degrees=>degrees*Math.PI/180;
  ctx.scale(p.scale,p.scale);ctx.rotate(angle(p.axis));ctx.scale(1,p.squash);ctx.rotate(angle(-p.axis));ctx.rotate(angle(-44+p.rotation));ctx.scale(p.stretch,1);ctx.rotate(angle(44));ctx.scale(p.size/48,p.size/48);ctx.drawImage(artwork,-4,-5);
}
window.addEventListener('resize',()=>{closeMenu();drawPose();});
host?.addEventListener('message',e=>{if(e.data.type==='state')render(e.data);else if(e.data.type==='pose'){pose=e.data;drawPose();}});
send({action:'ready'});
