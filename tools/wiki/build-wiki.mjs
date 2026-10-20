import fs from 'node:fs/promises';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

export async function buildWiki({input,repoRoot,sharp}) {
 const out=path.join(repoRoot,'docs/wiki');await fs.mkdir(path.join(out,'images'),{recursive:true});
 const data=JSON.parse(await fs.readFile(path.join(input,'catalog.json'),'utf8'));
 if(data.errors.length)throw Error('Resolve exporter errors before publishing: '+data.errors.join('; '));
 const localeDir=path.join(repoRoot,'OtherworldTreasures/localization/zhs');
 const locales={};for(const table of ['relics','cards','events','powers','potions','enchantments'])locales[table]=JSON.parse(await fs.readFile(path.join(localeDir,table+'.json'),'utf8'));
 for(const e of data.entries){if(e.class==='SCP294Potion')e.description='由本次调制配方决定，可包含多个正面或负面效果；可主动使用、作用于敌人，或带有致命伤害时触发的保命效果。';if(e.class==='EnchantingTable')e.description=e.description.replace(/^当前经验等级：[\s\S]*?。/,'');if(e.class==='FourSoulsPower')e.description='标记该敌人携带的四魂之玉碎片种类：荒魂、和魂、幸魂或奇魂。具体种类随敌人变化。';}
 const names={relics:'遗物图鉴',cards:'卡牌图鉴',potions:'药水图鉴',events:'事件图鉴',enchantments:'附魔图鉴',powers:'能力图鉴',golden_relics:'金色遗物',silver_relics:'银色遗物'};
 const roots={relics:'Relics',cards:'Cards',potions:'Potions',events:'Events',enchantments:'Enchantments',powers:'Powers',golden_relics:'Golden-Relics',silver_relics:'Silver-Relics'};
 const labels={Common:'普通',Uncommon:'罕见',Rare:'稀有',Starter:'初始',Shop:'商店',Event:'事件',Ancient:'先古',Token:'衍生',Basic:'基础',None:'无',Attack:'攻击',Skill:'技能',Power:'能力',Status:'状态',Curse:'诅咒',Self:'自身',AnyEnemy:'单个敌人',AllEnemies:'所有敌人',AnyAlly:'一名队友',AllAllies:'所有队友',AnyPlayer:'任意玩家'};
 const substitutions={Item:'本次物品',Relic:'所选遗物',Card:'所选卡牌',Skill:'本次传授的招式',Potion:'调制结果',Option:'本次选项',Details:'实际效果预览',Gold:'实际金币数',Cost:'所需金币',Amount:'X',Description:'效果',Title:'物品名',Status:'',Remaining:'剩余次数',Turn:'对应回合',Cards:'对应牌数',TurnBonus:'回合增益',Energy:'能量',Block:'格挡值'};
 function plain(text=''){
  return String(text).replace(/\[img\][\s\S]*?\[\/img\]/g,s=>/star/i.test(s)?'星星':'能量').replace(/\[gold\]([\s\S]*?)\[\/gold\]/g,'**$1**').replace(/\[(?:\/?[a-zA-Z][^\]]*)\]/g,'').replace(/res:\/\/[^\s]+/g,'能量').replace(/\{([^}:]+)(?::[^}]*)?\}/g,(_,k)=>substitutions[k]??'对应数值').replace(/\r\n/g,'\n').trim();
 }
 const paragraph=t=>plain(t).replace(/\n/g,'<br>\n');
 const cell=t=>plain(t).replace(/\|/g,'&#124;').replace(/\n/g,'<br>');
 const page=e=>roots[e.kind]+'-'+e.id;
 const imageUrl=e=>'https://raw.githubusercontent.com/OldMemoryCollector/OtherworldTreasures/main/docs/wiki/images/'+e.image.replace(/\.png$/,'.webp');
 const image=(e,width)=>e.image?'<img src="'+imageUrl(e)+'" width="'+width+'" alt="'+plain(e.title).replaceAll('"','&quot;')+'">':'';
 const sourceFiles=[];
 async function scan(dir){for(const d of await fs.readdir(dir,{withFileTypes:true})){const p=path.join(dir,d.name);if(d.isDirectory())await scan(p);else if(d.name.endsWith('.cs'))sourceFiles.push({path:path.relative(repoRoot,p).replaceAll('\\','/'),text:await fs.readFile(p,'utf8')});}}
 await scan(path.join(repoRoot,'Scripts'));
 function source(e){const file=sourceFiles.find(f=>new RegExp('\\bclass\\s+'+e.class+'\\b').test(f.text));return file?.path;}
 const links=(items)=>items.map(e=>'['+plain(e.title)+']('+page(e)+'.md)').join('、');
 const sections={};for(const k of Object.keys(names))sections[k]=data.entries.filter(e=>e.kind===k);
 const notes={
  LifeCrystal:'战斗胜利后有2%的独立概率作为额外奖励出现。可重复获得。同类遗物会合并显示，第二颗起在图标上标记数量。可在微光湖消耗一颗并失去10点生命上限，换取活力水晶。',
  LifeFruit:'战斗胜利后有2%的独立概率作为额外奖励出现。可重复获得并合并计数。每颗提供5点战斗开始时的伤害吸收。可在微光湖消耗一颗换取神盾果。',
  VitalCrystal:'通过微光湖由生命水晶嬗变获得。每次拾取按当时的生命上限提高20%，向下取整，至少增加1点。',
  AegisFruit:'通过微光湖由生命果嬗变获得。同类遗物合并计数，每颗在战斗开始时提供4层金属化。',
  TerrariaCrossNecklace:'只改变同一次攻击后续段数的受伤数值。攻击命中仍正常结算，因此荆棘等命中反应仍会逐段触发。',
  SporeSac:'同一回合仅向第一个发动攻击的敌人释放一次。释放后清空本场战斗累积的临时中毒，需要重新积累。周围孢子数量对应当前可释放的中毒量。',
  PaladinShield:'在久别重逢事件中仅多人局可抽取。回合结束格挡为6＋3×队友数：单人6点、两人9点、三人12点、四人15点。',
  HoneyThorns:'每只蜜蜂各自选择随机敌人，造成3点伤害并施加1层中毒。',
  CrossNecklace:'久别重逢中的隐藏候选，有较低概率进入本次三选一。',
  EnchantingTable:'战斗内为临时附魔；休息处可以永久附魔并升级。只从适配所选卡牌的附魔中抽取。',
  AirCannonBashCard:'由空气炮生成，创建时自带击退Ⅱ附魔。',
  SCP294Potion:'由SCP-294调制，名称、效果、目标与外观由本次配方决定。没有统一固定配方；下面导出的默认效果只表示未附加配方时的占位实例。实际玩法见SCP-294页面。'
 };
 let written=0;const images=new Set();
 for(const entry of data.entries){
  const e={...entry};if(!names[e.kind])throw Error('Unknown kind '+e.kind);
  if(e.image&&!images.has(e.image)){const bound=e.kind==='events'?{width:1100,height:650}:e.kind==='cards'?{width:560,height:560}:{width:160,height:160};await sharp(path.join(input,'images',e.image)).resize({...bound,fit:'inside',withoutEnlargement:true}).webp({quality:90,effort:5}).toFile(path.join(out,'images',e.image.replace(/\.png$/,'.webp')));images.add(e.image);}
  let text='# '+plain(e.title)+'\n\n[首页](Home.md) / ['+names[e.kind]+']('+roots[e.kind]+'.md)\n\n';
  if(e.image)text+=image(e,e.kind==='events'?800:e.kind==='cards'?280:96)+'\n\n';
  const props=[];if(e.rarity)props.push('**稀有度：**'+(labels[e.rarity]??e.rarity));if(e.type)props.push('**类型：**'+(labels[e.type]??e.type));if(e.cost!=null)props.push('**基础费用：**'+(e.cost==='-1'?'X':e.cost));if(e.target)props.push('**目标：**'+(labels[e.target]??e.target));if(props.length)text+=props.join('　')+'\n\n';
  if(e.baseDescription)text+='## 强化前\n\n'+paragraph(e.baseDescription)+'\n\n## 强化后\n\n';
  if(e.kind==='events'){
   const initial=locales.events[e.id+'.pages.INITIAL.description']??locales.events[e.id+'.pages.PREVIEW.description'];if(initial)text+=paragraph(initial)+'\n\n';
   if(e.class==='Scp294'){
    text+='## 使用方式\n\n- **随机调制 · 50金币**：依次选择口味、感觉和浓度，最终选项直接预览可能获得的产物。\n- **自由输入 · 50金币**：根据输入的内容进行识别，调制一杯饮品。确认后付费，取消输入不扣金币。\n- **离开**：继续前行。\n\n输入框每5秒轮换一条饮品建议；玩家输入时不会改动已写的文字。可以输入普通饮料，也可以输入药水图鉴中的名字。\n\n指定图鉴药水时，保留原药水效果，再加入1～2项可好可坏的额外效果。指定复活效果时，额外加入两条负面效果。未识别药效的饮品仍会调制，倾向对自己不利或强化敌人的效果，也可能混入正面收益。\n\n偶尔会发生掉血、获得诅咒或赠送一次免费重做的事故。药水栏已满时会保留已经调好的同一杯，腾出位置后可领取。\n\n[16杯调制实例](Brew-Examples.md) · [饮品建议池](Drink-Suggestions.md)\n\n';
    for(const group of ['taste','feeling','concentration']){const opts=Object.entries(locales.events).filter(([k])=>k.startsWith(e.id+'.'+group+'.')&&!k.endsWith('.description'));if(opts.length)text+='### '+({taste:'口味',feeling:'感觉',concentration:'浓度'}[group])+'\n\n'+opts.map(([,v])=>plain(v)).join('、')+'\n\n';}
   }
   if(e.class==='ShimmerLake')text+='## 出现与玩法\n\n第二幕进入事件池，尚未遇见时第三幕仍可出现；整局不会重复遇见。生命水晶和生命果分别提供额外遇见机会。\n\n- **微光嬗变**：生命水晶→活力水晶，生命果→神盾果。水晶分支失去一颗生命水晶及10点生命上限；生命果分支消耗一颗生命果。\n- **微光转变**：选择一张卡牌，变为同品质的其他卡牌。\n- **微光拆解**：将一张牌的升级与附魔转移到另一张适配的可附魔卡牌上。\n- **离开**：继续前行。\n\n';
   if(e.class==='TerrariaReunion')text+='## 奖励规则\n\n从当前可选且尚未持有的饰品中随机展示三件，玩家选择一件。圣骑士护盾只在多人局进入候选；十字章护身符是较低概率出现的隐藏候选。\n\n'+links(sections.relics.filter(x=>['MythicCharm','SporeSac','TerrariaCrossNecklace','HoneyThorns','CrossNecklace','PaladinShield'].includes(x.class)))+'。\n\n';
   if(e.class==='GoldenAxe')text+='## 金银强化\n\n诚实归还只有一个选项，本次为金色还是银色由遗物适配情况与随机决定。金色主要提高正面收益，银色主要降低触发所需回合或牌数。具体结果见[金色遗物](Golden-Relics.md)与[银色遗物](Silver-Relics.md)。\n\n';
   const options=Object.entries(locales.events).filter(([k])=>k.startsWith(e.id+'.pages.')&&k.includes('.options.')&&k.endsWith('.title')&&!/\.INITIAL\.options\.PAY\.title$/.test(e.class==='Scp294'?k:''));
   if(options.length){text+='## 分支选项\n\n不同页面的同名选项属于不同分支，并不同时出现。实际奖励与数量以本次事件预览为准。\n\n| 选项 | 效果 |\n| --- | --- |\n';for(const[k,v]of options){const description=locales.events[k.replace(/\.title$/,'.description')]??'';text+='| '+cell(v)+' | '+cell(description)+' |\n';}text+='\n';}
  }else{
   text+='## 效果\n\n'+(paragraph(e.description)||'这是用于表现其他效果的辅助能力，具体规则见其来源条目。')+'\n\n';
   if(e.kind==='powers')text+='数值按1层实例展示，实际层数随战斗变化。\n\n';
   if(e.upgrades?.length){text+='## 升级\n\n| 等级 | 费用 | 效果 |\n| --- | --- | --- |\n';for(const up of e.upgrades)text+='| +'+up.level+' | '+(up.cost??e.cost)+' | '+cell(up.description)+' |\n';text+='\n';}else if(e.kind==='cards'&&e.maxUpgrades===0)text+='此牌不能升级。\n\n';
   if(notes[e.class])text+='## 使用说明\n\n'+notes[e.class]+'\n\n';
   const lorePrefix=e.kind==='powers'?'OTHERWORLD_TREASURES_POWER_'+e.id:e.id;
   const extras=Object.entries(locales[e.kind]??{}).filter(([k])=>k.startsWith(lorePrefix+'.')&&/\.(description|desc)$/.test(k)&&k!==lorePrefix+'.description'&&!/\.selection/.test(k));
   if(extras.length){text+='## 补充说明\n\n';for(const[k,v]of extras){const root=k.replace(/\.(description|desc)$/,'');const heading=locales[e.kind][root+'.title'];if(heading)text+='### '+plain(heading)+'\n\n';text+=paragraph(v)+'\n\n';}}
   if(e.flavor)text+='## 轶闻\n\n> '+plain(e.flavor).replace(/\n/g,'\n> ')+'\n\n';
  }
  const src=source(e)??(e.kind==='golden_relics'?'Scripts/Events/GoldenRelics.cs':e.kind==='silver_relics'?'Scripts/Events/SilverRelics.cs':undefined);
  text+='---\n\n';if(src)text+='[实现来源](https://github.com/OldMemoryCollector/OtherworldTreasures/blob/main/'+src+') · ';text+='[返回'+names[e.kind]+']('+roots[e.kind]+'.md)\n';
  await fs.writeFile(path.join(out,page(e)+'.md'),text,'utf8');written++;
 }
 for(const[k,list]of Object.entries(sections)){
  let text='# '+names[k]+'\n\n[返回首页](Home.md)\n\n共 **'+list.length+'** 项。点击名称查看完整效果与说明。\n\n';
  if(k==='cards')text+='含修行招式、植物召唤、惊奇卡牌以及其他衍生牌。\n\n';
  if(k==='golden_relics'||k==='silver_relics')text+='以下列出当前版本可以强化的原版遗物，并提供强化前后效果对照。\n\n';
  text+='| 图片 | 名称 | '+(k==='cards'?'费用 / 类型':'类别')+' | 效果 |\n| --- | --- | --- | --- |\n';
  for(const e of list)text+='| '+image(e,k==='cards'?100:48)+' | ['+plain(e.title)+']('+page(e)+'.md) | '+(k==='cards'?(e.cost==='-1'?'X':e.cost)+' / '+(labels[e.type]??e.type):(labels[e.rarity]??(k==='powers'?'能力':k==='enchantments'?'附魔':k==='events'?'事件':'强化')))+' | '+cell((k==='events'?locales.events[e.id+'.pages.INITIAL.description']:e.description)??'点击查看')+' |\n';
  await fs.writeFile(path.join(out,roots[k]+'.md'),text,'utf8');
 }
 let home='# 异次元乱入 · 图鉴\n\n这里收录《杀戮尖塔2》模组「异次元乱入」当前实现的遗物、卡牌、药水、事件与附魔。点击分类，再点击条目名称即可查看详细效果。\n\n';
 home+='| 分类 | 数量 | 内容 |\n| --- | ---: | --- |\n';for(const[k,list]of Object.entries(sections))home+='| ['+names[k]+']('+roots[k]+'.md) | '+list.length+' | '+({relics:'遗物效果、主动用法与获取线索',cards:'费用、类型、基础效果和升级结果',potions:'固定药水与SCP-294异常调制',events:'事件背景、分支选项与奖励',enchantments:'附魔效果与卡牌适配',powers:'战斗能力与特殊状态',golden_relics:'原版遗物金色强化前后对照',silver_relics:'原版遗物银色强化前后对照'}[k])+' |\n';
 home+='\n## 常用入口\n\n- [微光湖](Events-OTHERWORLD_TREASURES_EVENT_SHIMMER_LAKE.md)：生命水晶与生命果嬗变、卡牌转变与拆解。\n- [久别重逢](Events-OTHERWORLD_TREASURES_EVENT_TERRARIA_REUNION.md)：泰拉瑞亚饰品随机三选一。\n- [SCP-294：咖啡机](Events-OTHERWORLD_TREASURES_EVENT_SCP294.md)：随机调制与自由输入。\n- [16杯调制实例](Brew-Examples.md) · [饮品建议池](Drink-Suggestions.md)。\n- [附魔台](Relics-OTHERWORLD_TREASURES_RELIC_ENCHANTING_TABLE.md)。\n\n## 阅读说明\n\n卡牌数值按未受到战斗增减益影响的状态展示；能力数值按1层展示。随机产物、充能、经验、变身时间等会随游戏进程变化，以游戏中的实际预览为准。\n\n当前数据从本地游戏实际模型导出，并与中文文本核对。更新日期：'+new Date().toLocaleDateString('sv-SE',{timeZone:'Asia/Hong_Kong'})+'。\n\n[项目源代码](https://github.com/OldMemoryCollector/OtherworldTreasures) · [图鉴维护说明](Maintenance.md)\n';
 await fs.writeFile(path.join(out,'Home.md'),home,'utf8');
 await fs.writeFile(path.join(out,'_Sidebar.md'),'## 异次元乱入\n\n[图鉴首页](Home.md)\n\n'+Object.keys(names).map(k=>'- ['+names[k]+']('+roots[k]+'.md)').join('\n')+'\n\n- [16杯调制实例](Brew-Examples.md)\n- [饮品建议池](Drink-Suggestions.md)\n','utf8');
 await fs.writeFile(path.join(out,'_Footer.md'),'异次元乱入 · 内容以当前游戏版本为准 · [源代码](https://github.com/OldMemoryCollector/OtherworldTreasures)\n','utf8');
 await fs.writeFile(path.join(out,'Maintenance.md'),'# 图鉴维护\n\n[返回首页](Home.md)\n\n图鉴由游戏运行时导出的模型与中文本地化文本生成。页面同时保存在主仓库的 docs/wiki 目录与独立的 GitHub Wiki 仓库。\n\n维护工具位于 tools/wiki。先用隔离测试实例运行 ExportCatalog.cs.txt 导出 catalog.json 和 images，再运行 build-wiki.mjs。生成前要求导出错误数为0；发布前检查条目数量、图片与所有内部链接。\n\n每次功能完成并验证后提交代码；涉及玩法、数值或获取方式时同步更新图鉴。\n','utf8');
 const examples=JSON.parse(await fs.readFile(path.join(input,'brew-examples.json'),'utf8'));
 let samplePage='# SCP-294 · 16杯调制实例\n\n[返回咖啡机](Events-OTHERWORLD_TREASURES_EVENT_SCP294.md) · [返回药水图鉴](Potions.md)\n\n这些是实际调制得到的随机样例，展示不同药效如何组合。它们不是16种固定配方，同名或同一组选择在其他跑局中仍可能得到不同结果。\n\n';
 for(const e of examples){const file=String(e.Number).padStart(2,'0')+'-'+e.Name+'.png';const target='BREW_SAMPLE_'+String(e.Number).padStart(2,'0')+'.webp';await sharp(path.join(input,'sample-images',file)).webp({quality:95}).toFile(path.join(out,'images',target));}
 for(const e of examples)samplePage+='## '+e.Number+'. '+plain(e.Name)+'\n\n<img src="https://raw.githubusercontent.com/OldMemoryCollector/OtherworldTreasures/main/docs/wiki/images/BREW_SAMPLE_'+String(e.Number).padStart(2,'0')+'.webp" width="96" alt="'+plain(e.Name)+'">\n\n**口味：**'+e.Taste+'　**感觉：**'+e.Feeling+'　**浓度：**'+e.Concentration+'\n\n'+paragraph(e.Description)+'\n\n';
 await fs.writeFile(path.join(out,'Brew-Examples.md'),samplePage,'utf8');
 const suggestions=JSON.parse(await fs.readFile(path.join(input,'drink-suggestions.json'),'utf8'));let drinkPage='# 饮品建议池\n\n[返回咖啡机](Events-OTHERWORLD_TREASURES_EVENT_SCP294.md)\n\n共'+suggestions.length+'种饮品建议。输入框为空时每5秒轮换一句建议，另外也会从游戏药水图鉴中抽取名字。这些文字是输入提示，不会自动替玩家填写。\n\n';
 for(const category of [...new Set(suggestions.map(x=>x.category))])drinkPage+='## '+category+'\n\n'+suggestions.filter(x=>x.category===category).map(x=>x.zh).join('、')+'。\n\n';
 await fs.writeFile(path.join(out,'Drink-Suggestions.md'),drinkPage,'utf8');
 const options=JSON.parse(await fs.readFile(path.join(input,'brew-options.json'),'utf8'));
 const machinePath=path.join(out,'Events-OTHERWORLD_TREASURES_EVENT_SCP294.md');let machine=await fs.readFile(machinePath,'utf8');let optionDetails='## 调制选择说明\n\n';for(const group of ['Taste','Feeling']){optionDetails+='### '+(group==='Taste'?'口味倾向':'感觉倾向')+'\n\n| 选择 | 倾向 |\n| --- | --- |\n';for(const choice of options[group])optionDetails+='| '+cell(choice.Name)+' | '+cell(choice.Description)+' |\n';optionDetails+='\n';}optionDetails+='浓度提供六种选择：'+options.Concentration.join('、')+'。最后一步直接展示本次已经生成的药水，不依赖固定配方。\n\n';machine=machine.replace('---\n\n',optionDetails+'---\n\n');await fs.writeFile(machinePath,machine,'utf8');
 for(const extra of ['brew-examples.json','drink-suggestions.json','brew-options.json'])await fs.copyFile(path.join(input,extra),path.join(out,extra));
 await fs.writeFile(path.join(out,'catalog.json'),JSON.stringify(data,null,2)+'\n','utf8');
 for(const file of await fs.readdir(out)){if(file.endsWith('.md')){const p=path.join(out,file);await fs.writeFile(p,(await fs.readFile(p,'utf8')).replace(/[ \t]+$/gm,'').trimEnd()+'\n','utf8');}}
 return {entries:written,images:images.size,counts:Object.fromEntries(Object.entries(sections).map(([k,v])=>[k,v.length])),output:out};
}
if(typeof process!=='undefined'&&process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)){
 const sharp=(await import('sharp')).default;
 console.log(await buildWiki({input:path.resolve(process.argv[2]),repoRoot:path.resolve(process.argv[3]??'.'),sharp}));
}
