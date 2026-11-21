(() => {
  'use strict';
  const DAYS = ['Понеділок','Вівторок','Середа','Четвер','П’ятниця'];
  const $ = id => document.getElementById(id);
  const todayIndex = () => ['Mon','Tue','Wed','Thu','Fri'].indexOf(
    new Intl.DateTimeFormat('en-GB',{weekday:'short',timeZone:'Europe/Kyiv'}).format(new Date()));
  let state = {classes:[],days:{},extras:{},selected:'',day:todayIndex()<0?'week':todayIndex(),updated:'',filter:''};
  const esc = s => String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  function setStatus(message,kind='warn'){
    $('statusText').textContent=message;
    $('stateDot').className='dot '+(kind==='ok'?'':kind);
  }
  function selectClass(){
    const el=$('classSelect');el.replaceChildren();
    for(const cl of state.classes){const option=document.createElement('option');option.value=cl.id;option.textContent=cl.name;el.append(option)}
    let stored='';try{stored=localStorage.getItem('school-last-class')||''}catch{}
    state.selected=state.classes.some(c=>c.id===state.selected)?state.selected:
      state.classes.some(c=>c.id===stored)?stored:state.classes[0]?.id||'';
    el.value=state.selected;
  }
  function visibleLessons(day){
    const lessons=(state.days[day]||[]).map(l=>({...l,subject:l.subjects[state.selected]||''})).filter(l=>l.subject.trim());
    const query=state.filter.toLocaleLowerCase('uk-UA');
    return query?lessons.filter(l=>l.subject.toLocaleLowerCase('uk-UA').includes(query)):lessons;
  }
  function render(){
    if(!state.classes.length){$('overview').innerHTML='';$('schedule').innerHTML='<div class="empty"><strong>Розклад ще не завантажено</strong>Перевірте доступ і натисніть «Оновити».</div>';return;}
    document.querySelectorAll('[data-day]').forEach(b=>{let active=String(state.day)===b.dataset.day;b.classList.toggle('active',active);b.setAttribute('aria-pressed',active?'true':'false')});
    const total=[0,1,2,3,4].reduce((n,i)=>n+visibleLessons(i).length,0);
    const today=typeof state.day==='number'?state.day:todayIndex();
    const selected=state.classes.find(c=>c.id===state.selected);
    $('overview').innerHTML='<div class="stat"><strong>'+esc(selected?.name||'—')+'</strong><span>Обраний клас</span></div><div class="stat"><strong>'+total+'</strong><span>Уроків за тиждень'+(state.filter?' за пошуком':'')+'</span></div><div class="stat"><strong>'+(today<=4?visibleLessons(today).length:'—')+'</strong><span>Уроків за обраний день</span></div>';
    const root=$('schedule');
    if(state.day==='week'){
      root.className='weekly';
      root.innerHTML=DAYS.map((day,i)=>{const ls=visibleLessons(i),now=todayIndex()===i;return '<article class="daycard'+(now?' today':'')+'"><h3>'+esc(day)+'</h3>'+(ls.length?ls.map(l=>'<div class="mini"><span>'+l.num+' урок · '+esc(l.time)+'</span><strong>'+esc(l.subject)+'</strong></div>').join(''):'<p class="count">Немає уроків</p>')+'<div class="count">'+ls.length+' уроків</div></article>'}).join('');return;
    }
    root.className='daily';
    const lessons=visibleLessons(state.day);
    root.innerHTML=lessons.length?lessons.map(l=>'<article class="lesson"><div class="num">'+l.num+'</div><div><div class="subject">'+esc(l.subject)+'</div><div class="times">'+esc(l.time)+'</div></div><span class="tag">Урок '+l.num+'</span></article>').join(''):'<div class="empty"><strong>Уроків не знайдено</strong>Спробуйте інший день або приберіть пошук.</div>';
    let extra=state.extras?.[state.day]?.[state.selected];
    if(extra&&!state.filter)root.innerHTML+='<aside class="extra"><strong>Після уроків:</strong> '+esc(extra)+'</aside>';
  }
  async function load(force=false){
    setStatus('Завантажуємо актуальний розклад…');$('refresh').disabled=true;
    try{
      const res=await fetch('/api/schedule'+(force?'?refresh=true':''),{cache:'no-store'});
      const data=await res.json();
      if(!res.ok)throw new Error(data.error||'Помилка HTTP '+res.status);
      state={...state,...data};selectClass();
      $('updatedAt').textContent='Оновлено: '+new Date(data.updated).toLocaleString('uk-UA');
      render();
      try{localStorage.setItem('school-schedule-mvc',JSON.stringify(data))}catch{}
      setStatus('Розклад отримано · перевірка раз на 60 хв','ok');
    }catch(err){setStatus('Помилка читання: '+err.message,'bad');}
    finally{$('refresh').disabled=false;}
  }
  function readCache(){try{const data=JSON.parse(localStorage.getItem('school-schedule-mvc')||'null');if(data?.classes?.length){state={...state,...data};selectClass();render();setStatus('Показано збережений розклад, перевіряємо оновлення…')}}catch{}}
  $('classSelect').addEventListener('change',e=>{state.selected=e.target.value;try{localStorage.setItem('school-last-class',state.selected)}catch{}render()});
  $('filter').addEventListener('input',e=>{state.filter=e.target.value.trim();render()});
  $('refresh').onclick=()=>load(true);
  $('tabs').addEventListener('click',e=>{let button=e.target.closest('[data-day]');if(!button)return;state.day=button.dataset.day==='week'?'week':Number(button.dataset.day);render()});
  readCache();load();
  let lastCheck=Date.now();
  setInterval(()=>{if(!document.hidden){lastCheck=Date.now();load()}},3600000);
  document.addEventListener('visibilitychange',()=>{if(!document.hidden&&Date.now()-lastCheck>=3600000){lastCheck=Date.now();load()}});
})();
