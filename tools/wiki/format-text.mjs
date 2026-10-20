// Native text may repeat one icon per point, or use a number followed by one unit icon.
// Preserve explicit costs (including zero); repeated icons supply the displayed amount.
export function formatResourceIcons(text) {
 let result=String(text);
 for(const [resource,unit] of [['(?:[a-z0-9_]+_)?energy_icon','点能量'],['star_icon','点星星']]) {
  const pattern=new RegExp('(?:([+-]?\\d+(?:\\.\\d+)?)(?:点|颗|个)?\\s*)?((?:\\[img\\][^\\[]*?/'+resource+'\\.png\\[/img\\][ \\t]*)+)','gi');
  result=result.replace(pattern,(_all,number,icons)=>{
   const count=(icons.match(/\[img\]/g)||[]).length;
   const amount=count>1?count:(number??count);
   return amount+unit;
  });
 }
 return result.replace(/\[img\][\s\S]*?\[\/img\]/g,'图标');
}
