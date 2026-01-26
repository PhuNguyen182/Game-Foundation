using System;
using UnityEngine;
using Object = UnityEngine.Object;

public abstract class Debug : UnityEngine.Debug
{
    public new static void Assert(bool condition)
    {
#if USE_LOGGER_UTILS
        UnityEngine.Debug.Assert(condition);
#endif
    }

    public new static void Assert(bool condition, Object context)
    {
#if USE_LOGGER_UTILS
        UnityEngine.Debug.Assert(condition, context);
#endif
    }

    public new static void Assert(bool condition, string message)
    {
#if USE_LOGGER_UTILS
        UnityEngine.Debug.Assert(condition, message);
#endif
    }

    public new static void Assert(bool condition, string message, Object context)
    {
#if USE_LOGGER_UTILS
        UnityEngine.Debug.Assert(condition, message, context);
#endif
    }

    public new static void AssertFormat(bool condition, string format, params object[] args)
    {
#if USE_LOGGER_UTILS
        UnityEngine.Debug.AssertFormat(condition, format, args);
#endif
    }

    public new static void AssertFormat(bool condition, Object context, string format, params object[] args)
    {
#if USE_LOGGER_UTILS
        UnityEngine.Debug.AssertFormat(condition, context, format, args);
#endif
    }

    public new static void Log(object message, Object context = null)
    {
#if USE_LOGGER_UTILS
        UnityEngine.Debug.Log(message, context);
#endif
    }

    public new static void LogFormat(string format, params object[] args)
    {
#if USE_LOGGER_UTILS
        UnityEngine.Debug.LogFormat(format, args);
#endif
    }

    public new static void LogFormat(Object context, string format, params object[] args)
    {
#if USE_LOGGER_UTILS
        UnityEngine.Debug.LogFormat(context, format, args);
#endif
    }

    public new static void LogFormat(LogType logType, LogOption logOptions, Object context, string format,
        params object[] args)
    {
#if USE_LOGGER_UTILS
        UnityEngine.Debug.LogFormat(logType, logOptions, context, format, args);
#endif
    }

    public new static void LogWarning(object message, Object context = null)
    {
#if USE_LOGGER_UTILS
        UnityEngine.Debug.LogWarning(message, context);
#endif
    }

    public new static void LogWarningFormat(string format, params object[] args)
    {
#if USE_LOGGER_UTILS
        UnityEngine.Debug.LogWarningFormat(format, args);
#endif
    }

    public new static void LogError(object message, Object context = null)
    {
#if USE_LOGGER_UTILS
        UnityEngine.Debug.LogError(message, context);
#endif
    }

    public new static void LogErrorFormat(string format, params object[] args)
    {
#if USE_LOGGER_UTILS
        UnityEngine.Debug.LogErrorFormat(format, args);
#endif
    }

    public new static void LogErrorFormat(Object context, string format, params object[] args)
    {
#if USE_LOGGER_UTILS
        UnityEngine.Debug.LogErrorFormat(context, format, args);
#endif
    }

    public new static void LogException(Exception exception)
    {
#if USE_LOGGER_UTILS
        UnityEngine.Debug.LogException(exception);
#endif
    }

    public new static void LogException(Exception exception, Object context)
    {
#if USE_LOGGER_UTILS
        UnityEngine.Debug.LogException(exception, context);
#endif
    }

    public new static void LogAssertion(object message, Object context = null)
    {
#if USE_LOGGER_UTILS
        UnityEngine.Debug.LogAssertion(message, context);
#endif
    }

    public new static void LogAssertionFormat(string format, params object[] args)
    {
#if USE_LOGGER_UTILS
        UnityEngine.Debug.LogAssertionFormat(format, args);
#endif
    }

    public new static void LogAssertionFormat(Object context, string format, params object[] args)
    {
#if USE_LOGGER_UTILS
        UnityEngine.Debug.LogAssertionFormat(context, format, args);
#endif
    }

    public new static StartupLog[] RetrieveStartupLogs()
    {
#if USE_LOGGER_UTILS
        return UnityEngine.Debug.RetrieveStartupLogs();
#else
        return null;
#endif
    }
}
