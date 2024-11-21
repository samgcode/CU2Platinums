using Monocle;

using Celeste.Mod.CU2Platinums.PacePing;

namespace Celeste.Mod.CU2Platinums.IO
{
  public class Commands
  {
    [Command("cu2p_test_ping", "test pace ping")]
    public static void CMDTestPing(string arg)
    {
      PacePingManager.SendPing("Test pace ping");
    }

    [Command("cu2p_test_ping_update", "test pace ping")]
    public static void CMDTestUpdate(string arg)
    {
      PacePingManager.SendUpdate("test map");
    }
  }
}
