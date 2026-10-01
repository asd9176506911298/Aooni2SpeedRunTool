using Blue.Data.Game;
using Blue.ScoreCalculate;
using HarmonyLib;
using Shirakami;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aooni2SpeedRunTool
{
    internal class Patch
    {
        //[HarmonyPostfix, HarmonyPatch(typeof(Blue.Room.RoomBase), "CanMove")]
        //public static void HookCanMove(Blue.Room.CanMoveArgumentOption option)
        //{
        //    Plugin.Log.LogInfo($"[Patch]");
        //}

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ScoreCalculator), nameof(ScoreCalculator.CalcScore), new[] { typeof(ScoreCalculator.ScoreParameter) })]
        public static bool HookCalcScore(ref Score __result)
        {
            // 將結果強制設為 0 分
            __result = new Score((ProtectedInt)0);

            // 回傳 false 代表跳過遊戲原本複雜的計算邏輯（步驟、時間、係數運算通通不跑）
            return false;
        }
    }
}
