using Blue.Common;
using Blue.Data;
using Blue.Data.Master.Model;
using Blue.Room;
using Il2CppSystem;
using UnityEngine;

namespace Aooni2SpeedRunTool
{
    internal class SpeedRunTool : MonoBehaviour
    {
        private string displaySteps = "0";
        private string displayTime = "00:00:00";
        private string timeLabel = "Time";
        private float updateTimer = 0f;
        private const float updateInterval = 0.5f;

        private readonly System.Collections.Generic.Queue<string> _addItemQueue
            = new System.Collections.Generic.Queue<string>();
        private const int AddItemsPerFrame = 3;   // 每幀加幾個，想更保守可改成 1

        private class MapEntry { public string Id; public string Label; }

        private readonly System.Collections.Generic.List<MapEntry> _maps
            = new System.Collections.Generic.List<MapEntry>();
        private bool _showMapPanel;
        private int _mapPage;
        private const int MapsPerPage = 12;
        private string _pendingTeleportId;
        private string _repoMapId;
        private bool _repoSawTransition;
        private float _repoTimeout;
        private enum Lang { En, Zh, Ja }
        private Lang _lang = Lang.En;
        private string _lastLangProbe;
        public static bool NoClip;
        public static bool NoGameOver;

        private PlayerData _snap;
        private string _snapMapId;
        private string _toast = "";
        private float _toastUntil;

        // 精確定位（讀檔用）
        private bool _repoExact;
        private Vector2Int _repoPos;
        private Blue.Direction _repoDir;

        // 自訂速度數值與最大值（預設值為 4.5 和 15.0）
        private float customSpeed = 4.5f;
        private float customMax = 6.0f;

        // 預設直接開啟自訂速度狀態
        private bool isCustomSpeedActive = true;

        // 備份原始的數值（預設值為 4.5 和 6.0，保留給 * 鍵切換用）
        private float originSpeed = 4.5f;
        private float originMax = 6.0f;

        private string L(string en, string zh, string ja)
        {
            return _lang == Lang.Ja ? ja : (_lang == Lang.Zh ? zh : en);
        }

        private void CheckLanguageChange()
        {
            string probe = null;
            try { probe = MasterProvider.SystemTextMaster.Get("MenuWindow_PlayTimeText").Text; }
            catch { return; }
            if (string.IsNullOrEmpty(probe) || probe == _lastLangProbe) return;

            _lastLangProbe = probe;

            bool kana = false, han = false;
            foreach (char c in probe)
            {
                if (c >= '\u3040' && c <= '\u30FF') kana = true;      // 平假名、片假名
                else if (c >= '\u4E00' && c <= '\u9FFF') han = true;  // 漢字
            }
            _lang = kana ? Lang.Ja : (han ? Lang.Zh : Lang.En);

            Plugin.Log.LogInfo($"[Lang] probe={probe}, lang={_lang}");
            if (_showMapPanel) RefreshMapList();   // 地圖名稱立刻跟著換
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                NoClip = !NoClip;

                Toast(L(
                    $"NoClip: {NoClip}",
                    $"穿牆: {NoClip}",
                    $"すり抜け: {NoClip}"
                ));
            }

            if (Input.GetKeyDown(KeyCode.F2)) SaveState();
            if (Input.GetKeyDown(KeyCode.F3)) LoadState();

            if (Input.GetKeyDown(KeyCode.F4))
            {
                NoGameOver = !NoGameOver;

                Toast(L(
                    $"NoGameOver: {NoGameOver}",
                    $"無敵: {NoGameOver}",
                    $"ゲームオーバーなし: {NoGameOver}"
                ));
            }

            if (Input.GetKeyDown(KeyCode.F5))
            {
                var all = MasterProvider.MapMaster.GetAll();
               

                int count = -1;
               
                var col = all.TryCast<Il2CppSystem.Collections.Generic.ICollection<MapMasterModel>>();

                for (int i = 0; count < 0 || i < count; i++)
                {
                    MapMasterModel m;
                    try { m = all[i]; }
                    catch { break; }          // 超出範圍 → 結束

                    if (m == null) break;
                    AddMapEntry(m);
                    string name = m.AssetName;
                  
                    var t = MasterProvider.SystemTextMaster.Get(m.SystemTextId);
                    if (t != null && !string.IsNullOrEmpty(t.Text))
                        name = $"SceneName: {t.Text}  AssetName: ({m.AssetName}) Id: {m.Id}";
                    Plugin.Log.LogInfo(name);
                }
            }

