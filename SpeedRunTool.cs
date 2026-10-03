using System.Collections.Generic;
using Blue;
using Blue.Common;
using Blue.Data;
using Blue.Data.Master.Model;
using Blue.Room;
using Blue.Sprites;
using Blue.Star;
using Il2CppSystem;
using UnityEngine;

namespace Aooni2SpeedRunTool
{
    internal class SpeedRunTool : MonoBehaviour
    {
        private string displaySteps = "0";
        private string displayTime = "00:00:00";
        private string displayMap = "-";
        private string timeLabel = "Time";
        private float updateTimer = 0f;
        private const float updateInterval = 0.5f;

        private readonly Queue<string> _addItemQueue = new Queue<string>();
        private const int AddItemsPerFrame = 3;

        private class MapEntry { public string Id; public string Label; }
        private readonly List<MapEntry> _maps = new List<MapEntry>();
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
        public static bool ShowEnemyPath = true; // ★ F5 切換敵人路徑顯示

        private PlayerData _snap;
        private string _snapMapId;
        private string _toast = "";
        private float _toastUntil;

        // 精確定位（讀檔用）
        private bool _repoExact;
        private Vector2Int _repoPos;
        private Blue.Direction _repoDir;

        // 自訂速度數值與最大值
        private float customSpeed = 4.5f;
        private float customMax = 6.0f;
        private bool isCustomSpeedActive = true;
        private float originSpeed = 4.5f;
        private float originMax = 6.0f;

        // ★ 敵人路徑繪製相關變數
        private readonly List<LineRenderer> _pathRenderers = new List<LineRenderer>();
        private float _pathUpdateTimer = 0f;
        private const float PathUpdateInterval = 0.08f; // 每 0.08 秒刷新一次路徑預測

        private Blue.Direction _currentKillerDir;
        private Vector2Int _currentKillerTarget;
        private int _currentKillerSteps = -1; // -1 代表無敵人或未追擊

        private string L(string en, string zh, string ja)
        {
            return _lang == Lang.Ja ? ja : (_lang == Lang.Zh ? zh : en);
        }

        private void Start()
        {
            InitPathRenderers();
        }

        // 初始化畫線組件
        private void InitPathRenderers()
        {
            Material lineMat = new Material(Shader.Find("Sprites/Default"));
            for (int i = 0; i < 4; i++)
            {
                var lineObj = new GameObject($"EnemyPathRenderer_{i}");
                lineObj.transform.SetParent(this.transform);
                var lr = lineObj.AddComponent<LineRenderer>();
                lr.material = lineMat;
                // 加粗線條，並讓起點最粗、終點收細
                lr.startWidth = 0.22f;
                lr.endWidth = 0.08f;
                lr.startColor = new Color(1f, 0.15f, 0.15f, 0.95f); // 亮紅
                lr.endColor = new Color(1f, 0.85f, 0.1f, 0.6f);     // 暖黃
                lr.sortingLayerName = "Sprite";
                lr.sortingOrder = 9999;
                lr.positionCount = 0;
                _pathRenderers.Add(lr);
            }
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
                if (c >= '\u3040' && c <= '\u30FF') kana = true;
                else if (c >= '\u4E00' && c <= '\u9FFF') han = true;
            }
            _lang = kana ? Lang.Ja : (han ? Lang.Zh : Lang.En);

            UpdateData();
            if (_showMapPanel) RefreshMapList();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                NoClip = !NoClip;
                Toast(L($"NoClip: {NoClip}", $"穿牆: {NoClip}", $"すり抜け: {NoClip}"));
            }

            if (Input.GetKeyDown(KeyCode.F2)) SaveState();
            if (Input.GetKeyDown(KeyCode.F3)) LoadState();

            if (Input.GetKeyDown(KeyCode.F4))
            {
                NoGameOver = !NoGameOver;
                Toast(L($"NoGameOver: {NoGameOver}", $"無敵: {NoGameOver}", $"ゲームオーバーなし: {NoGameOver}"));
            }

