#region 模块信息
/*----------------------------------------------------------------
// Copyright (C) 2013 广州，爱游
//
// 模块名：
// 创建者：HongChengguo
// 修改者列表：
// 创建日期：
// 模块描述：
//----------------------------------------------------------------*/
#endregion

using UnityEngine;
using System.Collections;
using Mogo.Util;
using System;
using System.Text;
using Mogo.Game;
using Mogo.GameData;

public class DiamondToGoldUILogicManager : UILogicManager
{
    private static DiamondToGoldUILogicManager m_instance;
    public static DiamondToGoldUILogicManager Instance
    {
        get
        {
            if (m_instance == null)
            {
                m_instance = new DiamondToGoldUILogicManager();                
            }

            return DiamondToGoldUILogicManager.m_instance;
        }
    }

    int m_UseTimes = 0;    

    #region 事件

    public void Initialize()
    {
        DiamondToGoldUIViewManager.Instance.DIAMONDTOGOLDUIUSEALLUP += OnDiamondToGoldUseAllUp;
        DiamondToGoldUIViewManager.Instance.DIAMONDTOGOLDUIUSEONEUP += OnDiamondToGoldUseOneUp;
        DiamondToGoldUIViewManager.Instance.DIAMONDTOGOLDUICLOSEUP += OnDiamondToGoldCloseUp;
        DiamondToGoldUIViewManager.Instance.DIAMONDTOGOLDUITRUNUP += OnDiamondToGoldTurnUp;

        DiamondToGoldUIViewManager.Instance.DIAMONDTOGOLDUIUSEOK += OnDiamondToGoldUseOK;
        DiamondToGoldUIViewManager.Instance.DIAMONDTOGOLDUIUSECANCEL += OnDiamondToGoldUseCancel;
        DiamondToGoldUIViewManager.Instance.DIAMONDTOGOLDUIUSETIPENABLE += OnDiamondToGoldUseTipEnable;

        // 属性绑定
        ItemSource = MogoWorld.thePlayer; // 由于目前Logic可能会跟随UI关闭而Release，故在此重新设置ItemSource
        SetBinding<uint>(EntityMyself.ATTR_DIAMOND, DiamondToGoldUIViewManager.Instance.SetDiamondNum);
        SetBinding<uint>(EntityMyself.ATTR_GLOD, DiamondToGoldUIViewManager.Instance.SetGoldNum);
    }

    public override void Release()
    {
        base.Release();
        DiamondToGoldUIViewManager.Instance.DIAMONDTOGOLDUIUSEALLUP -= OnDiamondToGoldUseAllUp;
        DiamondToGoldUIViewManager.Instance.DIAMONDTOGOLDUIUSEONEUP -= OnDiamondToGoldUseOneUp;
        DiamondToGoldUIViewManager.Instance.DIAMONDTOGOLDUICLOSEUP -= OnDiamondToGoldCloseUp;
        DiamondToGoldUIViewManager.Instance.DIAMONDTOGOLDUITRUNUP -= OnDiamondToGoldTurnUp;

        DiamondToGoldUIViewManager.Instance.DIAMONDTOGOLDUIUSEOK -= OnDiamondToGoldUseOK;
        DiamondToGoldUIViewManager.Instance.DIAMONDTOGOLDUIUSECANCEL -= OnDiamondToGoldUseCancel;
        DiamondToGoldUIViewManager.Instance.DIAMONDTOGOLDUIUSETIPENABLE -= OnDiamondToGoldUseTipEnable;
    }

    void OnDiamondToGoldUseAllUp()
    {
        DiamondToGoldUIViewManager.Instance.UseType = DiamondToGoldUseType.UseAll;
        DiamondToGoldUIViewManager.Instance.SetDiamondToGoldCostAndReward();
    }

    void OnDiamondToGoldUseOneUp()
    {
        DiamondToGoldUIViewManager.Instance.UseType = DiamondToGoldUseType.UseOne;
        DiamondToGoldUIViewManager.Instance.SetDiamondToGoldCostAndReward();
    }

