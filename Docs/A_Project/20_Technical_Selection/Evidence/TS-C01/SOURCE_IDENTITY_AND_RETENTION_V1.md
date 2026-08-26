# TS-C01 Source Identity and Retention V1

状态：`SOURCE NOT FROZEN — BINARY HASHES AUTHORITATIVE FOR EXISTING RUNS`

- branch：`impl/Refactor`；
- parent HEAD：`83962bf2f0786b68951ef5bf5f020666a1ea1c46`；
- 盘点时工作树存在已修改和未跟踪的 R4/R5、adapter、installer 与 lifecycle 文件；
- 不同人工阶段使用不同 EXE SHA-256；
- 因此历史 run 的直接身份是各自 `session.json` 中的 EXE 哈希，而不是当前 HEAD；
- 当前工作树不能反向冒充历史已测试源码；
- 未经用户明确要求，不在 R5 自动 commit、push 或删除原始 run；
- 最终选择若依赖 Qt，必须先冻结一个源码身份并用该身份重新执行所有要求回归。

保留策略：在 R6 用户处置完成前保留 `prototype/renderer-adapters/ts-c01-qt-quick/out/`。若需释放空间，先把完整性清单所列原始文件迁入受控归档并复核 SHA-256。