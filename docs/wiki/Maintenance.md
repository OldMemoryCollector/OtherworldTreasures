# 图鉴维护

[返回首页](Home.md)

图鉴由游戏运行时导出的模型与中文本地化文本生成。页面同时保存在主仓库的 docs/wiki 目录与独立的 GitHub Wiki 仓库。

维护工具位于 tools/wiki。先用隔离测试实例运行 ExportCatalog.cs.txt 导出 catalog.json 和 images，再运行 build-wiki.mjs。生成前要求导出错误数为0；发布前检查条目数量、图片与所有内部链接。

每次功能完成并验证后提交代码；涉及玩法、数值或获取方式时同步更新图鉴。
