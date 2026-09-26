# GitHub Release Updates 架構契約

## 責任與依賴

```text
SDK caller → GitHubReleaseUpdatePlugin → GitHubReleaseClient → HttpClient
                                      → GitHubReleasePolicy → wire models
```

入口擁有 client，將例外／回應轉為 SDK 結果與 host 日誌。Client 擁有並釋放 HttpClient，只負責 request headers、10 秒 HTTP timeout、JSON decoding；Policy 不操作網路、檔案或 host。此插件只查 latest release，宿主負責判斷是否更新與後續操作。

| 需求 | 修改位置 | 必要測試 |
| --- | --- | --- |
| HTTP headers、transport／JSON | [GitHubReleaseClient](GitHubReleaseClient.cs) | ReleaseBehaviorTests.NormalizesRequestAndPreservesHeaders、HttpFailureReturnsNullAndLogs |
| GitHub URL、tag、asset、changelog | [GitHubReleasePolicy](GitHubReleasePolicy.cs) | ReleaseBehaviorTests 的 URL、asset、changelog cases |
| SDK capability、host logging | [入口](GitHubReleaseUpdatePlugin.cs) | GitHubReleaseUpdatePluginTests、ArchitectureTests |
| 發布／依賴驗證 | [驗證入口](eng/Verify-Plugin.ps1) | build/release workflows 都執行相同 gate |

## 必須保留的行為

- 公開型別只有 `SceneGallery.Plugin.GitHubReleaseUpdates.GitHubReleaseUpdatePlugin`，保留無參數 constructor、IPluginUpdateProvider、IDisposable、名稱及 update metadata。
- GitHub repository/API URL 正規化成 HTTPS latest API；tag 只移除單一 v/V。
- plugin name 移除非字母數字後形成 assembly suffix；無版本 DLL 優先，其次精確版本 DLL；大小寫不敏感。維持任意非空 asset URL 的既有行為。
- Changelog trim 後最多 2000 字再加 `...`。不比較 request.CurrentVersion，不下載或安裝。
- Caller cancellation 與 HTTP timeout 的 OperationCanceledException 向外傳播；其他失敗記錄後回傳 null。畸形 JSON／null asset 的既有無更新行為保留。
- SDK 套件精確版 `1.3.0`、runtime assembly identity `1.0.0.0`、compile-only、PrivateAssets=all；宿主是唯一 runtime SDK 來源。正式產物不帶 SDK、Common runtime 或測試依賴。

## 決策：2026-09-22

沿用一個 shipping DLL，抽 internal client/policy/models，沒有引入插件基底或共用 cache。NetArchTest.Rules 1.3.2 僅存在測試專案；ArchitectureTests 固定公開能力、完整 top-level 型別 inventory（含子命名空間）及依賴方向，並以 ordinary/static/async 禁止案例驗證規則有效。新增型別須指定責任與相應依賴限制；變更上述契約時，同時修改本決策、相容性證據和測試，不能只更改 allowlist。

2026-09-26 依使用者選擇對齊宿主的 SDK `1.3.0` 套件基線；此版維持 assembly identity `1.0.0.0`。只調整 compile-only/test PackageReference 與對應驗證，公開能力、入口方法和 release policy 不變。

架構規則與回歸測試：[ArchitectureTests](SceneGallery.Plugin.GitHubReleaseUpdates.Tests/ArchitectureTests.cs)、[ReleaseBehaviorTests](SceneGallery.Plugin.GitHubReleaseUpdates.Tests/ReleaseBehaviorTests.cs)。
