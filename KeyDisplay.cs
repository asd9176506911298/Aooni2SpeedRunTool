using System.Globalization;
using UnityEngine;

namespace Aooni2SpeedRunTool
{
    /// <summary>
    /// 按鍵輸入顯示面板：預設固定在左下角（Toast 上方），
    /// 可用滑鼠左鍵拖曳移動，放開時位置會存進 BepInEx 的 .cfg；
    /// 在面板上按右鍵可重置回預設位置。
    /// 純 C# 類別：由 SpeedRunTool 持有，
    /// 在它的 OnGUI 開頭呼叫 HandleInput()，結尾呼叫 Draw()。
    ///
    /// IL2CPP 注意：這個遊戲裡 GUI.DrawTexture 被 strip、自建 GUIStyle 會 NRE、
    /// PlayerPrefs 會 MissingMethodException，所以這裡只使用：
    ///   GUI.Box(rect, text) / GUI.Label(rect, text) / GUI.skin.label.normal.textColor / GUI.color
    /// </summary>
    internal class KeyDisplay
    {
        public bool Visible = true;

        private const float KeySize = 36f;
        private const float KeyGap = 4f;
        private const float Pad = 8f;
        private const float Cols = 10f;
        private const int Rows = 3;

        private const float MarginLeft = 20f;
        private const float MarginBottom = 60f;

        private const float LabelOffsetX = 11f;
        private const float LabelOffsetY = 9f;
        private const float LabelOffsetXMulti = 9f;   // 多字元標籤（Tab / Enter / Back）

        private const int PressedLayers = 3;

        private struct KeyCell
        {
            public KeyCode Key;
            public string Label;
            public float Col;
            public int Row;

            public KeyCell(KeyCode key, string label, float col, int row)
            {
                Key = key;
                Label = label;
                Col = col;
                Row = row;
            }
        }

        private static readonly KeyCell[] Cells =
        {
            new KeyCell(KeyCode.W,       "W",  1f,   0),
            new KeyCell(KeyCode.UpArrow, "↑",  4.5f, 0),

            new KeyCell(KeyCode.A,         "A",  0f,   1),
            new KeyCell(KeyCode.S,         "S",  1f,   1),
            new KeyCell(KeyCode.D,         "D",  2f,   1),
            new KeyCell(KeyCode.LeftArrow, "←",  3.5f, 1),
            new KeyCell(KeyCode.DownArrow, "↓",  4.5f, 1),
            new KeyCell(KeyCode.RightArrow,"→",  5.5f, 1),
            new KeyCell(KeyCode.Z,         "Z",  7f,   1),
            new KeyCell(KeyCode.X,         "X",  8f,   1),
            new KeyCell(KeyCode.C,         "C",  9f,   1),

            new KeyCell(KeyCode.Tab,       "Tab",   0.5f, 2),
            new KeyCell(KeyCode.Return,    "Enter", 3.5f, 2),
            new KeyCell(KeyCode.Backspace, "Back",  6.5f, 2),
        };

        private bool _moved;          // true = 使用自訂位置；false = 貼齊左下角
        private float _x, _y;
        private bool _dragging;
        private Vector2 _dragOffset;

        private static float PanelWidth => Cols * (KeySize + KeyGap) - KeyGap + Pad * 2f;
        private static float PanelHeight => Rows * (KeySize + KeyGap) - KeyGap + Pad * 2f;

        public KeyDisplay()
        {
            LoadPosition();
        }

        // ---------- 存 / 讀 / 重置 ----------

        private void LoadPosition()
        {
            try
            {
                float sx = Plugin.KeyDisplayX.Value;
                float sy = Plugin.KeyDisplayY.Value;
                if (sx < 0f || sy < 0f) return;   // 沒存過 → 用預設
                _x = sx;
                _y = sy;
                _moved = true;
            }
            catch { }
        }

        private void SavePosition()
        {
            try
            {
                Plugin.KeyDisplayX.Value = _x;
                Plugin.KeyDisplayY.Value = _y;
            }
            catch { }
        }

