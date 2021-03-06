using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Mogo.Util;
using Mogo.GameData;
using Mogo.Game;
using Mogo.FSM;
using Mogo.RPC;
using Utils = Mogo.Util.Utils;
using System.Text;
using Mogo.GameLogic.LocalServer;

namespace Mogo.Mission
{
    public class MercenaryInfo
    {
        public ulong dbid { get; set; }
        public string name { get; set; }
        public int level { get; set; }
        public int vocation { get; set; }
        public int gender { get; set; }
        public int fight { get; set; }
        public int isFriend { get; set; }
    }

    public class SweepMissionRepostData
    {
        public Dictionary<int, int> Enemys { get; set; }
        public Dictionary<int, int> Items { get; set; }
        public int gold = 0;
        public int exp = 0;
    }

    public class CientMissionFinalPacket
    {
        public bool hasReceive;
        public ushort sceneID;
        public ushort imapID;
    }

    public class MissionManager : IEventManager
    {
        #region 主要的静态变量，变量，属性

        public static bool HasWin { get; protected set; }

        private EntityMyself theOwner;

        public Dictionary<int, Dictionary<int, int>> enterableMissions { get; protected set; }

        public Dictionary<int, Dictionary<int, int>> finishedMissions { get; protected set; }

        public Dictionary<int, Dictionary<int, int>> missionTimes { get; protected set; }

        public Dictionary<int, Dictionary<int, int>> missionStars { get; protected set; }

        protected bool isFlush = false;
        protected Action flushAction = null;

        protected int missionMessageCount = 0;

        // 关卡难度次数重置
        protected int MissionCache = 0;
        protected int LevelCache = 0;

        protected bool isFirstCalculateEnterableMissions = false;
        protected List<int> newEnterableMissions = null;
        protected List<int> oldEnterableMissions = null;

        public LuaTable finishedMissionsInfo
        {
            set
            {
                // Debug.LogError("finishedMissionsInfo:" + value.ToString());
                object obj;
                if (Utils.ParseLuaTable(value, typeof(Dictionary<int, Dictionary<int, int>>), out obj))
                {
                    finishedMissions = obj as Dictionary<int, Dictionary<int, int>>;
                    UpdateEnterableMissions();
                }
                else
                {
                    LoggerHelper.Debug("finishedMissionsInfo wrong!");
                }
            }
        }


        public LuaTable missionTimesInfo
        {
            set
            {
                // Debug.LogError("missionTimesInfo:" + value.ToString());
                object obj;
                if (Utils.ParseLuaTable(value, typeof(Dictionary<int, Dictionary<int, int>>), out obj))
                {
                    missionTimes = obj as Dictionary<int, Dictionary<int, int>>;
                }
                else
                {
                    LoggerHelper.Debug("missionTimesInfo wrong!");
                }
            }
        }


        public LuaTable missionStarsInfo
        {
            set
            {
                // Debug.LogError("missionStarsInfo:" + value.ToString());
                object obj;
                if (Utils.ParseLuaTable(value, typeof(Dictionary<int, Dictionary<int, int>>), out obj))
                {
                    missionStars = obj as Dictionary<int, Dictionary<int, int>>;

                    if (missionStars != null)
                    {
                        if (missionStars.Count != 0)
                        {
                            foreach (var item in missionStars)
                            {
                                Mogo.Util.LoggerHelper.Debug("missionStars          " + item.Key + " " + item.Value);
                            }
                        }
                        else
                        {
                            Mogo.Util.LoggerHelper.Debug("missionStars    Count == 0");
                        }
                    }
                    else
                    {
                        Mogo.Util.LoggerHelper.Debug("missionStars    missionStars == null");
                    }

                    if (value.Count == 0)
                    {
                        Mogo.Util.LoggerHelper.Debug("missionStars    source.Count == 0");
                    }
                }
                else
                {
                    LoggerHelper.Debug("missionStarsInfo wrong!");
                }
            }
        }

        #endregion


        #region 构造函数

        public MissionManager(EntityMyself owner)
        {
            theOwner = owner;

            enterableMissions = new Dictionary<int, Dictionary<int, int>>();
            enterableMissions.Add(1, new Dictionary<int, int>());

            isFirstCalculateEnterableMissions = false;

            finishedMissions = new Dictionary<int, Dictionary<int, int>>();
            UpdateEnterableMissions();

            missionTimes = new Dictionary<int, Dictionary<int, int>>();
            missionStars = new Dictionary<int, Dictionary<int, int>>();

            isFirstCalculateEnterableMissions = true;

            missionDropsCache = new Dictionary<int, List<List<int>>>();

            MissionRewardData.FixCondition();

            AddListeners();

            UpdateMissionMessage();
        }

        #endregion


        #region 事件监听

        public void AddListeners()
        {
            EventDispatcher.AddEventListener<bool>(Events.InstanceEvent.UpdateMissionMessage, UpdateMissionMessage);

            //EventDispatcher.AddEventListener(Events.InstanceEvent.UpdateEnterableMissions, UpdateFinishedMissionsReq);
            //EventDispatcher.AddEventListener(Events.InstanceEvent.UpdateMissionTimes, UpdateMissionTimesReq);
            //EventDispatcher.AddEventListener(Events.InstanceEvent.UpdateMissionStars, UpdateMissionStarsReq);

            EventDispatcher.AddEventListener<int, int>(Events.InstanceEvent.InstanceSelected, EnterMissionReq);
            EventDispatcher.AddEventListener(Events.InstanceEvent.ReturnHome, ExitMissionReq);
            EventDispatcher.AddEventListener(Events.InstanceEvent.WinReturnHome, WinExitMissionReq);

            EventDispatcher.AddEventListener<int, bool>(Events.InstanceEvent.InstanceLoaded, StartMissionReq);

            EventDispatcher.AddEventListener(Events.InstanceEvent.GetCurrentReward, GetCurrentRewardReq);

            EventDispatcher.AddEventListener(Events.InstanceEvent.ResetMission, ResetMissionTimesReq);

            EventDispatcher.AddEventListener(Events.InstanceEvent.NotReborn, AvatarNotRebornReq);
            EventDispatcher.AddEventListener(Events.InstanceEvent.Reborn, AvatarRebornReq);

            // Gear
            EventDispatcher.AddEventListener(Events.GearEvent.UploadAllGear, UploadAllGear);
            EventDispatcher.AddEventListener<string>(Events.GearEvent.DownloadAllGear, DownloadAllGear);
            EventDispatcher.AddEventListener<int>(Events.InstanceEvent.SpawnPointStart, SpawnPointStartReq);

            // UI
            EventDispatcher.AddEventListener(Events.InstanceUIEvent.UpdateMapName, UpdateMapUIGridName);

            EventDispatcher.AddEventListener(Events.InstanceUIEvent.UpdateMissionEnable, SetMissionUIGridEnable);
            EventDispatcher.AddEventListener(Events.InstanceUIEvent.UpdateMissionName, SetMissionUIGridName);
            EventDispatcher.AddEventListener(Events.InstanceUIEvent.UpdateMissionStar, SetMissionUIGridStar);

            EventDispatcher.AddEventListener<int>(Events.InstanceUIEvent.UpdateLevelEnable, SetLevelUIGridEnable);
            EventDispatcher.AddEventListener<int, int>(Events.InstanceUIEvent.UpdateLevelTime, SetLevelUITime);
            EventDispatcher.AddEventListener<int, int>(Events.InstanceUIEvent.UpdateLevelStar, SetLevelUIStar);

            EventDispatcher.AddEventListener(Events.InstanceUIEvent.CheckMissionTimes, CheckTotalResetTimesReq);

            EventDispatcher.AddEventListener<int>(Events.GearEvent.FlushGearState, MotityGearState);

            // EventDispatcher.AddEventListener<int>(Events.InstanceEvent.GetMercenaryInfo, GetMercenaryInfo);
            EventDispatcher.AddEventListener(Events.InstanceEvent.AddFriendDegree, AddFriendDegree);

            EventDispatcher.AddEventListener<int, int>(Events.InstanceEvent.SweepMission, SweepMission);
            EventDispatcher.AddEventListener<int, int>(Events.InstanceEvent.GetSweepMissionList, GetSweepMissionList);
            EventDispatcher.AddEventListener(Events.InstanceEvent.GetSweepTimes, GetSweepTimes);

            EventDispatcher.AddEventListener<int>(Events.InstanceUIEvent.GetDrops, GetDrops);

            EventDispatcher.AddEventListener<int>(Events.InstanceEvent.UploadMaxCombo, UploadMaxCombo);

            EventDispatcher.AddEventListener(Events.InstanceUIEvent.GetChestRewardGotMessage, GetChestRewardGotMessage);
            EventDispatcher.AddEventListener<int>(Events.InstanceEvent.GetChestReward, GetChestRewardReq);
            EventDispatcher.AddEventListener(Events.InstanceUIEvent.UpdateChestMessage, UpdateChestMessage);

            EventDispatcher.AddEventListener(Events.InstanceUIEvent.GetBossChestRewardGotMessage, GetBossChestRewardGotMessage);
            EventDispatcher.AddEventListener<int>(Events.InstanceEvent.GetBossChestRewardReq, GetBossChestRewardReq);
            EventDispatcher.AddEventListener(Events.InstanceUIEvent.UpdateBossChestMessage, UpdateBossChestMessage);

            EventDispatcher.AddEventListener<int, int>(Events.InstanceUIEvent.ShowResetMissionWindow, ShowResetMissionWindow);

            EventDispatcher.AddEventListener<int, int>(Events.InstanceUIEvent.UpdateMercenaryButton, UpdateMercenaryButton);

            EventDispatcher.AddEventListener(Events.InstanceEvent.StopAutoFight, StopAutoFight);

            EventDispatcher.AddEventListener<int>("ChooseLevelPathAnimPlayDone", EmptyNewMissions);

            EventDispatcher.AddEventListener<int>(Events.InstanceUIEvent.FlipCard, FlipCard);
            EventDispatcher.AddEventListener(Events.InstanceUIEvent.FlipRestCard, SetCardCanNotGetItem);
            EventDispatcher.AddEventListener<int>(Events.InstanceUIEvent.AutoFlipCard, AutoFlipCard);
            EventDispatcher.AddEventListener(Events.InstanceUIEvent.AutoFlipRestCard, AutoSetCardCanNotGetItem);

            EventDispatcher.AddEventListener<int, int>(Events.InstanceUIEvent.UpdateLevelRecord, GetBestRecord);

            EventDispatcher.AddEventListener(Events.InstanceEvent.EnterRandomMission, EnterRandomMission);

            EventDispatcher.AddEventListener<int, int>(Events.InstanceUIEvent.ShowCard, ShowCardMessage);

            EventDispatcher.AddEventListener(Events.InstanceUIEvent.GetFoggyAbyssMessage, GetFoggyAbyssInfoReq);
            EventDispatcher.AddEventListener<int, int>(Events.InstanceUIEvent.EnterFoggyAbyss, EnterFoggyAbyssReq);
        }


        public void RemoveListeners()
        {
            EventDispatcher.RemoveEventListener<bool>(Events.InstanceEvent.UpdateMissionMessage, UpdateMissionMessage);

            //EventDispatcher.RemoveEventListener(Events.InstanceEvent.UpdateEnterableMissions, UpdateFinishedMissionsReq);
            //EventDispatcher.RemoveEventListener(Events.InstanceEvent.UpdateMissionTimes, UpdateMissionTimesReq);
            //EventDispatcher.RemoveEventListener(Events.InstanceEvent.UpdateMissionStars, UpdateMissionStarsReq);

            EventDispatcher.RemoveEventListener<int, int>(Events.InstanceEvent.InstanceSelected, EnterMissionReq);

            EventDispatcher.RemoveEventListener(Events.InstanceEvent.ReturnHome, ExitMissionReq);
            EventDispatcher.RemoveEventListener(Events.InstanceEvent.WinReturnHome, WinExitMissionReq);
            EventDispatcher.RemoveEventListener<int, bool>(Events.InstanceEvent.InstanceLoaded, StartMissionReq);

            EventDispatcher.RemoveEventListener(Events.InstanceEvent.GetCurrentReward, GetCurrentRewardReq);

            EventDispatcher.RemoveEventListener(Events.InstanceEvent.ResetMission, ResetMissionTimesReq);

            EventDispatcher.RemoveEventListener(Events.InstanceEvent.NotReborn, AvatarNotRebornReq);
            EventDispatcher.RemoveEventListener(Events.InstanceEvent.Reborn, AvatarRebornReq);

            // Gear
            EventDispatcher.RemoveEventListener(Events.GearEvent.UploadAllGear, UploadAllGear);
            EventDispatcher.RemoveEventListener<string>(Events.GearEvent.DownloadAllGear, DownloadAllGear);
            EventDispatcher.RemoveEventListener<int>(Events.InstanceEvent.SpawnPointStart, SpawnPointStartReq);

            // UI
            EventDispatcher.RemoveEventListener(Events.InstanceUIEvent.UpdateMapName, UpdateMapUIGridName);

            EventDispatcher.RemoveEventListener(Events.InstanceUIEvent.UpdateMissionEnable, SetMissionUIGridEnable);
            EventDispatcher.RemoveEventListener(Events.InstanceUIEvent.UpdateMissionName, SetMissionUIGridName);
            EventDispatcher.RemoveEventListener(Events.InstanceUIEvent.UpdateMissionStar, SetMissionUIGridStar);


            EventDispatcher.RemoveEventListener<int>(Events.InstanceUIEvent.UpdateLevelEnable, SetLevelUIGridEnable);
            EventDispatcher.RemoveEventListener<int, int>(Events.InstanceUIEvent.UpdateLevelTime, SetLevelUITime);
            EventDispatcher.RemoveEventListener<int, int>(Events.InstanceUIEvent.UpdateLevelStar, SetLevelUIStar);

            EventDispatcher.RemoveEventListener(Events.InstanceUIEvent.CheckMissionTimes, CheckTotalResetTimesReq);

            EventDispatcher.RemoveEventListener<int>(Events.GearEvent.FlushGearState, MotityGearState);

            // EventDispatcher.RemoveEventListener<int>(Events.InstanceEvent.GetMercenaryInfo, GetMercenaryInfo);
            EventDispatcher.RemoveEventListener(Events.InstanceEvent.AddFriendDegree, AddFriendDegree);

            EventDispatcher.RemoveEventListener<int, int>(Events.InstanceEvent.SweepMission, SweepMission);
            EventDispatcher.RemoveEventListener<int, int>(Events.InstanceEvent.GetSweepMissionList, GetSweepMissionList);
            EventDispatcher.RemoveEventListener(Events.InstanceEvent.GetSweepTimes, GetSweepTimes);

            EventDispatcher.RemoveEventListener<int>(Events.InstanceUIEvent.GetDrops, GetDrops);

            EventDispatcher.RemoveEventListener<int>(Events.InstanceEvent.UploadMaxCombo, UploadMaxCombo);

            EventDispatcher.RemoveEventListener(Events.InstanceUIEvent.GetChestRewardGotMessage, GetChestRewardGotMessage);
            EventDispatcher.RemoveEventListener<int>(Events.InstanceEvent.GetChestReward, GetChestRewardReq);
            EventDispatcher.RemoveEventListener(Events.InstanceUIEvent.UpdateChestMessage, UpdateChestMessage);

            EventDispatcher.RemoveEventListener(Events.InstanceUIEvent.GetBossChestRewardGotMessage, GetBossChestRewardGotMessage);
            EventDispatcher.RemoveEventListener<int>(Events.InstanceEvent.GetBossChestRewardReq, GetBossChestRewardReq);
            EventDispatcher.RemoveEventListener(Events.InstanceUIEvent.UpdateBossChestMessage, UpdateBossChestMessage);

            EventDispatcher.RemoveEventListener<int, int>(Events.InstanceUIEvent.ShowResetMissionWindow, ShowResetMissionWindow);

            EventDispatcher.RemoveEventListener<int, int>(Events.InstanceUIEvent.UpdateMercenaryButton, UpdateMercenaryButton);

            EventDispatcher.RemoveEventListener(Events.InstanceEvent.StopAutoFight, StopAutoFight);

            EventDispatcher.RemoveEventListener<int>("ChooseLevelPathAnimPlayDone", EmptyNewMissions);

            EventDispatcher.RemoveEventListener<int>(Events.InstanceUIEvent.FlipCard, FlipCard);
            EventDispatcher.RemoveEventListener(Events.InstanceUIEvent.FlipRestCard, SetCardCanNotGetItem);
            EventDispatcher.RemoveEventListener<int>(Events.InstanceUIEvent.AutoFlipCard, AutoFlipCard);
            EventDispatcher.RemoveEventListener(Events.InstanceUIEvent.AutoFlipRestCard, AutoSetCardCanNotGetItem);

            EventDispatcher.RemoveEventListener<int, int>(Events.InstanceUIEvent.UpdateLevelRecord, GetBestRecord);

            EventDispatcher.RemoveEventListener(Events.InstanceEvent.EnterRandomMission, EnterRandomMission);

            EventDispatcher.RemoveEventListener<int, int>(Events.InstanceUIEvent.ShowCard, ShowCardMessage);

            EventDispatcher.RemoveEventListener(Events.InstanceUIEvent.GetFoggyAbyssMessage, GetFoggyAbyssInfoReq);
            EventDispatcher.RemoveEventListener<int, int>(Events.InstanceUIEvent.EnterFoggyAbyss, EnterFoggyAbyssReq);
        }

