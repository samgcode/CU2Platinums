using System;
using System.Reflection;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MonoMod.RuntimeDetour;
using Celeste.Mod.CollabUtils2;
using Celeste.Mod.CollabUtils2.UI;
using Celeste.Mod.CollabUtils2.Entities;
using Celeste.Mod.CollabUtils2.Triggers;
using Monocle;
using Celeste.Mod.CU2Platinums.PacePing;

namespace Celeste.Mod.CU2Platinums;

public class CU2PlatinumsModule : EverestModule
{
    public static CU2PlatinumsModule Instance { get; private set; }

    public override Type SettingsType => typeof(CU2PlatinumsModuleSettings);
    public static CU2PlatinumsModuleSettings Settings => (CU2PlatinumsModuleSettings)Instance._Settings;

    public override Type SessionType => typeof(CU2PlatinumsModuleSession);
    public static CU2PlatinumsModuleSession Session => (CU2PlatinumsModuleSession)Instance._Session;

    public override Type SaveDataType => typeof(CU2PlatinumsModuleSaveData);
    public static CU2PlatinumsModuleSaveData SaveData => (CU2PlatinumsModuleSaveData)Instance._SaveData;

    public static readonly Random rand = new Random();

    private static Hook hook_Player_orig_Die;
    private static Hook miniHeartCollect;
    private static Hook heartCollect;
    private static Hook GBRP_Restart; // GoldenBerryPlayerRespawnPoint.onSessionRestart
    private static Hook storeStrawberries;
    private static Hook restoreStrawberries;
    private static Hook getHeartCount;
    private static Hook openChapterPanel;

    private static Dictionary<string, Vector2> spawnPositions = new Dictionary<string, Vector2>
    {
        { "StrawberryJam2021/0-Lobbies/1-Beginner", new Vector2(940, 630) },
        { "StrawberryJam2021/0-Lobbies/2-Intermediate", new Vector2(2780, 600) },
        { "StrawberryJam2021/0-Lobbies/3-Advanced", new Vector2(2535, 2095) },
        { "StrawberryJam2021/0-Lobbies/4-Expert", new Vector2(1488, 850) },
        { "StrawberryJam2021/0-Lobbies/5-Grandmaster", new Vector2(2392, 1985) },

        { "SecretSanta2024/0-Lobbies/1-Easy", new Vector2(1431, 531) },
        { "SecretSanta2024/0-Lobbies/2-Medium", new Vector2(1272, 1129) },
        { "SecretSanta2024/0-Lobbies/3-Hard", new Vector2(619, 305) },

        { "SpringCollab2020/0-Lobbies/1-Beginner", new Vector2(1010, 1670) },
        { "SpringCollab2020/0-Lobbies/2-Intermediate", new Vector2(1355, 2335) },
        { "SpringCollab2020/0-Lobbies/3-Advanced", new Vector2(1368, 946) },
        { "SpringCollab2020/0-Lobbies/4-Expert", new Vector2(1452, 552) },
        { "SpringCollab2020/0-Lobbies/5-Grandmaster", new Vector2(1394, 1098) },

        { "BeginnerCollab/0-Lobbies/1-MainLobby", new Vector2(672, 2808) },
    };

    public static int mapsInLobby = 0;

    public static int silversSpawned = 0;
    public static int timer = 0;

    public static Entity platEntity = null;
    public static Follower platFollower = null;
    public static List<IStrawberry> silverBerries = new List<IStrawberry>();

    public static AreaKey lobbyArea;
    public static string lobbyRoom;

    public static bool shouldUpdate = false;
    public static bool returnToLobby = false;
    public static bool paused = false;

    public static string currentLobby = null;
    public static string currentMap = null;
    public static string currentMapClean = null;

    public static List<string> mapsCompleted = new List<string>();

    public CU2PlatinumsModule()
    {
        Instance = this;
#if DEBUG
        // debug builds use verbose logging
        Logger.SetLogLevel(nameof(CU2PlatinumsModule), LogLevel.Verbose);
#else
        // release builds use info logging to reduce spam in log files
        Logger.SetLogLevel(nameof(CU2PlatinumsModule), LogLevel.Info);
#endif
    }

