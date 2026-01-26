using System.Collections.Generic;
using UnityEngine;

public static class LoggerUtils
{
    private static HashSet<string> _logChannels;

    public static void Initialize(string channelCollectionPath)
    {
        _logChannels = new();
        LogChannels logChannels = Resources.Load<LogChannels>(channelCollectionPath);

        for (int i = 0; i < logChannels.channels.Length; i++)
            _logChannels.Add(logChannels.channels[i]);
    }

    public static void Log(string message, string channel = null)
    {
        string logInfo = IsAvailableChannel(channel) ? $"[{channel}]{message}" : message;
        Debug.Log(logInfo);
    }

    public static void LogWarning(string message, string channel = null)
    {
        string logInfo = IsAvailableChannel(channel) ? $"[{channel}]{message}" : message;
        Debug.LogWarning(logInfo);
    }

    public static void LogError(string message, string channel = null)
    {
        string logInfo = IsAvailableChannel(channel) ? $"[{channel}]{message}" : message;
        Debug.LogError(logInfo);
    }

    private static bool IsAvailableChannel(string channel = null) =>
        !string.IsNullOrEmpty(channel) && _logChannels.Contains(channel);
}