            // 按下 [+]：增加數值，且會直接套用
            if (Input.GetKeyDown(KeyCode.KeypadPlus))
            {
                customSpeed += 1f;
                customMax += 1f;

                isCustomSpeedActive = true;
                Blue.Const.ValueSet.CharacterSpeed._max = new Shirakami.ProtectedFloat(customMax);
                Blue.Const.ValueSet.CharacterSpeed._value = new Shirakami.ProtectedFloat(customSpeed);

                Toast(L(
                    $"Custom Speed: {customSpeed}",
                    $"自訂速度: {customSpeed}",
                    $"カスタム速度: {customSpeed}"
                ));
            }

            // 按下 [-]：減少數值，且會直接套用
            if (Input.GetKeyDown(KeyCode.KeypadMinus))
            {
                customSpeed -= 1f;
                customMax -= 1f;

                isCustomSpeedActive = true;
                Blue.Const.ValueSet.CharacterSpeed._max = new Shirakami.ProtectedFloat(customMax);
                Blue.Const.ValueSet.CharacterSpeed._value = new Shirakami.ProtectedFloat(customSpeed);

                Toast(L(
                    $"Custom Speed: {customSpeed}",
                    $"自訂速度: {customSpeed}",
                    $"カスタム速度: {customSpeed}"
                ));
            }

            // 按下 [*] (KeypadMultiply)：切換「原始速度」與「自訂速度」
            if (Input.GetKeyDown(KeyCode.KeypadMultiply))
            {
                isCustomSpeedActive = !isCustomSpeedActive; // 反轉狀態

                if (isCustomSpeedActive)
                {
                    // 切換到自訂速度
                    Blue.Const.ValueSet.CharacterSpeed._max = new Shirakami.ProtectedFloat(customMax);
                    Blue.Const.ValueSet.CharacterSpeed._value = new Shirakami.ProtectedFloat(customSpeed);

                    Toast(L(
                        $"Switched to Custom Speed ({customSpeed})",
                        $"已切換至：自訂速度 ({customSpeed})",
                        $"カスタム速度に切り替えました ({customSpeed})"
                    ));
                }
                else
                {
                    // 切換回原始速度
                    Blue.Const.ValueSet.CharacterSpeed._max = new Shirakami.ProtectedFloat(originMax);
                    Blue.Const.ValueSet.CharacterSpeed._value = new Shirakami.ProtectedFloat(originSpeed);

                    Toast(L(
                        $"Switched to Original Speed ({originSpeed})",
                        $"已切換至：原始速度 ({originSpeed})",
                        $"元の速度に切り替えました ({originSpeed})"
                    ));
                }
            }

            // 按下 [0]：將「自訂速度」重設回初始預設值（4.5 與 15.0）並直接套用
            if (Input.GetKeyDown(KeyCode.Keypad0))
            {
                customSpeed = 4.5f;
                customMax = 15.0f;
                isCustomSpeedActive = true;

                Blue.Const.ValueSet.CharacterSpeed._max = new Shirakami.ProtectedFloat(customMax);
                Blue.Const.ValueSet.CharacterSpeed._value = new Shirakami.ProtectedFloat(customSpeed);

                Toast(L(
                    $"Custom Speed Reset: {customSpeed}",
                    $"自訂速度已重設: {customSpeed}",
                    $"カスタム速度をリセット: {customSpeed}"
                ));
            }

            ProcessAddItemQueue();
            ProcessPendingTeleport();
            ProcessReposition();


            updateTimer += Time.deltaTime;
            if (updateTimer >= updateInterval)
            {
                updateTimer = 0f;
                UpdateData();
                CheckLanguageChange();
            }
        }

        private void Toast(string s)
        {
            _toast = s;
            _toastUntil = Time.unscaledTime + 2.5f;
            Plugin.Log.LogInfo($"[State] {s}");
        }

