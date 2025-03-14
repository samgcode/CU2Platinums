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

      areaStats.ForEach(area => customAreaStats.Add(new CustomAreaStats(area.Modes)));

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

    private static List<OuiJournalPage.Cell> UpdateEntry(List<OuiJournalPage.Cell> entries, AreaStats area, int offset, List<CustomAreaStats> customAreaStats, int index)
    {
      OuiJournalPage.Cell berriesCell;
      OuiJournalPage.Cell deathsCell;
      OuiJournalPage.Cell timeCell;

      AreaData areaData = AreaData.Get(area);
      AreaKey areaKey = areaData.ToKey();
      AreaModeStats areaModeStats = SaveData.Instance.Areas_Safe[areaKey.ID].Modes[(int)areaKey.Mode];
      CustomAreaStats areaStats = customAreaStats[index + offset];
      ModeProperties modeProperties = AreaData.Get(area).Mode[(int)areaKey.Mode];



      int collectedBerries = areaModeStats.TotalStrawberries;

      int areaBerries = areaData.Mode[0].StartStrawberries;

      int newDeaths = area.Modes[0].Deaths;
      int deaths = newDeaths;

      long totalTime = area.TotalTimePlayed;
      long time = totalTime;

      if (displayDiff)
      {
        if (CU2PlatinumsModule.mapsCompleted.Contains(areaData.Name))
        {
          collectedBerries = CU2PlatinumsModule.collectedStrawberries[areaData.Name].Count;
        }
        else
        {
          collectedBerries = 0;
        }

        int oldDeaths = areaStats.Deaths[0];

        int diff = newDeaths - oldDeaths;

        if (diff > 0 || CU2PlatinumsModule.mapsCompleted.Contains(AreaData.Get(area).Name))
        {
          deaths = diff;
        }
        else
        {
          deaths = -1;
        }

        long newTimePlayed = area.Modes[0].TimePlayed;
        long oldTimePlayed = areaStats.TimePlayed[0];

        time = newTimePlayed - oldTimePlayed;
      }

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
        string deathDialog = deaths > -1 ? Dialog.Deaths(deaths) : "-";
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
        string timeDialog = time > 0 ? Dialog.Time(time) : "-";
        timeCell = new OuiJournalPage.TextCell(timeDialog, JournalProgressPage?.TextJustify ?? Vector2.Zero, 0.5f, JournalProgressPage.TextColor);
      }

      entries[5] = berriesCell;
      entries[6] = deathsCell;
      // entries[7] = displayDiff ? new OuiJournalPage.IconCell("dot") : entries[7];
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
          int heartSideOffset = CollabUtils2Integration.IsHeartSide(AreaData.Get(area).SID) ? 1 : 0;

          OuiJournalPage.Row row = rows[i + heartSideOffset];
          List<OuiJournalPage.Cell> entries = row.Entries;

          try
          {
            row.Entries = UpdateEntry(row.Entries, area, offset, JournalSnapshot, firstIndexOnPage + i - 1);
          }
          catch (Exception ex)
          {
            Logger.Error("CU2Platinums", ex.Message);
            Console.WriteLine(ex.StackTrace);
          }
        }

        // GetInterludeOffset(firstIndexOnPage + lastUnModdedRow - 1, instance, journal, out AreaStats _area);
        // if (_area is null)
        // {
        //   Logger.Log(LogLevel.Info, "CU2Platinums", $"Totals Row:");
        //   OuiJournalPage.Row
        //       row = rows[lastUnModdedRow + 1];
        //   List<OuiJournalPage.Cell> entries = row.Entries;

        //   OuiJournalPage.Cell cell = new OuiJournalPage.EmptyCell(100f);

        //   cell = new OuiJournalPage.TextCell($"{totalBerries}/{totalTotalBerries}", JournalProgressPage?.TextJustify ?? Vector2.Zero, 0.5f, JournalProgressPage.TextColor);

        //   entries[5] = cell;
        //   entries[6] = cell;
        //   entries[8] = cell;
        // }



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
      public long[] TimePlayed = new long[3];
      public int[] Deaths = new int[3];

      public CustomAreaStats(AreaModeStats[] areaModes)
      {
        TimePlayed[0] = areaModes[0].TimePlayed;
        TimePlayed[1] = areaModes[1].TimePlayed;
        TimePlayed[2] = areaModes[2].TimePlayed;

        Deaths[0] = areaModes[0].Deaths;
        Deaths[1] = areaModes[1].Deaths;
        Deaths[2] = areaModes[2].Deaths;
      }

      public CustomAreaStats(long[] timePlayed, int[] deaths)
      {
        TimePlayed = timePlayed;
        Deaths = deaths;
      }

      public CustomAreaStats() { }
    }
  }
}
