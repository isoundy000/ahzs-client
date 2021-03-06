/*----------------------------------------------------------------
// Copyright (C) 2013 广州，爱游
//
// 模块名：BodyEnhanceManager
// 创建者：Joe Mo
// 修改者列表：
// 创建日期：2013-4-8
// 模块描述：身体强化系统
//----------------------------------------------------------------*/

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Mogo.GameData;
using Mogo.Util;
using Mogo.Game;

public class BodyEnhanceManager
{
    const byte ERR_BODY_ENHANCE_SUCCEED = 0;                //  --成功
    const byte ERR_BODY_ENHANCE_PARA = 1;                   // --参数错误
    const byte ERR_BODY_LEVEL_ALREADY_MAX = 2;              //--等级已达最高
    const byte ERR_BODY_ENHANCE_CONFIG = 3;                 //--配置错误
    const byte ERR_BODY_ENHANCE_GOLD_NOT_ENOUGH = 4;        //--金币不够
    const byte ERR_BODY_ENHANCE_MATERIAL_NOT_ENOUGH = 5;    // --材料不齐
    const byte ERR_BODY_ENHANCE_LEVEL = 6;                  // --等级不够
    const byte ERR_BODY_ENHANCE_OTHER = 9;                  //  --其他

    public const string ON_SELECT_SLOT = "BodyEnhanceManager.ON_SELECT_SLOT";
    public const string ON_ENHANCE = "BodyEnhanceManager.ON_ENHANCE";
    public const string ON_SHOW = "BodyEnhanceManager.ON_SHOW";
    public const int LEVEL_PER_STAR = 10;

    int m_iCurrentSlot = -1;
    public int CurrentSlot
    {
        get
        {
            return m_iCurrentSlot;
        }
        set
        {
            int newIndex = 0;
            int oldIndex = 0;
            if (m_slotToIndexDic.ContainsKey(value))
                newIndex = m_slotToIndexDic[value];
            if (m_slotToIndexDic.ContainsKey(m_iCurrentSlot))
                oldIndex = m_slotToIndexDic[m_iCurrentSlot];
            //if(newIndex<actualGridNum)
            StrenthenUIViewManager.Instance.HandleStrenthTabChange(oldIndex, newIndex);
            m_iCurrentSlot = value;
        }
    }

    EntityMyself myself;
    public static BodyEnhanceManager Instance;
    public Dictionary<int, int> myEnhance = new Dictionary<int, int>();//<slot,levet>
    Dictionary<int, Dictionary<int, BodyEnhanceData>> enhanceDataDic;//<pos,<level,data>

    private List<int> m_indexToSlotList = new List<int>();
    int actualGridNum = 0;
    private Dictionary<int, int> m_slotToIndexDic = new Dictionary<int, int>();

    private bool m_isEnhancing = false;
    private uint m_timerId;

    public BodyEnhanceManager(EntityMyself _myself)
    {
        Instance = this;
        myself = _myself;
        InitEnhanceData();
        myself.RpcCall("BodyEnhaLevReq");
        AddListener();
    }

    /// <summary>
    /// 构造方便使用的数据结构
    /// </summary>
    private void InitEnhanceData()
    {
        enhanceDataDic = new Dictionary<int, Dictionary<int, BodyEnhanceData>>();
        foreach (BodyEnhanceData data in BodyEnhanceData.dataMap.Values)
        {
            if (!enhanceDataDic.ContainsKey(data.pos))
            {
                enhanceDataDic[data.pos] = new Dictionary<int, BodyEnhanceData>();
            }
            enhanceDataDic[data.pos][data.level] = data;
            //Mogo.Util.LoggerHelper.Debug("pos:" + data.pos + ",level:" + data.level);
        }

        myEnhance = new Dictionary<int, int>();
        myEnhance.Add(1, 0);
        myEnhance.Add(2, 0);
        myEnhance.Add(3, 0);
        myEnhance.Add(4, 0);
        myEnhance.Add(5, 0);
        myEnhance.Add(6, 0);
        myEnhance.Add(7, 0);
        myEnhance.Add(8, 0);
        myEnhance.Add(9, 0);
        myEnhance.Add(10, 0);
    }