        private void SaveState()
        {
            try
            {
                var scene = GameScene.Instance;
                if (scene == null || scene.IsMapTransition || scene.CurrentMap == null
                    || scene.CurrentMap.Player == null || scene.CurrentMap.MapData == null)
                {
                    Toast(L("Can't save now", "目前無法存檔", "今は保存できません"));
                    return;
                }

                var map = scene.CurrentMap;
                var snap = new PlayerData(UserData.GetPlayerData());   // 深層複製
                snap.Position = map.Player.Position;                    // 複製來的是舊座標，改成目前位置
                snap.Direction = map.Player.Direction;

                _snap = snap;
                _snapMapId = map.MapData.Id;
                Toast(L("State saved", "已存檔 (F2)", "保存しました") +
                      $"  {_snapMapId} ({snap.Position.x},{snap.Position.y})");
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[State] 存檔失敗: {e}");
                Toast(L("Save failed", "存檔失敗", "保存失敗"));
            }
        }

        private void LoadState()
        {
            try
            {
                if (_snap == null)
                {
                    Toast(L("No saved state", "還沒有存檔", "保存データなし"));
                    return;
                }
                var scene = GameScene.Instance;
                if (scene == null || scene.IsMapTransition)
                {
                    Toast(L("Can't load now", "目前無法讀檔", "今は読み込めません"));
                    return;
                }

                var live = UserData.GetPlayerData();
                var copy = new PlayerData(_snap);         // 每次讀檔都用新複本，快照可重複使用

                live.GameStatus = copy.GameStatus;
                live.InventoryItems = copy.InventoryItems;
                live.PartyMemberDatas = copy.PartyMemberDatas;
                live.Steps.Set(copy.Steps.Value.Get());
                live.DeadCount.Set(copy.DeadCount.Value.Get());
                live.PlayTime.Set(copy.PlayTime.Value.Get());
                Blue.PlayTimeCounter._seconds = (float)copy.PlayTime.Value.Get();
                live.Position = _snap.Position;
                live.Direction = _snap.Direction;

                // 背包整個換掉了，重新載入圖示，否則 UI 會等不到圖示
                scene._inventoryService.LoadPossessionIconsAsync();

                // 4. 同地圖瞬讀
                if (scene.CurrentMap != null && scene.CurrentMap.MapData != null && scene.CurrentMap.MapData.Id == _snapMapId)
                {
                    var map = scene.CurrentMap;
                    map.SetSprite(map.Player, _snap.Position);
                    map.Player.ChangeDirection(_snap.Direction);
                    map.ResetPartyMemberInformation();
                    return;
                }

                // 跨地圖轉場
                scene.TransitionMapAsync(_snapMapId, Blue.TransitionKind.Other, "", Blue.TransitionType.FadeoutCutin);

                _repoMapId = _snapMapId;
                _repoSawTransition = false;
                _repoTimeout = 10f;
                _repoExact = true;
                _repoPos = _snap.Position;
                _repoDir = _snap.Direction;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[State] 讀檔失敗: {e}");
                Toast(L("Load failed", "讀檔失敗", "読み込み失敗"));
            }
        }

        private void RefreshMapList()
        {
            _maps.Clear();
            try
            {
                var all = MasterProvider.MapMaster.GetAll();
                if (all == null)
                {
                    Plugin.Log.LogWarning("[Map] GetAll() 回傳 null（劇本類型可能還沒設定）");
                    return;
                }

                int count = -1;
                try
                {
                    var col = all.TryCast<Il2CppSystem.Collections.Generic.ICollection<MapMasterModel>>();
                    if (col != null) count = col.Count;
                }
                catch { }

                for (int i = 0; count < 0 || i < count; i++)
                {
                    MapMasterModel m;
                    try { m = all[i]; }
                    catch { break; }          // 超出範圍 → 結束

                    if (m == null) break;
                    AddMapEntry(m);
                }

                Plugin.Log.LogInfo($"[Map] 載入 {_maps.Count} 張地圖 (count={count})");
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[Map] 讀取失敗: {e}");
            }
        }

        private void AddMapEntry(MapMasterModel m)
        {
            if (m == null) return;

            string name = m.AssetName;
            try
            {
                var t = MasterProvider.SystemTextMaster.Get(m.SystemTextId);
                if (t != null && !string.IsNullOrEmpty(t.Text))
                    name = $"{t.Text}  ({m.AssetName})";
            }
            catch { }

            _maps.Add(new MapEntry { Id = m.Id, Label = $"{m.Id}  {name}" });
        }

