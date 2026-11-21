using System;
using System.Collections.Generic;
using System.Text;

namespace ImogeIndex.Web2.Captionize;

internal static class CaptionReference
{
    #region std
    public const string STD = """
time_of_day = "midday"
dominant_color_palette = "暖色系の木目と白い容器、茶色のスープ"
people_count = 0
keywords = ["ラーメン", "カップ麺", "箸", "容器", "木目"]
details.scene = "昼間の室内で、木目調のテーブルに置かれたカップ麺の容器。"
details.subjects = "ラーメンが入った容器と箸が配置されている。"
details.action = "ラーメンはスープに浸かっており、箸で食べようとしている状態。"
details.mood = "日常的でシンプルな食事の瞬間。"
details.shot_type = "close-up"
details.impression = "手軽な食事の風景をリアルに捉えた写真。"
details.costume = []
details.objects = ["ラーメン", "箸", "容器", "スープ", "木目調のテーブル"]
details.use_cases = ["食事", "即席食品", "携帯食", "撮影素材"]
""";
    #endregion

    #region 01
    public const string GEMMA_01 = """
"time_of_day = "afternoon""
"dominant_color_palette = "緑色の背景に白文字"
"people_count = 0"
"keywords = ["Google Play", "わたあめやきそばどっちが好き"]"

"details.scene = "緑色の背景にGoogle Playのロゴとアプリ名が表示された画面。""
"details.subjects = "画像内には人物はいない。""
"details.action = "静止画として、Google Playのアプリページを示している状態。""
"details.mood = "明確で情報的。""
"details.shot_type = "static""
"details.impression = "Google Playのアプリページを示すシンプルな画面。""
"details.costume = []"
"details.objects = ["Google Playロゴ", "テキスト"]"
"details.use_cases = ["アプリの紹介", "ダウンロード案内", "ゲームの宣伝"]"
""";

    public const string GEMMA_01_C = """
time_of_day = "afternoon"
dominant_color_palette = "緑色の背景に白文字"
people_count = 0
keywords = ["Google Play", "わたあめやきそばどっちが好き"]
details.scene = "緑色の背景にGoogle Playのロゴとアプリ名が表示された画面。"
details.subjects = "画像内には人物はいない。"
details.action = "静止画として、Google Playのアプリページを示している状態。"
details.mood = "明確で情報的。"
details.shot_type = "static"
details.impression = "Google Playのアプリページを示すシンプルな画面。"
details.costume = []
details.objects = ["Google Playロゴ", "テキスト"]
details.use_cases = ["アプリの紹介", "ダウンロード案内", "ゲームの宣伝"]
""";
    #endregion

    #region 02
    public const string GEMMA_02 = """
"time_of_day = "golden_hour"
dominant_color_palette = "白とオレンジ"
people_count = 0
keywords = ["枠", "白い背景", "オレンジ色の線"]

details.scene = "白い背景にオレンジ色の枠が描かれたシンプルな構図で、時間帯は不明。"
details.subjects = "画像内には人物はいない。"
details.action = "静止状態の空のフレーム。"
details.mood = "無機質でミニマル。"
details.shot_type = "static"
details.impression = "シンプルな枠だけの画像で、視覚的に落ち着いている。"
details.costume = []
details.objects = ["オレンジ色の線", "白い背景"]
details.use_cases = ["デザインテンプレート", "プレゼンテーション用背景", "ウェブサイトのフレーム"]
""";

    public const string GEMMA_02_C = """
time_of_day = "golden_hour"
dominant_color_palette = "白とオレンジ"
people_count = 0
keywords = ["枠", "白い背景", "オレンジ色の線"]

details.scene = "白い背景にオレンジ色の枠が描かれたシンプルな構図で、時間帯は不明。"
details.subjects = "画像内には人物はいない。"
details.action = "静止状態の空のフレーム。"
details.mood = "無機質でミニマル。"
details.shot_type = "static"
details.impression = "シンプルな枠だけの画像で、視覚的に落ち着いている。"
details.costume = []
details.objects = ["オレンジ色の線", "白い背景"]
details.use_cases = ["デザインテンプレート", "プレゼンテーション用背景", "ウェブサイトのフレーム"]
""";
    #endregion

