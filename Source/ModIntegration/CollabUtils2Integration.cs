using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using MonoMod.Utils;

namespace Celeste.Mod.CU2Platinums.ModIntegration;

// functions stolen from izumiano/izumisQOL/ModIntegration/CollabUtils2Integration.cs
// https://github.com/izumiano/izumisQOL/blob/master/Scripts/ModIntegration/CollabUtils2Integration.cs
public static class CollabUtils2Integration
{
  public static bool Loaded;

  private static Type progressPageType;

  private static EverestModule collabUtils2Module;
  private static MethodInfo isHeartSide_MethodInfo;

  public static bool TryGetModule(EverestModuleMetadata meta, out EverestModule module)
  {
    foreach (EverestModule other in Everest.Modules)
    {
      EverestModuleMetadata otherData = other.Metadata;
      if (otherData.Name != meta.Name) continue;

      Version version = otherData.Version;
      if (!Everest.Loader.VersionSatisfiesDependency(meta.Version, version)) continue;

      module = other;
      return true;
    }

    module = null;
    return false;
  }

  public static void Load()
  {
    if (!TryGetModule(new EverestModuleMetadata
    {
      Name = "CollabUtils2",
      VersionString = "1.8.11",
    }, out collabUtils2Module))
    {
      return;
    }

    Loaded = true;

    progressPageType = collabUtils2Module!.GetType().Module.GetType("Celeste.Mod.CollabUtils2.UI.OuiJournalCollabProgressInLobby");
    isHeartSide_MethodInfo = collabUtils2Module.GetType().Module.GetType("Celeste.Mod.CollabUtils2.LobbyHelper")?.GetMethod("IsHeartSide", BindingFlags.Static | BindingFlags.Public);
  }

  public static bool IsHeartSide(string sid)
  {
    object isHeartSide = isHeartSide_MethodInfo?.Invoke(null, new object[] { sid });
    return (bool)(isHeartSide ?? false);
  }

  private static List<AreaStats> GetUnsortedCollabStats(SaveData instance, string levelSet)
  {
    string journalLevelSet = levelSet;

    LevelSetStats levelSetStats = instance.GetLevelSetStatsFor(journalLevelSet);

    if (levelSetStats.Areas.TrueForAll(area => area.SID != "SpringCollab2020/5-Grandmaster/ZZ-NewHeartSide"))
    {
      return levelSetStats.Areas;
    }
    return levelSetStats.Areas.Where(area => area.SID != "SpringCollab2020/5-Grandmaster/ZZ-HeartSide").ToList();
  }

  public static List<AreaStats> GetSortedCollabAreaStats(SaveData instance, string levelSet)
  {
    List<AreaStats> areaStats = GetUnsortedCollabStats(instance, levelSet);

    var areaStatsArray = new AreaStats[areaStats.Count];
    areaStats.CopyTo(areaStatsArray);
    List<AreaStats> areaStatsCopy = areaStatsArray.ToList();

    Regex startsWithNumber = new(".*/[0-9]+-.*");
    if (areaStats.Select(map => AreaData.Get(map).Icon ?? "").All(icon => startsWithNumber.IsMatch(icon)))
    {
      areaStatsCopy.Sort(delegate (AreaStats a, AreaStats b)
      {
        AreaData aAreaData = AreaData.Get(a);
        AreaData bAreaData = AreaData.Get(b);
        bool aIsHeartSide = IsHeartSide(a.SID);
        bool bIsHeartSide = IsHeartSide(b.SID);
        if (aIsHeartSide && !bIsHeartSide)
        {
          return 1;
        }
        if (!aIsHeartSide && bIsHeartSide)
        {
          return -1;
        }
        return aAreaData.Icon != bAreaData.Icon ? aAreaData.Icon.CompareTo(bAreaData.Icon) : aAreaData.Name.CompareTo(bAreaData.Name);
      });
    }

    return areaStatsCopy;
  }

  private static int FirstProgressPage(OuiJournal journal)
  {
    var i = 0;
    while (journal.Pages[i].GetType() != progressPageType)
    {
      if (i + 1 > journal.Pages.Count - 1)
      {
        return -1;
      }
      i++;
    }
    return i;
  }

  private const int MAPS_PER_PAGE = 12;
  public static int MapsOnPage(OuiJournalPage page, OuiJournal journal, SaveData instance, out int firstIndexOnPage)
  {
    int firstProgressPage = FirstProgressPage(journal);
    if (firstProgressPage == -1)
    {
      firstIndexOnPage = -1;
      return -1;
    }

    int i = page.PageIndex - firstProgressPage;
    firstIndexOnPage = MAPS_PER_PAGE * i;
    string levelSet = journal.Overworld is null ? null : new DynData<Overworld>(journal.Overworld).Get<AreaData>("collabInGameForcedArea")?.LevelSet;
    int val = GetUnsortedCollabStats(instance, levelSet).Count - MAPS_PER_PAGE * i;
    return val > MAPS_PER_PAGE ? MAPS_PER_PAGE : val;
  }
}
