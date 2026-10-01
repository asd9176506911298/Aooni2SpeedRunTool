using Blue;
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
        [HarmonyPrefix, HarmonyPatch(typeof(Blue.Room.RoomBase), "CanMove")]
        public static bool HookCanMove(ref bool __result)
        {
            if (!SpeedRunTool.NoClip) return true;
            __result = true;
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(GameScene), "GameOver")]
        public static bool HookGameOver(DeathKind deathKind, KillerKind killerKind, bool isDrawing = false)
        {
            if (!SpeedRunTool.NoGameOver) return true;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ScoreCalculator), nameof(ScoreCalculator.CalcScore), new[] { typeof(ScoreCalculator.ScoreParameter) })]
        public static bool HookCalcScore(ref Score __result)
        {
            __result = new Score((ProtectedInt)0);
            return false;
        }
    }
}