    #region GEMMA_03
    public const string GEMMA_03 = """
"time_of_day = "dusk""
dominant_color_palette = "暗い青灰色の空と機体のシルエット"
people_count = 0
keywords = ["戦闘機", "AWACS", "夜間飛行", "ゲーム画面"]
details.scene = "夕暮れ時の曇り空を背景に、戦闘機が飛行しているシーン。"
details.subjects = "戦闘機が空中で飛行しており、操縦席には乗員はいない。"
details.action = "戦闘機は一定の高度で水平に飛行している状態。"
details.mood = "緊張感と静けさが混在する空戦の雰囲気。"
details.shot_type = "close-up"
details.impression = "戦闘機の迫力ある姿と、夕暮れの空のコントラストが印象的。"
details.costume = []
details.objects = ["戦闘機", "AWACS表示", "文字「イエス！」"]
details.use_cases = ["ゲームプレイ画面","航空機シミュレーション","軍事訓練"]
""";

    public const string GEMMA_03_C = """
time_of_day = "dusk"
dominant_color_palette = "暗い青灰色の空と機体のシルエット"
people_count = 0
keywords = ["戦闘機", "AWACS", "夜間飛行", "ゲーム画面"]
details.scene = "夕暮れ時の曇り空を背景に、戦闘機が飛行しているシーン。"
details.subjects = "戦闘機が空中で飛行しており、操縦席には乗員はいない。"
details.action = "戦闘機は一定の高度で水平に飛行している状態。"
details.mood = "緊張感と静けさが混在する空戦の雰囲気。"
details.shot_type = "close-up"
details.impression = "戦闘機の迫力ある姿と、夕暮れの空のコントラストが印象的。"
details.costume = []
details.objects = ["戦闘機", "AWACS表示", "文字「イエス！」"]
details.use_cases = ["ゲームプレイ画面", "航空機シミュレーション", "軍事訓練"]
""";
    #endregion

    #region GEMMA_04

    // []補完入れる
    public const string GEMMA_04 = """
time_of_day = "predawn"
dominant_color_palette = "青と黒のコントラスト"
people_count = 0
keywords = ["ゲーム内アイテム", "達成条件", "MRP"
details.scene = "ゲーム内の達成画面で、暗い背景に青い光るアイコンと日本語テキストが表示されている。"
details.subjects = "画像内には人物はいない。"
details.action = "画面に「ありあまる富」という達成メッセージと「累計獲得MRPが25,000,000に達した」という情報が静止して表示されている。"
details.mood = "達成感とゲーム内報酬の提示"
details.shot_type = "static"
details.impression = "ゲーム内で大きな成果を達成したことを示すシンプルな画面だ。"
details.costume = []
details.objects = ["アイコン", "テキスト"]
details.use_cases = ["ゲーム内報酬表示", "達成条件通知"]
""";

    public const string GEMMA_04_C = """
time_of_day = "predawn"
dominant_color_palette = "青と黒のコントラスト"
people_count = 0
keywords = ["ゲーム内アイテム", "達成条件", "MRP"]
details.scene = "ゲーム内の達成画面で、暗い背景に青い光るアイコンと日本語テキストが表示されている。"
details.subjects = "画像内には人物はいない。"
details.action = "画面に「ありあまる富」という達成メッセージと「累計獲得MRPが25,000,000に達した」という情報が静止して表示されている。"
details.mood = "達成感とゲーム内報酬の提示"
details.shot_type = "static"
details.impression = "ゲーム内で大きな成果を達成したことを示すシンプルな画面だ。"
details.costume = []
details.objects = ["アイコン", "テキスト"]
details.use_cases = ["ゲーム内報酬表示", "達成条件通知"]
""";

