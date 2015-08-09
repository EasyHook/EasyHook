
using System.Diagnostics;
namespace EasyHook.IPC
{
  /// <summary>
  /// Represents EasyHook's (future) CoreClass/DomainManager/...
  /// </summary>
  public static class DummyCore
  {

    public static ConnectionManager ConnectionManager { get; set; }

    static DummyCore()
    {
      ConnectionManager = new ConnectionManager();
    }

    public static void StartRemoteProcess(string exe)
    {
      string channelUrl = ConnectionManager.InitializeInterDomainConnection();
      Process.Start(exe, channelUrl);
    }

    public static void InitializeAsRemoteProcess(string channelUrl)
    {
      ConnectionManager.ConnectInterDomainConnection(channelUrl);
    }

  }
}
