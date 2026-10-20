import fs from 'node:fs/promises';
import path from 'node:path';
import assert from 'node:assert/strict';
export async function checkWiki(root){
 const names=await fs.readdir(root);const pages=names.filter(n=>n.endsWith('.md'));const imageNames=await fs.readdir(path.join(root,'images'));let links=0,imageLinks=0;const errors=[];
 const data=JSON.parse(await fs.readFile(path.join(root,'catalog.json'),'utf8'));assert.equal(data.errors.length,0,'export errors');assert.equal(new Set(data.entries.map(e=>e.id)).size,data.entries.length,'duplicate model IDs');
 for(const file of pages){const text=await fs.readFile(path.join(root,file),'utf8');
  for(const match of text.matchAll(/\[[^\]]*\]\(([^)]+)\)/g)){const target=match[1];if(/^(https?:|#)/.test(target))continue;const local=target.split('#')[0];links++;if(!names.includes(local))errors.push(file+': missing '+local);}
  for(const match of text.matchAll(/<img src="[^"]*\/([^/" ]+)"/g)){imageLinks++;if(!imageNames.includes(match[1]))errors.push(file+': missing image '+match[1]);}
  if(/\[(?:\/?gold|\/?blue|\/?rainbow)[^\]]*\]|\{\w+(?::[^}]*)?\}/.test(text))errors.push(file+': unresolved localization markup');
 }
 const shimmer=await fs.readFile(path.join(root,'Events-OTHERWORLD_TREASURES_EVENT_SHIMMER_LAKE.md'),'utf8');assert(shimmer.includes('| 微光嬗变 | **生命水晶**→**活力水晶**，**生命果**→**神盾果** |'),'Shimmer name and description');
 const coffee=await fs.readFile(path.join(root,'Events-OTHERWORLD_TREASURES_EVENT_SCP294.md'),'utf8');assert(coffee.includes('随机调制 · 50金币')&&coffee.includes('自由输入 · 50金币')&&coffee.includes('两条负面效果'),'SCP-294 entry modes and revival cost');
 assert.equal(errors.length,0,errors.join('\n'));return{pages:pages.length,entries:data.entries.length,images:imageNames.length,links,imageLinks,errors};
}
if(typeof process!=='undefined'&&process.argv[1]?.endsWith('check-wiki.mjs'))console.log(await checkWiki(path.resolve(process.argv[2]??'docs/wiki')));
