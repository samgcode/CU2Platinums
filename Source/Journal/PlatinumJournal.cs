using System;
using System.Collections.Generic;
using System.IO;
using Celeste.Mod.CU2Platinums.ModIntegration;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;


// adapted from izumiano/izumisQOL/Scripts/BetterJournalModule.cs
// https://github.com/izumiano/izumisQOL/blob/master/Scripts/BetterJournalModule.cs
namespace Celeste.Mod.CU2Platinums.Journal
{
  public class PlatinumJournal
  {

    private static readonly string journalStatsPath = UserIO.SavePath.Replace("\\", "/") + "/CU2Platinums/";

    private static readonly Dictionary<OuiJournalPage, VirtualRenderTarget> renderTargets = new();

    private static VirtualRenderTarget RenderTarget
    {
      get
      {
        if (JournalProgressPage is null) return null;

        return renderTargets.GetValueOrDefault(JournalProgressPage);
      }
    }

    private static OuiJournal _journal;

    private static OuiJournalPage JournalProgressPage
    {
      get
      {
        if (_journal is null || _journal.PageIndex > _journal.Pages.Count - 1 || _journal.PageIndex < 0)
        {
          return null;
        }
        return _journal.Page();
      }
    }

    private static OuiJournalPage previousPage;

    private static List<CustomAreaStats> JournalSnapshot;

    private static bool displayDiff = false;

    public static void Load()
    {
      Directory.CreateDirectory(journalStatsPath);
    }

    public static void Update(On.Celeste.OuiJournal.orig_Update orig, OuiJournal self)
    {
      orig(self);

      if (CU2PlatinumsModule.currentLobby != null)
      {
        if (Input.MenuUp.Pressed)
        {
          if (!displayDiff)
          {
            displayDiff = true;
            UpdateJournalData(self);
          }
        }
        if (Input.MenuDown.Pressed)
        {
          if (displayDiff)
          {
            displayDiff = false;
            UpdateJournalData(self);
          }
        }

        if (JournalProgressPage != previousPage)
        {
          UpdateJournalData(self);
          previousPage = JournalProgressPage;
        }
      }
    }

    public static void OnPlatinumPickup()
    {
      SaveJournalSnapshot(CU2PlatinumsModule.currentLevelSet);
    }

    private static bool SaveJournalSnapshot(string levelSet)
    {
      SaveData instance = SaveData.Instance;
      if (instance == null)
        return false;

      List<CustomAreaStats> customAreaStats = new List<CustomAreaStats>();
      List<AreaStats> areaStats;

      areaStats = CollabUtils2Integration.GetSortedCollabAreaStats(instance, levelSet);

      CustomAreaStats TotalStats = new CustomAreaStats();

      areaStats.ForEach(area =>
      {
        CustomAreaStats customAreaStat = new CustomAreaStats(area.Modes);
        TotalStats.Add(customAreaStat);
        customAreaStats.Add(customAreaStat);
      });
      customAreaStats.Add(TotalStats);

      string levelSetName = levelSet.Replace("/", "").Replace("\n", "");
      string path = journalStatsPath + instance.FileSlot + "_" + levelSetName + ".txt";
      try
      {
        FileStream fileStream = File.Create(path);
        using StreamWriter writer = new(fileStream);
        YamlHelper.Serializer.Serialize(writer, customAreaStats, typeof(List<CustomAreaStats>));

        return true;
      }
      catch (Exception ex)
      {
        Logger.Error("CU2Platinums", ex.Message);
        return false;
      }
    }

    private static bool JournalStatFileExists(string levelSet, SaveData instance, out string path)
    {
      levelSet = levelSet?.Replace("/", "").Replace("\n", "");
      path = journalStatsPath + instance.FileSlot + "_" + levelSet + ".txt";
      return File.Exists(path);
    }

    private static List<CustomAreaStats> LoadJournalSnapshot(string levelSet)
    {
      SaveData instance = SaveData.Instance;
      if (instance == null)
        return null;

      List<CustomAreaStats> customAreaStats = null;

      try
      {
        if (!JournalStatFileExists(levelSet, instance, out string path))
        {
          Logger.Log(LogLevel.Info, "CU2Platinums", "Could not read from: " + path);
          return null;
        }

        FileStream fileStream = File.OpenRead(path);
        using StreamReader reader = new(fileStream);

        customAreaStats =
          (List<CustomAreaStats>)YamlHelper.Deserializer.Deserialize(reader, typeof(List<CustomAreaStats>));
      }
      catch (Exception ex)
      {
        Logger.Error("CU2Platinums", ex.Message);
      }

      return customAreaStats;
    }

