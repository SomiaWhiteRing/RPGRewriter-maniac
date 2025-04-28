これは動作テスト版です。
パッチの適用は自己責任のもとおこなってください。
また、データのバックアップはこまめに取るようお願いします。
内容は中上級者向けです。
不具合等ありましたら報告して頂けると助かります(下のほうに連絡先があります)。


ゲームエンジンについては部分的な更新ではどうしても誤検知率が上がってしまうため全更新しました。



◆概要：
    Steamで販売されているRPG Maker 2003のver1.12aにいくつかの機能を付け加えるパッチです。
    全体的に自作システムを意識した内容になっています。
    
    適用方法は日本語化パッチと同様です。
    特にこだわりがなければ「utility.exe」を使うと便利かもしれません。
    
    今回は英語版と日本語版の2種類あり、前者は元ファイルが英語版の場合のみ適用可能です。

    
    
◆利用規約：    
    以下のスレッドにエンドユーザーライセンス契約が載っています。
    内容に同意する場合のみパッチを使用できます。
    http://steamcommunity.com/app/362870/discussions/0/541906348059148217/    

    ライセンスに従った制作物なので合法ですがあくまで非公式の拡張である事に注意してください。
    
    
    
◆同梱ファイルの説明：
    ●command_code.txt
        イベントコマンドのコード一覧です。
    
    
    ●LICENSE.txt
        エディタのプラグイン機能の作成に使用したライブラリのライセンス表記です。

        
    ●readme.txt
        このテキストファイルです。

        
    ●update.txt
        パッチの更新履歴です。

    
    ●utility.exe
        パッチ適用の補助ツールです。

    
    ●vexpr.txt
        「変数の操作」に導入された式の説明です。


    ●Common
        日本語・英語版共通のファイル群です
        
        ○accord.dll
            ogg/vorbisの再生用のライブラリで、エディタとゲームエンジンとで同じものを使用します。
            「rpg2003.exe」「RPG_RT.exe」と同じディレクトリにおくことで機能を利用できます。
            
            stb_vorbisというデコーダをもとにしています。
            stb_vorbisは使用に際してMITとパブリックドメインの2種類のライセンスを選択できます。
            今回は利用形態を考えてパブリックドメインを選択したため再配布時に表記が必要になることはありません。
            ※accord.dll自体はパブリックドメインではありません。
            
        ○backupper.exe
            ゲームプロジェクトのバックアップを取るアプリケーションです。
            エディタと同じフォルダに配置するとメインメニューから起動できます。

        ○battle_command_list.txt
            標準のデフォ戦コマンドリストを正規の順に記載したテキストです。
            
        ○cmdcs.dll
            エディタのプラグイン機能を使用するために必要なライブラリです。
            
        ○cmdl_editor.exe
            エディタのコマンドリストを編集するアプリケーションです。
            エディタと同じフォルダに配置するとコマンド編集画面の右クリックから起動できます。
            
        ○command_list.txt
            現行のコマンドリストを正規の順に記載したテキストです。
            「cmdl_editor.exe」はここからコマンド名を読み取ります。
            
        ○Footy2.dll
            TPCとの連携時に使用するテキストエディタコンポーネントです。
        
        
    ●English
        英語版の拡張パッチです。
        
            
        ○cmdTos.dll
            エディタのイベントコマンドをテキスト化する処理や、プラグイン機能の橋渡しを担うライブラリです。            
            このファイルを消去すると旧式のテキスト化処理に戻すことが出来ます。            
        
        ○patch.exe
            エディタ用のパッチ(WDiff製)です。
            直接使用したい場合はエディタと同じディレクトリに置いて実行してください。
            
        ○RPG_RT.exe
            ゲーム実行ファイルです。
            負荷の軽減が利点ですが、イベントページ切り替えのタイミングを考慮する必要があります。

        ○Plugin
            プラグインを格納するフォルダです（プリセットのプラグインがないため空です）。
        
    
    ●patch_jp
        日本語版の拡張パッチです。
        英語版を日本語化する処理も含まれます。
        ファイル構成は英語版と同様です。
    
    
    
◆参考URL：
    ●RPG Maker Web
        http://www.rpgmakerweb.com/
        
    ●RTP (RPG Maker Web)
        http://www.rpgmakerweb.com/download/additional/run-time-packages

    ●Steamのストアページ
        http://store.steampowered.com/app/362870/RPG_Maker_2003/

    ●RPG Maker 2003 総合掲示板
        http://steamcommunity.com/app/362870/discussions/
        
    ●RPG Maker 2003 - Patch EULA
        http://steamcommunity.com/app/362870/discussions/0/541906348059148217/
            
    ●電脳亜空間　阿闍梨のページ
        (WDiff制作者のwebページです)
        http://www008.upp.so-net.ne.jp/ajari/
        
    ●stb_vorbis
        http://www.nothings.org/stb_vorbis/
        https://github.com/nothings/stb

        
        
◆制作：
    氷山羊
    
    Twitter(X): @BingShan1024
    メール: bingshanyang@gmail.com
    制作室: discord.gg/5NnbMtQ

