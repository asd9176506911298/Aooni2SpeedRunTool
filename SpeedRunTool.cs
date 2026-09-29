using Blue.Common;
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

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                Plugin.Log.LogInfo($"[F1]");
                LogAndGetStatus();
            }

            updateTimer += Time.deltaTime;
            if (updateTimer >= updateInterval)
            {
                updateTimer = 0f;
                UpdateData();
            }
        }

        private void UpdateData()
        {
            try
            {
                // 1. 取得 MasterProvider 的字串（例如 "{0}steps"），並將 {0} 換成 steps 數值
                // 如果你想改成 "steps: {0}" 或 "步: {0}"，可以把原本的文字中的 "{0}" 替換掉
                int steps = Blue.Data.UserData.GetPlayerData().Steps.Value.Get();
                try
                {
                    string rawFormat = MasterProvider.SystemTextMaster.Get("MenuWindow_StepsValue").Text;
                    // 如果原格式是 "{0}steps"，我們可以把它轉換成你想要的格式
                    // 這裡示範將 {0} 轉移到自訂格式，例如：steps: {0} 或 步: {0}
                    // 假設我們想把 rawFormat 裡面的 {0} 抽出來：
                    string stepsOnly = steps.ToString();

                    // 方法 A：直接用原本的格式 (如果你希望維持遊戲內原本的翻譯位置)
                    displaySteps = string.Format(rawFormat, stepsOnly);

                    // 方法 B：如果你想強制改成 "steps: {0}" 或依語系變化，可以這樣處理：
                    // 如果原字串包含 "steps"，我們把它換成 "steps: {0}" 或套用你的規則
                    // 這裡示範直接把 {0} 放到你指定的位置：
                    // displaySteps = $"steps: {stepsOnly}"; // 或者是抓出數字填入
                }
                catch
                {
                    displaySteps = $"steps: {steps}";
                }

                // 2. 取得時間標題 (例如 Time)
                try
                {
                    timeLabel = MasterProvider.SystemTextMaster.Get("MenuWindow_PlayTimeText").Text;
                }
                catch
                {
                    timeLabel = "Time";
                }

                // 3. 取得並格式化遊戲時間 (格式為 {0}:{1}:{2})
                int playTimeValue = Blue.Data.UserData.GetPlayerData().PlayTime.Value.Get();
                TimeSpan timeSpan = new TimeSpan(0, 0, playTimeValue);

                string hours = timeSpan.Hours.ToString().PadLeft(2, '0');
                string minutes = timeSpan.Minutes.ToString().PadLeft(2, '0');
                string seconds = timeSpan.Seconds.ToString().PadLeft(2, '0');

                displayTime = $"{hours}:{minutes}:{seconds}";
            }
            catch
            {
                // 略過未初始化時的錯誤
            }
        }

        private void OnGUI()
        {
            float x = 20f;
            float y = 20f;
            float width = 220f;
            float height = 55f;

            // 繪製背景框
            GUI.Box(new Rect(x - 5, y - 5, width, height), "");

            // 設定白色字體
            Color originalColor = GUI.skin.label.normal.textColor;
            GUI.skin.label.normal.textColor = Color.white;

            // 顯示文字 (步數與時間)
            GUI.Label(new Rect(x, y, width, 25), displaySteps);
            GUI.Label(new Rect(x, y + 25, width, 25), $"{timeLabel}: {displayTime}");

            // 恢復原本的顏色
            GUI.skin.label.normal.textColor = originalColor;
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