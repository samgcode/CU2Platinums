using Newtonsoft.Json;
using System;
using System.Collections.Generic;

// Copied from viddie/ConsistencyTrackerMod/Utility/DiscordWebhookResponse.cs
// https://github.com/viddie/ConsistencyTrackerMod/blob/5732074464d71573c7eca27fc1f6385311abba75/Utility/DiscordWebhookResponse.cs
namespace Celeste.Mod.CU2Platinums.PacePing
{
  internal class DiscordWebhookResponse
  {
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("type")]
    public int Type { get; set; }

    [JsonProperty("content")]
    public string Content { get; set; }

    [JsonProperty("channel_id")]
    public string ChannelId { get; set; }

    [JsonProperty("author")]
    public WebhookAuthor Author { get; set; }

    [JsonProperty("attachments")]
    public List<object> Attachments { get; set; }

    [JsonProperty("embeds")]
    public List<DiscordWebhookRequest.Embed> Embeds { get; set; }

    [JsonProperty("mentions")]
    public List<object> Mentions { get; set; }

    [JsonProperty("mention_roles")]
    public List<object> MentionRoles { get; set; }

    [JsonProperty("pinned")]
    public bool Pinned { get; set; }

    [JsonProperty("mention_everyone")]
    public bool MentionEveryone { get; set; }

    [JsonProperty("tts")]
    public bool Tts { get; set; }

    [JsonProperty("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    [JsonProperty("edited_timestamp")]
    public object EditedTimestamp { get; set; }

    [JsonProperty("flags")]
    public int Flags { get; set; }

    [JsonProperty("components")]
    public List<object> Components { get; set; }

    [JsonProperty("webhook_id")]
    public string WebhookId { get; set; }


    public class WebhookAuthor
    {
      [JsonProperty("id")]
      public string Id { get; set; }

      [JsonProperty("username")]
      public string Username { get; set; }

      [JsonProperty("avatar")]
      public object Avatar { get; set; }

      [JsonProperty("discriminator")]
      public string Discriminator { get; set; }

      [JsonProperty("public_flags")]
      public int PublicFlags { get; set; }

      [JsonProperty("flags")]
      public int Flags { get; set; }

      [JsonProperty("bot")]
      public bool Bot { get; set; }
    }
  }
}
