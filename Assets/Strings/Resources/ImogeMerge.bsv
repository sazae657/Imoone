#ImogeMerge
PMG_Description2つのプロファイルの設定とDBをマージして新しいプロファイルを作る
PMG_OptCenturyマージ元のプロファイルCentury(衝突される側)。プロファイル名かフォルダー
PMG_OptBongoマージ元のプロファイルBongo(衝突する側)。プロファイル名かフォルダー
PMG_OptOut出力先フォルダー(存在しないか空であること)
PMG_OptName指定するとProfile.tomlにこの名前で登録する
PMG_E_Errorエラー: {0}
PMG_E_ProfileNotFoundプロファイル {0} が見つからない
PMG_E_DatumNotFound{0} が見つからない
PMG_E_SameProfile同じプロファイル同士はマージできない: {0}
PMG_E_OutputIsSource出力先がマージ元と同じ: {0}
PMG_E_OutputNotEmpty出力先 {0} が空じゃない
PMG_E_VersionMismatch{0} のバージョンが一致しない: A={1} B={2}
PMG_E_ProfileExistsプロファイル {0} は既に登録されておる
PMG_W_Running警告: ImogeIndexが起動中。マージ中に書き込まれた内容は反映されないですん
PMG_W_SettingDiffers警告: 設定 {0} が異なるのでAの値を使う (Century={1} Bongo={2})
PMG_W_MetaTypeConflict警告: メタデータムエイリアス {0} は {1} と型が食い違うので捨て申した
PMG_W_ExcludeOverlap警告: 除外パス {0} が検索パス {1} と重なっている。次回のインデックス生成でレコードが消える可能性がありますん
PMG_W_ExcludePatternAdded警告: 除外パターン {0} が両方のプロファイルに適用される
PMG_W_ModelMixed警告: TaggerかキャプショニングのLLMが異なるので、タグ/キャプションは複数のモデル由来が混在する
PMG_W_ColumnMismatch警告: テーブル {0} の列が一致しないので共通の列だけコピーしましたん
PMG_W_UnknownTable警告: テーブル {0} はマージ対象外
PMG_I_Mergingマージ中: Century={0} Bongo={1}
PMG_I_ResultCentury: {0}件 + Bongo: {1}件 (パス重複でスキップ: {2}件) → {3}件
PMG_I_RegisteredProfile.tomlに {0} を登録した
PMG_I_Done完了: {0}
PMG_I_Hint一度、ImogeIndexを実行して検索インデックスを作る事が必要でごわす