    private void AddListener()
    {
        EventDispatcher.AddEventListener<int>(ON_SELECT_SLOT, OnSelectSlot);
        EventDispatcher.AddEventListener(ON_ENHANCE, OnEnhance);
        EventDispatcher.AddEventListener(ON_SHOW, OnShow);
    }

    public void RemoveListener()
    {
        EventDispatcher.RemoveEventListener<int>(ON_SELECT_SLOT, OnSelectSlot);
        EventDispatcher.RemoveEventListener(ON_ENHANCE, OnEnhance);
        EventDispatcher.RemoveEventListener(ON_SHOW, OnShow);
    }

    private void OnSelectSlot(int index)
    {
        if (index >= actualGridNum)
        {
            StrenthenUIViewManager.Instance.SetCurrentDownGrid(m_slotToIndexDic[CurrentSlot]);
            return;
        }
        CurrentSlot = m_indexToSlotList[index];
        //if (index == 0)
        //{
        //    CurrentSlot = 10;
        //}
        //else
        //{
        //    CurrentSlot = index;
        //}

        //Debug.LogError("RefreshUI begin");
        RefreshUI();
        //Debug.LogError("RefreshUI done");
    }

    private void OnShow()
    {
        //Debug.LogError("bodyEnhanceManager.refreshUI");
        RefreshUI();
    }

    private void OnEnhance()
    {
        if (CheckEnhanceCondition())
        {
            //Debug.LogError("OnEnhance");
            LoggerHelper.Debug("RpcCall(BodyEnhaUpgReq)");
            LoggerHelper.Debug("(byte)currentSlot:" + (byte)CurrentSlot);
            m_isEnhancing = true;
            m_timerId = TimerHeap.AddTimer(2000, 0, () => { m_isEnhancing = false; });
            myself.RpcCall("BodyEnhaUpgReq", (byte)CurrentSlot);
        }
    }

    private bool CanEnhance(int slot)
    {
        int level = myEnhance[slot];
        if (!enhanceDataDic[slot].ContainsKey(level + 1))
        {
            LoggerHelper.Debug("level max");
            return false;
        }
        BodyEnhanceData enhanceData = enhanceDataDic[slot][level + 1];

        if (MogoWorld.thePlayer.level < enhanceData.characterLevel)
        {
            LoggerHelper.Debug("level isnot enough ");
            return false;
        }
        if (MogoWorld.thePlayer.gold < enhanceData.gold)
        {
            LoggerHelper.Debug("gold isnot enough ");
            return false;
        }

        if (enhanceData.material != null)
        {
            foreach (KeyValuePair<int, int> pair in enhanceData.material)
            {
                ItemMaterialData material = ItemMaterialData.dataMap[pair.Key];
                int materialNum = InventoryManager.Instance.GetMaterialNum(pair.Key);
                if (materialNum < pair.Value)
                {
                    materialNum = InventoryManager.Instance.GetMaterialAllNum(pair.Key);
                    if (materialNum >= pair.Value)
                    {
                        continue;
                    }
                    else
                    {
                        LoggerHelper.Debug("材料不够！");
                        return false;
                    }
                }
            }
        }


        return true;
    }

