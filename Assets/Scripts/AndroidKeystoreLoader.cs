using UnityEditor;

#if UNITY_EDITOR
[InitializeOnLoad]
public class AndroidKeystoreLoader
{
    static AndroidKeystoreLoader()
    {
        PlayerSettings.Android.keystorePass = "grassproject";
        PlayerSettings.Android.keyaliasName = "grass";
        PlayerSettings.Android.keyaliasPass = "grassproject";
    }
}
#endif