        #endregion


        #region 回调分发

        public void MissionResp(byte msg, LuaTable luaTable)
        {
            switch (msg)
            {
                case (byte)MissionReq.ENTER_MISSION:
                    LoggerHelper.Debug("ENTER_MISSION");
                    int enterMissionArg1 = Convert.ToInt32(luaTable["1"]);
                    OnEnterMissionResp(enterMissionArg1);
                    break;

                case (byte)MissionReq.START_MISSION:
                    LoggerHelper.Debug("START_MISSION");
                    OnStartMissionResp();
                    break;

                case (byte)MissionReq.EXIT_MISSION:
                    LoggerHelper.Debug("EXIT_MISSION");
                    OnExitMissionResp();
                    break;

                case (byte)MissionReq.GET_STAR_MISSION:
                    LoggerHelper.Debug("GET_STAR_MISSION");
                    OnUpdateMissionStarsResp(luaTable);
                    break;

                case (byte)MissionReq.RESET_MISSION_TIMES:
                    LoggerHelper.Debug("RESET_MISSION_TIMES");
                    int resetMissionErrorCode = Convert.ToInt32(luaTable["1"]);
                    OnResetMissionTimesResp(resetMissionErrorCode);
                    break;

                case (byte)MissionReq.GET_MISSION_TIMES:
                    LoggerHelper.Debug("GET_MISSION_TIMES");
                    OnUpdateMissionTimesResp(luaTable);
                    break;

                case (byte)MissionReq.GET_FINISHED_MISSIONS:
                    LoggerHelper.Debug("GET_FINISHED_MISSIONS");
                    OnUpdateCharaterFinishedMissionsResp(luaTable);
                    break;

                case (byte)MissionReq.GET_NOTIFY_TO_CLENT_EVENT:
                    LoggerHelper.Debug("GET_NOTIFY_TO_CLENT_EVENT");
                    int eventID = int.Parse(luaTable["1"].ToString());
                    OnNotifyToClientEventResp(eventID);
                    break;

                case (byte)MissionReq.NOTIFY_TO_CLIENT_RESULT:
                    LoggerHelper.Debug("NOTIFY_TO_CLIENT_RESULT");
                    break;

                case (byte)MissionReq.GET_MISSION_LEFT_TIME:
                    LoggerHelper.Debug("GET_MISSION_LEFT_TIME");
                    int getMissionLeftTimeArg1 = (int)luaTable["1"];
                    OnGetMissionLeftTimeResp(getMissionLeftTimeArg1);
                    break;

                case (byte)MissionReq.SPAWNPOINT_START:
                    LoggerHelper.Debug("SPAWNPOINT_START");
                    OnSpawnPointStartResp();
                    break;

                case (byte)MissionReq.SPAWNPOINT_STOP:
                    LoggerHelper.Debug("SPAWNPOINT_STOP");
                    break;

                case (byte)MissionReq.NOTIFY_TO_CLIENT_RESULT_SUCCESS:
                    LoggerHelper.Debug("NOTIFY_TO_CLIENT_RESULT_SUCCESS");

                    if (theOwner.deathFlag != 0)
                    {
                        MogoUIManager.Instance.ShowMogoInstanceRebornUI(false);
                        MogoUIManager.Instance.ShowMogoBattleMainUI();
                        MainUIViewManager.Instance.ResetUIData();
                    }

                    HasWin = true;

                    //停托管
                    //MogoWorld.thePlayer.AutoFight = AutoFightState.IDLE;

                    var id = MissionData.GetCGByMission(MogoWorld.thePlayer.sceneId);
                    if (id > 0)
                    {
                        StoryManager.Instance.SetCallBack(() => { OnNotifyToClientResultSuccessResp(luaTable); });
                        StoryManager.Instance.PlayStory(id);
                    }
                    else
                    {
                        OnNotifyToClientResultSuccessResp(luaTable);
                    }

                    break;

                case (byte)MissionReq.NOTIFY_TO_CLIENT_RESULT_FAILED:
                    LoggerHelper.Debug("NOTIFY_TO_CLIENT_RESULT_FAILED");
                    OnNotifyToClientResultFailedResp();
                    break;

                case (byte)MissionReq.GET_MISSION_REWARDS:
                    LoggerHelper.Debug("GET_MISSION_REWARDS");
                    if (MapData.dataMap.Get(MogoWorld.thePlayer.sceneId).type == MapType.ClimbTower)
                    {
                        return;
                    }
                    OnGetMissionRewardResp(luaTable);
                    break;

                case (byte)MissionReq.CLIENT_MISSION_INFO:
                    LoggerHelper.Debug("CLIENT_MISSION_INFO");
                    string result = (string)luaTable["1"];
                    OnUpdateMissionGearInfo(result);
                    break;

                case (byte)MissionReq.KILL_MONSTER_EXP:
                    LoggerHelper.Debug("KILL_MONSTER_EXP");
                    Dictionary<int, int> data;
                    if (Utils.ParseLuaTable(luaTable, out data))
                    {
                        if (data.ContainsKey(1))
                        {
                            BillboardLogicManager.Instance.AddAloneBattleBillboard(MogoWorld.thePlayer.Transform.position, data[1], AloneBattleBillboardType.Exp);
                        }
                    }
                    break;

                case (byte)MissionReq.QUIT_MISSION:
                    LoggerHelper.Debug("QUIT_MISSION");
                    break;

                case (byte)MissionReq.NOTIFY_TO_CLENT_SPAWNPOINT:
                    LoggerHelper.Debug("NOTIFY_TO_CLENT_SPAWNPOINT");
                    int preID = Convert.ToInt32(luaTable["1"]);
                    EventDispatcher.TriggerEvent(Events.GearEvent.SpawnPointDead, preID);
                    break;

                case (byte)MissionReq.UPLOAD_COMBO:
                    LoggerHelper.Debug("UPLOAD_COMBO");
                    break;

                case (byte)MissionReq.GET_MISSION_TRESURE_REWARDS:
                    LoggerHelper.Debug("GET_MISSION_TRESURE_REWARDS: " + luaTable.ToString());
                    if (Utils.ParseLuaTable(luaTable, out chestData))
                        CheckChestState(chestData);
                    break;

                case (byte)MissionReq.REVIVE:
                    LoggerHelper.Debug("REVIVE");
                    OnAvatarRebornResp(luaTable);
                    break;

                case (byte)MissionReq.GET_REVIVE_TIMES:
                    LoggerHelper.Debug("GET_REVIVE_TIMES");
                    int rebornTimes = Convert.ToInt32(luaTable["1"]);
                    OnGetRebornTimesResp(rebornTimes);
                    break;

                case (byte)MissionReq.MSG_SWEEP_MISSION:
                    LoggerHelper.Debug("MSG_SWEEP_MISSION");
                    int flag = Convert.ToInt32(luaTable["1"]);
                    OnSweepMissionResp(flag);
                    break;

                case (byte)MissionReq.GET_MISSION_SWEEP_LIST:
                    LoggerHelper.Debug("GET_MISSION_SWEEP_LIST");
                    OnGetSweepMissionResp(luaTable);
                    break;

                case (byte)MissionReq.MSG_GET_SWEEP_TIMES:
                    LoggerHelper.Debug("MSG_GET_SWEEP_TIMES");
                    int times = Convert.ToInt32(luaTable["1"]);
                    OnGetSweepTimesResp(times);
                    break;

                case (byte)MissionReq.GET_RESET_TIMES:
                    LoggerHelper.Debug("GET_RESET_TIMES");
                    int totalResetTimes = Convert.ToInt32(luaTable["1"]);
                    OnCheckTotalResetTimesResp(totalResetTimes);
                    break;

                case (byte)MissionReq.GET_RESET_TIMES_BY_MISSION:
                    LoggerHelper.Debug("GET_RESET_TIMES_BY_MISSION");
                    int missionResetTimes = Convert.ToInt32(luaTable["1"]);
                    OnCheckMissionResetTimesResp(MissionCache, LevelCache, missionResetTimes);
                    break;

                case (byte)MissionReq.NOTIFY_TO_CLIENT_TO_UPLOAD_COMBO:
                    LoggerHelper.Debug("NOTIFY_TO_CLIENT_TO_UPLOAD_COMBO");
                    UploadMaxCombo(theOwner.GetMaxCombo());
                    break;

                case (byte)70:
                    OnGetDrops(luaTable);
                    break;

                case (byte)MissionReq.GO_TO_INIT_MAP:
                    break;

                case (byte)MissionReq.GET_MISSION_TRESURE:
                    LoggerHelper.Debug("GET_MISSION_TRESURE: " + luaTable.ToString());
                    int getMissionChestResult = Convert.ToInt32(luaTable["1"]);
                    OnGetChestRewardResp(getMissionChestResult);
                    break;

                case (byte)MissionReq.NOTIFY_TO_CLIENT_MISSION_REWARD:
                    OnBuiltClientMissionRewardPool(luaTable);
                    ServerProxy.SomeToLocal = true;
                    MogoWorld.IsClientPositionSync = false;
                    Mogo.GameLogic.LocalServer.LocalServerSceneManager.Instance.EnterMission(theOwner.CurMissionID, theOwner.CurMissionLevel);
                    int sceneId = MissionData.GetSceneId(theOwner.CurMissionID, theOwner.CurMissionLevel);
                    if (sceneId > -1)
                    {
                        theOwner.sceneId = (ushort)sceneId;
                    }
                    break;

                case (byte)MissionReq.UPLOAD_COMBO_AND_BOTTLE:
                    LoggerHelper.Debug("UPLOAD_COMBO_AND_BOTTLE:" + luaTable.ToString());
                    HandleClientMissionWonData(luaTable);
                    break;

                case (byte)MissionReq.NOTIFY_TO_CLIENT_TO_LOAD_ITNI_MAP:
                    LoggerHelper.Debug("NOTIFY_TO_CLIENT_TO_LOAD_ITNI_MAP:" + luaTable.ToString());
                    ServerProxy.SomeToLocal = false;
                    LocalServerSceneManager.Instance.ExitMission();

                    ushort sceneID = Convert.ToUInt16(luaTable["1"]);
                    ushort imap_id = Convert.ToUInt16(luaTable["2"]);

                    var map = MapData.dataMap.Get(sceneID);
                    theOwner.position = new Vector3(map.enterX * 0.01f, -10000f, map.enterY * 0.01f);
                    theOwner.imap_id = imap_id;
                    theOwner.sceneId = sceneID;
                    break;

                case (byte)MissionReq.GET_MISSION_RECORD:
                    HandleBestRecordMessage(luaTable);
                    break;

                case (byte)MissionReq.NOTIFY_TO_CLIENT_MISSION_INFO:
                    theOwner.CurMissionID = Convert.ToInt32(luaTable["1"]);
                    theOwner.CurMissionLevel = Convert.ToInt32(luaTable["2"]);
                    break;

                case (byte)MissionReq.GET_ACQUIRED_MISSION_BOSS_TREASURE:
                    if (Utils.ParseLuaTable(luaTable, out bossChestData))
                        SetBossChestState(bossChestData);
                    break;

                case (byte)MissionReq.GET_MISSION_BOSS_TREASURE:
                    int bossChestErrorCode = Convert.ToInt32(luaTable["1"]);
                    OnGetBossChestRewardResp(bossChestErrorCode);
                    break;

                case (byte)MissionReq.MWSY_MISSION_NOTIFY_CLIENT:
                    OnNotifyToClientFoggyAbyssOpen();
                    break;

                case (byte)MissionReq.MWSY_MISSION_GET_INFO:
                    bool isShowFoggyAbyss = true;
                    int currentFoggyAbyssLevel = 0;
                    bool hasFoggyAbyssPlay = false;
                    if (!luaTable.ContainsKey("1"))
                    {
                        isShowFoggyAbyss = false;
                    }
                    else
                    {
                        currentFoggyAbyssLevel = Convert.ToInt32(luaTable["1"]);
                        int hasPlayFoggyAbyssCode = Convert.ToInt32(luaTable["2"]);
                        if (hasPlayFoggyAbyssCode > 0)
                            hasFoggyAbyssPlay = true;
                    }
                    OnGetFoggyAbyssInfoResp(isShowFoggyAbyss, currentFoggyAbyssLevel, hasFoggyAbyssPlay);
                    break;

                default:
                    break;
            }
        }

        #endregion


        #region 进入退出关卡

        #region 进入关卡

        // 进入关卡
        public void EnterMissionReq(int missionID, int level)
        {
            // if (enterableMissions.ContainsKey(missionID))

            theOwner.ApplyMissionID = missionID;
            theOwner.ApplyMissionLevel = level;

            theOwner.CurMissionID = missionID;
            theOwner.CurMissionLevel = level;

            if (missionID == 50000)
                theOwner.RpcCall("MissionReq", (byte)MissionReq.ENTER_MISSION, (ushort)missionID, (ushort)1, "");
            else
                theOwner.RpcCall("MissionReq", (byte)MissionReq.ENTER_MISSION, (ushort)missionID, (ushort)level, "");
        }

