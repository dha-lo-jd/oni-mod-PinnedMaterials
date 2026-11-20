# CLAUDE.md

ONI mod（建築メニューの素材リストで、ピン留めした素材を先頭に出す）。共通事項は HQ（親フォルダ）の `CLAUDE.md`、機能の概要は `README.md`。Build: `dotnet build PinnedMaterials.sln`（Debug は `Local.Build.props` の `ModInstallPath` へ配備。コンパイル確認だけなら `-p:ModInstallPath=`。Release は `release/PinnedMaterials` に配布用フォルダを作る）。

- ソースは `PinnedMaterials/`。`Core/PinStore.cs`（ピンの一覧。`mods/config/PinnedMaterials.json` に保存。PC 単位でセーブには入らない）、`Patches/MaterialSelectorPatches.cs`（`MaterialSelector.ElementSorter` の Postfix で並べ替え、`ConfigureScreen` の Postfix で各素材ボタンにピンを足す）、`Ui/PinToggleOverlay.cs`（ピンのボタン）、`Ui/PinDragHandle.cs`（ドラッグで並べ替え）。

## 実機検証
`PinnedMaterials/HarnessTests.cs`（`#if DEBUG`、タグ `pinnedmaterials`、7 件）: ピンの付与、ピンした素材が先頭に来て解除で元に戻ること、ピンの順序、建物をまたぐ共通性と複数スロット、ドラッグでの並べ替えと保存、設定ファイルへの保存と読み直し、未発見の素材が隠れたままであること。
実行: `GRAVITAS_EXCLUSIVE=1 python C:/Users/dha_1/ONI-Modding/GravitasTestHarness/Gravitas.TestProbe/probe.py run --project . --summary --tests '{"tags":["pinnedmaterials"]}'`
ゲームを起動し本番の mods.json を書き換えるので、実行前にユーザーに確認する。ほかの実行がゲームを使っていると `stage=busy` になるので、待って再試行する。
テストはプレイヤーの実際のピン（`mods/config/PinnedMaterials.json`）を一時的に書き換える。`Session` が元の内容（ファイルの有無も）を戻す。
テストが見ていないもの: ピンの見た目（スプライト、ホバー時の表示）、実マウスのドラッグ（ハンドラを直接呼んでいる）、ツールチップの文言。
`csproj` に Debug 専用のハーネス参照（`HarnessDllPath`、`Local.Build.props` に書く）がある。Release には入らない。