    public override void Load()
    {
        On.Celeste.LevelLoader.StartLevel += OnLoadLevel;
        On.Celeste.Player.OnTransition += OnPlayerTransition;
        On.Celeste.Player.Update += Player_Update;
        Everest.Events.Level.OnPause += OnPause;
        Everest.Events.Level.OnUnpause += OnUnpause;
        Everest.Events.Level.OnExit += Level_OnExit;

        hook_Player_orig_Die = new Hook(
                typeof(Player).GetMethod("orig_Die", BindingFlags.Public | BindingFlags.Instance),
                typeof(CU2PlatinumsModule).GetMethod("OnPlayerDie"));
        heartCollect = new Hook(
            typeof(HeartGem).GetMethod("CollectRoutine", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            typeof(CU2PlatinumsModule).GetMethod("HeartSmash"));
        miniHeartCollect = new Hook(
            typeof(MiniHeart).GetMethod("SmashRoutine", BindingFlags.Instance | BindingFlags.NonPublic),
            typeof(CU2PlatinumsModule).GetMethod("MiniHeartSmash"));
        GBRP_Restart = new Hook(
            typeof(GoldenBerryPlayerRespawnPoint).GetMethod("onSessionRestart", BindingFlags.Static | BindingFlags.NonPublic),
            typeof(CU2PlatinumsModule).GetMethod("onSessionRestart"));
        storeStrawberries = new Hook(
            typeof(Leader).GetMethod("StoreStrawberries", BindingFlags.Static | BindingFlags.Public),
            typeof(CU2PlatinumsModule).GetMethod("onStoreStrawberries"));
        restoreStrawberries = new Hook(
            typeof(Leader).GetMethod("RestoreStrawberries", BindingFlags.Static | BindingFlags.Public),
            typeof(CU2PlatinumsModule).GetMethod("onRestoreStrawberries"));
        getHeartCount = new Hook(
            typeof(MiniHeartDoor).GetMethod("getCollectedHeartGems", BindingFlags.Static | BindingFlags.NonPublic),
            typeof(CU2PlatinumsModule).GetMethod("onGetHeartCount"));
        openChapterPanel = new Hook(
            typeof(InGameOverworldHelper).GetMethod("OpenChapterPanel", BindingFlags.Static | BindingFlags.Public),
            typeof(CU2PlatinumsModule).GetMethod("onOpenChapterPanel"));
    }

    public static void OnLoadLevel(On.Celeste.LevelLoader.orig_StartLevel orig, LevelLoader self)
    {
        paused = false;

        AreaData area = AreaData.Areas[self.Level.Session.Area.ID];
        currentMap = self.Level.Session.Area.GetSID();
        currentMapClean = area.Name.DialogCleanOrNull() ?? area.Name.SpacedPascalCase();

        if (InLobby(self.Level.Session))
        {
            string newLobby = currentMap;
            if (currentLobby != null && newLobby != currentLobby)
            {
                reset();
            }
            currentLobby = newLobby;
            PacePingManager.SetCampaignName(currentMapClean, Dialog.CleanLevelSet(area.LevelSet));
        }
        shouldUpdate = true;

        if (platEntity != null)
        {
            PacePingManager.OnEnter(currentMap, currentMapClean);
        }

        orig(self);
    }


    public static void SetSpawnPosition()
    {
        Level level = Engine.Scene as Level;
        if (level == null)
        {
            return;
        }
        if (InLobby(level.Session))
        {
            Vector2 position = level.Tracker.GetEntity<Player>().Center - level.LevelOffset + new Vector2(0, -4);

            SaveData.SpawnPositions[currentLobby] = position;
        }
    }

    public static void RemoveSpawnPosition()
    {
        Level level = Engine.Scene as Level;
        if (level == null)
        {
            return;
        }
        if (InLobby(level.Session))
        {
            SaveData.SpawnPositions.Remove(currentLobby);
        }
    }

    public static void OnPlayerTransition(On.Celeste.Player.orig_OnTransition orig, Player self)
    {
        if (platEntity != null)
        {
            self.level.Tracker.GetEntities<GoldBerryCollectTrigger>().ForEach(trigger => trigger.RemoveSelf());
        }

        orig(self);
    }

    private static void Player_Update(On.Celeste.Player.orig_Update orig, Player player)
    {
        orig(player);

        if (platEntity == null)
        {
            Level level = player.SceneAs<Level>();
            savePlat(level.Session, player);
            if (platEntity != null && InLobby(level.Session))
            {
                try
                {
                    forceDoorState(level, player);
                    LobbyVisitManager visitManager = level.Tracker.GetEntity<LobbyMapController>().VisitManager;
                    visitManager.Reset();
                }
                catch (Exception e)
                {
                    Logger.Log(LogLevel.Warn, "CU2Platinums", $"Failed to reset lobby visit manager: {e}");
                }
            }
        }

        if (Settings.EnableSilverTrain && silversSpawned < silverBerries.Count)
        {
            if (timer > 5)
            {
                restoreSilver(player.Leader, silversSpawned);
                silversSpawned++;
                timer = 0;
            }
            timer++;
        }

        if (!shouldUpdate)
        {
            return;
        }
        shouldUpdate = false;
        playerOnFirstUpdate(player);
    }

    private static void playerOnFirstUpdate(Player player)
    {
        Level level = Engine.Scene as Level;

        if (platEntity != null)
        {
            Leader leader = player.Leader;
            restorePlatinum(leader);

            silversSpawned = 0;

            if (InLobby(level.Session))
            {
                forceDoorState(level, player);
            }
        }

        if (platEntity == null && InLobby(level.Session))
        {
            EntityData data = new EntityData();
            data.Name = "PlatinumStrawberry/PlatinumStrawberry";

            if (SaveData.SpawnPositions.ContainsKey(currentLobby))
            {
                data.Position = SaveData.SpawnPositions[currentLobby];
            }
            else if (spawnPositions.ContainsKey(currentLobby))
            {
                data.Position = spawnPositions[currentLobby];
            }
            else
            {
                Logger.Log(LogLevel.Info, "CU2Platinums", $"No spawn position found for {currentLobby}");
                data.Position = player.Position - level.LevelOffset;
            }

            Level.LoadCustomEntity(data, level);
        }
    }

    public static void forceDoorState(Level level, Player player)
    {
        MiniHeartDoor heartDoor = level.Entities.FindFirst<MiniHeartDoor>();
        if (heartDoor != null)
        {
            if (!lobbyCompleted())
            {
                heartDoor.Opened = false;
                mapsInLobby = heartDoor.Requires;
                level.Session.SetFlag("opened_heartgem_door_" + heartDoor.Requires, false);
                CollabModule.Instance.SaveData.OpenedMiniHeartDoors.Remove(heartDoor.GetDoorSaveDataID(player.Scene));
            }
        }
    }

    public static void onStoreStrawberries(Action<Leader> orig, Leader leader)
    {
        Player player = leader.Entity.SceneAs<Level>().Tracker.GetEntity<Player>();
        savePlat(leader.Entity.SceneAs<Level>().Session, player);

        orig(leader);
    }

    public static void onRestoreStrawberries(Action<Leader> orig, Leader leader)
    {
        if (platEntity != null)
        {
            restorePlatinum(leader);
        }

        orig(leader);
    }

    public static int onGetHeartCount(Func<Delegate, HeartGemDoor, int> orig, Delegate orig_orig, HeartGemDoor door)
    {
        if (platEntity != null)
        {
            return mapsCompleted.Count;
        }
        return orig(orig_orig, door);
    }

    public static void onOpenChapterPanel(Action<Player, string, ChapterPanelTrigger.ReturnToLobbyMode, bool, bool> orig, Player player, string sid, ChapterPanelTrigger.ReturnToLobbyMode returnToLobbyMode, bool savingAllowed, bool exitFromGym)
    {
        if (platEntity != null)
        {
            AreaData areaData = AreaData.Get(sid);
            ModeProperties mode = areaData.Mode[(int)areaData.ToKey().Mode];
            mode.Checkpoints = new CheckpointData[] { };

            CollabModule.Instance.SaveData.SessionsPerLevel.Remove(sid);
        }

        orig(player, sid, returnToLobbyMode, savingAllowed, exitFromGym);
    }

    public static PlayerDeadBody OnPlayerDie(Func<Player, Vector2, bool, bool, PlayerDeadBody> orig, Player self, Vector2 direction, bool ifInvincible, bool registerStats)
    {
        if (platEntity != null)
        {
            PacePingManager.OnDeath(currentMapClean);
            returnToLobby = true;
            reset();
        }

        return orig(self, direction, ifInvincible, registerStats);
    }

    public static Session onSessionRestart(
        Func<On.Celeste.Session.orig_Restart, Session, string, Session> orig,
        On.Celeste.Session.orig_Restart orig_celeste, Session session, string intoLevel)
    {
        if (returnToLobby)
        {
            returnToLobby = false;
            session.Area = lobbyArea;
            intoLevel = lobbyRoom;
        }

        return orig(orig_celeste, session, intoLevel);
    }

    public static System.Collections.IEnumerator MiniHeartSmash(Func<MiniHeart, Player, Level, System.Collections.IEnumerator> orig, MiniHeart self, Player player, Level level)
    {
        if (platEntity != null)
        {
            platFollower.Leader.Followers.Remove(platFollower);

            string map = level.Session.Area.GetSID();
            if (!mapsCompleted.Contains(map))
            {
                mapsCompleted.Add(map);
            }

            if (Settings.EnableSilverTrain)
            {
                saveSilvers(player);
            }

            PacePingManager.OnComplete(currentMap, currentMapClean);
        }

        return orig(self, player, level);
    }

    public static System.Collections.IEnumerator HeartSmash(Func<HeartGem, Player, System.Collections.IEnumerator> orig, HeartGem self, Player player)
    {
        if (platEntity != null)
        {
            if (!lobbyCompleted())
            {
                platFollower.Leader.Followers.Remove(platFollower);
            }

            mapsCompleted.Add(currentMap);
            PacePingManager.OnComplete(currentMap, currentMapClean);

            reset();
        }

        return orig(self, player);
    }

    public void OnPause(Level level, int startIndex, bool minimal, bool quickReset)
    {
        paused = true;
    }

    public void OnUnpause(Level level)
    {
        paused = false;
    }

    public void Level_OnExit(Level level, LevelExit exit, LevelExit.Mode mode, Session session, HiresSnow snow)
    {
        switch (mode)
        {
            case LevelExit.Mode.SaveAndQuit:
            case LevelExit.Mode.Restart:
            case LevelExit.Mode.GoldenBerryRestart:
                reset();
                return;
            case LevelExit.Mode.GiveUp:
                if (paused)
                {
                    reset();
                    return;
                }
                break;
        }

        savePlat(session, level.Tracker.GetEntity<Player>());
    }

    public static void savePlat(Session session, Player player)
    {
        if (InLobby(session))
        {
            lobbyArea = session.Area;
            lobbyRoom = session.Level;
        }

        if (player != null)
        {
            Follower plat = null;
            foreach (Follower follower in player.Leader.Followers)
            {
                if (follower.Entity.GetType().ToString() == "Celeste.Mod.PlatinumStrawberry.Entities.PlatinumBerry")
                {
                    plat = follower;
                }
            }

            if (plat != null)
            {
                platEntity = plat.Entity;
                platFollower = plat;
            }
        }
    }

    public static void saveSilvers(Player player)
    {
        silverBerries = new List<IStrawberry>();
        List<Follower> silverFollowers = new List<Follower>();

        foreach (Follower follower in player.Leader.Followers)
        {
            if (follower.Entity.GetType().ToString() == "Celeste.Mod.CollabUtils2.Entities.SilverBerry")
            {
                silverBerries.Add(follower.Entity as IStrawberry);
                silverFollowers.Add(follower);
            }
        }
        foreach (Follower follower in silverFollowers)
        {
            player.Leader.Followers.Remove(follower);
        }
        silversSpawned = silverBerries.Count;
    }

    public static void restorePlatinum(Leader leader)
    {
        Level level = leader.Entity.SceneAs<Level>();

        platEntity.Position = leader.Entity.Center + new Vector2(-8f, -8f);
        leader.GainFollower(platFollower);
        level.Add(platEntity);
    }

    public static void restoreSilver(Leader leader, int i)
    {
        double a = i * 6.2831852 / silverBerries.Count;

        EntityData entityData = new EntityData();
        entityData.Position = leader.Entity.Center + new Vector2(
            (float)Math.Cos(a) * 20, (float)Math.Sin(a) * 20
        );
        entityData.ID = Calc.Random.Next();
        entityData.Name = "CollabUtils2/SilverBerry";
        SilverBerry silverBerry = new SilverBerry(entityData, Vector2.Zero, (silverBerries[silversSpawned] as Strawberry).ID);

        leader.GainFollower(silverBerry.Follower);
        leader.Entity.SceneAs<Level>().Add(silverBerry);
    }

    public static bool lobbyCompleted()
    {
        return (mapsInLobby != 0) && mapsCompleted.Count >= mapsInLobby;
    }

    public static void reset()
    {
        platEntity = null;
        platFollower = null;
        mapsCompleted = new List<string>();
        currentLobby = null;
        silverBerries = new List<IStrawberry>();
    }

    private static bool InLobby(Session session)
    {
        return LobbyHelper.IsCollabLobby(session.Area.GetSID());
    }

    public override void Unload()
    {
        On.Celeste.LevelLoader.StartLevel -= OnLoadLevel;
        On.Celeste.Player.OnTransition -= OnPlayerTransition;
        On.Celeste.Player.Update -= Player_Update;
        Everest.Events.Level.OnPause -= OnPause;
        Everest.Events.Level.OnUnpause -= OnUnpause;
        Everest.Events.Level.OnExit -= Level_OnExit;

        hook_Player_orig_Die?.Dispose();
        hook_Player_orig_Die = null;
        heartCollect?.Dispose();
        heartCollect = null;
        miniHeartCollect?.Dispose();
        miniHeartCollect = null;
        GBRP_Restart?.Dispose();
        GBRP_Restart = null;
        storeStrawberries?.Dispose();
        storeStrawberries = null;
        restoreStrawberries?.Dispose();
        restoreStrawberries = null;
        getHeartCount?.Dispose();
        getHeartCount = null;
        openChapterPanel?.Dispose();
        openChapterPanel = null;
    }
}
