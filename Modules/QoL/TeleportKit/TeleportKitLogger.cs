using System.IO;
using System.Text;

namespace GodhomeQoL.Modules.QoL;

internal sealed class TeleportKitLogger
{
    private readonly string logFilePath;

    internal TeleportKitLogger()
    {
        logFilePath = Path.Combine(Application.persistentDataPath, "QoLTeleportKit.log");
    }

    internal void Write(string message)
    {
        try
        {
            File.AppendAllText(
                logFilePath,
                $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] {message}\n",
                Encoding.UTF8
            );
        }
        catch (Exception e)
        {
            Logger.LogError($"[TeleportKit] Failed to write log: {e.Message}");
        }
    }
}
