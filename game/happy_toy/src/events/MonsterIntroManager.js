import { CyclopseIntroEvent } from "./CyclopseIntroEvent.js";
import { UncatIntroEvent } from "./UncatIntroEvent.js";
import { BabyIntroEvent } from "./BabyIntroEvent.js";
import { LovelyDollIntroEvent } from "./LovelyDollIntroEvent.js";
import { WeepingAngelIntroEvent } from "./WeepingAngelIntroEvent.js";

export class MonsterIntroManager {
  constructor(game) {
    this.game = game;
    this.events = [
      new CyclopseIntroEvent(game),
      new UncatIntroEvent(game),
      new BabyIntroEvent(game),
      new LovelyDollIntroEvent(game),
      new WeepingAngelIntroEvent(game),
    ];
  }


  get blocksPlayerControl() {
    return this.events.some(e => e.blocksPlayerControl);
  }

  update(deltaTime) {
    for (const event of this.events) {
      event.update(deltaTime);
    }
  }

  reset() {
    for (const event of this.events) {
      event.reset();
    }
  }
}