        public void OnEnterMissionResp(int errorCode)
        {
            switch (errorCode)
            {
                case -2:
                    // 等级不足
                    MogoMsgBox.Instance.ShowFloatingText(LanguageData.GetContent(300301));
                    InstanceUILogicManager.Instance.EnterFailed();
                    LevelNoEnoughUILogicManager.IsChooseLevelUI = true;
                    MogoUIManager.Instance.ShowLevelNoEnoughUI(null);
                    ResetApplyMissionData();
                    break;
                case -3:
                    MogoMsgBox.Instance.ShowFloatingText(LanguageData.GetContent(300302));
                    InstanceUILogicManager.Instance.EnterFailed();
                    ResetApplyMissionData();
                    break;
                case -4:
                    MogoMsgBox.Instance.ShowFloatingText(LanguageData.GetContent(300303));
                    InstanceUILogicManager.Instance.EnterFailed();
                    ResetApplyMissionData();
                    break;
                case -5:
                    // 体力不足
                    MogoMsgBox.Instance.ShowFloatingText(LanguageData.GetContent(300304));
                    InstanceUILogicManager.Instance.EnterFailed();
                    MogoUIManager.Instance.ShowEnergyNoEnoughUI(null);
                    ResetApplyMissionData();
                    break;
                case -6:
                    MogoMsgBox.Instance.ShowFloatingText(LanguageData.GetContent(300305));
                    InstanceUILogicManager.Instance.EnterFailed();
                    ResetApplyMissionData();
                    break;
                case -7:
                    MogoMsgBox.Instance.ShowFloatingText(LanguageData.GetContent(48500)); // 战斗力不足
                    InstanceUILogicManager.Instance.EnterFailed();
                    ResetApplyMissionData();
                    break;
                case -8:
                    MogoMsgBox.Instance.ShowFloatingText(LanguageData.GetContent(48501)); // 随机场景不存在
                    InstanceUILogicManager.Instance.EnterFailed();
                    ResetApplyMissionData();
                    break;
                default:
                    InstanceUILogicManager.Instance.EnterSuccess();
                    break;
            }

            // MogoWorld.SwitchScene(missionID);
            // EventDispatcher.TriggerEvent(Events.InstanceEvent.InstanceSelected, missionID);
        }

        public void ResetApplyMissionData()
        {
            theOwner.ApplyMissionID = 0;
            theOwner.ApplyMissionLevel = 0;
        }

        #endregion

        #region 开始关卡

        // 关卡可以开始
        public void StartMissionReq(int missionID, bool isInstance)
        {
            // theOwner.SetModelInfo(isInstance);
            //重新装载本场景自动战斗路点

            HasWin = false;

            var mission = MapData.dataMap.GetValueOrDefault(missionID, null);
            if (mission != null && mission.type != MapType.Normal)
            {
                theOwner.RpcCall("MissionReq", (byte)MissionReq.START_MISSION, (ushort)InstanceUILogicManager.Instance.mercenaryID, (ushort)1, "");
                ///theOwner.RpcCall("MissionReq", (byte)MissionReq.START_MISSION, (ushort)0, (ushort)1, "");
            }
        }

        public void OnStartMissionResp()
        {
            EventDispatcher.TriggerEvent(Events.InstanceEvent.MissionStart);
        }

        #endregion

        #region 退出关卡

        // 退出关卡
        public void ExitMissionReq()
        {
            ResetApplyMissionData();

            MogoMainCamera.Instance.ResetToNormal();

            if (ControlStick.instance != null)
                ControlStick.instance.Reset();

            MogoWorld.ClearEntities();
            InstanceUILogicManager.Instance.ResetMercenaryID();
            theOwner.RpcCall("MissionReq", (byte)MissionReq.EXIT_MISSION, (ushort)1, (ushort)1, "");
            UpdateMissionMessage();
        }


        // 胜利退出关卡
        public void WinExitMissionReq()
        {
            ResetApplyMissionData();

            MogoMainCamera.Instance.ResetToNormal();

            if (ControlStick.instance != null)
                ControlStick.instance.Reset();

            MogoWorld.ClearEntities();
            InstanceUILogicManager.Instance.ResetMercenaryID();
            theOwner.RpcCall("MissionReq", (byte)MissionReq.QUIT_MISSION, (ushort)1, (ushort)1, "");
            UpdateMissionMessage();
        }


        // 强制退出关卡
        public void ForceExitMissionReq()
        {
            ResetApplyMissionData();

            MogoMainCamera.Instance.ResetToNormal();

            if (ControlStick.instance != null)
                ControlStick.instance.Reset();

            MogoWorld.ClearEntities();
            InstanceUILogicManager.Instance.ResetMercenaryID();
            theOwner.RpcCall("MissionReq", (byte)MissionReq.GO_TO_INIT_MAP, (ushort)1, (ushort)1, "");
            UpdateMissionMessage();
        }


        public void OnExitMissionResp()
        {
            theOwner.RpcCall("MissionReq", (byte)MissionReq.CLIENT_MISSION_INFO, (ushort)1, (ushort)1, "");
            UpdateMissionMessage();

            // MogoUIManager.Instance.ShowMogoNormalMainUI();
        }

        #endregion

        #endregion


        #region 关卡数据

        // 更新关卡数据 
        public void UpdateMissionMessage(bool isFlushUI = false)
        {
            isFlush = isFlushUI;
            UpdateFinishedMissionsReq();
            UpdateMissionTimesReq();
            UpdateMissionStarsReq();
        }

        public void UpdateMissionMessage(bool isFlushUI, Action action)
        {
            isFlush = isFlushUI;
            flushAction = action;
            UpdateFinishedMissionsReq();
            UpdateMissionTimesReq();
            UpdateMissionStarsReq();
        }


        // 更新已完成列表
        public void UpdateFinishedMissionsReq()
        {
            theOwner.RpcCall("MissionReq", (byte)MissionReq.GET_FINISHED_MISSIONS, (byte)1, (byte)1, "");
        }

        public void OnUpdateCharaterFinishedMissionsResp(LuaTable luaTable)
        {
            finishedMissionsInfo = luaTable;
            CheckFlushUI();
        }


        // 更新可进入的列表
        protected void UpdateEnterableMissions()
        {
            if (isFirstCalculateEnterableMissions)
            {
                newEnterableMissions = new List<int>();
                oldEnterableMissions = new List<int>();
                isFirstCalculateEnterableMissions = false;
            }

            enterableMissions.Clear();

            if (finishedMissions == null || finishedMissions.Count == 0 ||
                (finishedMissions.Count == 1 && finishedMissions.ContainsKey(10100)))
            {
                enterableMissions.Add(10101, new Dictionary<int, int>());
                enterableMissions[10101].Add(1, 0);

                InstanceMissionChooseUIViewManager.Instance.SetMapOpenPage(1);

                foreach (var map in MapUIMappingData.dataMap)
                    InstanceUIViewManager.Instance.SetEnable(map.Key, false);

                InstanceUIViewManager.Instance.SetEnable(0, true);
                return;
            }

            foreach (KeyValuePair<int, Dictionary<int, int>> item in finishedMissions)
            {
                if (oldEnterableMissions.Contains(item.Key))
                {
                    oldEnterableMissions.Remove(item.Key);
                }

                if (!enterableMissions.ContainsKey(item.Key))
                {
                    enterableMissions.Add(item.Key, new Dictionary<int, int>());
                }

                foreach (var finishLevel in item.Value)
                {
                    if (enterableMissions[item.Key].ContainsKey(finishLevel.Key))
                        enterableMissions[item.Key][finishLevel.Key] = finishLevel.Value;
                    else
                        enterableMissions[item.Key].Add(finishLevel.Key, finishLevel.Value);

                    var tempData = MissionData.dataMap.FirstOrDefault(t => t.Value.mission == item.Key && t.Value.difficulty == finishLevel.Key);

                    if (tempData.Value == null)
                    {
                        // Mission表里面没有该关卡该难度的数据
                        continue;
                    }

                    MissionData data = tempData.Value;

                    if (data.postMissions == null)
                    {
                        // 没有后置关
                        continue;
                    }
                    foreach (int postMission in data.postMissions)
                    {
                        if (postMission == 0)
                        {
                            // 后置关填写为0
                            continue;
                        }

                        foreach (KeyValuePair<int, int> preMission in MissionData.dataMap[postMission].preMissions)
                        {
                            bool flag = true;

                            if (!finishedMissions.ContainsKey(preMission.Key)
                                || !finishedMissions[preMission.Key].ContainsKey(preMission.Value)
                                || finishedMissions[preMission.Key][preMission.Value] < 2)
                            {
                                // 如果后置关的前置关只要有一个关卡/难度没完成，将不会进行下述真值带来的操作
                                flag = false;
                            }

                            if (flag)
                            {
                                // 如果可进入列表没有后置关
                                if (!enterableMissions.ContainsKey(MissionData.dataMap[postMission].mission))
                                {
                                    // 把后置关加进去
                                    enterableMissions.Add(MissionData.dataMap[postMission].mission, new Dictionary<int, int>());

                                    // 新增关卡，处理线路显示
                                    if (!isFirstCalculateEnterableMissions &&
                                        !finishedMissions.ContainsKey(MissionData.dataMap[postMission].mission) &&
                                        newEnterableMissions != null &&
                                        !newEnterableMissions.Contains(MissionData.dataMap[postMission].mission))
                                    {
                                        // 加进待显示列表
                                        newEnterableMissions.Add(MissionData.dataMap[postMission].mission);
                                    }
                                }

                                // 如果后置关不存在这个难度的评分，把评分加进去
                                if (!enterableMissions[MissionData.dataMap[postMission].mission].ContainsKey(MissionData.dataMap[postMission].difficulty))
                                    enterableMissions[MissionData.dataMap[postMission].mission].Add(MissionData.dataMap[postMission].difficulty, 0);
                            }
                        }
                    }
                }
            }

            if (!enterableMissions.ContainsKey(10101))
            {
                enterableMissions.Add(10101, new Dictionary<int, int>());
                enterableMissions[10101].Add(1, 0);
            }

            List<int> enterableMaps = new List<int>();
            foreach (var enterableMission in enterableMissions)
            {
                //string output = enterableMission.Key.ToString() + " ";
                //foreach (var tempDataLevel in enterableMission.Value)
                //{
                //    output += "(" + tempDataLevel.Key + " " + tempDataLevel.Value + ") ";
                //}
                //Debug.LogError(output);

                KeyValuePair<int, MapUIMappingData> tempData = MapUIMappingData.dataMap.FirstOrDefault(t => t.Value.grid.ContainsValue(enterableMission.Key));
                if (tempData.Value != null && !enterableMaps.Contains(tempData.Key))
                    enterableMaps.Add(tempData.Key);
            }

            foreach (var map in MapUIMappingData.dataMap)
                InstanceUIViewManager.Instance.SetEnable(map.Key, false);

            foreach (int enterableMap in enterableMaps)
                InstanceUIViewManager.Instance.SetEnable(enterableMap, true);

            InstanceMissionChooseUIViewManager.Instance.SetMapOpenPage(enterableMaps.Count);
        }


        // 更新关卡剩余次数
        public void UpdateMissionTimesReq()
        {
            theOwner.RpcCall("MissionReq", (byte)MissionReq.GET_MISSION_TIMES, (byte)1, (byte)1, "");
        }

        public void OnUpdateMissionTimesResp(LuaTable luaTable)
        {
            missionTimesInfo = luaTable;
            CheckFlushUI();
        }


        // 更新关卡星星数量
        public void UpdateMissionStarsReq()
        {
            theOwner.RpcCall("MissionReq", (byte)MissionReq.GET_STAR_MISSION, (byte)1, (byte)1, "");
        }

        public void OnUpdateMissionStarsResp(LuaTable luaTable)
        {
            missionStarsInfo = luaTable;
            CheckFlushUI();
        }

        private void CheckFlushUI()
        {
            missionMessageCount++;
            if (missionMessageCount == 3)
            {
                missionMessageCount = 0;

                if (!isFlush)
                    return;

                isFlush = false;
                if (InstanceUIViewManager.Instance != null
                    && InstanceUILogicManager.hasInit)
                {
                    SetMissionUIGridEnable();
                    SetMissionUIGridName();
                    SetMissionUIGridStar();
                    SetLevelUITime(MissionCache, LevelCache);
                }

                if (flushAction != null)
                {
                    flushAction();
                    flushAction = null;
                }
            }
        }

        #endregion


        #region 副本UI设置

        #region 地图选择

        // 更新地图格子名字
        public void UpdateMapUIGridName()
        {
            foreach (var data in MapUIMappingData.dataMap)
            {
                InstanceUILogicManager.Instance.SetMapName(data.Value.id, LanguageData.dataMap[data.Value.name].content);
            }
        }

        #endregion

        #region 关卡选择

