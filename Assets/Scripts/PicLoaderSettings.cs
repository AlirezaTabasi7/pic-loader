using UnityEngine;

[CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/Settings/PicLoader", order = 1)]
public class PicLoaderSettings : ScriptableObject
{
    [Header("References")] 
    public Texture2D loadingPlaceholder;
    public Texture2D errorPlaceholder;
    
    [Header("Settings")]
    public bool enableLog;
    public bool cached = true;
    public float fadeTime = 1;
    public int timeout = 30;
    public int timeoutAttempts = 3;
}
