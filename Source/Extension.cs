namespace Celeste.Mod.CU2Platinums;

// functions stolen from izumiano/izumisQOL/Scripts/Extensions.cs
// https://github.com/izumiano/izumisQOL/blob/master/Scripts/Extensions.cs
public static class Extensions
{
  public static OuiJournalPage Page(this OuiJournal journal)
  {
    if (journal.PageIndex > journal.Pages.Count - 1 || journal.PageIndex < 0)
    {
      Logger.Log(LogLevel.Warn, "CU2Platinums", "Could not get the current journal page");
      return null;
    }

    return journal.Page;
  }

  public static string AsDialog(this string dialogID)
  {
    return Dialog.Clean(dialogID);
  }
}