        // 更新关卡格子是否可以进入
        public void SetMissionUIGridEnable()
        {
            if (InstanceMissionChooseUIViewManager.SHOW_MISSION_BY_DRAG)
            {
                foreach (var mData in MapUIMappingData.dataMap)
                {
                    var defaultGrid = MapUIMappingData.dataMap[mData.Key].grid;
                    foreach (var grid in defaultGrid)
                    {
                        bool flag = false;
                        if (enterableMissions.ContainsKey(grid.Value))
                        {
                            flag = true;
                        }

                        // if (MogoUIManager.Instance.CurrentUI == MogoUIManager.Instance.m_NewInstanceChooseLevelUI && 
                        if (MogoUIManager.Instance.m_InstanceMissionChooseUI != null &&
                            InstanceUILogicManager.hasInit)
                        {
                            // InstanceUIViewManager.Instance.SetInstanceGridEnable(grid.Key, flag);
                            InstanceMissionChooseUIViewManager.Instance.SetGridEnable(mData.Key, grid.Key, flag);
                        }
                    }

                    if (MogoUIManager.Instance.CurrentUI == MogoUIManager.Instance.m_InstanceMissionChooseUI &&
                        MogoUIManager.Instance.m_InstanceMissionChooseUI != null &&
                        InstanceUILogicManager.hasInit &&
                        newEnterableMissions != null)
                    {

                        foreach (var grid in mData.Value.grid)
                        {
                            if (finishedMissions.ContainsKey(grid.Value) || oldEnterableMissions.Contains(grid.Value))
                            {
                                if (grid.Key - 1 >= 0)
                                    InstanceMissionChooseUIViewManager.Instance.ShowChooseLevelPath(mData.Key, grid.Key - 1, true);
                            }
                            else
                            {
                                if (grid.Key - 1 >= 0)
                                    InstanceMissionChooseUIViewManager.Instance.ShowChooseLevelPath(mData.Key, grid.Key - 1, false);
                            }
                        }
                    }
                }

                foreach (var mData in MapUIMappingData.dataMap)
                {
                    foreach (var newMissionID in newEnterableMissions)
                    {
                        if (mData.Value.grid.ContainsValue(newMissionID))
                        {
                            Dictionary<int, int> gridMessages = mData.Value.grid;
                            foreach (var gridMessage in gridMessages)
                            {
                                if (gridMessage.Value == newMissionID)
                                {
                                    InstanceMissionChooseUIViewManager.Instance.PlayChooseLevelPathAnim(mData.Key, gridMessage.Key - 1);
                                    oldEnterableMissions.Add(newMissionID);
                                    break;
                                }
                            }
                            break;
                        }
                    }
                }

                newEnterableMissions.Clear();
            }

            // Old Code
            else
            {
                var defaultGrid = MapUIMappingData.dataMap[InstanceUILogicManager.Instance.MapID].grid;
                foreach (var grid in defaultGrid)
                {
                    bool flag = false;
                    if (enterableMissions.ContainsKey(grid.Value))
                    {
                        flag = true;
                    }

                    // if (MogoUIManager.Instance.CurrentUI == MogoUIManager.Instance.m_NewInstanceChooseLevelUI && 
                    if (MogoUIManager.Instance.m_NewInstanceChooseLevelUI != null &&
                        InstanceUILogicManager.hasInit)
                    {
                        // InstanceUIViewManager.Instance.SetInstanceGridEnable(grid.Key, flag);
                        NewInstanceUIChooseLevelViewManager.Instance.SetGridEnable(grid.Key, flag);
                    }
                }

                if (MogoUIManager.Instance.CurrentUI == MogoUIManager.Instance.m_NewInstanceChooseLevelUI &&
                    MogoUIManager.Instance.m_NewInstanceChooseLevelUI != null &&
                    InstanceUILogicManager.hasInit &&
                    newEnterableMissions != null)
                {
                    foreach (var data in MapUIMappingData.dataMap)
                    {
                        if (data.Value.id != InstanceUILogicManager.Instance.MapID)
                            continue;
                        foreach (var grid in data.Value.grid)
                        {
                            if (finishedMissions.ContainsKey(grid.Value) || oldEnterableMissions.Contains(grid.Value))
                            {
                                if (grid.Key - 1 >= 0)
                                    NewInstanceUIChooseLevelViewManager.Instance.ShowChooseLevelPath(grid.Key - 1, true);
                            }
                            else
                            {
                                if (grid.Key - 1 >= 0)
                                    NewInstanceUIChooseLevelViewManager.Instance.ShowChooseLevelPath(grid.Key - 1, false);
                            }
                        }
                    }


                    foreach (var newMissionID in newEnterableMissions)
                    {
                        foreach (var data in MapUIMappingData.dataMap)
                        {
                            if (data.Value.grid.ContainsValue(newMissionID)
                                && data.Value.id == InstanceUILogicManager.Instance.MapID)
                            {
                                Dictionary<int, int> gridMessages = data.Value.grid;
                                foreach (var gridMessage in gridMessages)
                                {
                                    if (gridMessage.Value == newMissionID)
                                    {
                                        NewInstanceUIChooseLevelViewManager.Instance.PlayChooseLevelPathAnim(gridMessage.Key - 1);
                                        oldEnterableMissions.Add(newMissionID);
                                        break;
                                    }
                                }
                                break;
                            }
                        }
                    }

                    newEnterableMissions.Clear();
                }
            }
        }

        // 更新关卡格子名字
        public void SetMissionUIGridName()
        {
            if (InstanceMissionChooseUIViewManager.SHOW_MISSION_BY_DRAG)
            {
                foreach (var mData in MapUIMappingData.dataMap)
                {
                    int mapID = mData.Key;
                    foreach (var data in MapUIMappingData.dataMap[mapID].gridName)
                    {
                        InstanceUILogicManager.Instance.SetGridName(mapID, data.Key, LanguageData.dataMap[data.Value].content);
                    }

                    foreach (var data in MapUIMappingData.dataMap[mapID].gridImage)
                    {
                        InstanceUILogicManager.Instance.SetGridImage(mapID, data.Key, IconData.dataMap.Get(data.Value).path);
                    }
                }
            }

                // Old Code
            else
            {
                int mapID = InstanceUILogicManager.Instance.MapID;

                foreach (var data in MapUIMappingData.dataMap[mapID].gridName)
                {
                    InstanceUILogicManager.Instance.SetGridName(data.Key, LanguageData.dataMap[data.Value].content);
                }

                foreach (var data in MapUIMappingData.dataMap[mapID].gridImage)
                {
                    InstanceUILogicManager.Instance.SetGridImage(data.Key, IconData.dataMap.Get(data.Value).path);
                }
            }
        }

        // 更新关卡格子星星
        public void SetMissionUIGridStar()
        {
            if (InstanceMissionChooseUIViewManager.SHOW_MISSION_BY_DRAG)
            {
                foreach (var mData in MapUIMappingData.dataMap)
                {
                    int mapID = mData.Key;
                    foreach (var data in MapUIMappingData.dataMap[mapID].grid)
                    {
                        int missionID = data.Value;
                        int sum = 0;
                        int easyGrade = 0;
                        int hardGrade = 0;

                        if (missionStars.ContainsKey(missionID))
                        {
                            if (missionStars[missionID].ContainsKey(1))
                            {
                                sum += missionStars[missionID][1];
                                easyGrade = missionStars[missionID][1];
                            }

                            if (missionStars[missionID].ContainsKey(2))
                            {
                                sum += missionStars[missionID][2];
                                hardGrade = missionStars[missionID][2];
                            }

                            if (missionStars[missionID].ContainsKey(3))
                                sum += missionStars[missionID][3];
                        }
                        else
                        {
                            sum = 0;
                        }

                        if (InstanceMissionChooseUIViewManager.Instance != null)
                        {
                            InstanceMissionChooseUIViewManager.Instance.ShowMarks(mapID, data.Key, easyGrade, hardGrade);
                        }
                    }
                }
            }

            else
            {
                int mapID = InstanceUILogicManager.Instance.MapID;
                foreach (var data in MapUIMappingData.dataMap[mapID].grid)
                {
                    int missionID = data.Value;
                    int sum = 0;
                    int easyGrade = 0;
                    int hardGrade = 0;

                    if (missionStars.ContainsKey(missionID))
                    {
                        if (missionStars[missionID].ContainsKey(1))
                        {
                            sum += missionStars[missionID][1];
                            easyGrade = missionStars[missionID][1];
                        }

                        if (missionStars[missionID].ContainsKey(2))
                        {
                            sum += missionStars[missionID][2];
                            hardGrade = missionStars[missionID][2];
                        }

                        if (missionStars[missionID].ContainsKey(3))
                            sum += missionStars[missionID][3];
                    }
                    else
                    {
                        sum = 0;
                    }

                    // InstanceUIViewManager.Instance.SetInstanceGridStars(data.Key, sum);
                    if (NewInstanceUIChooseLevelViewManager.Instance != null)
                    {
                        NewInstanceUIChooseLevelViewManager.Instance.ShowMarks(data.Key, easyGrade, hardGrade);
                    }
                }
            }
        }

        #endregion

        #region 难度选择

        // 刷新难度格子是否可以进入
        public void SetLevelUIGridEnable(int missionID)
        {
            if (enterableMissions.ContainsKey(missionID))
            {
                if (enterableMissions[missionID].ContainsKey(1))
                {
                    // InstanceUIViewManager.Instance.SetInstanceLevelEnable(0, true);
                    InstanceLevelChooseUIViewManager.Instance.SetBtnLevelChooseEnable(0, true);
                }
                else
                {
                    // InstanceUIViewManager.Instance.SetInstanceLevelEnable(0, false);
                    InstanceLevelChooseUIViewManager.Instance.SetBtnLevelChooseEnable(0, false);
                }

                if (enterableMissions[missionID].ContainsKey(2))
                {
                    // InstanceUIViewManager.Instance.SetInstanceLevelEnable(1, true);
                    InstanceLevelChooseUIViewManager.Instance.SetBtnLevelChooseEnable(1, true);
                }
                else
                {
                    // InstanceUIViewManager.Instance.SetInstanceLevelEnable(1, false);
                    InstanceLevelChooseUIViewManager.Instance.SetBtnLevelChooseEnable(1, false);
                }

                //if (enterableMissions[missionID].ContainsKey(3))
                //{
                //    InstanceUIViewManager.Instance.SetInstanceLevelEnable(2, true);
                //}
                //else
                //{
                //    InstanceUIViewManager.Instance.SetInstanceLevelEnable(2, false);
                //}
            }
        }

        // 刷新难度格子剩余次数
        //public void SetLevelUITime(int missionID)
        //{
        //    if (enterableMissions.ContainsKey(missionID))
        //    {
        //        foreach (var missionData in MissionData.dataMap)
        //        {
        //            if (missionData.Value.mission == missionID && missionData.Value.difficulty == 1)
        //            {
        //                SetLevelUIMessage(missionID, 1, missionData.Value);
        //                continue;
        //            }

        //            else if (missionData.Value.mission == missionID && missionData.Value.difficulty == 2)
        //            {
        //                SetLevelUIMessage(missionID, 2, missionData.Value);
        //                continue;
        //            }

        //            else if (missionData.Value.mission == missionID && missionData.Value.difficulty == 3)
        //            {
        //                SetLevelUIMessage(missionID, 3, missionData.Value);
        //                continue;
        //            }
        //        }
        //    }
        //    else
        //    {
        //        LoggerHelper.Debug("!enterableMissions[missionID].ContainsKey: " + missionID);
        //    }
        //}

        public void SetLevelUITime(int missionID, int level)
        {
            if (!enterableMissions.ContainsKey(missionID))
            {
                LoggerHelper.Debug("!enterableMissions[missionID].ContainsKey: " + missionID);
                return;
            }

            foreach (var missionData in MissionData.dataMap)
            {
                if (missionData.Value.mission == missionID && missionData.Value.difficulty == level)
                {
                    SetLevelUIMessage(missionID, level, missionData.Value);
                    break;
                }
            }
        }

        // 刷新难度格子固定信息：次数，推荐等级
        //public void SetLevelUIMessage(int missionID, int level, MissionData missionData)
        //{
        //    int dayTimes = -1;
        //    int maxDayTimes = -1;
        //    int recommendLevel = -1;

        //    if (enterableMissions[missionID].Contains(level))
        //    {
        //        maxDayTimes = missionData.dayTimes;
        //        recommendLevel = missionData.recommend_level;

        //        if (missionTimes.ContainsKey(missionID))
        //        {
        //            if (missionTimes[missionID].ContainsKey(level))
        //            {
        //                dayTimes = missionTimes[missionID][level];

        //                SetLevelUIDateTime(level - 1, dayTimes, maxDayTimes);
        //                SetLevelUIRecommendLevel(level - 1, recommendLevel);
        //            }
        //            else
        //            {
        //                dayTimes = 0;

        //                SetLevelUIDateTime(level - 1, dayTimes, maxDayTimes);
        //                SetLevelUIRecommendLevel(level - 1, recommendLevel);
        //            }
        //        }
        //        else
        //        {
        //            dayTimes = 0;

        //            SetLevelUIDateTime(level - 1, dayTimes, maxDayTimes);
        //            SetLevelUIRecommendLevel(level - 1, recommendLevel);
        //        }
        //    }
        //    else
        //    {
        //        dayTimes = -1;
        //        maxDayTimes = -1;
        //        recommendLevel = -1;

        //        SetLevelUIDateTime(level - 1, dayTimes, maxDayTimes);
        //        SetLevelUIRecommendLevel(level - 1, recommendLevel);
        //    }
        //}

        public void SetLevelUIMessage(int missionID, int level, MissionData missionData)
        {
            int dayTimes = -1;
            int maxDayTimes = -1;
            if (enterableMissions[missionID].ContainsKey(level))
            {
                maxDayTimes = missionData.dayTimes;

                if (missionTimes.ContainsKey(missionID))
                {
                    if (missionTimes[missionID].ContainsKey(level))
                    {
                        dayTimes = missionTimes[missionID][level];
                        InstanceLevelChooseUIViewManager.Instance.SetEnterEnable(true);
                        InstanceLevelChooseUIViewManager.Instance.SetEnterTimes(MissionType.Normal, maxDayTimes - dayTimes, level - 1);
                    }
                    else
                    {
                        dayTimes = 0;
                        InstanceLevelChooseUIViewManager.Instance.SetEnterEnable(true);
                        InstanceLevelChooseUIViewManager.Instance.SetEnterTimes(MissionType.Normal, maxDayTimes - dayTimes, level - 1);
                    }
                }
                else
                {
                    dayTimes = 0;
                    InstanceLevelChooseUIViewManager.Instance.SetEnterEnable(true);
                    InstanceLevelChooseUIViewManager.Instance.SetEnterTimes(MissionType.Normal, maxDayTimes - dayTimes, level - 1);
                }
            }
            else
            {
                InstanceLevelChooseUIViewManager.Instance.SetEnterEnable(false);
                InstanceLevelChooseUIViewManager.Instance.SetEnterTimes(MissionType.Normal, 0, level - 1);
            }

            InstanceLevelChooseUIViewManager.Instance.SetCleanTimes(InstanceUILogicManager.Instance.SweepTimes);
        }

        //// 更新难度格子每日固定次数
        //public void SetLevelUIDateTime(int level, int dayTimes, int maxDayTimes)
        //{
        //    if (maxDayTimes > 0)
        //        InstanceUIViewManager.Instance.SetInstanceLevelDateTimes(level, dayTimes.ToString(), maxDayTimes.ToString());
        //    else if (maxDayTimes < 0)
        //        InstanceUIViewManager.Instance.SetInstanceLevelDateTimes(level, "??", "??");
        //}

        //// 更新难度格子推荐等级
        //public void SetLevelUIRecommendLevel(int level, int recommendLevel)
        //{
        //    if (recommendLevel > 0)
        //        InstanceUIViewManager.Instance.SetInstanceLevelRecommendLevel(level, recommendLevel.ToString());
        //    else if (recommendLevel < 0)
        //        InstanceUIViewManager.Instance.SetInstanceLevelRecommendLevel(level, "??");
        //}

        // 更新难度格子星星
        //public void SetLevelUIStar(int missionID)
        //{
        //    int defaultChoose = 1;

        //    if (missionStars.ContainsKey(missionID))
        //    {
        //        if (missionStars[missionID].ContainsKey(3))
        //        {
        //            InstanceUIViewManager.Instance.SetInstanceLevelStars(2, missionStars[missionID][3]);
        //            if (missionStars[missionID][3] < 3)
        //                defaultChoose = 3;
        //        }
        //        else
        //        {
        //            InstanceUIViewManager.Instance.SetInstanceLevelStars(2, 0);
        //            defaultChoose = 3;
        //        }

        //        if (missionStars[missionID].ContainsKey(2))
        //        {
        //            InstanceUIViewManager.Instance.SetInstanceLevelStars(1, missionStars[missionID][2]);
        //            if (missionStars[missionID][2] < 3)
        //                defaultChoose = 2;
        //        }
        //        else
        //        {
        //            InstanceUIViewManager.Instance.SetInstanceLevelStars(1, 0);
        //            defaultChoose = 2;
        //        }

        //        if (missionStars[missionID].ContainsKey(1))
        //        {
        //            InstanceUIViewManager.Instance.SetInstanceLevelStars(0, missionStars[missionID][1]);
        //            if (missionStars[missionID][1] < 3)
        //                defaultChoose = 1;
        //        }
        //        else
        //        {
        //            InstanceUIViewManager.Instance.SetInstanceLevelStars(0, 0);
        //            defaultChoose = 1;
        //        }