    #endregion

    #region GEMMA_05

    // string, string, int, arryのﾊﾟﾀーﾝでｷー挿入
    public const string GEMMA_05 = """
"predawn"
"ダークな背景に金色のメダルが際立つ"
0
["メダル", "ゲーム画面", "受賞通知", "OKボタン"]
details.scene = "ゲーム内の受賞画面で、暗い背景に金色のメダルと日本語のテキストが表示されている。"
details.subjects = "画像内には人物は存在しない。"
details.action = "メダルの受賞を示す静止画で、OKボタンが操作可能状態である。"
details.mood = "達成感とゲーム内の報酬の提示"
details.shot_type = "static"
details.impression = "ゲーム内で獲得した栄誉を示すシンプルな画面だね。"
details.costume = []
details.objects = ["メダル", "テキスト", "OKボタン"]
details.use_cases = ["ゲーム内報酬表示", "ユーザー操作確認", "ゲーム内通知"]
""";

    public const string GEMMA_05_C = """
time_of_day = "predawn"
dominant_color_palette = "ダークな背景に金色のメダルが際立つ"
people_count = 0
keywords = [ "メダル", "ゲーム画面", "受賞通知", "OKボタン"]
details.scene = "ゲーム内の受賞画面で、暗い背景に金色のメダルと日本語のテキストが表示されている。"
details.subjects = "画像内には人物は存在しない。"
details.action = "メダルの受賞を示す静止画で、OKボタンが操作可能状態である。"
details.mood = "達成感とゲーム内の報酬の提示"
details.shot_type = "static"
details.impression = "ゲーム内で獲得した栄誉を示すシンプルな画面だね。"
details.costume = []
details.objects = ["メダル", "テキスト", "OKボタン"]
details.use_cases = ["ゲーム内報酬表示", "ユーザー操作確認", "ゲーム内通知"]
""";

    #endregion

    #region GEMMA_06
    public const string GEMMA_06 = """
`time_of_day = 'midday'`
`dominant_color_palette = "背番号10"`
people_count = "5"
keywords = ["女性", "水着", "海", "ボート", "水"

details.scene = "晴れた日の海で、複数の女性が水に立っている。背景にはボートと遠くの陸地がある。"
details.subjects = "複数の女性が水に立っており、一部は背中を向けていて、別の女性は水に腰を下ろしている。"
details.action = "女性たちは水に立っており、一部は手を水面に触れている。"
details.mood = "リラックスした雰囲気"
details.shot_type = "wide"
details.impression = "海で楽しんでいる女性たちの様子が見える。"
"details.costume = ["水着", "水着", "水着", "水着", "水着"]"
details.objects = ["ボート", "水", "陸地", "空", "波"]
details.use_cases = ["海水浴", "写真撮影", "レジャー", "観光", "スポーツ"]
""";

    public const string GEMMA_06_C = """
time_of_day = "midday"
dominant_color_palette = "背番号10"
people_count = 5
keywords = ["女性", "水着", "海", "ボート", "水"]

details.scene = "晴れた日の海で、複数の女性が水に立っている。背景にはボートと遠くの陸地がある。"
details.subjects = "複数の女性が水に立っており、一部は背中を向けていて、別の女性は水に腰を下ろしている。"
details.action = "女性たちは水に立っており、一部は手を水面に触れている。"
details.mood = "リラックスした雰囲気"
details.shot_type = "wide"
details.impression = "海で楽しんでいる女性たちの様子が見える。"
details.costume = ["水着", "水着", "水着", "水着", "水着"]
details.objects = ["ボート", "水", "陸地", "空", "波"]
details.use_cases = ["海水浴", "写真撮影", "レジャー", "観光", "スポーツ"]
""";

    #endregion
}