    private bool CheckEnhanceCondition()
    {
        if (m_isEnhancing) return false;
        int level = myEnhance[CurrentSlot];
        if (!enhanceDataDic[CurrentSlot].ContainsKey(level + 1))
        {
            LoggerHelper.Debug("等级已满！");
            OnBodyEnhaUpgResp(ERR_BODY_LEVEL_ALREADY_MAX);
            return false;
        }
        BodyEnhanceData enhanceData = enhanceDataDic[CurrentSlot][level + 1];

        if (MogoWorld.thePlayer.level < enhanceData.characterLevel)
        {
            LoggerHelper.Debug("等级不够！");
            OnBodyEnhaUpgResp(ERR_BODY_ENHANCE_LEVEL);
            return false;
        }
        if (MogoWorld.thePlayer.gold < enhanceData.gold)
        {
            LoggerHelper.Debug("金币不够！");
            OnBodyEnhaUpgResp(ERR_BODY_ENHANCE_GOLD_NOT_ENOUGH);
            return false;
        }


        if (enhanceData.material != null)
        {
            bool hasEnoughMaterial = true;
            bool needToSpendOtherMaterial = false;
            string msg = string.Empty;
            List<string> needMaterialNameList = new List<string>();
            List<int> needMaterialValueList = new List<int>();
            List<string> costMaterialNameList = new List<string>();
            List<int> costMaterialValueList = new List<int>();

            foreach (KeyValuePair<int, int> pair in enhanceData.material)
            {
                ItemMaterialData material = ItemMaterialData.dataMap[pair.Key];
                int materialNum = InventoryManager.Instance.GetMaterialNum(pair.Key);
                if (materialNum < pair.Value)
                {
                    needToSpendOtherMaterial = true;
                    materialNum = InventoryManager.Instance.GetMaterialAllNum(pair.Key);
                    if (materialNum >= pair.Value)
                    {
                        List<KeyValuePair<int, int>> list = InventoryManager.Instance.GetMaterialList(pair.Key, pair.Value);
                        needMaterialNameList.Add(material.Name);
                        needMaterialValueList.Add(pair.Value);
                        //msg += "\n" + LanguageData.dataMap[458].Format(material.Name, pair.Value);
                        for (int i = 0; i < list.Count; i++)
                        {
                            ItemMaterialData tempMaterial = ItemMaterialData.dataMap[list[i].Key];

                            costMaterialNameList.Add(tempMaterial.Name);
                            costMaterialValueList.Add(list[i].Value);
                            //msg += "\n" + LanguageData.dataMap[459].Format(tempMaterial.Name, list[i].Value);
                        }
                    }
                    else
                    {
                        hasEnoughMaterial = false;
                        LoggerHelper.Debug("材料不够！");
                        OnBodyEnhaUpgResp(ERR_BODY_ENHANCE_MATERIAL_NOT_ENOUGH, pair.Value, material.Name);
                        return false;
                    }

                }
                else
                {
                    continue;
                }
            }

            if (hasEnoughMaterial && needToSpendOtherMaterial)
            {
                msg = LanguageData.dataMap[458].content;
                for (int i = 0; i < needMaterialNameList.Count; i++)
                {
                    msg += LanguageData.dataMap[460].Format(needMaterialNameList[i], needMaterialValueList[i]) + ",";
                }
                msg += LanguageData.GetContent(459) + "\n";
                for (int i = 0; i < costMaterialNameList.Count; i++)
                {
                    msg += LanguageData.dataMap[460].Format(costMaterialNameList[i], costMaterialValueList[i]) + ",\n";
                }


                MogoGlobleUIManager.Instance.Confirm(msg, (rst) =>
                {
                    if (rst)
                    {
                        m_isEnhancing = true;
                        m_timerId = TimerHeap.AddTimer(2000, 0, () => { m_isEnhancing = false; });
                        myself.RpcCall("BodyEnhaUpgReq", (byte)CurrentSlot);
                        MogoGlobleUIManager.Instance.ConfirmHide();
                    }
                    else
                    {
                        MogoGlobleUIManager.Instance.ConfirmHide();
                    }
                });
                return false;
            }


        }


        return true;
    }

    /// <summary>
    /// 强化成功
    /// </summary>
    private void Enhance()
    {
        myEnhance[CurrentSlot] = myEnhance[CurrentSlot] + 1;
        RefreshUI(true);
    }

    public void OnBodyEnhaLevResp(LuaTable _info, byte errorId)
    {
        LoggerHelper.Debug("OnBodyEnhaLevResp:" + errorId);
        object obj;
        Utils.ParseLuaTable(_info, typeof(Dictionary<int, int>), out obj);

        Dictionary<int, int> temp = obj as Dictionary<int, int>;

        foreach (KeyValuePair<int, int> pair in temp)
        {
            LoggerHelper.Debug(pair.Key + ":" + pair.Value);
            myEnhance[pair.Key] = pair.Value;
        }

        //这里暂时用客户端伪造的数据测试
        RefreshUI();
    }

    public void UpDateGold(uint gold)
    {
        if (CurrentSlot <= 0) return;
        if (!myEnhance.ContainsKey(CurrentSlot)) return;
        int level = myEnhance[CurrentSlot];
        if (!enhanceDataDic[CurrentSlot].ContainsKey(level + 1)) return;
        if (StrenthenUIViewManager.Instance == null) return;
        BodyEnhanceData afterEnhanceData = enhanceDataDic[CurrentSlot][level + 1];
        string goldStr = "" + afterEnhanceData.gold;
        if (gold < afterEnhanceData.gold)
        {
            goldStr = "[FF0000]" + goldStr + "[-]";
            StrenthenUIViewManager.Instance.SetCostText(false);
        }
        Mogo.Util.LoggerHelper.Debug("haha");
        StrenthenUIViewManager.Instance.SetStrenthenNeedGold(goldStr);
        StrenthenUIViewManager.Instance.SetCurrentGold(gold + "");
    }