        //        InstanceUILogicManager.Instance.SetDefalutLevel(defaultChoose);
        //    }
        //    else
        //    {
        //        InstanceUIViewManager.Instance.SetInstanceLevelStars(0, 0);
        //        InstanceUIViewManager.Instance.SetInstanceLevelStars(1, 0);
        //        InstanceUIViewManager.Instance.SetInstanceLevelStars(2, 0);
        //    }
        //}


        public void SetLevelUIStar(int missionID, int level)
        {
            if (missionStars.ContainsKey(missionID))
            {
                if (missionStars[missionID].ContainsKey(level))
                {
                    InstanceLevelChooseUIViewManager.Instance.ShowStars(missionStars[missionID][level]);
                }
                else
                {
                    InstanceLevelChooseUIViewManager.Instance.ShowStars(0);
                }
            }
            else
            {
                InstanceLevelChooseUIViewManager.Instance.ShowStars(0);
            }
        }

        #endregion

        #endregion


        #region 重置副本

        // 重置副本
        protected int totalResetTimes;

        #region 副本总的剩余次数

        public void CheckTotalResetTimesReq()
        {
            GetMissionTotalResetTimes();
        }


        public void GetMissionTotalResetTimes()
        {
            if (PrivilegeData.dataMap.ContainsKey(theOwner.VipLevel))
                theOwner.RpcCall("MissionReq", (byte)MissionReq.GET_RESET_TIMES, (ushort)1, (ushort)1, "");
            else
                NewInstanceUIChooseLevelViewManager.Instance.SetResetNum(LanguageData.dataMap[26000].content + ": " + 0);
        }


        protected void OnCheckTotalResetTimesResp(int times)
        {
            totalResetTimes = times;

            if (InstanceMissionChooseUIViewManager.SHOW_MISSION_BY_DRAG)
            {
                InstanceMissionChooseUIViewManager.Instance.SetResetNum(LanguageData.GetContent(46972) + (PrivilegeData.dataMap[theOwner.VipLevel].dailyHardModResetLimit < times ? 0 : PrivilegeData.dataMap[theOwner.VipLevel].dailyHardModResetLimit - times));
            }

                // Old Code
            else
            {
                NewInstanceUIChooseLevelViewManager.Instance.SetResetNum(LanguageData.GetContent(46972) + (PrivilegeData.dataMap[theOwner.VipLevel].dailyHardModResetLimit < times ? 0 : PrivilegeData.dataMap[theOwner.VipLevel].dailyHardModResetLimit - times));
            }
        }

        #endregion

        #region 重置副本相关

        protected void ShowResetMissionWindow(int missionID, int level)
        {
            // Debug.LogError("ShowResetMissionWindow: " + missionID + " " + level);

            if (theOwner.VipLevel != 0
                && PrivilegeData.dataMap.ContainsKey(theOwner.VipLevel))
            {
                MissionCache = missionID;
                LevelCache = level;

                if (totalResetTimes < PrivilegeData.dataMap[theOwner.VipLevel].dailyHardModResetLimit)
                {
                    // CheckMissionResetTimesReq(ResetTimesMissionCache, ResetTimesLevelCache);
                    OnCheckMissionResetTimesResp(missionID, level, totalResetTimes);
                }
                else
                    InstanceUILogicManager.Instance.ShowResetMissionWindow(true, 1);
            }
            else
            {
                InstanceUILogicManager.Instance.ShowResetMissionWindow(true, 3);
            }
        }


        public void CheckMissionResetTimesReq(int missionID, int level)
        {
            theOwner.RpcCall("MissionReq", (byte)MissionReq.GET_RESET_TIMES_BY_MISSION, (ushort)missionID, (ushort)level, "");
        }


        protected void OnCheckMissionResetTimesResp(int missionID, int level, int resetTimes)
        {
            foreach (var missionResetCostData in PriceListData.dataMap[10].priceList)
            {
                if (missionResetCostData.Key == resetTimes + 1)
                {
                    if (theOwner.diamond < missionResetCostData.Value)
                        InstanceUILogicManager.Instance.ShowResetMissionWindow(true, 2, missionResetCostData.Value, PrivilegeData.dataMap[theOwner.VipLevel].dailyHardModResetLimit - totalResetTimes > 0 ? PrivilegeData.dataMap[theOwner.VipLevel].dailyHardModResetLimit - totalResetTimes : 0);
                    else
                        InstanceUILogicManager.Instance.ShowResetMissionWindow(true, 0, missionResetCostData.Value, PrivilegeData.dataMap[theOwner.VipLevel].dailyHardModResetLimit - totalResetTimes > 0 ? PrivilegeData.dataMap[theOwner.VipLevel].dailyHardModResetLimit - totalResetTimes : 0);
                    break;
                }
            }
        }


        // 请求重设次数
        public void ResetMissionTimesReq()
        {
            Mogo.Util.LoggerHelper.Debug("ResetMissionTimesReq: " + MissionCache + " " + LevelCache);

            theOwner.RpcCall("MissionReq", (byte)MissionReq.RESET_MISSION_TIMES, (ushort)MissionCache, (ushort)LevelCache, "");
        }


        public void OnResetMissionTimesResp(int errorCode)
        {
            switch (errorCode)
            {
                case 0:
                    MogoMsgBox.Instance.ShowFloatingTextQueue(LanguageData.GetContent(300004));
                    GetMissionTotalResetTimes();
                    UpdateMissionMessage(true, () =>
                    {
                        SetLevelUITime(MissionCache, LevelCache);
                    });
                    break;

                case -1:
                    MogoMsgBox.Instance.ShowFloatingTextQueue(LanguageData.GetContent(300005));
                    break;

                case -2:
                    MogoMsgBox.Instance.ShowFloatingTextQueue(LanguageData.GetContent(300006));
                    break;

                case -3:
                    MogoMsgBox.Instance.ShowFloatingTextQueue(LanguageData.GetContent(300007));
                    break;

                case -4:
                    MogoMsgBox.Instance.ShowFloatingTextQueue(LanguageData.GetContent(300008));
                    break;

                case -5:
                    MogoMsgBox.Instance.ShowFloatingTextQueue(LanguageData.GetContent(300009));
                    break;

                case -6:
                    MogoMsgBox.Instance.ShowFloatingTextQueue(LanguageData.GetContent(300010));
                    break;
            }
        }

        #endregion

        #endregion


        #region 机关相关

        #region 保存机关状态（主要功能已注释）

        // 上传所有机关
        public void UploadAllGear()
        {
            return;
            //为消除警告而注释以下代码
            ////Mogo.Util.LoggerHelper.Debug("UploadAllGear");

            //GearParent[] allGear = (GearParent[])GameObject.FindObjectsOfType(typeof(GearParent));

            //List<byte> gearStates = new List<byte>();

            //// byte[] gearStateBytes = new byte[allGear.Length * 2]; 
            //if (allGear.Length > 0)
            //{
            //    for (int i = 0; i < allGear.Length; i++)
            //    {
            //        GearParent gear = allGear[i];
            //        if (gear.ID != 0)
            //        {
            //            gearStates.Add(Convert.ToByte(gear.ID));

            //            if (gear.triggleEnable && gear.stateOne)
            //                gearStates.Add(Convert.ToByte(GearParent.EnableByte + GearParent.StateOneByte));
            //            else if (gear.triggleEnable && !gear.stateOne)
            //                gearStates.Add(Convert.ToByte(GearParent.EnableByte + GearParent.StateTwoByte));
            //            else if (!gear.triggleEnable && gear.stateOne)
            //                gearStates.Add(Convert.ToByte(GearParent.DisableByte + GearParent.StateOneByte));
            //            else
            //                gearStates.Add(Convert.ToByte(GearParent.DisableByte + GearParent.StateTwoByte));
            //        }
            //    }
            //}

            //if (gearStates.Count > 0)
            //{
            //    byte[] gearStateBytes = gearStates.ToArray();
            //    string result = Encoding.UTF8.GetString(gearStateBytes);

            //    //for (int i = 0; i < gearStateBytes.Length; i++)
            //    //    LoggerHelper.Debug("Send gearStateBytes: " + gearStates[i]);
            //    // gearString = result;
            //    theOwner.RpcCall("MissionReq", (byte)MissionReq.CLIENT_MISSION_INFO, (ushort)1, (ushort)1, result);
            //}
        }


        // 更新Mission的机关状态
        public void OnUpdateMissionGearInfo(string str)
        {
            Mogo.Util.LoggerHelper.Debug("OnUpdateMissionInfo" + str);

            // to do
            DownloadAllGear(str);
        }


        // 下载所有机关
        public void DownloadAllGear(string result)
        {
            return;
            //为消除警告而注释以下代码
            //if (result != "")
            //{
            //    byte[] gearStateBytes = Encoding.UTF8.GetBytes(result);

            //    //for (int i = 0; i < gearStateBytes.Length; i++)
            //    //    LoggerHelper.Debug("Receive gearStateBytes: " + gearStateBytes[i]);

            //    for (int i = 0; i < gearStateBytes.Length / 2; i++)
            //    {
            //        uint gearID = Convert.ToUInt32(gearStateBytes[i * 2]);
            //        byte gearState = gearStateBytes[i * 2 + 1];

            //        if ((gearState & GearParent.EnableByte) == GearParent.EnableByte)
            //        {
            //            LoggerHelper.Debug("Out SetGearEnable: " + gearID);
            //            EventDispatcher.TriggerEvent(Events.GearEvent.SetGearEnable, gearID);
            //        }
            //        else if ((gearState & GearParent.DisableByte) == GearParent.DisableByte)
            //        {
            //            LoggerHelper.Debug("Out SetGearDisable: " + gearID);
            //            EventDispatcher.TriggerEvent(Events.GearEvent.SetGearDisable, gearID);
            //        }

            //        if ((gearState & GearParent.StateOneByte) == GearParent.StateOneByte)
            //        {
            //            LoggerHelper.Debug("Out SetGearStateOne: " + gearID);
            //            EventDispatcher.TriggerEvent(Events.GearEvent.SetGearStateOne, gearID);
            //        }
            //        else if ((gearState & GearParent.StateTwoByte) == GearParent.StateTwoByte)
            //        {
            //            LoggerHelper.Debug("Out SetGearStateTwo: " + gearID);
            //            EventDispatcher.TriggerEvent(Events.GearEvent.SetGearStateTwo, gearID);
            //        }
            //    }
            //}
        }

        #endregion

        #region 服务器事件触发机关，改变机关状态

        public void OnNotifyToClientEventResp(int eventID)
        {
            #region 进度记录

            GameProcManager.NotifyToClientEvent(theOwner.CurMissionID, theOwner.CurMissionLevel, eventID);

            #endregion

            MotityGearState(eventID);
        }

        public void MotityGearState(int eventID)
        {
            ClientEventData.TriggerGearEvent(eventID);
        }

        #endregion

        #endregion


        #region 获取关卡剩余时间（目前没用）

        public void OnGetMissionLeftTimeResp(int liftTime)
        {
            LoggerHelper.Debug("OnGetMissionLeftTimeResp: liftTime: " + liftTime);
        }

        #endregion


        #region 触发SpawnPoint

        public void SpawnPointStartReq(int spawnPointID)
        {
            #region 进度记录

            GameProcManager.TriggerSpawnPoint(MogoWorld.thePlayer.CurMissionID, MogoWorld.thePlayer.CurMissionLevel, spawnPointID);

            #endregion

            theOwner.RpcCall("MissionReq", (byte)MissionReq.SPAWNPOINT_START, (byte)spawnPointID, (byte)1, "");
        }

        public void OnSpawnPointStartResp()
        {
            LoggerHelper.Debug("OnSpawnPointStartResp");
        }

        #endregion


        #region 结算相关

        // 翻牌奖励缓存
        protected List<Dictionary<int, int>> cards = new List<Dictionary<int, int>>();
        protected List<Dictionary<int, int>> itemPool = new List<Dictionary<int, int>>();

        #region 上传最大连击，单机副本将缓存这个数据

        public void UploadMaxCombo(int maxCombo)
        {
            theOwner.RpcCall("MissionReq", (byte)MissionReq.UPLOAD_COMBO, (ushort)maxCombo, (ushort)1, "");
        }

        #endregion

        #region 关卡胜利回调，非单机副本此时得到时间、评分、评级翻牌奖励

        public void OnNotifyToClientResultSuccessResp(LuaTable luaTable)
        {
            LoggerHelper.Debug("Handle Packet");

            if (theOwner.sceneId == 10100)
            {
                #region 进度记录

                GameProcManager.BattleWin(theOwner.CurMissionID, theOwner.CurMissionLevel);

                #endregion

                theOwner.IsNewPlayer = false;
                ForceExitMissionReq();
                return;
            }

            MogoUIManager.Instance.ShowMogoCommuntiyUI(CommunityUIParent.MainUI, false);

            #region 进度记录

            GameProcManager.BattleWin(theOwner.CurMissionID, theOwner.CurMissionLevel);

            #endregion

            #region 音效

            EventDispatcher.TriggerEvent(SettingEvent.ChangeMusic, 60, SoundManager.PlayMusicMode.Single);

            #endregion

            if (luaTable != null && luaTable.Count > 0)
            {
                cards.Clear();
                itemPool.Clear();

                int time = Convert.ToInt32(luaTable["1"]);

                // 能收到说明已经通关
                int starNum = Convert.ToInt32(luaTable["2"]);

                int scorePoint = Convert.ToInt32(luaTable["3"]);

                int minutes = time / 60;
                int second = time % 60;

                if (luaTable.ContainsKey("4"))
                {
                    List<Dictionary<int, int>> cardItems;
                    Utils.ParseLuaTable<List<Dictionary<int, int>>>((LuaTable)luaTable["4"], out cardItems);
                    if (cardItems != null)
                    {
                        int counter = 0;
                        foreach (var cardItem in cardItems)
                        {
                            if (counter < starNum - 1)
                            {
                                cards.Add(cardItem);
                                counter++;
                            }
                            itemPool.Add(cardItem);
                        }
                    }
                    BattlePassCardListUILogicManager.Instance.SetFlipCount(cards.Count);
                }


                //MogoUIManager.Instance.LoadMogoInstanceUI(() =>
                //{
                //    InstanceUIViewManager.Instance.ShowInstancePassUI(true);

                //    InstanceUIViewManager.Instance.SetPassTime(minutes, second);
                //    InstanceUIViewManager.Instance.SetPassMark(starNum > 3 ? 3 : starNum);
                //}, false);

                LoggerHelper.Debug("Handle End");

                MogoUIManager.Instance.LoadMogoInstanceUI(() =>
                {
                    // Debug.LogError("LoadMogoInstanceUI");
                    //InstanceUIViewManager.Instance.ShowInstancePassUI(true);
                    InstancePassRewardUIViewManager.Instance.ShowInstancePassRewardUI(true);
                    InstanceUILogicManager.Instance.SetNewPassMessage(minutes, second, MogoWorld.thePlayer.GetMaxCombo(), scorePoint, starNum > 4 ? 4 : starNum);
                    theOwner.ResetMaxCombo();
                }, false);
            }
            else
            {
                ClientMissionWon();
            }
        }