        private void TeleportTo(string mapId)
        {
            Plugin.Log.LogInfo($"[Map] 點擊傳送 {mapId}");
            _pendingTeleportId = mapId;
            _showMapPanel = false;
        }

        private void ProcessPendingTeleport()
        {
            if (_pendingTeleportId == null) return;
            string mapId = _pendingTeleportId;
            _pendingTeleportId = null;

            try
            {
                var scene = GameScene.Instance;
                if (scene == null)
                {
                    Plugin.Log.LogWarning("[Map] GameScene.Instance 為 null（可能不在遊戲場景）");
                    return;
                }
                if (scene.IsMapTransition)
                {
                    Plugin.Log.LogWarning("[Map] IsMapTransition 為 true，略過");
                    return;
                }

                Plugin.Log.LogInfo($"[Map] 開始傳送到 {mapId}, CurrentMap={(scene.CurrentMap != null)}, EventState={scene.EventState}");
                scene.TransitionMapAsync(mapId, Blue.TransitionKind.Other, "", Blue.TransitionType.FadeoutCutin);
                _repoMapId = mapId;
                _repoSawTransition = false;
                _repoTimeout = 10f;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[Map] 傳送失敗: {e}");
            }
        }

        private void ProcessReposition()
        {
            if (_repoMapId == null) return;

            var scene = GameScene.Instance;
            _repoTimeout -= Time.deltaTime;
            if (scene == null || _repoTimeout <= 0f)
            {
                Plugin.Log.LogWarning("[Map] 重新定位逾時或場景消失");
                _repoMapId = null;
                return;
            }

            if (scene.IsMapTransition) { _repoSawTransition = true; return; }
            if (!_repoSawTransition) return;

            var map = scene.CurrentMap;
            if (map == null || map.MapData == null || map.MapData.Id != _repoMapId
                || map.Player == null || map.MapInfo == null) return;

            _repoMapId = null;

            if (_repoExact)
            {
                _repoExact = false;
                try
                {
                    map.SetSprite(map.Player, _repoPos);
                    map.Player.ChangeDirection(_repoDir);
                    map.ResetPartyMemberInformation();
                    Toast(L("State loaded", "已讀檔 (F3)", "読み込みました"));
                }
                catch (System.Exception e)
                {
                    Plugin.Log.LogError($"[State] 定位失敗: {e}");
                }
                return;
            }

            try
            {
                Vector2Int pos;
                if (TryFindSafeCell(map, out pos))
                {
                    map.SetSprite(map.Player, pos);
                    map.ResetPartyMemberInformation();
                    Plugin.Log.LogInfo($"[Map] 玩家移到 ({pos.x}, {pos.y})");
                }
                else
                {
                    Plugin.Log.LogWarning("[Map] 找不到安全格子，維持原位置");
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[Map] 重新定位失敗: {e}");
            }
        }

        private static bool IsFloor(RoomBase map, int x, int y)
        {
            return map.GetCellType(new Vector2Int(x, y)) == MapInfo.CellType.Floor;
        }

        private static bool IsOpen(RoomBase map, int x, int y)
        {
            return IsFloor(map, x, y)
                && IsFloor(map, x + 1, y) && IsFloor(map, x - 1, y)
                && IsFloor(map, x, y + 1) && IsFloor(map, x, y - 1);
        }

        private bool TryFindSafeCell(RoomBase map, out Vector2Int result)
        {
            result = Vector2Int.zero;
            var info = map.MapInfo;

            var events = info.Events;
            for (int i = 0; i < events.Length; i++)
            {
                var e = events[i];
                if (e == null || string.IsNullOrEmpty(e.TagName)) continue;
                if (e.TagName.Contains("\\killer")) continue;

                int x = e.Position.x, y = e.Position.y;
                if (IsOpen(map, x, y))
                {
                    result = new Vector2Int(x, y);
                    Plugin.Log.LogInfo($"[Map] 使用 Tag={e.TagName}");
                    return true;
                }
            }

            int w = info.Width, h = info.Height;
            float cx = w / 2f, cy = -h / 2f;
            float best = float.MaxValue;
            bool found = false;

            for (int row = 0; row < h; row++)
            {
                for (int x = 0; x < w; x++)
                {
                    int y = -row;
                    if (!IsOpen(map, x, y)) continue;
                    float d = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                    if (d < best)
                    {
                        best = d;
                        result = new Vector2Int(x, y);
                        found = true;
                    }
                }
            }
            return found;
        }

        private void EnqueueAllItems()
        {
            try
            {
                if (GameScene.Instance == null)
                {
                    Plugin.Log.LogWarning("[F2] GameScene 尚未就緒");
                    return;
                }

                var items = MasterProvider.ItemMaster.GetAll();
                if (items == null) return;

                var collection = items.TryCast<Il2CppSystem.Collections.Generic.ICollection<ItemMasterModel>>();
                int count = collection.Count;

                for (int i = 0; i < count; i++)
                {
                    string id = items[i].Id;
                    if (!UserData.HasItem(id))
                        _addItemQueue.Enqueue(id);
                }
                Plugin.Log.LogInfo($"[F2] 準備加入 {_addItemQueue.Count} 個物品");
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[F2] 失敗: {e}");
            }
        }

        private void ProcessAddItemQueue()
        {
            if (_addItemQueue.Count == 0) return;
            if (GameScene.Instance == null) { _addItemQueue.Clear(); return; }

            try
            {
                var svc = GameScene.Instance._inventoryService;

                for (int n = 0; n < AddItemsPerFrame && _addItemQueue.Count > 0; n++)
                {
                    svc.AddItem(_addItemQueue.Dequeue());
                }

                if (_addItemQueue.Count == 0)
                    Plugin.Log.LogInfo("[F2] 所有物品已加入");
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[F2] 加入物品失敗: {e}");
                _addItemQueue.Clear();
            }
        }

        private void UpdateData()
        {
            try
            {
                int steps = Blue.Data.UserData.GetPlayerData().Steps.Value.Get();
                try
                {
                    string rawFormat = MasterProvider.SystemTextMaster.Get("MenuWindow_StepsValue").Text;
                    string stepsOnly = steps.ToString();
                    displaySteps = string.Format(rawFormat, stepsOnly);
                }
                catch
                {
                    displaySteps = $"steps: {steps}";
                }

                try
                {
                    timeLabel = MasterProvider.SystemTextMaster.Get("MenuWindow_PlayTimeText").Text;
                }
                catch
                {
                    timeLabel = "Time";
                }

                int playTimeValue = Blue.Data.UserData.GetPlayerData().PlayTime.Value.Get();
                TimeSpan timeSpan = new TimeSpan(0, 0, playTimeValue);

                string hours = timeSpan.Hours.ToString().PadLeft(2, '0');
                string minutes = timeSpan.Minutes.ToString().PadLeft(2, '0');
                string seconds = timeSpan.Seconds.ToString().PadLeft(2, '0');

                displayTime = $"{hours}:{minutes}:{seconds}";
            }
            catch
            {
            }
        }

        private bool ClickButton(Rect r, string text)
        {
            GUI.Box(r, text);
            var ev = Event.current;
            if (ev.type == EventType.MouseDown && ev.button == 0 && r.Contains(ev.mousePosition))
            {
                ev.Use();
                return true;
            }
            return false;
        }

        private void OnGUI()
        {
            float x = 20f;
            float y = 20f;
            float width = 220f;
            float height = 165f;

            GUI.Box(new Rect(x - 5, y - 5, width, height), "");

            Color originalColor = GUI.skin.label.normal.textColor;
            GUI.skin.label.normal.textColor = Color.white;
            GUI.Label(new Rect(x, y, width, 25), displaySteps);
            GUI.Label(new Rect(x, y + 25, width, 25), $"{timeLabel}: {displayTime}");
            GUI.skin.label.normal.textColor = originalColor;

            bool busy = _addItemQueue.Count > 0;
            bool oldEnabled = GUI.enabled;
            GUI.enabled = !busy;
            if (GUI.Button(new Rect(x, y + 55, width - 10, 30),
                busy ? L("Adding...", "加入中...", "追加中...")
                     : L("Add all items", "添加該模式所有物品", "このモードの全アイテムを追加")))
            {
                EnqueueAllItems();
            }
            GUI.enabled = oldEnabled;

            if (busy)
            {
                GUI.Label(new Rect(x, y + 90, width, 25),
                    $"{L("Remaining", "剩餘", "残り")}: {_addItemQueue.Count}");
            }

            var mapBtn = new Rect(x, y + 120, width - 10, 30);
            GUI.Box(mapBtn, _showMapPanel
                ? L("Close map menu", "關閉地圖選單", "マップメニューを閉じる")
                : L("Teleport", "地圖傳送", "マップ移動"));

            var ev = Event.current;
            if (ev.type == EventType.MouseDown && ev.button == 0 && mapBtn.Contains(ev.mousePosition))
            {
                _showMapPanel = !_showMapPanel;
                Plugin.Log.LogInfo($"[Map] 按鈕點擊, show={_showMapPanel}");
                if (_showMapPanel) RefreshMapList();
                ev.Use();
            }

            if (_showMapPanel)
            {
                try { DrawMapPanel(); }
                catch (System.Exception e) { Plugin.Log.LogError($"[Map] DrawMapPanel 例外: {e}"); }
            }

            if (Time.unscaledTime < _toastUntil)
            {
                GUI.Box(new Rect(20, Screen.height - 50, 420, 30), _toast);
            }
        }

        private void DrawMapPanel()
        {
            float px = 20f, py = 195f;
            float pw = Mathf.Min(380f, Screen.width - px - 10f);
            const float rowH = 28f;
            int totalPages = Mathf.Max(1, (_maps.Count + MapsPerPage - 1) / MapsPerPage);
            _mapPage = Mathf.Clamp(_mapPage, 0, totalPages - 1);

            float ph = 30f + MapsPerPage * rowH + 40f;
            ph = Mathf.Min(ph, Screen.height - py - 10f);

            string title;
            if (_lang == Lang.Ja) title = $"マップ移動 ({_maps.Count})  {_mapPage + 1}/{totalPages} ページ";
            else if (_lang == Lang.Zh) title = $"地圖傳送 ({_maps.Count})  第 {_mapPage + 1}/{totalPages} 頁";
            else title = $"Map Teleport ({_maps.Count})  Page {_mapPage + 1}/{totalPages}";
            GUI.Box(new Rect(px, py, pw, ph), title);

            string target = null;
            int start = _mapPage * MapsPerPage;
            for (int i = 0; i < MapsPerPage; i++)
            {
                int idx = start + i;
                if (idx >= _maps.Count) break;

                var r = new Rect(px + 8, py + 28 + i * rowH, pw - 16, rowH - 2);
                if (ClickButton(r, _maps[idx].Label))
                {
                    Plugin.Log.LogInfo($"[Map] 清單點擊 idx={idx} id={_maps[idx].Id}");
                    target = _maps[idx].Id;
                }
            }

            float navY = py + 28 + MapsPerPage * rowH + 4;
            if (ClickButton(new Rect(px + 8, navY, 90, 28), L("Prev", "上一頁", "前へ"))) { _mapPage--; }
            if (ClickButton(new Rect(px + pw - 98, navY, 90, 28), L("Next", "下一頁", "次へ"))) { _mapPage++; }

            if (target != null) TeleportTo(target);
        }

        private void LogAndGetStatus()
        {
            int steps = Blue.Data.UserData.GetPlayerData().Steps.Value.Get();
            string stepsText = steps.ToString();
            try
            {
                stepsText = string.Format(MasterProvider.SystemTextMaster.Get("MenuWindow_StepsValue").Text, steps);
            }
            catch { }
            Plugin.Log.LogInfo($"步數: {stepsText}");

            int playTimeValue = Blue.Data.UserData.GetPlayerData().PlayTime.Value.Get();
            TimeSpan timeSpan = new TimeSpan(0, 0, playTimeValue);
            string hours = timeSpan.Hours.ToString().PadLeft(2, '0');
            string minutes = timeSpan.Minutes.ToString().PadLeft(2, '0');
            string seconds = timeSpan.Seconds.ToString().PadLeft(2, '0');

            string tLabel = "Time";
            try { tLabel = MasterProvider.SystemTextMaster.Get("MenuWindow_PlayTimeText").Text; } catch { }

            Plugin.Log.LogInfo($"{tLabel}: {hours}:{minutes}:{seconds}");
        }
    }
}