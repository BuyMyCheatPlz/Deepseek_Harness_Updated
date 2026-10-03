# Agent Note: 移除打包会话 fixture 分支迁移器

Status: implemented

[English](2026-07-26-remove-packed-session-fixture-migrator.md) | 中文

## 问题

仓库的默认写入器和快照检查会使会话 fixture（测试前置数据）保持规范打包行布局。在永久强制机制之外仍保留 `pnpm run migrate:packed-session-fixtures`，唯一原因是让携带旧版 fixture 改动的在途分支可以合并当前 `master`，并在不重新录制模型输出的情况下通过机械转换收敛。

一旦每个此类分支均已合并、关闭或符合规范，写入命令及其分支收敛指引便不再有持续维护者。过渡结束后继续保留会修改仓库内容的命令，会在永久只读快照检查旁增加第二条看似有效的维护路径。

## 决策

移除临时 CLI `scripts/migrate-packed-session-fixtures.ts`，以及根包提供的 `migrate:packed-session-fixtures` 命令。移除测试政策、ACP 快照 README 和已实现打包行 Agent Note 中指向该过渡命令的链接，并将 `scripts/session-fixture-layout.snapshot.ts` 中仅适用于该命令的修复指引替换为与具体命令无关的规范布局指引。

保留 `scripts/session-fixture-layout.ts`、其单元测试和 `scripts/session-fixture-layout.snapshot.ts`。它们定义并强制执行永久规范布局；仅移除面向分支的写入器。

## 曾考虑的替代方案

**无限期保留该命令。** 这会让旧 fixture 转换更方便，但也会在唯一已知迁移窗口关闭后，留下一个仓库级写入工具。只读门禁已经提供可长期保留的行为与诊断。

**随 CLI 一同移除规范布局转换模块。** 该模块不是过渡残留：快照 CI 使用它发现未来 fixture、解码混合物理记录，并与规范打包表示进行比较。移除该模块也会移除强制机制。

**打包行进入 `master` 后立即删除命令。** 较旧的开放分支在调整目标分支后，只能使用临时脚本或手动重新生成快照，这会增加冲突风险，也会让解码事件保真度更难评审。

## 后果

仓库不再包含分支迁移写入命令和临时收敛说明。签入的 fixture 永久由只读规范布局转换器与 `scripts/session-fixture-layout.snapshot.ts` 约束。未来任何引入非规范会话 JSONL 的分支都会在无密钥快照门禁中失败，且必须编写或重新录制规范打包行。
