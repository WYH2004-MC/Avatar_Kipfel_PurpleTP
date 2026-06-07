< 導入方法 >
0. Modular Avatar, liltoonをインポート・最新に更新してください！
1. LaboLab > PBCatcherからPBCatcher.prefabをアバター直下に入れる
2. 必要に応じて位置を調整する
　　prefab内のHandL_proxy, HandR_proxyにある「Object」を動かして位置・サイズを調整してください
    (参考画像：sankou1.png)
3. 後はアップロードするだけ！


※マジックハンドが手に追従しない場合
HandL_proxy, HandR_proxyを選択し、inspectorからMA Bone Proxyを探してください
MA Bone Proxy内のターゲットを手のボーンに設定してください


※掴み判定が変だなと感じた場合
VRChatで丸いメニュー(エクスプレッションメニュー出すときのやつ)から
オプション > アバター > アバターのオーバーレイ > PhysBones
を押してください
手かマジックハンドに赤い球が表示されると思います、それがPhysBoneの掴み判定です

あまりにもマジックハンドのアーム部分からズレている場合は、
Hand_proxy > Object にある「ArmPos」の位置を調整してください
(参考画像：sankou2.png)


※マジックハンド自体は動くが、掴み判定が移動していない場合
Modular Avatarが最新でない可能性があります
バージョンが1.15.0以降になっているか確認してください