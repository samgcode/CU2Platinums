using System;
using System.Collections.Generic;

using Celeste.Mod.CU2Platinums.PacePing;

namespace Celeste.Mod.CU2Platinums;

public class CU2PlatinumsModuleSettings : EverestModuleSettings
{
  [SettingName("CU2PLATINUMS_ENABLE_SILVER_TRAIN")]
  public bool EnableSilverTrain { get; set; } = true;

  [SettingName("CU2PLATINUMS_ENABLE_COUNT_COLLECT")]
  [SettingSubText("CU2PLATINUMS_ENABLE_COUNT_COLLECT_DESC")]
  public bool EnableCountCollect { get; set; } = false;

  [SettingName("CU2PLATINUMS_CARRY_PLATINUM")]
  [SettingSubText("CU2PLATINUMS_CARRY_PLATINUM_DESC")]
  public bool CarryPlatinum { get; set; } = true;

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

  [SettingIgnore]
  public bool PacePing { get; set; } = false;

  [SettingIgnore]
  public bool PacePingEnabled { get; set; } = false;

  [SettingIgnore]
  public PaceState PaceState { get; set; } = new PaceState();

  [SettingIgnore]
  public Dictionary<string, string> Messages { get; set; } = new Dictionary<string, string>();

  [SettingIgnore]
  public Dictionary<string, MapPingSetting> MapPingSettings { get; set; } = new Dictionary<string, MapPingSetting>();

  public void CreatePacePingEntry(TextMenu menu, bool inGame)
  {
    if (CU2PlatinumsModule.currentMap != null)
    {
      TextMenuExt.SubMenu subMenu = new TextMenuExt.SubMenu(Dialog.Clean("CU2PLATINUMS_PACE_PING"), false);
      TextMenu.Item menuItem;

      subMenu.Add(menuItem = new TextMenu.OnOff(Dialog.Clean("CU2PLATINUMS_PACE_PING_ENABLED"), PacePingEnabled).Change(value =>
      {
        PacePingEnabled = value;
        CU2PlatinumsModule.Instance.SaveSettings();
      }));
      menuItem.AddDescription(subMenu, menu, Dialog.Clean("CU2PLATINUMS_PACE_PING_ENABLED_DESC"));

      subMenu.Add(menuItem = new TextMenu.Button(Dialog.Clean("CU2PLATINUMS_PACE_PING_IMPORT_WEBHOOK")).Pressed(() =>
      {
        string text = TextInput.GetClipboardText();
        PaceState.WebhookURL = text;
        CU2PlatinumsModule.Instance.SaveSettings();
      }));
      menuItem.AddDescription(subMenu, menu, Dialog.Clean("CU2PLATINUMS_PACE_PING_IMPORT_WEBHOOK_DESC"));

      subMenu.Add(menuItem = new TextMenu.Button(Dialog.Clean("CU2PLATINUMS_PACE_PING_BOT_USERNAME")).Pressed(() =>
      {
        string text = TextInput.GetClipboardText();
        PaceState.Username = text;
        CU2PlatinumsModule.Instance.SaveSettings();
      }));
      menuItem.AddDescription(subMenu, menu, Dialog.Clean("CU2PLATINUMS_PACE_PING_BOT_USERNAME_DESC"));

      subMenu.Add(menuItem = new TextMenu.Button(Dialog.Clean("CU2PLATINUMS_PACE_PING_SET_PING")).Pressed(() =>
      {
        string text = TextInput.GetClipboardText();
        string currentMap = CU2PlatinumsModule.currentMap;
        if (currentMap != null)
        {
          Messages[currentMap] = text;
        }

        CU2PlatinumsModule.Instance.SaveSettings();
      }));
      menuItem.AddDescription(subMenu, menu, Dialog.Clean("CU2PLATINUMS_PACE_PING_SET_PING_DESC"));

      if (!MapPingSettings.ContainsKey(CU2PlatinumsModule.currentMap))
      {
        MapPingSettings[CU2PlatinumsModule.currentMap] = MapPingSetting.None;
      }

      subMenu.Add(menuItem = new TextMenuExt.EnumSlider<MapPingSetting>(Dialog.Clean("CU2PLATINUMS_PACE_PING_MAP_SETTING"), MapPingSettings[CU2PlatinumsModule.currentMap]).Change((setting) =>
      {
        string currentMap = CU2PlatinumsModule.currentMap;
        if (currentMap != null)
        {
          MapPingSettings[currentMap] = setting;
        }

        CU2PlatinumsModule.Instance.SaveSettings();
      }));

      menuItem.AddDescription(subMenu, menu, Dialog.Clean("CU2PLATINUMS_PACE_PING_MAP_SETTING_DESC"));

      menu.Add(subMenu);
    }
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
