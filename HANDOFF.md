# GitHub Release Updates 交接

- 交接版本：v1（2026-09-22）
- 必讀：[架構與修改地圖](ARCHITECTURE.md)、[agent 規則](AGENTS.md)。
- 原工作樹無既有變更；2026-09-22 重構驗證階段未提交、推送或發布。

## 完成範圍

入口拆成 SDK adapter、GitHubReleaseClient、GitHubReleasePolicy；保留原公開介面、metadata 和 release asset 規則。沒有變更下載 URL 限制、錯誤分類、安裝或版本比較行為。

新增 24 個行為案例，在拆分前後各 29 個測試通過；新增公開契約／分層檢查及其自我測試。Build 與 Release 都使用 [Verify-Plugin](eng/Verify-Plugin.ps1)，驗證時關閉自動部署。

## 可重現驗證

獨立 checkout 且已取得原有 GitHub Packages 讀取權限：

```powershell
pwsh -NoProfile -File eng/Verify-Plugin.ps1
```

尚未遠端发布時，在五 repo 工作區明確使用既有 local-feed config；下列命令使用新的隔離輸出與套件 cache，不改 committed NuGet.config：

```powershell
pwsh -NoProfile -File eng/Verify-Plugin.ps1 -NuGetConfig ../KoikatsuSceneGallery/eng/PackageValidation/NuGet.config -ArtifactsPath ../validation/updater-local -PackagesPath ../validation/updater-local/packages
```

本次結果：拆分前 29/29、拆分後行為測試 29/29；最終含架構測試 **37/37 通過**，正式產物／MSBuild 套件契約／文件連結 gate 通過。行為基線 TRX 保存在工作區 validation/plugin-refactor-20260922 下，完整 gate 的 TRX 在測試專案 TestResults，不屬發布產物。

2026-09-26 相容性基線更新：依使用者選擇，將 shipping/test 的 SDK 套件參考與驗證腳本調至 `1.3.0`；SDK assembly identity 仍為 `1.0.0.0`，架構測試直接檢查此條件。以上 37 項為 9/22 舊套件的歷史結果。新版隔離 gate 使用 `validation/plugin-refactor-20260926/updater-sdk13`，**37/37 通過**，含架構、套件、shipping output 與文件檢查；TRX 在該目錄 `TestResults`。沙箱拒讀使用者 Roaming NuGet.Config，因此把 process `APPDATA` 指向隔離目錄並使用明確 local-feed config；未改正式套件來源。release 查詢行為、公開 API 與發佈形式沒有變動。

最終架構收尾新增完整 production top-level 型別清單，根命名空間與子命名空間皆受檢查；加入後全工作區隔離 gate 的 Updater **38/38 通過**。證據在主程式 `artifacts/plugin-workspace/d2ba7560c24246ecb11a173b2518d9f3/GitHubReleaseUpdatePlugin`；前述 37 項為新增清單測試前的歷史結果。

## 限制與後續

所有 HTTP 測試均離線；未呼叫真實 GitHub release API。2026-09-22 重構驗證階段尚未執行遠端 CI／Release，正式結果見下方發布記錄。上游畸形 assets 目前保留既有 null 結果；若要分類診斷需獨立缺陷修正與測試。

沒有儲存格式或資料遷移。部署回復可用先前插件 DLL；設定與 SDK 契約不變。發布前必須在乾淨 checkout 跑同一驗證入口。

## 發布準備（2026-09-27）

使用者已授權提交、推送與插件新版發布。遠端 `master` 與本地基線 `e2a1be0` 一致，現有最新 Release 為 `0.0.3`；下一版採 `v0.0.4` tag，符合 Release workflow 的 `v*` 觸發條件。Workflow 會將 tag 轉為 DLL 版本 `0.0.4` 與資產名稱 `SceneGallery.Plugin.GitHubReleaseUpdates-0.0.4.dll`。以 `-Version 0.0.4`、明確 local-feed config、隔離輸出重跑 [驗證入口](eng/Verify-Plugin.ps1)：38/38 通過，package/正式輸出/文件檢查通過；結果保存在 workspace `validation/release-20260927/updater-build-serial`。此輪沙箱需將 process APPDATA 指向隔離目錄並停用 Roslyn shared compilation，避免使用者 NuGet.Config 權限及並行編譯檔案存取錯誤。

遠端已先發佈 SDK 1.3.0、Common/Secrets 0.2.0，再推送本插件提交 `3fd3de28bd28fb840abdeb0feab71792c384770d`。正式 GitHub Packages 還原與驗證的 [Build CI](https://github.com/LowTechMaker/GitHubReleaseUpdatePlugin/actions/runs/36270019868) 成功；其後 `v0.0.4` annotated tag 指向該提交，[Release CI](https://github.com/LowTechMaker/GitHubReleaseUpdatePlugin/actions/runs/36270144789) 再次驗證、打包與建立 [GitHub Release](https://github.com/LowTechMaker/GitHubReleaseUpdatePlugin/releases/tag/v0.0.4)，均成功。發布的 `SceneGallery.Plugin.GitHubReleaseUpdates-0.0.4.dll` 為 13312 bytes，GitHub asset SHA256 為 `272b097e36ad336685356291f8677358a1111aa60fe7ba27f265cb7bca069d5b`。

以公開宿主 v0.4.0 Release ZIP 中實際 SDK DLL 對候選插件作獨立載入 smoke：5 個 SDK 型別及 4 個成員引用均可解析，能建立 `IPlugin` 實例，回報 Name=`GitHub Release Updates`、Version=`0.0.4`。此檢查沒有對真實 GitHub API 發出請求；上述離線測試限制仍然存在。此文件的結果補記提交位於發版 tag 之後，tag 保持指向已通過 Release CI 的程式提交。