    private static int GetInterludeOffset(int index, SaveData instance, OuiJournal journal, out AreaStats area)
    {
      List<AreaStats> areas;

      string levelSet = journal.Overworld is null ? null : new DynData<Overworld>(journal.Overworld).Get<AreaData>("collabInGameForcedArea")?.LevelSet;

      areas = CollabUtils2Integration.GetSortedCollabAreaStats(instance, levelSet);
      if (index >= areas.Count)
      {
        area = null;
        return 0;
      }

      area = areas[index];

      return 0;
    }

    private static List<OuiJournalPage.Cell> UpdateEntry(
      List<OuiJournalPage.Cell> entries, AreaStats area, CustomAreaStats customAreaStats, bool isHeartside)
    {
      AreaData areaData = AreaData.Get(area);
      AreaKey areaKey = areaData.ToKey();
      AreaModeStats areaModeStats = SaveData.Instance.Areas_Safe[areaKey.ID].Modes[(int)areaKey.Mode];

      bool mapCompleted = CU2PlatinumsModule.mapsCompleted.Contains(areaData.Name);

      int collectedBerries = areaModeStats.TotalStrawberries;
      int areaBerries = areaData.Mode[0].StartStrawberries;

      int newDeaths = area.Modes[0].Deaths;
      int deaths = newDeaths;

      long totalTime = area.TotalTimePlayed;
      long time = totalTime;

      if (displayDiff)
      {
        if (mapCompleted && CU2PlatinumsModule.collectedStrawberries.ContainsKey(areaData.Name))
        {
          collectedBerries = CU2PlatinumsModule.collectedStrawberries[areaData.Name].Count;
        }
        else
        {
          collectedBerries = 0;
        }

        int oldDeaths = customAreaStats.Deaths;

        int diff = newDeaths - oldDeaths;

        if (diff > 0 || mapCompleted)
        {
          deaths = diff;
        }
        else
        {
          deaths = -1;
        }

        long newTimePlayed = area.Modes[0].TimePlayed;
        long oldTimePlayed = customAreaStats.TimePlayed;

        time = newTimePlayed - oldTimePlayed;
      }

      OuiJournalPage.Cell berriesCell;
      OuiJournalPage.Cell deathsCell;
      OuiJournalPage.Cell timeCell;
      OuiJournalPage.Cell deathlessCell;
      OuiJournalPage.Cell completedCell;

      if (areaBerries == 0 && (displayDiff || collectedBerries == 0))
      {
        berriesCell = new OuiJournalPage.IconCell("dot");
      }
      else
      {
        berriesCell = new OuiJournalPage.TextCell($"{collectedBerries}/{areaBerries}", JournalProgressPage?.TextJustify ?? Vector2.Zero, 0.5f, JournalProgressPage.TextColor);
        berriesCell.SpreadOverColumns = 1;
      }

      if (deaths == -1)
      {
        deathsCell = new OuiJournalPage.IconCell("dot");
      }
      else
      {
        string deathDialog = Dialog.Deaths(deaths);
        deathDialog = (displayDiff && deaths > 0) ? $"+{deathDialog}" : deathDialog;

        Color customColor = deaths == 0 ? Color.Green : Color.Red;
        Color color = displayDiff ? customColor : JournalProgressPage.TextColor;
        deathsCell = new OuiJournalPage.TextCell(deathDialog, JournalProgressPage?.TextJustify ?? Vector2.Zero, 0.5f, color);
      }

      if (time == 0)
      {
        timeCell = new OuiJournalPage.IconCell("dot");
      }
      else
      {
        timeCell = new OuiJournalPage.TextCell(Dialog.Time(time), JournalProgressPage?.TextJustify ?? Vector2.Zero, 0.5f, JournalProgressPage.TextColor);
      }

      deathlessCell = new OuiJournalPage.IconCell("dot");
      completedCell = new OuiJournalPage.IconCell("dot");

      string heartTexture = MTN.Journal.Has("CollabUtils2Hearts/" + CU2PlatinumsModule.currentLevelSet) ? "CollabUtils2Hearts/" + CU2PlatinumsModule.currentLevelSet : "heartgem0";

      if (displayDiff)
      {
        if (mapCompleted)
        {
          completedCell = new OuiJournalPage.IconCell(heartTexture);
        }
      }
      else
      {
        if (areaModeStats.Completed)
        {
          completedCell = new OuiJournalPage.IconCell(heartTexture);

          if (areaModeStats.BestDeaths == 0)
          {
            deathlessCell = new OuiJournalPage.IconCell(isHeartside ? "CollabUtils2/golden_strawberry" : "CollabUtils2/silver_strawberry");
          }
          else
          {
            deathlessCell = new OuiJournalPage.TextCell($"{areaModeStats.BestDeaths}", JournalProgressPage?.TextJustify ?? Vector2.Zero, 0.5f, JournalProgressPage.TextColor);
          }
        }
      }

      entries[4] = completedCell;
      entries[5] = berriesCell;
      entries[6] = deathsCell;
      entries[7] = deathlessCell;
      entries[8] = timeCell;

      return entries;
    }


