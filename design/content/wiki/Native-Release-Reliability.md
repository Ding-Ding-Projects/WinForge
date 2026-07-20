# Native Release Reliability · 原生發佈可靠性

`native-release.yml` remains the sole C++ publisher: test-gated, immutable-tag retry-safe, exact-SHA/two-asset verified, and current-main-only for Latest. Site-data dispatches are non-canceling and retry their matching release. · `native-release.yml` 仍然係唯一 C++ publisher：一定要測試成功、不可變 tag retry 安全、驗確切 SHA／兩個 asset，而且 Latest 只畀 current-main。site-data dispatch 唔會中途取消，會 retry 對應 release。

Earlier hosted API-outage failures remain pending remote repair after this workflow is pushed; no managed publisher is added. · 較早 hosted API outage 失敗要等 workflow 推上去後遙距 repair；冇加入 managed publisher。
