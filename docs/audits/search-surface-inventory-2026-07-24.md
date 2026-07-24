# Search and query surface inventory · 搜尋與查詢介面清單

Generated from the current WinUI XAML by `tools/New-SearchSurfaceInventory.ps1`. The inventory deliberately separates ordinary product search from domain-specific query languages and configuration fields; a specialized field is never silently reinterpreted as .NET regex. · 呢份清單由現時 WinUI XAML 自動產生，刻意分開一般產品搜尋、專用查詢語言同設定欄位；專用欄位絕對唔會靜靜雞改成 .NET 正則。

Total candidate controls: **93** across **74** XAML files. · 候選控制項總數：**93**，分佈喺 **74** 個 XAML 檔案。

| Classification | Count | Meaning |
| --- | ---: | --- |
| dedicated-pattern-tool | 7 | Configuration or purpose-built pattern editor, not a product search bar. |
| integrated-core | 8 | Shared plain-text-first SearchPatternBox is active; the page uses the synchronized .NET pattern and flags. |
| plain-text-later | 64 | Applicable ordinary local search/filter, scheduled for later batches. |
| read-only-output | 2 | Output field, not editable search input. |
| shared-control-internal | 3 | Internal editor in the reusable synchronized search component, counted once as infrastructure rather than as another product surface. |
| specialized-dialect | 9 | Requires a domain/provider adapter; do not force .NET regex semantics. |

## Complete classified inventory · 完整分類清單

