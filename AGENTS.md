開工前先閱讀 [架構與修改地圖](ARCHITECTURE.md)，再閱讀 [交接與驗證](HANDOFF.md)。本 repo 可獨立 checkout，不要求 sibling repo 存在。

- 保留使用者未提交變更；先記錄 `git status`，只修改任務相關內容。
- 入口只做 SDK 適配與組裝；HTTP 在 client，選擇規則在 policy。不要加入下載、安裝或版本比較。
- 新增 top-level 型別須先列入 `ArchitectureTests` 的完整型別清單（包含子命名空間），再加對應依賴方向檢查。
- SDK 套件固定 `SceneGallery.PluginSdk 1.3.0`，runtime assembly identity 固定 `1.0.0.0`；同步維護專案參考、架構測試及驗證腳本，不得混淆兩種版號。
- 公開契約、依賴邊界或資產選擇政策有意變更時，先在 ARCHITECTURE.md 記錄決策及相容性證據，再更新對應測試。不可只刪除或放寬檢查。
- 缺陷修正先寫失敗測試。HTTP 測試使用假的 handler，禁止連外；取消必須向 caller 傳播。
- 完成後執行 `pwsh -NoProfile -File eng/Verify-Plugin.ps1`，更新 HANDOFF.md 的實際結果與未驗證項目。驗證不得啟用自動部署。
- 共通規範來源：[plugin-conventions](https://github.com/LowTechMaker/KoikatsuSceneGallery/blob/master/docs/plugin-conventions.md)。本地必要規則已收錄於上述文件，來源文件不可取代本 repo 的驗證。