    private static List<OuiJournalPage.Cell> UpdateEntryTotals(
      List<OuiJournalPage.Cell> entries, CustomAreaStats customAreaStats, List<AreaStats> areaStats)
    {

      int collectedBerries = customAreaStats.Berries;
      int deaths = customAreaStats.Deaths;
      long time = customAreaStats.TimePlayed;

      int currentDeaths = 0;
      int currentBerries = 0;
      long currentTime = 0;

      foreach (AreaStats area in areaStats)
      {
        currentDeaths += area.TotalDeaths;
        currentTime += area.TotalTimePlayed;
      }

      foreach (string map in CU2PlatinumsModule.collectedStrawberries.Keys)
      {
        currentBerries += CU2PlatinumsModule.collectedStrawberries[map].Count;
      }

      if (displayDiff)
      {
        collectedBerries = currentBerries;
        deaths = currentDeaths - deaths;
        time = currentTime - time;
      }

      OuiJournalPage.Cell berriesCell;
      OuiJournalPage.Cell deathsCell;
      OuiJournalPage.Cell timeCell;

      if (collectedBerries == 0)
      {
        berriesCell = new OuiJournalPage.IconCell("dot");
      }
      else
      {
        berriesCell = new OuiJournalPage.TextCell($"{collectedBerries}", JournalProgressPage?.TextJustify ?? Vector2.Zero, 0.5f, JournalProgressPage.TextColor);
      }

      if (deaths == -1)
      {
        deathsCell = new OuiJournalPage.IconCell("dot");
      }
      else
      {
        string deathDialog = Dialog.Deaths(deaths);
        deathDialog = (displayDiff && deaths > 0) ? $"+{deathDialog}" : deathDialog;

        Color customColor = deaths == 0 ? Color.Green : Color.Red;
        Color color = displayDiff ? customColor : JournalProgressPage.TextColor;
        deathsCell = new OuiJournalPage.TextCell(deathDialog, JournalProgressPage?.TextJustify ?? Vector2.Zero, 0.5f, color);
      }

      if (time == 0)
      {
        timeCell = new OuiJournalPage.IconCell("dot");
      }
      else
      {
        timeCell = new OuiJournalPage.TextCell(Dialog.Time(time), JournalProgressPage?.TextJustify ?? Vector2.Zero, 0.5f, JournalProgressPage.TextColor);
      }

      entries[5] = berriesCell;
      entries[6] = deathsCell;
      entries[8] = timeCell;

      return entries;
    }


    public static void OnJournalEnter(OuiJournal journal, Oui from)
    {
      _journal = journal;
    }

    public static void OnJournalPageRedraw(
      On.Celeste.OuiJournalPage.orig_Redraw orig, OuiJournalPage self, VirtualRenderTarget buffer
    )
    {
      orig(self, buffer);

      renderTargets[self] = buffer;
    }

    public static void OuiJournalProgressCtor(
      On.Celeste.OuiJournalProgress.orig_ctor orig, OuiJournalProgress self, OuiJournal journal
    )
    {
      orig(self, journal);

      DynamicData journalProgressDynData = DynamicData.For(self);
      OuiJournalPage.Table table = journalProgressDynData.Get<OuiJournalPage.Table>("table");
      if (table is null)
      {
        return;
      }

      table.AddColumn(new OuiJournalPage.EmptyCell(100f))
           .AddColumn(new OuiJournalPage.EmptyCell(100f));
    }