    void OnDiamondToGoldCloseUp()
    {
        LoggerHelper.Debug("OnDiamondToGoldCloseUp");
        MogoUIManager.Instance.ShowMogoNormalMainUI();
    }

    void OnDiamondToGoldTurnUp()
    {
        if (DiamondToGoldUIViewManager.Instance.CalLastUseTimes() == 0)
        {
            // 使用次数用尽，提升VIP可获得更多
            MogoGlobleUIManager.Instance.Confirm(LanguageData.GetContent(25566), (isOK) =>
            {
                if (isOK)
                {
                    MogoGlobleUIManager.Instance.ConfirmHide();
                    MogoUIManager.Instance.ShowVIPInfoUI();
                }
                else
                {
                    MogoGlobleUIManager.Instance.ConfirmHide();
                }
            } , LanguageData.GetContent(48301), LanguageData.GetContent(48302));
            return;
        }

        m_UseTimes = 0;
        if (DiamondToGoldUIViewManager.Instance.UseType == DiamondToGoldUseType.UseOne)
            m_UseTimes = 1;
        else if (DiamondToGoldUIViewManager.Instance.UseType == DiamondToGoldUseType.UseAll)
        {
            m_UseTimes = 10;
            if (m_UseTimes > DiamondToGoldUIViewManager.Instance.CalLastUseTimes())
            {
                MogoMsgBox.Instance.ShowFloatingText(LanguageData.GetContent(48300));
                return;
            }
        }

        int diamondCost = DiamondToGoldUIViewManager.Instance.CalDiamondCost(m_UseTimes);
        if(diamondCost > MogoWorld.thePlayer.diamond)
        {
            // 钻石不足
            MogoGlobleUIManager.Instance.Confirm(LanguageData.GetContent(25564), (isOK) =>
            {
                if (isOK)
                {
                    MogoGlobleUIManager.Instance.ConfirmHide();
                    //MogoMsgBox.Instance.ShowFloatingText(LanguageData.GetContent(25570));
                }
                else
                {
                    MogoGlobleUIManager.Instance.ConfirmHide();
                }
            }, LanguageData.GetContent(25565), LanguageData.GetContent(25561), -1, ButtonBgType.Yellow, ButtonBgType.Blue);

            return;
        }

        if (DiamondToGoldUIViewManager.Instance.IsShowGoldMetallurgyTipDialog)
            DiamondToGoldUIViewManager.Instance.ShowUseOKCancel(true);
        else
            EventDispatcher.TriggerEvent<int>(Events.DiamondToGoldEvent.GoldMetallurgy, m_UseTimes);
    }

    void OnDiamondToGoldUseOK()
    {
        EventDispatcher.TriggerEvent<int>(Events.DiamondToGoldEvent.GoldMetallurgy, m_UseTimes);
        DiamondToGoldUIViewManager.Instance.ShowUseOKCancel(false);
    }

    void OnDiamondToGoldUseCancel()
    {
        DiamondToGoldUIViewManager.Instance.ShowUseOKCancel(false);
    }

    void OnDiamondToGoldUseTipEnable()
    {
        DiamondToGoldUIViewManager.Instance.IsShowGoldMetallurgyTipDialog = !DiamondToGoldUIViewManager.Instance.IsShowGoldMetallurgyTipDialog;

        if (DiamondToGoldUIViewManager.Instance.IsShowGoldMetallurgyTipDialog == false)
        {
            Mogo.Util.SystemConfig.Instance.IsShowGoldMetallurgyTipDialog = false;
            Mogo.Util.SystemConfig.Instance.GoldMetallurgyTipDialogDisableDay = MogoTime.Instance.GetCurrentDateTime().Day;
            Mogo.Util.SystemConfig.SaveConfig();
        }
    }

    #endregion  	

    public void SetVipRealState()
    {
        DiamondToGoldUIViewManager.Instance.SetVipRealState();
    }
}