        #endregion

        #region 关卡失败

        public void OnNotifyToClientResultFailedResp()
        {
            MainUIViewManager.Instance.ShowBossTarget(false);
            ExitMissionReq();
        }

        #endregion

        #region 获取当前奖励，非单机副本获得服务器数据，单机副本收到这条通过别的协议获取整个结算数据

        public void GetCurrentRewardReq()
        {
            theOwner.RpcCall("MissionReq", (byte)MissionReq.GET_MISSION_REWARDS, (ushort)1, (ushort)1, "");
        }

        #endregion

        #region 非单机获取关卡物品回调

        public void OnGetMissionRewardResp(LuaTable luaTable)
        {
            Mogo.Util.LoggerHelper.Debug("OnGetMissionRewardResp; " + luaTable.ToString());

            int exp = Convert.ToInt32(luaTable["1"]);
            int money = Convert.ToInt32(luaTable["3"]);

            Dictionary<int, int> items = new Dictionary<int, int>();
            List<ItemParent> theItems = new List<ItemParent>();

            List<int> ids = new List<int>();
            List<int> counts = new List<int>();

            object obj;
            if (Utils.ParseLuaTable((LuaTable)luaTable["2"], typeof(Dictionary<int, int>), out obj))
            {
                items = obj as Dictionary<int, int>;
                foreach (KeyValuePair<int, int> item in items)
                {
                    var temp = ItemParentData.GetItem(item.Key);
                    if (temp != null)
                    {
                        ids.Add(temp.id);
                        counts.Add(item.Value);
                        LoggerHelper.Debug("mission reward item : ItemID = " + item.Key + ", ItemCount = " + item.Value);
                    }
                }

                if (money > 0)
                {
                    ids.Add(2);
                    counts.Add(money);
                }

                if (exp > 0)
                {
                    ids.Add(1);
                    counts.Add(exp);
                }

                // InstanceUIViewManager.Instance.SetInstanceLevelRewardItemImage(iconPaths, 1);
            }

            // InstanceUILogicManager.Instance.SetInstanceRewardUIReward(ids, counts, itemNames);
            // InstanceUILogicManager.Instance.UpdateFriendShip(theOwner.name);

            InstanceUILogicManager.Instance.SetNewInstanceRewardUIReward(ids, counts);
        }

        #endregion

        #region 单机副本结算

        public void ClientMissionWon()
        {
            MogoUIManager.Instance.LoadMogoInstanceUI(() =>
            {
                InstancePassRewardUIViewManager.Instance.ShowInstancePassRewardUI(true);
            }, false);
        }

        public void HandleClientMissionWonData(LuaTable luaTable)
        {
            Dictionary<int, int> items = new Dictionary<int, int>();
            int money = 0;
            int exp = 0;
            int time = 0;
            int starNum = 0;
            int scorePoint = 0;
            int minutes = 0;
            int second = 0;
            cards.Clear();
            itemPool.Clear();

            if (luaTable.ContainsKey("2"))
                money = Convert.ToInt32(luaTable["2"]);

            if (luaTable.ContainsKey("3"))
                exp = Convert.ToInt32(luaTable["3"]);

            if (luaTable.ContainsKey("4"))
            {
                time = Convert.ToInt32(luaTable["4"]);
                minutes = time / 60;
                second = time % 60;
            }

            if (luaTable.ContainsKey("5"))
                starNum = Convert.ToInt32(luaTable["5"]);

            if (luaTable.ContainsKey("6"))
                scorePoint = Convert.ToInt32(luaTable["6"]);

            InstanceUILogicManager.Instance.SetNewPassMessage(minutes, second, MogoWorld.thePlayer.GetMaxCombo(), scorePoint, starNum > 4 ? 4 : starNum);

            theOwner.ResetMaxCombo();

            if (luaTable.ContainsKey("1"))
            {
                if (Utils.ParseLuaTable<Dictionary<int, int>>((LuaTable)luaTable["1"], out items))
                {
                    if (items != null)
                    {
                        List<ItemParent> theItems = new List<ItemParent>();

                        List<int> ids = new List<int>();
                        List<int> counts = new List<int>();

                        foreach (KeyValuePair<int, int> item in items)
                        {
                            var temp = ItemParentData.GetItem(item.Key);
                            if (temp != null)
                            {
                                ids.Add(temp.id);
                                counts.Add(item.Value);
                                LoggerHelper.Debug("mission reward item : ItemID = " + item.Key + ", ItemCount = " + item.Value);
                            }
                        }

                        if (money > 0)
                        {
                            ids.Add(2);
                            counts.Add(money);
                        }

                        if (exp > 0)
                        {
                            ids.Add(1);
                            counts.Add(exp);
                        }
                        // InstanceUILogicManager.Instance.SetInstanceRewardUIReward(ids, counts, itemNames);
                        InstanceUILogicManager.Instance.SetNewInstanceRewardUIReward(ids, counts);
                    }
                }
            }

            if (luaTable.ContainsKey("7"))
            {
                List<Dictionary<int, int>> cardItems;
                Utils.ParseLuaTable<List<Dictionary<int, int>>>((LuaTable)luaTable["7"], out cardItems);
                if (cardItems != null)
                {
                    int counter = 0;
                    foreach (var cardItem in cardItems)
                    {
                        if (counter < starNum - 1)
                        {
                            cards.Add(cardItem);
                            counter++;
                        }
                        itemPool.Add(cardItem);
                    }
                }

                BattlePassCardListUILogicManager.Instance.SetFlipCount(cards.Count);
            }
        }

        #endregion

        #region 翻牌

        public void FlipCard(int i)
        {
            if (cards == null)
                return;

            if (cards.Count == 0)
                return;

            Dictionary<int, int> cardItem = GetCardItem();
            foreach (var part in cardItem)
            {
                BattlePassCardListUIViewManager.Instance.SetRewardItem(i, part.Key, part.Value);
            }

            BattlePassCardListUIViewManager.Instance.PlayCardAnim(i, true);

            if (cards.Count == 0)
            {
                BattlePassCardListUIViewManager.Instance.ShowOKBtn(true);
                BattlePassCardListUIViewManager.Instance.StopCountDown();
            }
        }

        public Dictionary<int, int> GetCardItem()
        {
            //int index = RandomHelper.GetRandomInt(0, cards.Count);
            int index = 0;
            Dictionary<int, int> result = cards[index];
            cards.RemoveAt(index);
            itemPool.RemoveAt(index);
            return result;
        }

        public void SetCardCanNotGetItem()
        {
            //MissionRandomRewardData data = MissionRandomRewardData.dataMap.FirstOrDefault(t => t.Value.mission == theOwner.ApplyMissionID && t.Value.difficulty == theOwner.ApplyMissionLevel).Value;

            //List<KeyValuePair<int, int>> result = new List<KeyValuePair<int, int>>();

            //DropData dropData = DropData.dataMap.Get(data.dropId);
            //Dictionary<int, int> item5 = new Dictionary<int, int>();
            //switch (theOwner.vocation)
            //{
            //    case Vocation.Warrior:
            //        item5 = dropData.dropGroup0;
            //        break;

            //    case Vocation.Assassin:
            //        item5 = dropData.dropGroup1;
            //        break;

            //    case Vocation.Archer:
            //        item5 = dropData.dropGroup2;
            //        break;

            //    case Vocation.Mage:
            //        item5 = dropData.dropGroup3;
            //        break;
            //}

            //foreach (var item in data.item1)
            //    result.Add(item);
            //foreach (var item in data.item2)
            //    result.Add(item);
            //foreach (var item in data.item3)
            //    result.Add(item);
            //foreach (var item in data.item4)
            //    result.Add(item);
            //foreach (var item in item5)
            //    result.Add(item);

            //foreach (var itemList in itemPool)
            //    foreach (var item in itemList)
            //        if (result.Contains(item))
            //            result.Remove(item);

            //itemPool.Clear();
            //BattlePassCardListUIViewManager.Instance.SetCardCanNotGetItem(result);


            List<KeyValuePair<int, int>> result = new List<KeyValuePair<int, int>>();

            foreach (var itemList in itemPool)
                foreach (var item in itemList)
                    result.Add(item);

            BattlePassCardListUIViewManager.Instance.SetCardCanNotGetItem(result);
        }

        public void AutoFlipCard(int i)
        {
            if (cards == null)
                return;

            if (cards.Count == 0)
                return;

            Dictionary<int, int> cardItem = GetCardItem();
            foreach (var part in cardItem)
            {
                BattlePassCardListUIViewManager.Instance.SetRewardItem(i, part.Key, part.Value);
            }

            BattlePassCardListUIViewManager.Instance.PlayCardAnim(i, true);
        }

        public void AutoSetCardCanNotGetItem()
        {
            List<KeyValuePair<int, int>> result = new List<KeyValuePair<int, int>>();

            foreach (var itemList in itemPool)
                foreach (var item in itemList)
                    result.Add(item);

            BattlePassCardListUIViewManager.Instance.SetCardCanNotGetItem(result);
            BattlePassCardListUIViewManager.Instance.ShowOKBtn(true);
        }

        #endregion

        #endregion


        #region 复活相关

        #region 复活次数

        public void GetRebornTimesReq()
        {
            theOwner.RpcCall("MissionReq", (byte)MissionReq.GET_REVIVE_TIMES, (ushort)1, (ushort)1, "");
        }

        public void OnGetRebornTimesResp(int times)
        {
            if (MapData.dataMap.Get(theOwner.sceneId).type == MapType.TOWERDEFENCE)
            {
                if (MFUIManager.CurrentUI != MFUIManager.MFUIID.BattlePassUINoCard)
                    theOwner.campaignSystem.ShowTowerDefenceRevive(times);
                return;
            }

            if (HasWin)
                return;

            if (times > 0)
            {
                MogoUIManager.Instance.ShowMogoInstanceRebornUI(true, () =>
                {
                    InstanceUIRebornNoneDropViewManager.Instance.ChangeRebornUIState(InstanceUIRebornNoneDropViewManager.RebornUIState.TimesZero);
                });
            }
            else
            {
                MogoUIManager.Instance.ShowMogoInstanceRebornUI(true, () =>
                {
                    if (InventoryManager.Instance.GetItemNumById(1100070) > 0)
                    {
                        InstanceUIRebornNoneDropViewManager.Instance.ChangeRebornUIState(InstanceUIRebornNoneDropViewManager.RebornUIState.Death);
                        return;
                    }
                    InstanceUIRebornNoneDropViewManager.Instance.ChangeRebornUIState(InstanceUIRebornNoneDropViewManager.RebornUIState.StoneNoEnough);
                });
            }
        }

        #endregion

        #region 复活界面上奖励池（已注释）

        //public void OnAvatarDeadGetMissionRewardResp(LuaTable luaTable)
        //{
        //    Mogo.Util.LoggerHelper.Debug("OnAvatarDeadGetMissionRewardResp; " + firstShow);
        //    firstShow++;

        //    #region 物品和奖励，已注释
        //    //int exp = Convert.ToInt32(luaTable["1"]);
        //    //int money = Convert.ToInt32(luaTable["3"]);

        //    //Dictionary<int, int> items = new Dictionary<int, int>();
        //    //List<ItemParent> theItems = new List<ItemParent>();

        //    //List<int> iconIDs = new List<int>();
        //    //List<string> iconPaths = new List<string>();
        //    //List<string> itemNames = new List<string>();

        //    //object obj;
        //    //if (Utils.ParseLuaTable((LuaTable)luaTable["2"], typeof(Dictionary<int, int>), out obj))
        //    //{
        //    //    items = obj as Dictionary<int, int>;
        //    //    foreach (KeyValuePair<int, int> item in items)
        //    //    {
        //    //        var temp = ItemParentData.GetItem(item.Key);
        //    //        iconIDs.Add(item.Key);
        //    //        iconPaths.Add(temp.Icon);
        //    //        itemNames.Add(temp.Name);
        //    //    }
        //    //}

        //    //// Money And Exp
        //    //iconIDs.Add(2);
        //    //iconPaths.Add(IconData.dataMap.Get(44).path);
        //    //itemNames.Add(LanguageData.GetContent(20002) + money);

        //    //iconIDs.Add(3);
        //    //iconPaths.Add(IconData.dataMap.Get(45).path);
        //    //itemNames.Add(LanguageData.GetContent(20003) + exp);
        //    #endregion

        //    MogoUIManager.Instance.ShowMogoInstanceRebornUI(true, () => 
        //    {
        //        if (InventoryManager.Instance.GetItemNumById(1100070) > 0)
        //        {
        //            InstanceUIRebornNoneDropViewManager.Instance.ChangeRebornUIState(InstanceUIRebornNoneDropViewManager.RebornUIState.StoneNoEnough);
        //            return;
        //        }
        //        InstanceUIRebornNoneDropViewManager.Instance.ChangeRebornUIState(InstanceUIRebornNoneDropViewManager.RebornUIState.Death);
        //    });
        //}

        #endregion

        #region 玩家请求不复活

        public void AvatarNotRebornReq()
        {
            MogoWorld.ClearEntities();
            InstanceUILogicManager.Instance.ResetMercenaryID();
            theOwner.RpcCall("MissionReq", (byte)MissionReq.EXIT_MISSION, (ushort)1, (ushort)1, "");
        }

        public void OnAvatarNotRebornResp()
        {
            Mogo.Util.LoggerHelper.Debug("OnAvatarNotRebornResp");

            //MainUIViewManager.Instance.ShowBossTarget(false);
            //ExitMissionReq();
        }

        #endregion

        #region 玩家请求复活

        public void AvatarRebornReq()
        {
            Mogo.Util.LoggerHelper.Debug("AvatarRebornReq");

            theOwner.RpcCall("MissionReq", (byte)MissionReq.REVIVE, (ushort)1, (ushort)1, "");
            // theOwner.RpcCall("MissionReq", (ushort)MSG_REBORN, (byte)1, (byte)1);
        }

        public void OnAvatarRebornResp(LuaTable luatable)
        {
            // Debug.LogError("luatable + " + luatable.ToString());
            int errorCode = Convert.ToInt32(luatable["1"]);

            switch (errorCode)
            {
                case 0:
                    if (MapData.dataMap.Get(theOwner.sceneId).type == MapType.TOWERDEFENCE)
                    {
                        theOwner.campaignSystem.ReviveSuccess();
                        break;
                    }
                    MogoUIManager.Instance.ShowMogoInstanceRebornUI(false);
                    MogoUIManager.Instance.ShowMogoBattleMainUI();
                    MainUIViewManager.Instance.ResetUIData();
                    break;

                case -1:
                    MogoMsgBox.Instance.ShowFloatingText(LanguageData.GetContent(300201));
                    break;

                case -2:
                    MogoMsgBox.Instance.ShowFloatingText(LanguageData.GetContent(300202));
                    break;

                case -3:
                    MogoMsgBox.Instance.ShowFloatingText(LanguageData.GetContent(300203));
                    break;

                case -4:
                    MogoMsgBox.Instance.ShowFloatingText(LanguageData.GetContent(300010));    //钻石不足
                    break;
            }
        }

        #endregion

        #endregion


        #region 佣兵相关

        #region 佣兵按钮