| Source | Control | Classification | Status | Notes |
| --- | --- | --- | --- | --- |
| `Controls/SearchPatternBox.xaml:20` | `QueryBox` | shared-control-internal | infrastructure | Primary query editor owned by the reusable synchronized search component. |
| `Controls/SearchPatternBox.xaml:59` | `RawPatternBox` | shared-control-internal | infrastructure | Raw regex editor owned by the reusable synchronized search component. |
| `Controls/SearchPatternBox.xaml:98` | `SampleBox` | shared-control-internal | infrastructure | Session-only preview sample owned by the reusable synchronized search component. |
| `MainWindow.xaml:63` | `SearchBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/AiChatModule.xaml:35` | `ChatSearchBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/AndroidAdbModule.xaml:167` | `LogTagBox` | specialized-dialect | adapter-required | ADB logcat tag selector; preserve logcat semantics. |
| `Pages/AppLauncherModule.xaml:38` | `SearchBox` | integrated-core | shipped | External app catalog search. |
| `Pages/AppUninstallerModule.xaml:34` | `FilterBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/ArchivesModule.xaml:112` | `OpsFilter` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/AsciiTableModule.xaml:29` | `SearchBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/AudioEditorModule.xaml:199` | `FxFilter` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/AudioTaggerModule.xaml:206` | `FromNamePattern` | dedicated-pattern-tool | not-applicable | Tag-to-filename template, not a search bar. |
| `Pages/AudioTaggerModule.xaml:218` | `ToNamePattern` | dedicated-pattern-tool | not-applicable | Filename-to-tag template, not a search bar. |
| `Pages/AwsCliModule.xaml:234` | `GlobalSearchBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/AwsCliModule.xaml:369` | `HomeResourceSearchBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/AwsCliModule.xaml:396` | `ResourceQueryBox` | specialized-dialect | adapter-required | AWS/JMESPath resource query; never reinterpret as .NET regex. |
| `Pages/AwsCliModule.xaml:464` | `S3BucketFilterBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/AwsCliModule.xaml:563` | `Ec2FilterBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/AwsCliModule.xaml:625` | `ConsoleServiceSearch` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/AwsCliModule.xaml:724` | `ServiceFilter` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/AwsCliModule.xaml:725` | `OperationFilter` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/BulkOpsModule.xaml:43` | `PatternBox` | dedicated-pattern-tool | not-applicable | Bulk file-operation pattern configured with the page mode. |
| `Pages/CategoryPage.xaml:26` | `FilterBox` | integrated-core | shipped | Per-category tweak catalog search. |
| `Pages/CharMapModule.xaml:25` | `SearchBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/CloudflareModule.xaml:53` | `OpsFilter` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/ColorNameModule.xaml:57` | `FilterBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/ConnectionsModule.xaml:31` | `FilterBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/DashboardPage.xaml:63` | `SearchBox` | integrated-core | shipped | Core dashboard catalog search. |
| `Pages/DecompilerModule.xaml:41` | `SearchBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/DevicesModule.xaml:27` | `FilterBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/DnsRefModule.xaml:25` | `SearchBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/DockerSshModule.xaml:98` | `SearchBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/EmojiModule.xaml:31` | `SearchBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/EmulatorModule.xaml:129` | `FilterBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/EventViewerModule.xaml:37` | `FilterBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/EverythingSearchModule.xaml:33` | `SearchBox` | specialized-dialect | adapter-required | File-index query with its own regex/provider contract; needs a dedicated adapter. |
| `Pages/FancyZonesModule.xaml:138` | `OpsFilter` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/FlashcardsModule.xaml:132` | `CardSearchBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/GitHubModule.xaml:596` | `OpsFilter` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/GlazeWmModule.xaml:194` | `OpsFilter` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/GlobTesterModule.xaml:20` | `PatternBox` | dedicated-pattern-tool | not-applicable | Glob dialect test input. |
| `Pages/GlobTesterModule.xaml:44` | `RegexBox` | read-only-output | not-applicable | Read-only generated regex output. |
| `Pages/HarAnalyzerModule.xaml:52` | `FilterBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/HexEditorModule.xaml:73` | `FindBox` | specialized-dialect | adapter-required | Hex/text byte search with a selected encoding mode. |
| `Pages/HomeAssistantModule.xaml:170` | `ToggleSearch` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/HttpHeaderRefModule.xaml:31` | `SearchBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/HttpStatusModule.xaml:23` | `SearchBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/JsonPathModule.xaml:29` | `QueryBox` | dedicated-pattern-tool | not-applicable | JSONPath dialect test input. |
| `Pages/KeePassModule.xaml:94` | `SearchBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/KomorebiModule.xaml:190` | `OpsFilter` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/LibreOfficeModule.xaml:97` | `FilterBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/LibreOfficeModule.xaml:158` | `OpsFilter` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/LicensesPage.xaml:38` | `SearchBox` | integrated-core | shipped | License and source notice search. |
| `Pages/MailModule.xaml:50` | `SearchBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/ManualPage.xaml:46` | `FilterBox` | integrated-core | shipped | In-app manual search. |
| `Pages/MediaModule.xaml:371` | `OpsFilter` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/MimeTypesModule.xaml:20` | `SearchBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/NilesoftShellModule.xaml:148` | `OpsFilter` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/NotesModule.xaml:38` | `SearchBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/OpenSourceAppHubModule.xaml:50` | `SearchBox` | integrated-core | shipped | Native OSS clone catalog search. |
| `Pages/PackageManagerModule.xaml:69` | `SearchBox` | specialized-dialect | adapter-required | Remote package-provider query; local regex requires a provider-aware result adapter. |
| `Pages/PackerModule.xaml:175` | `OpsFilter` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/ProcessExplorerModule.xaml:84` | `SearchBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/QBittorrentModule.xaml:86` | `SearchBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/RainmeterModule.xaml:44` | `SkinFilter` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/RainmeterModule.xaml:155` | `OpsFilter` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/ReactorSettingsModule.xaml:97` | `HaAlarmSearchBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/ReactorSettingsModule.xaml:105` | `HaGenLightsSearchBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/ReactorSettingsModule.xaml:113` | `HaGenSwitchesSearchBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/RegexCheatModule.xaml:26` | `SearchBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/RegexTesterModule.xaml:20` | `PatternBox` | dedicated-pattern-tool | not-applicable | Dedicated full .NET regex builder/tester already shipped. |
| `Pages/RenameModule.xaml:41` | `FindBox` | specialized-dialect | adapter-required | Rename transformation input; preserve its explicit rename/regex semantics. |
| `Pages/ScheduledTasksModule.xaml:27` | `FilterBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/SearchResultsPage.xaml:20` | `SearchBox` | integrated-core | shipped | Combined modules and tweak search. |
| `Pages/ServicesModule.xaml:27` | `FilterBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/SettingsHubModule.xaml:30` | `FilterBox` | integrated-core | shipped | In-app and Windows settings catalogs. |
| `Pages/ShortcutGuideModule.xaml:91` | `SearchBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/SshModule.xaml:158` | `OpsFilter` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/StartupModule.xaml:27` | `FilterBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/SymbolsModule.xaml:30` | `SearchBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/SystemMonitorModule.xaml:155` | `SearchBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/TaskbarTweakerModule.xaml:58` | `FilterBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/TextReplaceModule.xaml:54` | `(unnamed)` | dedicated-pattern-tool | not-applicable | Find/replace rule editor inside a data template; preserve its explicit regex option and replacement semantics. |
| `Pages/TorrentModule.xaml:47` | `SearchBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/UrlToolsModule.xaml:63` | `QueryField` | read-only-output | not-applicable | Read-only parsed URL query output. |
| `Pages/ViveToolModule.xaml:51` | `FilterBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/VsCodeModule.xaml:194` | `ExtFilterBox` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/VsCodeModule.xaml:241` | `OpsFilter` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/WindhawkModule.xaml:68` | `ModFilter` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |
| `Pages/WiresharkModule.xaml:89` | `CaptureFilterBox` | specialized-dialect | adapter-required | BPF capture-filter dialect. |
| `Pages/WiresharkModule.xaml:91` | `DisplayFilterBox` | specialized-dialect | adapter-required | Wireshark display-filter dialect. |
| `Pages/WiresharkModule.xaml:219` | `FileFilterBox` | specialized-dialect | adapter-required | Wireshark display-filter dialect for saved captures. |
| `Pages/WiresharkModule.xaml:320` | `OpsFilter` | plain-text-later | remaining | Local plain-text search/filter; eligible for the shared control in a later integration batch. |

The CSV beside this page is the machine-readable ledger. Regenerate both files after adding, removing, renaming, or integrating a candidate surface. · 同目錄 CSV 係機器可讀 ledger；新增、刪除、改名或整合候選介面後要重新產生兩份檔案。