            // ★ 按下 F5：切換敵人路徑預測顯示
            if (Input.GetKeyDown(KeyCode.F5))
            {
                ShowEnemyPath = !ShowEnemyPath;
                if (!ShowEnemyPath) ClearEnemyPaths();
                Toast(L($"Enemy Path: {ShowEnemyPath}", $"敵人路徑顯示: {ShowEnemyPath}", $"鬼の移動経路: {ShowEnemyPath}"));
            }

            if (Input.GetKeyDown(KeyCode.F12))
            {
                ReloadCurrentSave();
            }

            // 速度調整
            if (Input.GetKeyDown(KeyCode.KeypadPlus))
            {
                customSpeed += 1f; customMax += 1f; isCustomSpeedActive = true;
                Blue.Const.ValueSet.CharacterSpeed._max = new Shirakami.ProtectedFloat(customMax);
                Blue.Const.ValueSet.CharacterSpeed._value = new Shirakami.ProtectedFloat(customSpeed);
                Toast(L($"Custom Speed: {customSpeed}", $"自訂速度: {customSpeed}", $"カスタム速度: {customSpeed}"));
            }

            if (Input.GetKeyDown(KeyCode.KeypadMinus))
            {
                customSpeed -= 1f; customMax -= 1f; isCustomSpeedActive = true;
                Blue.Const.ValueSet.CharacterSpeed._max = new Shirakami.ProtectedFloat(customMax);
                Blue.Const.ValueSet.CharacterSpeed._value = new Shirakami.ProtectedFloat(customSpeed);
                Toast(L($"Custom Speed: {customSpeed}", $"自訂速度: {customSpeed}", $"カスタム速度: {customSpeed}"));
            }

            if (Input.GetKeyDown(KeyCode.KeypadMultiply))
            {
                isCustomSpeedActive = !isCustomSpeedActive;
                float spd = isCustomSpeedActive ? customSpeed : originSpeed;
                float max = isCustomSpeedActive ? customMax : originMax;
                Blue.Const.ValueSet.CharacterSpeed._max = new Shirakami.ProtectedFloat(max);
                Blue.Const.ValueSet.CharacterSpeed._value = new Shirakami.ProtectedFloat(spd);
                Toast(L($"Speed switched ({spd})", $"已切換速度 ({spd})", $"速度切替 ({spd})"));
            }

            if (Input.GetKeyDown(KeyCode.Keypad0))
            {
                customSpeed = 4.5f; customMax = 15.0f; isCustomSpeedActive = true;
                Blue.Const.ValueSet.CharacterSpeed._max = new Shirakami.ProtectedFloat(customMax);
                Blue.Const.ValueSet.CharacterSpeed._value = new Shirakami.ProtectedFloat(customSpeed);
                Toast(L($"Custom Speed Reset: {customSpeed}", $"自訂速度已重設: {customSpeed}", $"カスタム速度リセット: {customSpeed}"));
            }

            ProcessAddItemQueue();
            ProcessPendingTeleport();
            ProcessReposition();

            // ★ 更新敵人路徑邏輯
            _pathUpdateTimer += Time.deltaTime;
            if (_pathUpdateTimer >= PathUpdateInterval)
            {
                _pathUpdateTimer = 0f;
                UpdateEnemyPaths();
            }

