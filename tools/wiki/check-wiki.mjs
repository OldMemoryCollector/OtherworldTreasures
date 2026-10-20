import fs from 'node:fs/promises';
import path from 'node:path';
import assert from 'node:assert/strict';
export async function checkWiki(root){
 const names=await fs.readdir(root);const pages=names.filter(n=>n.endsWith('.md'));const imageNames=await fs.readdir(path.join(root,'images'));let links=0,imageLinks=0;const errors=[];
 const data=JSON.parse(await fs.readFile(path.join(root,'catalog.json'),'utf8'));assert.equal(data.errors.length,0,'export errors');assert.equal(new Set(data.entries.map(e=>e.id)).size,data.entries.length,'duplicate model IDs');
 for(const file of pages){const text=await fs.readFile(path.join(root,file),'utf8');
  for(const match of text.matchAll(/\[[^\]]*\]\(([^)]+)\)/g)){const target=match[1];if(/^(https?:|#)/.test(target))continue;const local=target.split('#')[0];links++;if(!names.includes(local))errors.push(file+': missing '+local);}
  for(const match of text.matchAll(/<img src="[^"]*\/([^/" ]+)"/g)){imageLinks++;if(!imageNames.includes(match[1]))errors.push(file+': missing image '+match[1]);}
  if(/能量能量|星星星星|\[img\]|资源图标/.test(text))errors.push(file+': unresolved resource count');
  if(/\[(?:\/?gold|\/?blue|\/?rainbow)[^\]]*\]|\{\w+(?::[^}]*)?\}/.test(text))errors.push(file+': unresolved localization markup');
 }
 const shimmer=await fs.readFile(path.join(root,'Events-OTHERWORLD_TREASURES_EVENT_SHIMMER_LAKE.md'),'utf8');assert(shimmer.includes('| 微光嬗变 | **生命水晶**→**活力水晶**，**生命果**→**神盾果** |'),'Shimmer name and description');
 const coffee=await fs.readFile(path.join(root,'Events-OTHERWORLD_TREASURES_EVENT_SCP294.md'),'utf8');assert(coffee.includes('随机调制 · 50金币')&&coffee.includes('自由输入 · 50金币')&&coffee.includes('两条负面效果'),'SCP-294 entry modes and revival cost');
 const eventIndex=await fs.readFile(path.join(root,'Events.md'),'utf8');assert(eventIndex.includes('出现层数')&&eventIndex.includes('前置条件')&&eventIndex.includes('重复限制'),'event index requirements columns');
 for(const event of data.entries.filter(e=>e.kind==='events')){const page=await fs.readFile(path.join(root,'Events-'+event.id+'.md'),'utf8');assert(page.includes('## 出现条件')&&page.includes('| 出现层数 |')&&page.includes('| 前置条件 |')&&page.includes('| 重复限制 |'),'complete encounter requirements: '+event.id);}
 const silverChandelier=await fs.readFile(path.join(root,'Silver-Relics-silver_relics_CHANDELIER.md'),'utf8');assert(silverChandelier.includes('获得3点能量'),'silver chandelier numerical energy');
 const miniRegent=await fs.readFile(path.join(root,'Golden-Relics-golden_relics_MINI_REGENT.md'),'utf8');assert(miniRegent.includes('花费1点星星'),'mini regent numerical stars');
 const zeroCostCard=await fs.readFile(path.join(root,'Cards-OTHERWORLD_TREASURES_CARD_FOUR_ARMS_RAPID_OFFENSIVE.md'),'utf8');assert(zeroCostCard.includes('费用变为0点能量'),'zero energy cost remains zero');
 const goldenChandelier=await fs.readFile(path.join(root,'Golden-Relics-golden_relics_CHANDELIER.md'),'utf8');assert(goldenChandelier.includes('获得6点能量'),'explicit number plus energy icon');
 const fountain=await fs.readFile(path.join(root,'Events-OTHERWORLD_TREASURES_EVENT_GOLDEN_AXE.md'),'utf8');assert(fountain.includes('第二、第三幕')&&fountain.includes('遇过「旧忆收藏家」后仍可出现'),'fountain revised requirements');
 const sword=await fs.readFile(path.join(root,'Events-OTHERWORLD_TREASURES_EVENT_SWORD_IN_THE_LAKE.md'),'utf8');assert(sword.includes('0～2次')&&sword.includes('3次及以上')&&sword.includes('5次精英'),'sword evolution protection');
 assert(coffee.includes('每幕最多一次')&&coffee.includes('50%')&&coffee.includes('25%'),'coffee repeat limits');
 const scp914=await fs.readFile(path.join(root,'Events-OTHERWORLD_TREASURES_EVENT_SCP914.md'),'utf8');assert(scp914.includes('所有持有的遗物均可投入')&&!scp914.includes('可安全移除'),'SCP-914 accepts every owned relic');assert(!eventIndex.includes('可安全移除'),'event index does not add SCP-914 relic restrictions');
 assert.equal(errors.length,0,errors.join('\n'));return{pages:pages.length,entries:data.entries.length,images:imageNames.length,links,imageLinks,errors};
}
if(typeof process!=='undefined'&&process.argv[1]?.endsWith('check-wiki.mjs'))console.log(await checkWiki(path.resolve(process.argv[2]??'docs/wiki')));
