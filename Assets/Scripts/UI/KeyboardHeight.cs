using UnityEngine;

public class KeyboardHeight : MonoBehaviour
{
    private static AndroidJavaClass pluginClass;

    [RuntimeInitializeOnLoadMethod]
    static void Init()
    {
        pluginClass = new AndroidJavaClass("KeyboardHeightPlugin");
        pluginClass.CallStatic("StartListening");
    }

    public static int GetHeight()
    {
        return pluginClass.CallStatic<int>("GetKeyboardHeight");
    }
}