            updateTimer += Time.deltaTime;
            if (updateTimer >= updateInterval)
            {
                updateTimer = 0f;
                UpdateData();
                CheckLanguageChange();
            }
        }

        // ★ 核心方法：計算並繪製所有在場敵人的尋路軌跡
        private void UpdateEnemyPaths()
        {
            if (!ShowEnemyPath) return;

            var scene = GameScene.Instance;
            if (scene == null || scene.CurrentMap == null || scene.CurrentMap.Player == null)
            {
                ClearEnemyPaths();
                return;
            }

            var km = scene.KillerManager;
            if (km == null || !km.Chasing || km.Killers == null || km.Killers.Count == 0)
            {
                ClearEnemyPaths();
                return;
            }

            var map = scene.CurrentMap;
            Vector2Int playerPos = map.Player.Position;
            int lrIndex = 0;

            for (int k = 0; k < km.Killers.Count && lrIndex < _pathRenderers.Count; k++)
            {
                var killer = km.Killers[k];
                if (killer == null || !killer.Active || killer.MapId != map.MapData.Id) continue;

                var lr = _pathRenderers[lrIndex++];
                DrawSingleKillerPath(lr, killer, map, playerPos);
            }

            // 清理多餘的線條
            for (int i = lrIndex; i < _pathRenderers.Count; i++)
            {
                _pathRenderers[i].positionCount = 0;
            }
        }

        private void DrawSingleKillerPath(LineRenderer lr, KillerBase killer, RoomBase map, Vector2Int playerPos)
        {
            List<Vector3> points = new List<Vector3>();

            // 1. 起點：鬼當前實體腳底坐標
            Vector3 killerRealPos = killer.transform.position;
            killerRealPos.z = -0.5f;
            points.Add(killerRealPos);

            // 2. 鬼這一步踩的目標格子（使用原生 CellToWorld 精準對齊）
            Vector2Int currentTargetTile = killer.Position;
            Vector3 currentTargetWorld = map.CellToWorld(currentTargetTile);
            currentTargetWorld.z = -0.5f;
            points.Add(currentTargetWorld);

            // 3. 確定性最短路徑計算（從目標格到玩家）
            List<Vector2Int> pathTiles = FindPathToPlayer(map, currentTargetTile, playerPos, killer);
            foreach (var tile in pathTiles)
            {
                Vector3 worldPt = map.CellToWorld(tile);
                worldPt.z = -0.5f;
                points.Add(worldPt);
            }

            // ★ 更新供 UI 渲染的數據
            _currentKillerDir = killer.Direction;
            _currentKillerTarget = currentTargetTile;
            _currentKillerSteps = pathTiles.Count + 1; // 加上當前邁出的這 1 步

            // 傳遞頂點給線條
            lr.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++)
            {
                lr.SetPosition(i, points[i]);
            }
        }

        private List<Vector2Int> FindPathToPlayer(RoomBase map, Vector2Int start, Vector2Int target, KillerBase killer)
        {
            List<Vector2Int> resultPath = new List<Vector2Int>();
            if (start == target) return resultPath;

            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            Dictionary<Vector2Int, Vector2Int> parent = new Dictionary<Vector2Int, Vector2Int>();
            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

            queue.Enqueue(start);
            visited.Add(start);

            // ★ 依照遊戲 BlueStar 的探索優先順序：直走 -> 右轉 -> 左轉 -> 後退
            Blue.Direction forward = killer != null ? killer.Direction : Blue.Direction.Down;
            Blue.Direction[] dirs = new Blue.Direction[]
            {
        forward,
        forward.TurnRight(),
        forward.TurnLeft(),
        forward.Reverse()
            };

            bool found = false;
            int limit = 400;

            while (queue.Count > 0 && limit-- > 0)
            {
                Vector2Int curr = queue.Dequeue();
                if (curr == target)
                {
                    found = true;
                    break;
                }

                for (int i = 0; i < 4; i++)
                {
                    Blue.Direction d = dirs[i];
                    Vector2Int next = curr.Plus(d);

                    if (visited.Contains(next)) continue;

                    CanMoveArgumentOption option = new CanMoveArgumentOption(curr, d)
                    {
                        IgnoreCarryGimmick = true,
                        IgnorePlayerWall = true,
                        ShouldForceOpenDoor = killer != null
                    };

                    if (!map.CanMove(option)) continue;

                    visited.Add(next);
                    parent[next] = curr;
                    queue.Enqueue(next);
                }
            }

            if (!found) return resultPath;

            Vector2Int currStep = target;
            while (currStep != start)
            {
                resultPath.Add(currStep);
                if (!parent.TryGetValue(currStep, out currStep)) break;
            }

            resultPath.Reverse();
            return resultPath;
        }

        private void ClearEnemyPaths()
        {
            for (int i = 0; i < _pathRenderers.Count; i++)
            {
                if (_pathRenderers[i] != null) _pathRenderers[i].positionCount = 0;
            }
        }

        private void OnDestroy()
        {
            ClearEnemyPaths();
            for (int i = 0; i < _pathRenderers.Count; i++)
            {
                if (_pathRenderers[i] != null) Destroy(_pathRenderers[i].gameObject);
            }
            _pathRenderers.Clear();
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
                var snap = new PlayerData(UserData.GetPlayerData());
                snap.Position = map.Player.Position;
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
                var copy = new PlayerData(_snap);

                live.GameStatus = copy.GameStatus;
                live.InventoryItems = copy.InventoryItems;
                live.PartyMemberDatas = copy.PartyMemberDatas;
                live.Steps.Set(copy.Steps.Value.Get());
                live.DeadCount.Set(copy.DeadCount.Value.Get());
                live.PlayTime.Set(copy.PlayTime.Value.Get());
                Blue.PlayTimeCounter._seconds = (float)copy.PlayTime.Value.Get();
                live.Position = _snap.Position;
                live.Direction = _snap.Direction;

                scene._inventoryService.LoadPossessionIconsAsync();

                if (scene.CurrentMap != null && scene.CurrentMap.MapData != null && scene.CurrentMap.MapData.Id == _snapMapId)
                {
                    var map = scene.CurrentMap;
                    map.SetSprite(map.Player, _snap.Position);
                    map.Player.ChangeDirection(_snap.Direction);
                    map.ResetPartyMemberInformation();
                    return;
                }

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
                if (all == null) return;

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
                    catch { break; }
                    if (m == null) break;
                    AddMapEntry(m);
                }
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
                if (scene == null || scene.IsMapTransition) return;

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
                if (GameScene.Instance == null) return;
                var items = MasterProvider.ItemMaster.GetAll();
                if (items == null) return;

                var collection = items.TryCast<Il2CppSystem.Collections.Generic.ICollection<ItemMasterModel>>();
                int count = collection.Count;
                for (int i = 0; i < count; i++)
                {
                    string id = items[i].Id;
                    if (!UserData.HasItem(id)) _addItemQueue.Enqueue(id);
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[F2] 失敗: {e}");
            }
        }

        private void ProcessAddItemQueue()
        {
            if (_addItemQueue.Count == 0 || GameScene.Instance == null) return;
            try
            {
                var svc = GameScene.Instance._inventoryService;
                for (int n = 0; n < AddItemsPerFrame && _addItemQueue.Count > 0; n++)
                {
                    svc.AddItem(_addItemQueue.Dequeue());
                }
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
                    displaySteps = string.Format(rawFormat, steps.ToString());
                }
                catch { displaySteps = $"steps: {steps}"; }

                try { timeLabel = MasterProvider.SystemTextMaster.Get("MenuWindow_PlayTimeText").Text; }
                catch { timeLabel = "Time"; }

                int playTimeValue = Blue.Data.UserData.GetPlayerData().PlayTime.Value.Get();
                TimeSpan timeSpan = new TimeSpan(0, 0, playTimeValue);
                displayTime = $"{timeSpan.Hours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";

                try
                {
                    var map = GameScene.Instance?.CurrentMap;
                    if (map != null && map.MapData != null)
                    {
                        var t = MasterProvider.SystemTextMaster.Get(map.MapData.SystemTextId);
                        displayMap = (t != null && !string.IsNullOrEmpty(t.Text)) ? t.Text : map.MapData.AssetName;
                    }
                    else { displayMap = "-"; }
                }
                catch { displayMap = "-"; }
            }
            catch { }
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
            float width = 250f; // 稍微加寬，避免多語系文字折行

            // 背景框依是否有鬼動作自動調整高度
            float boxHeight = (ShowEnemyPath && _currentKillerSteps >= 0) ? 230f : 205f;
            if (_addItemQueue.Count > 0) boxHeight += 25f;

            GUI.Box(new Rect(x - 5, y - 5, width, boxHeight), "");

            Color originalColor = GUI.skin.label.normal.textColor;
            GUI.skin.label.normal.textColor = Color.white;

            float curY = y;

            // 1. 步數
            GUI.Label(new Rect(x, curY, width, 22), displaySteps);
            curY += 24f;

            // 2. 遊戲時間
            GUI.Label(new Rect(x, curY, width, 22), $"{timeLabel}: {displayTime}");
            curY += 24f;

            // 3. 當前地圖
            GUI.Label(new Rect(x, curY, width, 22), $"{L("Map", "地圖", "マップ")}: {displayMap}");
            curY += 24f;

            // 4. 路徑預測開關狀態
            GUI.Label(new Rect(x, curY, width, 22),
                $"{L("Path (F5)", "路徑預測 (F5)", "経路表示 (F5)")}: {(ShowEnemyPath ? "ON" : "OFF")}");
            curY += 24f;

            // ★ 5. 鬼動作（支援 英文 / 中文 / 日文，且紅色高亮顯示）
            if (ShowEnemyPath && _currentKillerSteps >= 0)
            {
                string dirStr = _currentKillerDir switch
                {
                    Blue.Direction.Up => L("↑ Up", "↑ 上", "↑ 上"),
                    Blue.Direction.Down => L("↓ Down", "↓ 下", "↓ 下"),
                    Blue.Direction.Left => L("← Left", "← 左", "← 左"),
                    Blue.Direction.Right => L("→ Right", "→ 右", "→ 右"),
                    _ => _currentKillerDir.ToString()
                };

                string enemyLabel = L("Enemy", "鬼動作", "鬼行動");
                string remainLabel = L($"Remain: {_currentKillerSteps}",
                                       $"剩餘: {_currentKillerSteps}步",
                                       $"残り: {_currentKillerSteps}歩");

                GUI.skin.label.normal.textColor = new Color(1f, 0.35f, 0.35f); // 亮紅色
                GUI.Label(new Rect(x, curY, width, 22),
                    $"{enemyLabel}: {dirStr} ({_currentKillerTarget.x},{_currentKillerTarget.y}) {remainLabel}");
                GUI.skin.label.normal.textColor = originalColor;

                curY += 25f; // 自動將按鈕下推，不再重疊！
            }

            curY += 4f;

            // 6. 添加物品按鈕
            bool busy = _addItemQueue.Count > 0;
            bool oldEnabled = GUI.enabled;
            GUI.enabled = !busy;
            if (GUI.Button(new Rect(x, curY, width - 10, 30),
                busy ? L("Adding...", "加入中...", "追加中...")
                     : L("Add all items", "添加該模式所有物品", "このモードの全アイテムを追加")))
            {
                EnqueueAllItems();
            }
            GUI.enabled = oldEnabled;
            curY += 34f;

            if (busy)
            {
                GUI.Label(new Rect(x, curY, width, 22), $"{L("Remaining", "剩餘", "残り")}: {_addItemQueue.Count}");
                curY += 24f;
            }

            // 7. 地圖傳送按鈕
            var mapBtn = new Rect(x, curY, width - 10, 30);
            GUI.Box(mapBtn, _showMapPanel
                ? L("Close map menu", "關閉地圖選單", "マップメニューを閉じる")
                : L("Teleport", "地圖傳送", "マップ移動"));

            var ev = Event.current;
            if (ev.type == EventType.MouseDown && ev.button == 0 && mapBtn.Contains(ev.mousePosition))
            {
                _showMapPanel = !_showMapPanel;
                if (_showMapPanel) RefreshMapList();
                ev.Use();
            }

            // 地圖傳送清單面板
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
            float px = 20f, py = 240f;
            float pw = Mathf.Min(380f, Screen.width - px - 10f);
            const float rowH = 28f;
            int totalPages = Mathf.Max(1, (_maps.Count + MapsPerPage - 1) / MapsPerPage);
            _mapPage = Mathf.Clamp(_mapPage, 0, totalPages - 1);

            float ph = 30f + MapsPerPage * rowH + 40f;
            ph = Mathf.Min(ph, Screen.height - py - 10f);

            string title = L($"Map Teleport ({_maps.Count})  Page {_mapPage + 1}/{totalPages}",
                             $"地圖傳送 ({_maps.Count})  第 {_mapPage + 1}/{totalPages} 頁",
                             $"マップ移動 ({_maps.Count})  {_mapPage + 1}/{totalPages} ページ");
            GUI.Box(new Rect(px, py, pw, ph), title);

            string target = null;
            int start = _mapPage * MapsPerPage;
            for (int i = 0; i < MapsPerPage; i++)
            {
                int idx = start + i;
                if (idx >= _maps.Count) break;

                var r = new Rect(px + 8, py + 28 + i * rowH, pw - 16, rowH - 2);
                if (ClickButton(r, _maps[idx].Label)) target = _maps[idx].Id;
            }

            float navY = py + 28 + MapsPerPage * rowH + 4;
            if (ClickButton(new Rect(px + 8, navY, 90, 28), L("Prev", "上一頁", "前へ"))) { _mapPage--; }
            if (ClickButton(new Rect(px + pw - 98, navY, 90, 28), L("Next", "下一頁", "次へ"))) { _mapPage++; }

            if (target != null) TeleportTo(target);
        }

        private void ReloadCurrentSave()
        {
            try
            {
                var scene = GameScene.Instance;
                if (scene == null || scene.IsMapTransition)
                {
                    Toast(L("Can't reload now", "目前無法讀檔", "今は読み込めません"));
                    return;
                }

                // 1. 取得當前遊玩的存檔槽位 (0-based)
                int slotNo = SaveDataProvider.GetCurrentSlotNo();

                // 2. 從硬碟讀取該槽位的 PlayerData
                PlayerData savedData = SaveDataProvider.GetPlayerData(slotNo);
                if (savedData == null)
                {
                    Toast(L("No save data in current slot", "當前槽位無存檔", "セーブデータがありません"));
                    return;
                }

                // 3. 停用當前的鬼怪追擊，避免場景重啟時殘留音效或邏輯
                if (scene.KillerManager != null && scene.KillerManager.Chasing)
                {
                    scene.KillerManager.SuccessEscape(destroy: true);
                }

                // 4. 標記槽位並將硬碟存檔覆寫回遊戲全局記憶體 (UserData)
                SaveDataProvider.IsAutoSaveActive = true;
                SaveDataProvider.WriteSlotNo(slotNo);
                UserData.SetPlayerData(new PlayerData(savedData));

                // 5. 同步遊戲計時器
                Blue.PlayTimeCounter._seconds = (float)savedData.PlayTime.Value.Get();

                // 6. 官方標準重載：重啟主場景（會完全重置地圖機關、事件、鬼怪）
                string activeSceneName = Shirakami.SceneManagement.SceneController.GetActiveSceneBase().gameObject.scene.name;
                Shirakami.SceneManagement.SceneController.Change(activeSceneName);

                Toast(L($"Reloaded Slot {slotNo + 1}", $"已重讀存檔 {slotNo + 1}", $"スロット {slotNo + 1} を再読込"));
                Plugin.Log.LogInfo($"[Save] 成功重讀槽位 {slotNo + 1} ({savedData.MapId})");
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[Save] 重讀存檔失敗: {e}");
                Toast(L("Reload failed", "重讀存檔失敗", "再読込失敗"));
            }
        }
    }
}