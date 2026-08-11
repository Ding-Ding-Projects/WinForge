# Local OTP QR pairing · 本機 OTP QR 配對

## Behavior · 行為

The TOTP page can generate a QR code locally from the current registration values. The encoded `otpauth://totp/` URI includes issuer, account, secret, algorithm, digit count, and period, and the same values remain visible as a text alternative. QRCoder renders the bitmap in-process; no network request or remote QR service is used. · TOTP 頁面可以按照目前登記值喺本機產生 QR code。編碼嘅 `otpauth://totp/` URI 包括 issuer、account、secret、algorithm、digit count 同 period，同一批資料亦會以文字 alternative 顯示。QRCoder 喺 process 內畫 bitmap，唔會上網或者用 remote QR service。

The registration card is a draft pairing surface, not a claim that the complete authenticator contract is finished. It does not yet provide a multi-entry vault, QR image import, camera scan, pairing-code confirmation, or an ordinary secrets export path. · 登記卡目前係 pairing 草稿介面，唔代表完整 authenticator 合約已完成。佢暫時未提供多項 vault、QR 圖片匯入、相機掃描、配對 code 確認或者普通 secrets export path。

## Security and failure modes · 安全同失敗處理

- The QR bitmap stays in memory and is not written to disk, logs, history, telemetry, or exports.
- The credential value is handled through the existing local TOTP state; later authenticator work must move stored secrets to the operating-system credential vault and keep ordinary exports secret-free.
- A malformed or unsupported value produces a local status message and no QR output.

## Verification · 驗證

The surface is `Pages/TotpModule.xaml(.cs)` and the package reference is `QRCoder` in `WinForge.csproj`. Verification must decode the rendered QR, compare every URI parameter with the visible values, assert no network call, and cover invalid values and all language modes. · 介面係 `Pages/TotpModule.xaml(.cs)`，package reference 喺 `WinForge.csproj` 嘅 `QRCoder`。驗證要 decode 真 QR、比較 URI 每個 parameter 同畫面值、assert 冇 network call，並覆蓋無效值同所有語言模式。

## Suggested articles · 建議文章

- [Shared settings](shared-settings.md) — language and emoji behavior.
- [Offline changelog](offline-changelog.md) — documenting shipped feature state.
- [Managed release contract](../delivery/managed-release-contract.md) — unsigned release asset handling.