    public static void OnJournalClose(On.Celeste.OuiJournal.orig_Close orig, OuiJournal self)
    {
      JournalSnapshot = null;
      _journal = null;
      renderTargets.Clear();
      displayDiff = false;

      orig(self);
    }

    private static void UpdateJournalData(OuiJournal journal)
    {
      SaveData instance = SaveData.Instance;
      if (instance is null || JournalProgressPage is null)
        return;

      if (displayDiff)
      {
        JournalSnapshot = LoadJournalSnapshot(CU2PlatinumsModule.currentLevelSet);
      }

      if (JournalSnapshot == null)
      {
        return;
      }

      try
      {
        int firstIndexOnPage = 0;
        int mapsOnThisCollabUtils2Page = CollabUtils2Integration.MapsOnPage(JournalProgressPage, journal, instance, out firstIndexOnPage);

        DynamicData journalProgressDynData = DynamicData.For(JournalProgressPage);
        OuiJournalPage.Table table = journalProgressDynData.Get<OuiJournalPage.Table>("table");
        if (table is null)
        {
          return;
        }
        DynamicData tableDynData = DynamicData.For(table);

        List<OuiJournalPage.Row> rows = tableDynData.Get<List<OuiJournalPage.Row>>("rows");
        if (rows is null)
        {
          return;
        }

        rows[0].Entries[0] = new OuiJournalPage.TextCell(
          displayDiff ? "CU2PLATINUMS_JOURNAL_PROGRESS".AsDialog() : "journal_progress".AsDialog(),
          new Vector2(0f, 0.5f), 1f, Color.Black * 0.7f, 360f
        );

        int lastUnModdedRow = mapsOnThisCollabUtils2Page + 2;

        for (int i = 1; i < lastUnModdedRow - 1; i++)
        {
          int offset = GetInterludeOffset(firstIndexOnPage + i - 1, instance, journal, out AreaStats area);
          if (area is null)
          {
            return;
          }
          bool isHeartside = CollabUtils2Integration.IsHeartSide(AreaData.Get(area).SID);

          OuiJournalPage.Row row = rows[isHeartside ? i + 1 : i];
          List<OuiJournalPage.Cell> entries = row.Entries;

          try
          {
            row.Entries = UpdateEntry(
              row.Entries, area, JournalSnapshot[firstIndexOnPage + i - 1 + offset], isHeartside);
          }
          catch (Exception ex)
          {
            Logger.Error("CU2Platinums", ex.Message);
            Console.WriteLine(ex.StackTrace);
          }
        }


        GetInterludeOffset(firstIndexOnPage + lastUnModdedRow - 1, instance, journal, out AreaStats _area);
        if (_area is null)
        {
          OuiJournalPage.Row row = rows[lastUnModdedRow + 1];
          List<OuiJournalPage.Cell> entries = row.Entries;

          try
          {
            List<AreaStats> areaStats = CollabUtils2Integration.GetSortedCollabAreaStats(instance, CU2PlatinumsModule.currentLevelSet);
            row.Entries = UpdateEntryTotals(row.Entries, JournalSnapshot[JournalSnapshot.Count - 1], areaStats);
          }
          catch (Exception ex)
          {
            Logger.Error("CU2Platinums", ex.Message);
            Console.WriteLine(ex.StackTrace);
          }
        }

        if (RenderTarget != null)
        {
          JournalProgressPage.Redraw(RenderTarget);
        }
        else
        {
          Logger.Warn("CU2Platinums", "Could not find render target for journal progress page");
        }
      }
      catch (Exception ex)
      {
        Logger.Error("CU2Platinums", ex.Message);
        Console.WriteLine(ex.StackTrace);
      }
    }

    private struct CustomAreaStats
    {
      public long TimePlayed;
      public int Deaths;
      public int Berries;

      public CustomAreaStats(AreaModeStats[] areaModes)
      {
        TimePlayed = areaModes[0].TimePlayed;

        Deaths = areaModes[0].Deaths;

        Berries = areaModes[0].TotalStrawberries;
      }

      public CustomAreaStats(long timePlayed, int deaths, int berries)
      {
        TimePlayed = timePlayed;
        Deaths = deaths;
        Berries = berries;
      }

      public CustomAreaStats()
      {
        TimePlayed = 0;
        Deaths = 0;
        Berries = 0;
      }

      public void Add(CustomAreaStats other)
      {
        TimePlayed += other.TimePlayed;
        Deaths += other.Deaths;
        Berries += other.Berries;
      }
    }
  }
}
