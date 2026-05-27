using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using static scrMistakesManager;

namespace AdofaiOnline.Patches;

[HarmonyPatch(typeof(scrMistakesManager))]
internal static class scrMistakesManagerPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(scrMistakesManager.SaveCustom))]
    internal static bool SaveCustomPatch(scrMistakesManager __instance, string hash, bool wonLevel, float multiplier, ref EndLevelInfo __result)
    {
        if (!scrController.coopMode || !Networking.IsConnected
#if !EXPERIMENT_CUSTOMS
            || true
#endif
            )
            return true;

        EndLevelInfo result = default(EndLevelInfo);
        result.endLevelType = EndLevelType.None;
        result.newBestType = NewBestType.None;
        if (__instance.controller.noFail && __instance.controller.playerOne.marginTracker.GetDeaths() > 0)
        {
            __result = result;
            return false;
        }

        if (__instance.controller.unlockKeyLimiter && __instance.controller.maximumUsedKeys > 1000)
        {
            __result = result;
            return false;
        }

        float customWorldAccuracy = Persistence.GetCustomWorldAccuracy(hash);
        float customWorldXAccuracy = Persistence.GetCustomWorldXAccuracy(hash);
        float customWorldCompletion = Persistence.GetCustomWorldCompletion(hash);
        bool customWorldIsHighestPossibleAcc = Persistence.GetCustomWorldIsHighestPossibleAcc(hash);
        bool flag = __instance.controller.playerOne.marginTracker.IsAllPurePerfect();
        if (multiplier >= 1f)
        {
            if (!wonLevel)
            {
                if (__instance.percentComplete > customWorldCompletion && __instance.controller.currentFloorID > 0)
                {
                    result.endLevelType = EndLevelType.NewBest;
                    result.newBestType = NewBestType.Regular;
                    //Persistence.SetCustomWorldCompletion(hash, __instance.percentComplete);
                }
            }
            else
            {
                float customWorldSpeedTrial = Persistence.GetCustomWorldSpeedTrial(hash);
                int customWorldMinDeaths = Persistence.GetCustomWorldMinDeaths(hash);
                //Persistence.SetCustomWorldCompletion(hash, 1f);
                if (customWorldCompletion < 1f && multiplier == 1f)
                {
                    result.endLevelType = EndLevelType.FirstWin;
                }
                else if (GCS.speedTrialMode && multiplier >= 1f && multiplier > customWorldSpeedTrial)
                {
                    result.endLevelType = EndLevelType.FirstWinSpeedTrial;
                    //Persistence.SetCustomWorldSpeedTrial(hash, multiplier);
                }

                if (__instance.percentAcc > customWorldAccuracy)
                {
                    //Persistence.SetCustomWorldAccuracy(hash, __instance.percentAcc);
                    if (flag)
                    {
                        //Persistence.SetCustomWorldIsHighestPossibleAcc(hash, isHighest: true);
                    }
                }

                if (__instance.percentXAcc > customWorldXAccuracy)
                {
                    //Persistence.SetCustomWorldXAccuracy(hash, __instance.percentXAcc);
                }

                if (__instance.customLevel.checkpointsUsed < customWorldMinDeaths || customWorldMinDeaths == -1)
                {
                    //Persistence.SetCustomWorldMinDeaths(hash, __instance.customLevel.checkpointsUsed);
                }

                if (!customWorldIsHighestPossibleAcc && flag)
                {
                    //Persistence.SetCustomWorldIsHighestPossibleAcc(hash, isHighest: true);
                }

                if (ADOBase.isTechFeaturedLevel)
                {
                    //Persistence.clearedTechFeatured = true;
                }
            }

            //Persistence.Save();
        }

        __result = result;
        return false;
    }
}
