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

namespace Mogo.GameLogic.LocalServer
{
   
    public class CliEntityManager: IEventManager
    {
        private static CliEntityManager m_instance;

        public static CliEntityManager Instance
        {
            get
            {
                if (m_instance == null)
                {
                    m_instance = new CliEntityManager();
                }
                return CliEntityManager.m_instance;
            }
        }

        protected Dictionary<uint, List<int>> localDrops = new Dictionary<uint, List<int>>();//记录掉落物品信息 entityId<=>List<int>( 4:money 5:itemId)

        public void AddListeners()
        {

        }

        public void RemoveListeners()
        {

        }

        public void ClearData()
        {
            localDrops.Clear();
        }

        public List<int> GetDropByEntityId(uint entityId)
        {
            if (localDrops.ContainsKey(entityId))
                return localDrops[entityId];
            else
                return null;
        }

        public void onActionDie(LocalServerDummy entity, int vocation)
        {
            onActionDie(entity.monsterID, entity.x, entity.y, vocation);
        }

        public void onActionDie(int entityID, int entityX, int entityY, int vocation)
        {
            Dictionary<int, int> tblDstDropsItem = new Dictionary<int, int>();
            List<int> tblDstMoney = new List<int>();
            MonsterData.getDrop(tblDstDropsItem, tblDstMoney, entityID, vocation);


            List<List<int>> args = new List<List<int>>();

            foreach (KeyValuePair<int, int> subDstDropsItem in tblDstDropsItem)
            {
                if (Mogo.GameLogic.LocalServer.LocalServerSceneManager.Instance.GetOneDropItemFromPool(subDstDropsItem.Key))
                {
                    List<int> subArgs = new List<int>();
                    uint newEntityId = LocalServerSceneManager.getNextEntityId();
                    subArgs.Add((int)CliEntityType.CLI_ENTITY_TYPE_DROP);
                    subArgs.Add((int)newEntityId);
                    subArgs.Add(entityX);
                    subArgs.Add(entityY);
                    subArgs.Add(0);
                    subArgs.Add(subDstDropsItem.Key);
                    subArgs.Add((int)MogoWorld.thePlayer.ID);

                    localDrops[newEntityId] = subArgs;
                    args.Add(subArgs);
                }
            }

            foreach (int money in tblDstMoney)
            {
                int realDropMoney = Mogo.GameLogic.LocalServer.LocalServerSceneManager.Instance.GetOneDropMoneyFromPool(money);
                if (realDropMoney > 0)
                {
                    List<int> subArgs = new List<int>();
                    uint newEntityId = LocalServerSceneManager.getNextEntityId();
                    subArgs.Add((int)CliEntityType.CLI_ENTITY_TYPE_DROP);
                    subArgs.Add((int)newEntityId);
                    subArgs.Add(entityX);
                    subArgs.Add(entityY);
                    subArgs.Add(money);
                    subArgs.Add(0);
                    subArgs.Add((int)MogoWorld.thePlayer.ID);

                    localDrops[newEntityId] = subArgs;
                    args.Add(subArgs);
                }
            }

            if (args.Count > 0)
            {
                LuaTable table;
                Mogo.RPC.Utils.PackLuaTable(args, out table);

                System.Object[] Arguments = { table };// new System.Object[1];
                //Arguments[0] = table;
                EventDispatcher.TriggerEvent<object[]>(Util.Utils.RPC_HEAD + "CreateCliEntityResp", Arguments);
            }
        }
    }
}