        protected void UpdateMercenaryButton(int missionID, int level)
        {
            if (missionID == 10101 || missionID == 10102
                || (theOwner.CurrentTask != null && theOwner.CurrentTask.conditionType == 1 && theOwner.CurrentTask.condition[0] == missionID)
                || !finishedMissions.ContainsKey(missionID))
            {
                InstanceLevelChooseUIViewManager.Instance.ShowBtnHelp(false);
                OnSetMercenaryInfo(new LuaTable());
                return;
            }

            if (finishedMissions.ContainsKey(missionID))
            {
                if (!finishedMissions[missionID].ContainsKey(level))
                {
                    InstanceLevelChooseUIViewManager.Instance.ShowBtnHelp(false);
                    OnSetMercenaryInfo(new LuaTable());
                    return;
                }

                if (finishedMissions[missionID][level] == 1)
                {
                    InstanceLevelChooseUIViewManager.Instance.ShowBtnHelp(false);
                    OnSetMercenaryInfo(new LuaTable());
                    return;
                }
            }

            InstanceLevelChooseUIViewManager.Instance.ShowBtnHelp(true);
            theOwner.RpcCall("MercenaryInfoReq");
        }

        #endregion

        #region 获取雇佣兵信息并设置

        public void GetMercenaryInfo(int missionID)
        {
            if (missionID == 10101 || missionID == 10102
                || (theOwner.CurrentTask.conditionType == 1 && theOwner.CurrentTask.condition[0] == missionID)
                || !finishedMissions.ContainsKey(missionID))
            {
                OnSetMercenaryInfo(new LuaTable());
                return;
            }
            theOwner.RpcCall("MercenaryInfoReq");
        }


        public void OnSetMercenaryInfo(LuaTable luaTable)
        {
            Dictionary<int, MercenaryInfo> mercenaryInfo;
            if (Utils.ParseLuaTable<Dictionary<int, MercenaryInfo>>(luaTable, out mercenaryInfo))
            {
                InstanceUILogicManager.Instance.SetMercenaryGrid(mercenaryInfo);
            }
            else
            {
                LoggerHelper.Debug("OnSetMercenaryInfo Failed");
                InstanceUILogicManager.Instance.SetMercenaryGrid(null);
            }
        }

        #endregion

        #endregion


        #region 增加好友度

        public void AddFriendDegree()
        {
            theOwner.RpcCall("MissionReq", (byte)MissionReq.ADD_FRIEND_DEGREE, (ushort)1, (ushort)1, "");
        }

        #endregion


        #region 任务打开关卡

        public void MissionOpen(int theMission, int theLevel)
        {
            #region 进度记录

            GameProcManager.OpenInstanceUI(theMission, theLevel);

            #endregion

            MogoUIManager.Instance.ShowMogoInstanceUI(InstanceUILogicManager.Instance.MissionOpen, theMission, theLevel);
        }

        #endregion


        #region 副本扫荡

        public void SweepMission(int instance_id, int level)
        {
            MissionCache = instance_id;
            LevelCache = level;

            theOwner.RpcCall("MissionReq", (byte)MissionReq.MSG_SWEEP_MISSION, instance_id, level, "");
        }

        /// <summary>
        ///  0、成功
        ///  2、vip等级不足
        ///  3、每日扫荡次数超过上限
        ///  4、体力值不足
        ///  5、之前没打过该副本，不能扫荡
        /// </summary>
        /// <param name="flag"></param>
        void OnSweepMissionResp(int flag)
        {
            switch (flag)
            {
                case 0:
                    {
                        EventDispatcher.TriggerEvent(Events.InstanceEvent.GetSweepTimes);
                        UpdateMissionMessage(true);
                    } break;
                case 1:
                    {

                    } break;
                case 2:
                    {
                        MogoMsgBox.Instance.ShowFloatingText(LanguageData.GetContent(48502)); // vip等级不足
                    } break;
                case 3:
                    {
                        MogoMsgBox.Instance.ShowFloatingText(LanguageData.GetContent(48503)); // 每日扫荡次数超过上限
                    } break;
                case 4:
                    {
                        MogoMsgBox.Instance.ShowFloatingText(LanguageData.GetContent(48504)); // 体力值不足
                    } break;
                case 5:
                    {
                        MogoMsgBox.Instance.ShowFloatingText(LanguageData.GetContent(48505)); // 之前没打过该副本，不能扫荡
                    } break;
                default:
                    break;
            }
        }

        public void GetSweepMissionList(int instance_id, int level)
        {
            theOwner.RpcCall("MissionReq", (byte)MissionReq.GET_MISSION_SWEEP_LIST, instance_id, level, "");
        }

        void OnGetSweepMissionResp(LuaTable luaTable)
        {
            TimerHeap.AddTimer(1000, 0, () =>
            {
                SweepMissionRepostData sweepMissionRepostData = new SweepMissionRepostData();

                // 怪物信息   
                object enemyObj;
                sweepMissionRepostData.Enemys = new Dictionary<int, int>();
                if (Utils.ParseLuaTable((LuaTable)luaTable["1"], typeof(Dictionary<int, int>), out enemyObj))
                {
                    sweepMissionRepostData.Enemys = enemyObj as Dictionary<int, int>;
                    foreach (KeyValuePair<int, int> pair in sweepMissionRepostData.Enemys)
                    {
                        LoggerHelper.Debug("monsterId = " + pair.Key + " , " + "monsterCount = " + pair.Value);
                    }
                }

                // 物品信息
                object itemObj;
                sweepMissionRepostData.Items = new Dictionary<int, int>();
                if (Utils.ParseLuaTable((LuaTable)luaTable["2"], typeof(Dictionary<int, int>), out itemObj))
                {
                    sweepMissionRepostData.Items = itemObj as Dictionary<int, int>;
                    foreach (KeyValuePair<int, int> pair in sweepMissionRepostData.Items)
                    {
                        LoggerHelper.Debug("ItemId = " + pair.Key + " , " + "ItemCount = " + pair.Value);
                    }
                }

                if (luaTable.ContainsKey("3"))
                    sweepMissionRepostData.gold = Convert.ToInt32(luaTable["3"]);
                if (luaTable.ContainsKey("4"))
                    sweepMissionRepostData.exp = Convert.ToInt32(luaTable["4"]);

                InstanceUILogicManager.Instance.OpenMonsterReport(sweepMissionRepostData);
            });
        }

        public void GetSweepTimes()
        {
            theOwner.RpcCall("MissionReq", (byte)MissionReq.MSG_GET_SWEEP_TIMES, (byte)1, (byte)1, "");
        }

        public void OnGetSweepTimesResp(int times)
        {
            InstanceUILogicManager.Instance.SweepTimes = times < 0 ? 0 : times;
        }
        #endregion


        #region 路线显示相关

        public void EmptyNewMissions(int id)
        {
            // 现在直接调用播的接口之后就清空了
            // newEnterableMissions.Clear();
        }

        #endregion


        #region 获取关卡掉落

        Dictionary<int, List<List<int>>> missionDropsCache = new Dictionary<int, List<List<int>>>();
        protected int getDropMission = 0;

        public void GetDrops(int missionID)
        {
            getDropMission = missionID;

            if (missionDropsCache.ContainsKey(missionID))
            {
                InstanceUILogicManager.Instance.SetDropsGridData(missionDropsCache[missionID]);
            }
            else
            {
                theOwner.RpcCall("MissionReq", (byte)70, (ushort)missionID, (ushort)1, "");
            }
        }

        public void OnGetDrops(LuaTable luaTable)
        {
            List<List<int>> dataID = new List<List<int>>();
            List<List<int>> result;

            if (Utils.ParseLuaTable(luaTable, out result))
            {
                foreach (var resultItem in result)
                {
                    List<int> sortData = InventoryManager.Instance.SortDrops(resultItem);
                    dataID.Add(sortData);
                }
            }

            if (!missionDropsCache.ContainsKey(getDropMission))
                missionDropsCache.Add(getDropMission, dataID);

            InstanceUILogicManager.Instance.SetDropsGridData(dataID);
        }

        #endregion


        #region 关卡新宝箱

        Dictionary<int, int> chestData = new Dictionary<int, int>();

        public void GetChestRewardGotMessage()
        {
            Mogo.Util.LoggerHelper.Debug("GetChestRewardGotMessage");
            theOwner.RpcCall("MissionReq", (byte)MissionReq.GET_MISSION_TRESURE_REWARDS, (ushort)1, (ushort)1, "");
        }

        public void CheckChestState(Dictionary<int, int> hasGotReward)
        {
            InstanceTreasureChestUIViewManager.Instance.SetTitle(LanguageData.dataMap[25500].content);

            if (hasGotReward == null)
                return;

            if (!MapUIMappingData.dataMap.ContainsKey(InstanceUILogicManager.Instance.MapID))
                return;

            List<int> chest = MapUIMappingData.dataMap[InstanceUILogicManager.Instance.MapID].chest;

            if (chest == null)
                return;

            if (chest.Count == 0)
                return;

            bool allGot = true;
            foreach (var rewardID in chest)
                if (!hasGotReward.ContainsValue(rewardID))
                    allGot = false;

            bool isAnyCanGet = false;

            if (allGot)
            {
                // 设置全部领取完毕
                InstanceTreasureChestUIViewManager.Instance.SetDesc(LanguageData.dataMap[25504].content);
                InstanceTreasureChestUIViewManager.Instance.SetInstanceLevelRewardItemImage(new List<string>(), new List<string>());
                InstanceTreasureChestUIViewManager.Instance.ChangeTab((int)TreasureChestUITab.ShowItemTab);
            }

            foreach (var rewardID in chest)
            {
                if (!MissionRewardData.fixCondition.ContainsKey(rewardID))
                    continue;

                if (hasGotReward.ContainsValue(rewardID))
                    continue;

                bool canGet = true;
                List<List<int>> result = new List<List<int>>();

                foreach (var condition in MissionRewardData.fixCondition[rewardID])
                {
                    int missionID = condition[0];
                    int missionLevel = condition[1];
                    int missionGrade = condition[2];

                    // 统计所有的不符合情况
                    if (!finishedMissions.ContainsKey(missionID))
                    {
                        canGet = false;
                        List<int> temp = new List<int>();
                        temp.Add(missionID);
                        result.Add(temp);
                        continue;
                        //break;
                    }
                    if (!finishedMissions[missionID].ContainsKey(missionLevel))
                    {
                        canGet = false;
                        List<int> temp = new List<int>();
                        temp.Add(missionID);
                        temp.Add(missionLevel);
                        result.Add(temp);
                        continue;
                        //break;
                    }
                    if (finishedMissions[missionID][missionLevel] < missionGrade)
                    {
                        canGet = false;
                        List<int> temp = new List<int>();
                        temp.Add(missionID);
                        temp.Add(missionLevel);
                        temp.Add(missionGrade);
                        result.Add(temp);
                        continue;
                        //break;
                    }
                }

                if (canGet)
                {
                    // 设置可领取
                    InstanceTreasureChestUIViewManager.Instance.SetDesc(LanguageData.dataMap[25503].content);
                    SetChestImage(MissionRewardData.dataMap[rewardID].rewards);
                    InstanceTreasureChestUIViewManager.Instance.CurrentID = rewardID;
                    InstanceTreasureChestUIViewManager.Instance.ChangeTab((int)TreasureChestUITab.GetItemTab);

                    isAnyCanGet = true;
                    InstanceMissionChooseUIViewManager.Instance.ShowMapChestRotationAnimation(isAnyCanGet);

                    return;
                }
                else
                {
                    foreach (var resultDetail in result)
                    {
                        switch (resultDetail.Count)
                        {
                            case 1:
                                break;
                            case 2:
                                break;
                            case 3:
                                break;
                        }
                    }

                    switch (rewardID % 3)
                    {
                        case 1:
                            InstanceTreasureChestUIViewManager.Instance.SetDesc(LanguageData.dataMap[25501].content);
                            break;
                        case 2:
                            InstanceTreasureChestUIViewManager.Instance.SetDesc(LanguageData.dataMap[25502].content);
                            break;
                    }

                    SetChestImage(MissionRewardData.dataMap[rewardID].rewards);
                    InstanceTreasureChestUIViewManager.Instance.ChangeTab((int)TreasureChestUITab.ShowItemTab);

                    InstanceMissionChooseUIViewManager.Instance.ShowMapChestRotationAnimation(isAnyCanGet);

                    return;
                }
            }

            InstanceMissionChooseUIViewManager.Instance.ShowMapChestRotationAnimation(isAnyCanGet);
        }

        protected void SetChestImage(Dictionary<int, int> rewards)
        {
            List<string> imageNameList = new List<string>();
            List<string> itemNameList = new List<string>();

            List<int> itemIDList = new List<int>();
            List<int> itemCountList = new List<int>();

            if (rewards != null)
            {
                foreach (var reward in rewards)
                {
                    switch (reward.Key)
                    {
                        case 1:
                            imageNameList.Add(IconData.dataMap.Get(43).path);
                            itemNameList.Add(LanguageData.GetContent(20003) + reward.Value);
                            itemIDList.Add(-1);
                            itemCountList.Add(1);
                            break;

                        case 2:
                            imageNameList.Add(IconData.dataMap.Get(44).path);
                            itemNameList.Add(LanguageData.GetContent(20002) + reward.Value);
                            itemIDList.Add(-1);
                            itemCountList.Add(1);
                            break;

                        case 3:
                            imageNameList.Add(IconData.dataMap.Get(45).path);
                            itemNameList.Add(LanguageData.GetContent(20004) + reward.Value);
                            itemIDList.Add(-1);
                            itemCountList.Add(1);
                            break;

                        case 11:
                            imageNameList.Add(IconData.dataMap.Get(53).path);
                            itemNameList.Add(LanguageData.GetContent(272) + reward.Value);
                            itemIDList.Add(-1);
                            itemCountList.Add(1);
                            break;

                        case 12:
                            imageNameList.Add(IconData.dataMap.Get(54).path);
                            itemNameList.Add(LanguageData.GetContent(273) + reward.Value);
                            itemIDList.Add(-1);
                            itemCountList.Add(1);
                            break;

                        default:
                            imageNameList.Add(ItemParentData.GetItem(reward.Key).Icon);
                            itemNameList.Add(ItemParentData.GetItem(reward.Key).Name);
                            itemIDList.Add(reward.Key);
                            itemCountList.Add(reward.Value);
                            break;
                    }
                }
            }

            InstanceTreasureChestUIViewManager.Instance.SetRewardItemID(itemIDList);
            InstanceTreasureChestUIViewManager.Instance.SetInstanceLevelRewardItemCount(itemCountList);
            InstanceTreasureChestUIViewManager.Instance.SetInstanceLevelRewardItemImage(imageNameList, itemNameList);
        }

        public void GetChestRewardReq(int id)
        {
            theOwner.RpcCall("MissionReq", (byte)MissionReq.GET_MISSION_TRESURE, (ushort)id, (ushort)1, "");
        }

