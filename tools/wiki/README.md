# Wiki 图鉴维护

主站：https://github.com/OldMemoryCollector/OtherworldTreasures/wiki

`docs/wiki` 是可审阅的页面与图片快照；GitHub Wiki 使用独立仓库发布，页面内的图片从主仓库读取。先推送主仓库，再发布 Wiki。

## 数据导出

1. 使用隔离的游戏测试实例，按现有本地测试探针项目结构，将 `ExportCatalog.cs.txt` 作为探针源码编译。不要放入正式游戏模组目录。
2. 设置 `OTHERWORLD_WIKI_OUTPUT` 为新的绝对输出目录。启动测试实例后，导出器会等待初始化、导出实际模型文本、卡牌升级结果、图片及金银强化对照，然后退出。
3. 检查 `catalog.json` 的 `errors` 必须为空。图片默认重新导出；只有本轮图片没有改变且需要重试文本导出时，才可设 `OTHERWORLD_WIKI_REUSE_IMAGES=1`。
4. 同目录准备 `brew-examples.json`、`brew-options.json`、`drink-suggestions.json`，格式见 `docs/wiki` 的同名快照；`sample-images` 放16杯实际生成药水的原PNG，文件名为 `01-药水名.png`。这些是展示样例，不能作为固定配方说明。

## 页面生成与检查

本工具使用 Node.js 和 Sharp。首次在本目录安装依赖，再从项目根目录运行：

```powershell
npm install --prefix tools/wiki
node tools/wiki/build-wiki.mjs "导出数据的绝对目录" .
node tools/wiki/check-wiki.mjs docs/wiki
```

生成器使用中文本地化、实际运行时数值及少量已核对的玩法说明。主仓库页面使用 `.md` 内部链接；复制到 Wiki 仓库时移除内部页面链接的 `.md` 后缀，以适配 Wiki 路由。

检查包括：导出错误、重复模型、页面链接、图片链接、未解析标签，以及微光嬗变选项和SCP-294收费/复活规则。玩法或数值变更后重新导出并审阅差异；确认图片与条目同步，再分别提交主仓库和 Wiki。

## 事件条件与资源数值

`event-requirements.json` 维护八个事件的出现幕数、前置、重复限制和选项要求；修改机制时同时核对对应源文件。事件总表和各条目的条件区块均由此文件生成，缺少任一事件会直接中止生成。

`format-text.mjs` 将连续能量／星星图标转换为数量，保留数字加单个单位图标的写法，包括0费用。检查工具会验证吊灯、迷你储君、0费用卡牌与事件条件，避免把图标重复写成文字。
