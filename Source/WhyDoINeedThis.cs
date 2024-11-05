namespace Celeste.Mod.CU2Platinums;

public class WhyDoINeedThis
{
  public static AreaStats GetAreaStatsFor(AreaKey area)
  {
    return SaveData.Instance.GetAreaStatsFor(area);
  }
  public static LevelSetStats GetLevelSetStats(AreaKey area)
  {
    return SaveData.Instance.GetLevelSetStatsFor(area.GetLevelSet());
  }
}