        /// <summary>回到左下角預設位置，並清除存檔。</summary>
        public void ResetPosition()
        {
            _moved = false;
            _dragging = false;
            try
            {
                Plugin.KeyDisplayX.Value = -1f;
                Plugin.KeyDisplayY.Value = -1f;
            }
            catch { }
        }

        // ---------- 輸入（OnGUI 最開頭呼叫）----------

        public void HandleInput()
        {
            if (!Visible) return;

            float w = PanelWidth, h = PanelHeight;
            if (!_moved)
            {
                _x = MarginLeft;
                _y = Screen.height - h - MarginBottom;
            }
            else if (!_dragging)
            {
                // 解析度變小時，避免位置跑出畫面外
                _x = Mathf.Clamp(_x, 0f, Mathf.Max(0f, Screen.width - w));
                _y = Mathf.Clamp(_y, 0f, Mathf.Max(0f, Screen.height - h));
            }

            var ev = Event.current;
            var rect = new Rect(_x, _y, w, h);

            if (ev.type == EventType.MouseDown && ev.button == 0 && rect.Contains(ev.mousePosition))
            {
                _dragging = true;
                _moved = true;
                _dragOffset = new Vector2(ev.mousePosition.x - _x, ev.mousePosition.y - _y);
                ev.Use();
            }
            else if (ev.type == EventType.MouseDown && ev.button == 1 && rect.Contains(ev.mousePosition))
            {
                ResetPosition();      // 右鍵重置
                ev.Use();
            }
            else if (_dragging)
            {
                if (ev.type == EventType.MouseDrag || ev.type == EventType.MouseMove)
                {
                    _x = Mathf.Clamp(ev.mousePosition.x - _dragOffset.x, 0f, Mathf.Max(0f, Screen.width - w));
                    _y = Mathf.Clamp(ev.mousePosition.y - _dragOffset.y, 0f, Mathf.Max(0f, Screen.height - h));
                    ev.Use();
                }
                else if (ev.type == EventType.MouseUp)
                {
                    _dragging = false;
                    SavePosition();   // 放開才存，避免拖曳中每幀寫檔
                    ev.Use();
                }
            }
        }

        // ---------- 繪製（OnGUI 結尾呼叫）----------

        public void Draw()
        {
            if (!Visible) return;
            var ev = Event.current;
            if (ev.type != EventType.Repaint) return;

            float w = PanelWidth, h = PanelHeight;
            var rect = new Rect(_x, _y, w, h);

            var oldColor = GUI.color;
            var oldText = GUI.skin.label.normal.textColor;
            float step = KeySize + KeyGap;

            GUI.color = Color.white;
            GUI.Box(rect, "");
            if (_dragging) GUI.Box(rect, "");

            for (int i = 0; i < Cells.Length; i++)
            {
                var c = Cells[i];
                bool down = Input.GetKey(c.Key);

                // 3 字以上的標籤（Tab / Enter / Back）用寬鍵
                float keyW = (c.Label.Length > 2) ? KeySize * 1.6f : KeySize;
                var kr = new Rect(_x + Pad + c.Col * step, _y + Pad + c.Row * step, keyW, KeySize);

                if (down)
                {
                    GUI.color = new Color(1f, 0.85f, 0.2f, 1f);
                    for (int n = 0; n < PressedLayers; n++) GUI.Box(kr, "");
                }
                else
                {
                    GUI.color = Color.white;
                    GUI.Box(kr, "");
                }

                GUI.color = Color.white;
                GUI.skin.label.normal.textColor = down ? Color.black : Color.white;

                float ox = (c.Label.Length > 1) ? LabelOffsetXMulti : LabelOffsetX;
                GUI.Label(new Rect(kr.x + ox, kr.y + LabelOffsetY, keyW, KeySize), c.Label);
            }

            GUI.skin.label.normal.textColor = oldText;
            GUI.color = oldColor;
        }
    }
}