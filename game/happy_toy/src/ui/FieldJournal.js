const SOULS = [
  ['key-workshop', '지하의 이름', '본관 남쪽 계단 아래, 물에 잠긴 보육실의 요람을 조사한다.'],
  ['key-playroom', '인형의 이름', '본관 남서쪽 놀이방. 작은 인형의 시선이 길을 가리킨다.'],
  ['key-storage', '잊힌 이름', '본관 북동쪽 준비물 창고. 선반 사이를 살핀다.'],
  ['key-hwacat', '액자 뒤의 이름', '본관 북서쪽 계단으로 2층에 올라 액자를 조사한다.'],
];

// A run-local notebook: opening it pauses the simulation, never alters the map.
export class FieldJournal {
  constructor(game) {
    this.game = game;
    this.notes = new Map();
    this.opened = false;
    this.element = document.createElement('section');
    this.element.className = 'field-journal';
    this.element.hidden = true;
    this.element.setAttribute('role', 'dialog');
    this.element.setAttribute('aria-modal', 'true');
    this.element.setAttribute('aria-label', '출석 수첩');
    document.body.append(this.element);
    this.element.addEventListener('keydown', event => {
      if (event.key === 'Escape' || event.code === 'KeyJ') {
        event.preventDefault(); event.stopPropagation(); this.close();
      }
      if (event.key === 'Tab') {
        event.preventDefault(); this.element.querySelector('button')?.focus();
      }
    });
  }
  record(note) {
    this.notes.set(note.id, {title:note.label, body:note.body});
  }
  reset() {
    this.notes.clear(); this.opened = false; this.element.hidden = true;
  }
  open() {
    const g = this.game;
    if (!g.isStarted || g.isPaused || g.gameOver || g.gameCleared || g.cutsceneEvent || g.monsterIntroManager?.blocksPlayerControl || g.mirrorEvents.some(e=>e.blocksPlayerControl)) return;
    g.pause(); g.hud.hidePause();
    this.opened = true;
    this.element.hidden = false;
    this.element.replaceChildren();
    const paper = document.createElement('div'); paper.className='journal-paper';
    const eyebrow = document.createElement('p'); eyebrow.className='journal-eyebrow'; eyebrow.textContent='폐교 조사 기록 / 출석 수첩';
    const heading = document.createElement('h2'); heading.textContent='돌아오지 않은 네 이름';
    const close = document.createElement('button'); close.className='journal-close'; close.textContent='수첩 덮기 · J'; close.addEventListener('click',()=>this.close());
    paper.append(eyebrow, heading, close);
    const instruction=document.createElement('p'); instruction.className='journal-instruction';
    instruction.textContent=g.keyCount>=g.requiredKeyCount ? '모든 이름을 찾았다. 처음 깨어난 홀의 제단으로 돌아가 E를 6초 동안 누른다.' : '이름을 모아 처음 깨어난 홀의 제단에 돌려놓는다. 수첩을 보는 동안 복도는 멈춘다.';
    paper.append(instruction);
    const list=document.createElement('ol'); list.className='journal-objectives';
    SOULS.forEach(([id,title,hint],i)=>{
      const collected=g.collectedKeyIds.has(id);
      const row=document.createElement('li'); row.classList.toggle('is-found',collected);
      const name=document.createElement('strong'); name.textContent=`0${i+1} / ${title}${collected?' · 찾음':''}`;
      const body=document.createElement('p'); body.textContent=hint;row.append(name,body);list.append(row);
    });
    paper.append(list);
    const title=document.createElement('h3'); title.textContent=`주워 읽은 기록 · ${this.notes.size}`;paper.append(title);
    if (!this.notes.size) {
      const empty=document.createElement('p');empty.textContent='벽에 꽂힌 종이를 E로 읽으면 여기에 보관된다.';paper.append(empty);
    }
    for (const note of this.notes.values()) {
      const entry=document.createElement('article');const h=document.createElement('h4');const p=document.createElement('p');
      h.textContent=note.title;p.textContent=note.body;entry.append(h,p);paper.append(entry);
    }
    this.element.append(paper);close.focus();
  }
  close() {
    if (!this.opened) return;
    this.opened=false;this.element.hidden=true;
    this.game.input.clearKey('j');this.game.input.clearKey('escape');
    this.game.resume();
  }
}