    /// <summary>
    /// 武器slot=10,对应位置为0
    /// 更新ui
    /// </summary>
    /// <param name="bBodyEnhanceSuccess">是否是强化成功而刷新界面</param>
    public void RefreshUI(bool bBodyEnhanceSuccess = false)
    {
        Mogo.Util.LoggerHelper.Debug("bodyEnhanceManager RefreshUI:" + CurrentSlot);

        //add by winJ
        if (StrenthenUIViewManager.Instance == null)
            return;

        // 强化成功刷新界面，播放强化成功动画准备
        if (bBodyEnhanceSuccess)
        {
            StrenthenUIViewManager.Instance.PreparePlaySuccessAnimation();
        }
        UpdateSlotToIndexDic();

        if (CurrentSlot == -1)
        {
            CurrentSlot = 10;
        }

        StrenthenUIViewManager.Instance.SetCurrentGold(MogoWorld.thePlayer.gold + "");
        LoggerHelper.Debug("currentSlot:" + CurrentSlot);
        int level = myEnhance[CurrentSlot];
        LoggerHelper.Debug("level:" + level);
        BodyEnhanceData baseEnhanceData = enhanceDataDic[CurrentSlot][level];

        //刷新左边栏信息
        for (int i = 0; i < BodyName.names.Length; i++)
        {
            int slot = m_indexToSlotList[i];
            int levelTemp = myEnhance[slot];
            BodyEnhanceData baseEnhanceDataTemp = enhanceDataDic[slot][levelTemp];
            StrenthenUIViewManager.Instance.SetEquipmentName(i, BodyName.names[slot - 1]);
            int actualSlot = (slot == 10 ? 11 : slot);
            bool hasEquip = false;
            if (actualSlot == 9)
            {
                if (InventoryManager.Instance.EquipOnDic.ContainsKey(9) ||
                     InventoryManager.Instance.EquipOnDic.ContainsKey(10))
                    hasEquip = true;
            }
            else if (InventoryManager.Instance.EquipOnDic.ContainsKey(actualSlot)) hasEquip = true;
            StrenthenUIViewManager.Instance.SetEquipmentIcon(i, BodyIcon.icons[slot - 1], hasEquip ? 0 : 10);
            StrenthenUIViewManager.Instance.SetEquipmentTextImg(i, BodyTextIcon.icons[slot - 1]);

            // 设置星级
            if (myEnhance.ContainsKey(slot))
                StrenthenUIViewManager.Instance.SetEquipmentIconStarLevel(i, baseEnhanceDataTemp.star);
        }

        int id = baseEnhanceData.propertyEffectId;
        PropertyEffectData basePropertyData = PropertyEffectData.dataMap[id];
        StrenthenUIViewManager view = StrenthenUIViewManager.Instance;
        view.SetEquipmentAfterAttribute("");
        view.SetEquipmentAfterLevel0Reward("");
        view.SetEquipmentAfterLevel1Reward("");
        view.SetEquipmentAfterLevel2Reward("");
        view.SetEquipmentImage(BodyIcon.icons[CurrentSlot - 1]);

        BodyEnhanceData afterEnhanceData = null;
        PropertyEffectData afterPropertyData = null;
        if (enhanceDataDic[CurrentSlot].ContainsKey(level + 1))
        {
            afterEnhanceData = enhanceDataDic[CurrentSlot][level + 1];
            afterPropertyData = PropertyEffectData.dataMap[afterEnhanceData.propertyEffectId];

            int nextStarLevel = level + (LEVEL_PER_STAR - level % LEVEL_PER_STAR);
            string temp = "";
            if (enhanceDataDic[CurrentSlot].ContainsKey(nextStarLevel))
            {
                temp = LanguageData.dataMap[173].Format(enhanceDataDic[CurrentSlot][nextStarLevel].enhanceRate / 100);
            }

            view.SetEquipmentAfterAttribute(temp);
            view.SetEquipmentAfterLevel0Reward("+" + afterPropertyData.hpBase);
            view.SetEquipmentAfterLevel1Reward("+" + afterPropertyData.attackBase);
            view.SetEquipmentAfterLevel2Reward("+" + afterPropertyData.defenseBase);

            string gold = "" + afterEnhanceData.gold;
            bool hasEnoughGoldAndMat = true;
            if (myself.gold < afterEnhanceData.gold)
            {
                gold = "[FF0000]" + gold + "[-]";
                hasEnoughGoldAndMat = false;
            }         

            view.SetNeedLevel(afterEnhanceData.characterLevel);
            view.SetStrenthenNeedGold(gold);

            string material = "";
            bool hasEnoughMat = true;
            List<int> reqMatIdList = new List<int>();
            reqMatIdList.Clear();

            view.ShowStrenthenNeedMaterial2(false);
            if (afterEnhanceData.material == null || afterEnhanceData.material.Count <= 0)
            {
                view.SetStrenthenNeedMaterial1("0");
            }
            else
            {
                int materialNum = 0;
                //这里暂时默认只需要一种材料，以后可以扩展
                int count = 0;
                foreach (KeyValuePair<int, int> pair in afterEnhanceData.material)
                {
                    materialNum = myself.inventoryManager.GetMaterialNum(pair.Key);
                    material = "" + pair.Value;
                    if (materialNum < pair.Value)
                    {
                        material = "[FF0000]" + material + "[-]";
                        hasEnoughGoldAndMat = false;
                        hasEnoughMat = false;
                        reqMatIdList.Add(pair.Key);
                    }
                    if (count == 0)
                    {
                        view.SetStrenthenNeedMaterialIcon1(pair.Key);
                        view.SetStrenthenNeedMaterial1(material + "/" + materialNum);
                    }
                    else
                    {
                        view.SetStrenthenNeedMaterialIcon2(pair.Key);
                        view.SetStrenthenNeedMaterial2(material + "/" + materialNum);
                    }
                    count++;
                }

                if (count >= 2)
                {
                    view.ShowStrenthenNeedMaterial2(true);
                }
            }

            if (!hasEnoughGoldAndMat)
                view.SetCostText(false);
            else
                view.SetCostText(true);

            view.ShowMaterialTip(!hasEnoughMat);

            if (reqMatIdList.Count == 1)
                view.ShowMaterialObtainTip(false, true, reqMatIdList[0], 0);
            else if (reqMatIdList.Count > 1)
                view.ShowMaterialObtainTip(false, true, reqMatIdList[0], reqMatIdList[1]);
        }

        view.SetEquipmentStarLevel(baseEnhanceData.star, baseEnhanceData.progress);

        view.SetBaseEquipType(EquipSlotName.names[CurrentSlot - 1]);
        view.SetEquipmentBaseAttribute(LanguageData.dataMap[167].Format(baseEnhanceData.enhanceRate / 100 + "%"));

        view.SetEquipmentLevel(baseEnhanceData.progress);
        view.SetEquipmentExp(baseEnhanceData.progress);

        view.SetEquipmentBaseLevel0Reward(LanguageData.GetContent(48004), string.Concat("+ ", basePropertyData.hpBase));
        view.SetEquipmentBaseLevel1Reward(LanguageData.GetContent(48005), string.Concat("+ ", basePropertyData.attackBase));
        view.SetEquipmentBaseLevel2Reward(LanguageData.GetContent(48006), string.Concat("+ ", basePropertyData.defenseBase));

        //switch (myself.vocation)
        //{
        //    case Vocation.Archer:
        //    case Vocation.Mage:
        //        view.SetEquipmentBaseLevel3Reward(LanguageData.dataMap[173].Format(basePropertyData.atkA));
        //        if (hasNextLevel)
        //            view.SetEquipmentAfterLevel3Reward(LanguageData.dataMap[173].Format(afterPropertyData.atkA));
        //        break;
        //    case Vocation.Assassin:
        //    case Vocation.Warrior:
        //        view.SetEquipmentBaseLevel3Reward(LanguageData.dataMap[172].Format(basePropertyData.atkA));
        //        if (hasNextLevel)
        //            view.SetEquipmentAfterLevel3Reward(LanguageData.dataMap[172].Format(afterPropertyData.atkA));
        //        break;
        //}

        StrenthenUIViewManager.Instance.SetCurrentDownGrid(m_slotToIndexDic[CurrentSlot]);

        // 强化成功刷新界面，播放强化成功动画
        if (bBodyEnhanceSuccess)
        {
            StrenthenUIViewManager.Instance.PlaySuccessAnimation(baseEnhanceData.progress / 10);
        }
    }

