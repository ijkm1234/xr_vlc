using UnityEngine;
using System.IO;

public class TestKeystoreReader : MonoBehaviour
{
    public void CheckKeystore()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject dir = currentActivity.Call<AndroidJavaObject>("getDir", "keystore", 0))
            {
                string keystorePath = dir.Call<string>("getAbsolutePath") + "/file";
                Debug.Log($"[TestKeystoreReader] Checking {keystorePath}");
                
                if (File.Exists(keystorePath))
                {
                    string content = File.ReadAllText(keystorePath);
                    Debug.Log($"[TestKeystoreReader] File exists! Length: {content.Length}");
                    Debug.Log($"[TestKeystoreReader] Content:\n{content}");
                }
                else
                {
                    Debug.Log($"[TestKeystoreReader] File DOES NOT EXIST!");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TestKeystoreReader] Error: {e}");
        }
#endif
    }
}