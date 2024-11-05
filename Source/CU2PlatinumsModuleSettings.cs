using System;

namespace Celeste.Mod.CU2Platinums;

public class CU2PlatinumsModuleSettings : EverestModuleSettings
{
  [SettingName("CU2PLATINUMS_ENABLE_SILVER_TRAIN")]
  public bool EnableSilverTrain { get; set; } = true;

  public TextMenu.Button SetPosition { get; set; } = null;
  public TextMenu.Button ResetPosition { get; set; } = null;

  public void CreateSetPositionEntry(TextMenu menu, bool inGame)
  {
    TextMenu.Button item = CreateMenuButton(menu, "SetPosition", null, () =>
    {
      CU2PlatinumsModule.SetSpawnPosition();
    });
  }

  public void CreateResetPositionEntry(TextMenu menu, bool inGame)
  {
    TextMenu.Button item = CreateMenuButton(menu, "ResetPosition", null, () =>
    {
      CU2PlatinumsModule.RemoveSpawnPosition();
    });
  }

  public TextMenu.Button CreateMenuButton(TextMenu menu, string dialogLabel, Func<string, string> dialogTransform, Action onPress)
  {
    string label = $"CU2PLATINUMS_{dialogLabel}".DialogClean();
    TextMenu.Button item = new TextMenu.Button(dialogTransform?.Invoke(label) ?? label);
    item.Pressed(onPress);
    menu.Add(item);
    return item;
  }
}