    private void UpdateSlotToIndexDic()
    {
        m_slotToIndexDic.Clear();
        m_indexToSlotList.Clear();
        m_indexToSlotList.Add(10);
        m_slotToIndexDic[10] = 0;
        int index = 1;
        for (int slot = 1; slot <= 9; slot++)
        {
            if (slot == 9)
            {
                if (!InventoryManager.Instance.EquipOnDic.ContainsKey(9)
                    && !InventoryManager.Instance.EquipOnDic.ContainsKey(10))
                    continue;
            }
            else
            {
                if (!InventoryManager.Instance.EquipOnDic.ContainsKey(slot)) continue;
            }
            m_indexToSlotList.Add(slot);
            m_slotToIndexDic[slot] = index;
            index++;
        }
        actualGridNum = index;
        for (int slot = 1; slot <= 9; slot++)
        {
            if (slot == 9)
            {
                if (InventoryManager.Instance.EquipOnDic.ContainsKey(9)
                    || InventoryManager.Instance.EquipOnDic.ContainsKey(10))
                    continue;
            }
            else
            {
                if (InventoryManager.Instance.EquipOnDic.ContainsKey(slot)) continue;
            }
            m_indexToSlotList.Add(slot);
            m_slotToIndexDic[slot] = index;
            index++;
        }
    }


