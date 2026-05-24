using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace AdofaiOnline.Patches;


[HarmonyPatch(typeof(SceneManager))]
internal static class SceneManagerPatch
{
    public static bool remote = false;
    [HarmonyPostfix]
    [HarmonyPatch(nameof(SceneManager.LoadScene), new Type[] { typeof(string), typeof(LoadSceneMode) })]
    internal static void LoadScene1Postfix(string sceneName, LoadSceneMode mode)
    {
        if (!remote)
        {
            if (mode != LoadSceneMode.Single)
                return;
            byte[] levelData = new byte[1 + (GCS.internalLevelName == null ? 0 : GCS.internalLevelName.Length)];
            levelData[0] = (byte)PacketType.SetLevel;
            Buffer.BlockCopy(Encoding.UTF8.GetBytes(GCS.internalLevelName), 0, levelData, 1, GCS.internalLevelName.Length);
            Networking.SendToHost(levelData);

            byte[] data = new byte[1 + sceneName.Length];
            data[0] = (byte)PacketType.ChangeScene;
            Buffer.BlockCopy(Encoding.UTF8.GetBytes(sceneName), 0, data, 1, sceneName.Length);
            Networking.SendToHost(data);
        }
        remote = false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(SceneManager.LoadScene), new Type[] { typeof(string) })]
    internal static void LoadScene2Postfix(string sceneName)
    {
        if (!remote)
        {
            byte[] levelData = new byte[1 + (GCS.internalLevelName==null?0:GCS.internalLevelName.Length)];
            levelData[0] = (byte)PacketType.SetLevel;
            if (GCS.internalLevelName != null)
                Buffer.BlockCopy(Encoding.UTF8.GetBytes(GCS.internalLevelName), 0, levelData, 1, GCS.internalLevelName.Length);
            Networking.SendToHost(levelData);

            byte[] data = new byte[1 + sceneName.Length];
            data[0] = (byte)PacketType.ChangeScene;
            Buffer.BlockCopy(Encoding.UTF8.GetBytes(sceneName), 0, data, 1, sceneName.Length);
            Networking.SendToHost(data);
        }
        remote = false;
    }
}