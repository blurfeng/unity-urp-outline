# Unity URP Outline

<p align="center">
  <img alt="GitHub Release" src="https://img.shields.io/github/v/release/blurfeng/unity-urp-outline?color=blue">
  <img alt="GitHub Downloads (all assets, all releases)" src="https://img.shields.io/github/downloads/blurfeng/unity-urp-outline/total?color=green">
  <img alt="GitHub Repo License" src="https://img.shields.io/badge/license-MIT-blueviolet">
  <img alt="GitHub Repo Issues" src="https://img.shields.io/github/issues/blurfeng/unity-urp-outline?color=yellow">
</p>

<p align="center">
  🌍
  <a href="./README.md">中文</a> |
  <a href="./README_EN.md">English</a> |
  日本語
</p>

<p align="center">
  📥
  <a href="#インストール">インストール</a> |
  <a href="#ダウンロード">ダウンロード</a>
</p>

## プロジェクト概要
本プロジェクトは **Unity 2022.3.62f3** の **URP (14.0.12)** レンダリングパイプラインを用いて、アウトライン効果を実装しています。  
Shader は **UV サンプリングと畳み込み計算** によりアウトラインを描画します。  
アウトライン機能は **UPM パッケージ**（`com.fs.outline`）として提供されており、自身の URP プロジェクトに直接インストールして利用できます。

![](Documents/OutlineDemo.gif)

## インストール
アウトライン機能はリポジトリのサブフォルダ `Assets/Plugins/FsOutline` にあり、UPM パッケージとして提供されています。**Universal RP 14.0.12**（`com.unity.render-pipelines.universal`）に依存するため、プロジェクトで URP が有効になっていることを確認してください。

### 方法 1：UPM パッケージとしてインストール（推奨）
プロジェクトで **Window → Package Manager** を開き、左上の **[+] → Add package from git URL** をクリックして、次の URL を入力します：

```
https://github.com/blurfeng/unity-urp-outline.git?path=/Assets/Plugins/FsOutline
```

> 💡 URL の末尾に `#<ブランチまたはタグ>` を追加するとバージョンを固定できます（例：`...FsOutline#main`）。

### 方法 2：リポジトリ全体をサンプルプロジェクトとして clone
本リポジトリ自体が設定済みのサンプルプロジェクトです。clone して **Unity 2022.3.62f3** で開くと、アウトラインの完全な設定とデモシーンを確認できます。

```
git clone https://github.com/blurfeng/unity-urp-outline.git
```

## ダウンロード
UPM でのインストール以外に、GitHub Releases からパッケージをダウンロードして手動でインポートすることもできます。

[Releases](https://github.com/blurfeng/unity-urp-outline/releases) ページで最新版の `.unitypackage` をダウンロードし、Unity で **Assets → Import Package → Custom Package...** から選択してインポートしてください。

> 💡 手動でインポートしたパッケージはリポジトリと連動して自動更新されません。バージョン管理が必要な場合は、上記の UPM 方式を推奨します。

## 使用方法
本プロジェクトはすでに基本設定済みです。直接利用することもできますし、以下の手順で自身のプロジェクトに統合することも可能です。

### 1. Renderer Feature を追加
使用中の **Universal Renderer Data** で **[Add Renderer Feature]** ボタンをクリックし、Outline Renderer Feature を追加します。  
Inspector でパラメータを設定してください。**Rendering Layer Mask** により、どのレンダリングレイヤーのオブジェクトがアウトライン描画されるかが決まります。

![](Documents/RendererFeature.png)

### 2. 描画対象オブジェクトの Rendering Layer Mask を設定
対象オブジェクトの **Mesh Renderer → Additional Settings → Rendering Layer Mask** でレンダリングレイヤーを設定します。  
Renderer Feature の **Rendering Layer Mask** に対応するレイヤーを指定してください。  

🎉 設定が完了すると、オブジェクトにアウトラインが表示されます。

![](Documents/RenderingLayerMask.png)

### 3. Volume による実行時のアウトライン設定変更
実行時にアウトライン効果を変更したい場合は、**Volume** に **Outline** コンポーネントを追加し、Override を有効化します。  
この場合、Volume の設定が Renderer Feature のデフォルト設定を上書きします。

![](Documents/Volume.png)

## サポートされるパラメータ
- **HDR カラー**：アウトラインに使用する HDR カラー。発光効果も表現可能です。  
- **アウトライン幅**：UV サンプリングによって実装。調整範囲を拡張しました（上限 0.05）。幅を大きくすると、直角や四角形のエッジ部分で破綻が発生する可能性があります。  
- **不透明度**：アウトライン全体の濃さ（0〜1）。  
- **エッジの硬さ**：エッジを滑らかな（アンチエイリアス）状態から鋭い状態まで調整でき、べき乗として整形します（>1 で鋭く細く、<1 で柔らかく太く）。  
- **内部への深さ**：アウトラインがオブジェクト内部へにじむ深さ。大きいほど深く、小さいほど外縁に沿います。  
- **レンダリングレイヤーマスク**：アウトラインを描画するオブジェクトをレンダリングレイヤーで制御します。  
- **Render Pass Event（注入タイミング）**：アウトラインを描画するパイプライン上の段階。Renderer Feature レベルの設定です（Volume で実行時に上書きされません）。既定ではポストプロセス前に描画します。  

> 💡 カラー・幅・不透明度・エッジの硬さ・内部への深さ・レンダリングレイヤーマスクはすべて **Volume** で実行時に上書きできます。Render Pass Event は **Renderer Feature** レベルの設定です。

## Rendering Layers の説明
URP では **Rendering Layers** を用いてレンダリングレイヤーを制御・区別することができます。  
**Unity の標準 Layer ではなく Rendering Layers を使用することを推奨** します。その理由は以下の通りです：

- **Layer** は物理衝突やカメラカリングなどにも使用されるため、アウトライン処理と混用すると競合が発生する可能性があります。  
- **Rendering Layers** は物理やロジックから独立しているため、設定がより明確でシンプルになります。  

**Universal Render Pipeline Global Settings** で **Rendering Layers** を設定できます：  

![](Documents/RenderingLayers.png)

✅ **レンダリングレイヤーの自動読み込み**  
本パッケージは、現在有効な **URP アセット** から設定済みの Rendering Layers 名を自動的に読み込みます。  
付属の `RenderingLayerMaskDrawer` がレンダリングレイヤーマスクをチェックボックス式のドロップダウンとして描画し、その選択肢は上記の Global Settings で設定したレンダリングレイヤーそのものになります。  
そのため、Rendering Layers を変更すると自動的に選択肢へ反映され、スクリプトを手動で同期する必要はありません。  