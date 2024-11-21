using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;

// Adapted from viddie/ConsistencyTrackerMod/Utility/PacePingManager.cs
// https://github.com/viddie/ConsistencyTrackerMod/blob/5732074464d71573c7eca27fc1f6385311abba75/Utility/PacePingManager.cs
namespace Celeste.Mod.CU2Platinums.PacePing
{
  public class PaceState
  {
    public string WebhookURL;
    public string Username;
  }

  public enum MapPingSetting
  {
    None,
    OnEnter,
    OnComplete
  }

  public class PacePingManager
  {
    private static DiscordWebhookResponse EmbedMessage { get; set; }

    static string lobbyName = "";
    static string campaignName = "";

    public static void SaveDiscordWebhook(string webhook)
    {
      CU2PlatinumsModule.Settings.PaceState.WebhookURL = webhook;
    }

    public static void SetCampaignName(string lobby, string campaign)
    {
      lobbyName = lobby;
      campaignName = campaign;
    }

    static List<DiscordWebhookRequest.Embed> GetEmbeds(DiscordWebhookAction action, string mapName)
    {
      bool death = action == DiscordWebhookAction.Death;

      int mapsCompleted = CU2PlatinumsModule.mapsCompleted.Count;
      int totalMaps = CU2PlatinumsModule.mapsInLobby;

      string progress = "";
      for (int i = 0; i < totalMaps; i++)
      {
        if (i < mapsCompleted)
        {
          progress += ":green_square:";
        }
        else if (i == mapsCompleted)
        {
          progress += death ? ":skull:" : ":white_large_square:";
        }
        else
        {
          progress += ":black_large_square:";
        }
      }

      if (mapsCompleted < totalMaps)
      {
        progress += death ? ":black_heart::bomb:" : ":black_heart::checkered_flag:";
      }
      else if (mapsCompleted == totalMaps)
      {
        progress += death ? ":skull::bomb:" : ":white_heart::checkered_flag:";
      }
      else if (mapsCompleted > totalMaps)
      {
        progress += ":green_heart::star:";
      }


      List<DiscordWebhookRequest.Embed> embeds = new List<DiscordWebhookRequest.Embed>() {
        new DiscordWebhookRequest.Embed(){
          Title = $"{lobbyName}",
          Description = $"of {campaignName}",
          Color = 15258703,
          Fields = new List<DiscordWebhookRequest.Field>(){
            new DiscordWebhookRequest.Field() { Inline = true, Name = $"Current map", Value = $"{mapName}" },
            new DiscordWebhookRequest.Field() { Inline = true, Name = $"Completed", Value = $"{mapsCompleted}/{totalMaps+1}" },
            new DiscordWebhookRequest.Field() { Inline = false, Name = "Progress", Value = $"{progress}" },
          }
        },
      };


      return embeds;
    }

    public static void OnEnter(string currentMap, string mapNameClean)
    {
      CU2PlatinumsModuleSettings settings = CU2PlatinumsModule.Settings;
      if (!settings.PacePingEnabled) return;

      if (EmbedMessage != null)
      {
        SendUpdate(mapNameClean);
      }

      if (settings.MapPingSettings.TryGetValue(currentMap, out MapPingSetting setting))
      {
        if (setting == MapPingSetting.OnEnter)
        {
          if (EmbedMessage == null)
          {
            SendUpdate(mapNameClean);
          }
          SendPing(settings.Messages[currentMap]);
        }
      }
    }

    public static void OnDeath(string mapNameClean)
    {
      if (EmbedMessage != null)
      {
        SendUpdate(mapNameClean, DiscordWebhookAction.Death);
      }
    }

    public static void OnComplete(string currentMap, string mapNameClean)
    {
      CU2PlatinumsModuleSettings settings = CU2PlatinumsModule.Settings;
      if (!settings.PacePingEnabled) return;

      if (settings.MapPingSettings.TryGetValue(currentMap, out MapPingSetting setting))
      {
        if (setting == MapPingSetting.OnComplete)
        {
          if (EmbedMessage == null)
          {
            SendUpdate(mapNameClean);
          }
          SendPing(settings.Messages[currentMap]);
        }
      }
    }

    public static void SendPing(string message)
    {
      PaceState state = CU2PlatinumsModule.Settings.PaceState;

      try
      {
        SendDiscordWebhookMessage(new DiscordWebhookRequest()
        {
          Username = state.Username,
          Content = message,
        }, state.WebhookURL, DiscordWebhookAction.Separate);
      }
      catch (Exception ex)
      {
        Logger.Log(LogLevel.Info, "CU2Platinums", $"An exception occurred while trying to send pace ping: {ex}");
      }
    }

    public static void SendUpdate(string mapName, DiscordWebhookAction action = DiscordWebhookAction.Update)
    {
      PaceState state = CU2PlatinumsModule.Settings.PaceState;

      try
      {
        SendDiscordWebhookMessage(new DiscordWebhookRequest()
        {
          Username = state.Username,
          Embeds = GetEmbeds(action, mapName),
        }, state.WebhookURL, action);
      }
      catch (Exception ex)
      {
        Logger.Log(LogLevel.Info, "CU2Platinums", $"An exception occurred while trying to send pace ping: {ex}");
      }
    }

    public enum DiscordWebhookAction
    {
      Update,
      Separate,
      Death,
    }

    public static void SendDiscordWebhookMessage(DiscordWebhookRequest request, string url, DiscordWebhookAction action)
    {
      DiscordWebhookResponse localMessage = EmbedMessage;
      Task.Run(() =>
      {
        WebClient client = new WebClient();
        client.Headers.Add("Content-Type", "application/json");
        string payload = JsonConvert.SerializeObject(request);

        string response;
        if (localMessage == null || action == DiscordWebhookAction.Separate)
        {
          response = client.UploadString(url + "?wait=true", payload);
        }
        else
        {
          if (request.Embeds == null)
          {
            request.Embeds = localMessage.Embeds;
          }
          response = client.UploadString(url + "/messages/" + localMessage.Id + "?wait=true", "PATCH", payload);
        }

        // if (response != null)
        // {
        //   Logger.Log(LogLevel.Info, "CU2Platinums", $"Discord webhook response: {response}");
        // }

        DiscordWebhookResponse webhookResponse = JsonConvert.DeserializeObject<DiscordWebhookResponse>(response);
        if (webhookResponse == null)
        {
          Logger.Log(LogLevel.Info, "CU2Platinums", $"Couldn't parse discord webhook response to DiscordWebhookResponse");
          return;
        }

        if (action == DiscordWebhookAction.Update)
        {
          EmbedMessage = webhookResponse;
        }
        else if (action == DiscordWebhookAction.Death)
        {
          EmbedMessage = null;
        }
      });
    }
  }
}