        public void OnGetChestRewardResp(int errorCode)
        {
            switch (errorCode)
            {
                case 0:
                    GetChestRewardGotMessage();
                    break;

                case 1:
                    MogoMsgBox.Instance.ShowFloatingText(LanguageData.dataMap[25552].content);
                    break;

                case 2:
                    MogoMsgBox.Instance.ShowFloatingText(LanguageData.dataMap[25553].content);
                    break;

                case 3:
                    MogoMsgBox.Instance.ShowFloatingText(LanguageData.dataMap[25554].content);
                    break;
            }

            MogoUIManager.Instance.LoadMogoInstanceTreasureChestUI(false);

            if (InstanceMissionChooseUIViewManager.SHOW_MISSION_BY_DRAG)
            {
                MogoUIManager.Instance.ShowInstanceMissionChooseUI(true);
            }
            else
            {
                MogoUIManager.Instance.ShowNewInstanceChooseMissionUI(true);
            }
        }

        public void UpdateChestMessage()
        {
            CheckChestState(chestData);
        }

        #endregion


        #region Boss宝箱

        Dictionary<int, int> bossChestData = new Dictionary<int, int>();

        public void GetBossChestRewardGotMessage()
        {
            Mogo.Util.LoggerHelper.Debug("GetBossChestRewardGotMessage");
            theOwner.RpcCall("MissionReq", (byte)MissionReq.GET_ACQUIRED_MISSION_BOSS_TREASURE, (ushort)1, (ushort)1, "");
            // SetBossChestState(new Dictionary<int, int>());
        }

        public void SetBossChestState(Dictionary<int, int> hasGotReward)
        {
            if (!MapUIMappingData.dataMap.ContainsKey(InstanceUILogicManager.Instance.MapID))
                return;

            //List<int> bossChest = MapUIMappingData.dataMap[InstanceUILogicManager.Instance.MapID].bossChest;

            //if (bossChest == null)
            //    return;

            //if (bossChest.Count == 0)
            //    return;

            List<BossTreasureGridData> listBossTreasureGridData = new List<BossTreasureGridData>();

            //foreach (var bossChestID in bossChest)
            //{
            //    if (!BossChestData.dataMap.ContainsKey(bossChestID))
            //        continue;

            //    BossChestData bossChestData = BossChestData.dataMap[bossChestID];

            bool isAnyChestCanGet = false;

            foreach (var bossChestDataSource in BossChestData.dataMap)
            {
                int bossChestID = bossChestDataSource.Value.id;
                BossChestData bossChestData = bossChestDataSource.Value;

                BossTreasureGridData gridData = new BossTreasureGridData();
                gridData.id = bossChestData.id;
                gridData.iconName = IconData.dataMap.Get(bossChestData.icon).path;
                gridData.bossName = LanguageData.GetContent(bossChestData.name);
                gridData.level = MissionData.dataMap.First(t => t.Value.mission == bossChestData.mission && t.Value.difficulty == bossChestData.difficulty).Value.level;

                //gridData.reward = new List<string>();
                //foreach (var item in bossChestData.reward)
                //    gridData.reward.Add(string.Concat(ItemParentData.GetItem(item.Key).Name, " * ", item.Value));

                if (hasGotReward.ContainsValue(bossChestID))
                {
                    // 已领取
                    gridData.status = BossTreasureStatus.HasGotReward;
                }
                else if (CheckCurrentMissionHardByGrade(bossChestData.mission, 4))
                {
                    // 未领取，获得S
                    gridData.status = BossTreasureStatus.CanGetReward;
                    isAnyChestCanGet = true;
                }
                else if (enterableMissions.ContainsKey(bossChestData.mission))
                {
                    // 未领取，未获得S，可进入（解锁）
                    gridData.status = BossTreasureStatus.NoHasStarS;
                }
                else
                {
                    // 未解锁
                    gridData.status = BossTreasureStatus.LevelNoEnough;
                }

                listBossTreasureGridData.Add(gridData);
            }

            InstanceMissionChooseUIViewManager.Instance.ShowBossChestRotationAnimation(isAnyChestCanGet);
            InstanceBossTreasureUILogicManager.Instance.SetBossTreasureList(listBossTreasureGridData);
        }

        public void GetBossChestRewardReq(int index)
        {
            theOwner.RpcCall("MissionReq", (byte)MissionReq.GET_MISSION_BOSS_TREASURE, (ushort)index, (ushort)1, "");
        }

        public void OnGetBossChestRewardResp(int errorCode)
        {
            switch (errorCode)
            {
                case 0:
                    GetBossChestRewardGotMessage();
                    break;
                case -1:
                    MogoMsgBox.Instance.ShowFloatingTextQueue(LanguageData.GetContent(26400));
                    break;
                case -2:
                    MogoMsgBox.Instance.ShowFloatingTextQueue(LanguageData.GetContent(26401));
                    break;
            }
        }

        public void UpdateBossChestMessage()
        {
            SetBossChestState(bossChestData);
        }

        #endregion


        #region 容器破碎

        public void ContainerBroken(int type, Vector3 position)
        {
            int containerID = -1;
            MissionData missionData = MissionData.dataMap.First(t => t.Value.mission == theOwner.CurMissionID && t.Value.difficulty == theOwner.CurMissionLevel).Value;

            if (missionData == null)
                return;

            if (missionData.drop == null)
                return;

            if (missionData.drop.Count == 0)
                return;

            foreach (var dropData in missionData.drop)
            {
                var monsterData = MonsterData.dataMap.Get(dropData.Key);
                if (monsterData != null && monsterData.monsterType == type)
                {
                    containerID = dropData.Key;
                    break;
                }
            }

            if (containerID == -1)
                return;

            string positionString = ((int)(position.x * 100)) + "_" + ((int)(position.z * 100));

            theOwner.RpcCall("MissionReq", (byte)MissionReq.CREATE_CLIENT_DROP, (ushort)containerID, (ushort)1, positionString);
        }

        #endregion


        #region 停止托管

        public void StopAutoFight()
        {
            MogoWorld.thePlayer.AutoFight = AutoFightState.IDLE;
        }

        #endregion


        #region 检测副本是否已经通过

        public bool CheckCurrentMissionEnterable(int mission, int level)
        {
            if (enterableMissions.ContainsKey(mission)
                && enterableMissions[mission].ContainsKey(level))
                return true;
            return false;
        }

        public bool CheckCurrentMissionEasyComplete(int mission)
        {
            if (finishedMissions.ContainsKey(mission)
                && finishedMissions[mission].ContainsKey(1)
                && finishedMissions[mission][1] > 1)
                return true;
            return false;
        }

        public bool CheckCurrentMissionHardComplete(int mission)
        {
            if (finishedMissions.ContainsKey(mission)
                && finishedMissions[mission].ContainsKey(2)
                && finishedMissions[mission][2] > 1)
                return true;
            return false;
        }

        public bool CheckCurrentMissionHardByGrade(int mission, int grade)
        {
            if (finishedMissions.ContainsKey(mission)
               && finishedMissions[mission].ContainsKey(2)
               && finishedMissions[mission][2] == grade)
                return true;
            return false;
        }

        public bool CheckCurrentMissionComplete(int mission, int level)
        {
            if ((level == 1 && CheckCurrentMissionEasyComplete(mission)) || (level == 2 && CheckCurrentMissionHardComplete(mission)))
                return true;
            return false;
            // return true;
        }

        #endregion


        #region 获取能进的最大副本

        /// <summary>
        /// 获取能进的最大副本
        /// </summary>
        /// <returns></returns>
        public KeyValuePair<int, int> GetLastMissionCanEnter()
        {
            int resultID = 0;
            int resultLevel = 0;

            foreach (var data in MapUIMappingData.dataMap)
            {
                foreach (var gridData in data.Value.grid)
                {
                    int tempID = gridData.Value;
                    if (!enterableMissions.ContainsKey(tempID))
                        continue;

                    if (tempID < resultID)
                        continue;

                    int tempLevel = 0;
                    if (enterableMissions[tempID].ContainsKey(2))
                        tempLevel = 2;
                    else
                        tempLevel = 1;

                    MissionData missionData = MissionData.dataMap.First(t => t.Value.mission == tempID && t.Value.difficulty == tempLevel).Value;

                    if (missionData.level > theOwner.level)
                        continue;

                    resultID = tempID;
                    resultLevel = tempLevel;
                }
            }

            return new KeyValuePair<int, int>(resultID, resultLevel);
        }

        /// <summary>
        /// 判断副本是否可进
        /// </summary>
        /// <param name="theMission"></param>
        /// <param name="theLevel"></param>
        /// <returns></returns>
        public bool IsMissionCanEnter(int theMission, int theLevel)
        {
            KeyValuePair<int, MissionData> tempMissionData = MissionData.dataMap.FirstOrDefault(t => t.Value.mission == theMission && t.Value.difficulty == theLevel);
            if (MogoWorld.thePlayer.level >= tempMissionData.Value.level)
                return true;
            else
                return false;
        }

        #endregion


        #region 单人副本奖励池

        protected void OnBuiltClientMissionRewardPool(LuaTable luaTable)
        {
            Dictionary<int, int> item = new Dictionary<int, int>();
            int money = 0;
            int exp = 0;

            if (luaTable.ContainsKey("2"))
                money = Convert.ToInt32(luaTable["2"]);

            if (luaTable.ContainsKey("3"))
                exp = Convert.ToInt32(luaTable["3"]);

            if (luaTable.ContainsKey("1"))
                Utils.ParseLuaTable<Dictionary<int, int>>((LuaTable)luaTable["1"], out item);

            Mogo.GameLogic.LocalServer.LocalServerSceneManager.Instance.InitSrvPreCollect(item, money, exp);
        }

        #endregion


        #region 关卡最优记录

        public void GetBestRecord(int mission, int level)
        {
            theOwner.RpcCall("MissionReq", (byte)MissionReq.GET_MISSION_RECORD, mission, level, string.Empty);
        }

        public void HandleBestRecordMessage(LuaTable luaTable)
        {
            if (luaTable.ContainsKey("1")
                && luaTable.ContainsKey("2")
                && luaTable.ContainsKey("3"))
            {
                string name = Convert.ToString(luaTable["1"]);
                string score = string.Concat(Convert.ToString(luaTable["2"]), LanguageData.GetContent(7102));

                int vocation = Convert.ToInt32(luaTable["3"]);
                string vocationStr = LanguageData.GetContent(vocation);

                // 复用了时分秒的分
                // InstanceLevelChooseUIViewManager.Instance.SetInstanceLevelChooseUIPlayerNO1(true, name, score, vocationStr);
                InstanceLevelChooseUIViewManager.Instance.SetInstanceLevelChooseUIPlayerNO1(false);
            }
            else
            {
                InstanceLevelChooseUIViewManager.Instance.SetInstanceLevelChooseUIPlayerNO1(false);
            }
        }

        #endregion


        #region 随机副本

        public void EnterRandomMission()
        {
            int mission = MissionData.dataMap.Get(5000).mission;
            int level = theOwner.level;
            EnterMissionReq(mission, level);
        }

        #endregion


        #region 查看翻牌奖励

        public void ShowCardMessage(int mission, int difficulty)
        {
            var data = MissionRandomRewardData.dataMap.FirstOrDefault(t => t.Value.mission == mission && t.Value.difficulty == difficulty).Value;

            List<int> items = new List<int>();

            switch (theOwner.vocation)
            {
                case Vocation.Warrior:
                    items = data.item1;
                    break;
                case Vocation.Assassin:
                    items = data.item2;
                    break;
                case Vocation.Archer:
                    items = data.item3;
                    break;
                case Vocation.Mage:
                    items = data.item4;
                    break;
            }
       
            List<int> itemIds = new List<int>();
            List<string> itemNames = new List<string>();
            int numCount = 0;

            foreach (var item in items)
            {
                itemIds.Add(item);
                itemNames.Add(ItemParentData.GetNameWithNum(item, data.num[numCount]));
                numCount++;
            }

            InstanceLevelChooseUIViewManager.Instance.AddDragCardReward(itemIds.Count, () =>
                {
                    InstanceLevelChooseUIViewManager.Instance.SetCardRewardDataList(itemIds, itemNames);
                });            
        }

        #endregion


        #region 迷雾深渊

        public void OnNotifyToClientFoggyAbyssOpen()
        {
            NormalMainUIViewManager.Instance.ShowMissionFoggyAbyssOpenTip();
            EventDispatcher.TriggerEvent(Events.FoggyAbyssEvent.FoggyAbyssOpen);
        }

        public void GetFoggyAbyssInfoReq()
        {
            theOwner.RpcCall("MissionReq", (byte)MissionReq.MWSY_MISSION_GET_INFO, (ushort)1, (ushort)1, "");
        }

        public void OnGetFoggyAbyssInfoResp(bool isShow, int currentLevel = 0, bool hasPlay = true)
        {
            if (isShow)
            {
                FoggyAbyssData data = FoggyAbyssData.dataMap.FirstOrDefault(t => t.Value.level[0] <= theOwner.level && t.Value.level[1] >= theOwner.level).Value;

                string missionAndDifficulty = string.Empty;
                switch (currentLevel)
                {
                    case 1:
                        missionAndDifficulty = data.difficulty1;
                        break;
                    case 2:
                        missionAndDifficulty = data.difficulty2;
                        break;
                    case 3:
                        missionAndDifficulty = data.difficulty3;
                        break;
                }

                string[] messages = missionAndDifficulty.Split(':');

                int foggyAbyssMissionID = Convert.ToInt32(messages[0]);
                int foggyAbyssMissionDifficulty = Convert.ToInt32(messages[1]);

                int mapID = MapUIMappingData.dataMap.FirstOrDefault(t => t.Value.grid.ContainsValue(GetLastMissionCanEnter().Key)).Key;

                InstanceUILogicManager.Instance.SetSpecialMissionMessage(currentLevel, foggyAbyssMissionID, foggyAbyssMissionDifficulty, hasPlay);
                InstanceMissionChooseUIViewManager.Instance.ShowFoggyAbyssMission(mapID, true, currentLevel);
                //InstanceMissionChooseUIViewManager.Instance.ShowMissionFoggyAbyssGridTip(true);

                EventDispatcher.TriggerEvent(Events.FoggyAbyssEvent.FoggyAbyssOpen);
            }
            else
            {
                InstanceUILogicManager.Instance.SetSpecialMissionMessage(0, 0, 0, false);
                InstanceMissionChooseUIViewManager.Instance.ShowFoggyAbyssMission(0, false, 0);
                InstanceMissionChooseUIViewManager.Instance.ShowMissionFoggyAbyssGridTip(false);

                EventDispatcher.TriggerEvent(Events.FoggyAbyssEvent.FoggyAbyssClose);
            }
        }

        public void EnterFoggyAbyssReq(int missionID, int level)
        {
            theOwner.ApplyMissionID = missionID;
            theOwner.ApplyMissionLevel = level;

            theOwner.CurMissionID = missionID;
            theOwner.CurMissionLevel = level;

            theOwner.RpcCall("MissionReq", (byte)MissionReq.MWSY_MISSION_ENTER, (ushort)1, (ushort)1, "");
        }

        public void FoggyAbyssDead()
        {
            MogoUIManager.Instance.ShowMogoInstanceRebornUI(true, () =>
            {
                InstanceUIRebornNoneDropViewManager.Instance.ChangeRebornUIState(InstanceUIRebornNoneDropViewManager.RebornUIState.CantReborn);
            });
        }

        #endregion
    }
}