    /// <summary>
    /// 申请强化后返回的错误码
    /// </summary>
    /// <param name="errorId"></param>
    public void OnBodyEnhaUpgResp(byte errorId, int num = 0, string name = "")
    {
        TimerHeap.DelTimer(m_timerId);
        m_isEnhancing = false;
        LoggerHelper.Debug("errorid:" + errorId);
        int index = 450;
        string msg = "";
        switch (errorId)
        {
            case ERR_BODY_ENHANCE_SUCCEED: // 强化成功
                //msg = LanguageData.dataMap[index].content;
                Enhance();
                return;
            case ERR_BODY_ENHANCE_PARA:
                msg = LanguageData.dataMap[index + 1].content;
                break;
            case ERR_BODY_LEVEL_ALREADY_MAX:
                msg = LanguageData.dataMap[index + 2].content;
                break;
            case ERR_BODY_ENHANCE_CONFIG:
                msg = LanguageData.dataMap[index + 3].content;
                break;
            case ERR_BODY_ENHANCE_GOLD_NOT_ENOUGH:
                msg = LanguageData.dataMap[index + 4].content;
                MogoMsgBox.Instance.ShowFloatingText(msg);
                return;
            case ERR_BODY_ENHANCE_MATERIAL_NOT_ENOUGH:
                if (!name.Equals(string.Empty))
                    msg = LanguageData.dataMap[index + 5].Format(name, num);
                else return;
                break;
            case ERR_BODY_ENHANCE_LEVEL:
                msg = LanguageData.dataMap[index + 6].content;
                MogoMsgBox.Instance.ShowFloatingText(msg);
                return;
            case ERR_BODY_ENHANCE_OTHER:
                msg = LanguageData.dataMap[index + 7].content;
                break;
        }
        MogoMsgBox.Instance.ShowMsgBox(msg);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="level"></param>
    /// <returns>-1为无，其他为对应slot</returns>
    public int GetCanEnhanceSlot(int level)
    {
        int minLevelSlot = 10;
        //取最低级部位
        foreach (KeyValuePair<int, int> pair in myEnhance)
        {
            if (myEnhance[minLevelSlot] > pair.Value)
            {
                if (pair.Key == 9)
                {
                    if (!InventoryManager.Instance.EquipOnDic.ContainsKey(9) &&
                        !InventoryManager.Instance.EquipOnDic.ContainsKey(10)) continue;
                }
                else if (!InventoryManager.Instance.EquipOnDic.ContainsKey(pair.Key)) continue;

                minLevelSlot = pair.Key;
            }
        }
        //检查是否材料足够
        if (CanEnhance(minLevelSlot))
        {
            return minLevelSlot;
        }
        else
        {
            return -1;
        }
    }
}