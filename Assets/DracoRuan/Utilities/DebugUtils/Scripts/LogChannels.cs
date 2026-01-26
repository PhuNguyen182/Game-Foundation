using UnityEngine;

[CreateAssetMenu(fileName = "LogChannels", menuName = "Practical Utilities/Debug Utils/Log Channels")]
public class LogChannels : ScriptableObject
{
    [SerializeField] public string[] channels;
}
