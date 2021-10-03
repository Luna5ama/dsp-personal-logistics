using System;
using System.Collections.Generic;
using System.Text;
using BepInEx;
using HarmonyLib;
using PersonalLogistics.Model;
using PersonalLogistics.PlayerInventory;
using PersonalLogistics.Shipping;
using PersonalLogistics.UI;
using PersonalLogistics.Util;
using UnityEngine;
using static PersonalLogistics.Log;
using Debug = UnityEngine.Debug;

namespace PersonalLogistics
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("DSPGAME.exe")]
    public class PersonalLogisticsPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "semarware.dysonsphereprogram.PersonalLogistics";
        public const string PluginName = "PersonalLogistics";
        public const string PluginVersion = "1.0.1";
        private bool _initted;
        private Harmony _harmony;

        private static PersonalLogisticsPlugin instance;
        private List<GameObject> _objectsToDestroy = new List<GameObject>();
        private const float _inventorySyncInterval = 5.0f;
        private float _inventorySyncWaited = 0.0f;

        private void Awake()
        {
            logger = Logger;
            instance = this;
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(PersonalLogisticsPlugin));
            _harmony.PatchAll(typeof(RequestWindow));
            Debug.Log($"PersonalLogistics Plugin Loaded (plugin folder {FileUtil.GetBundleFilePath()})");
        }


        private void Update()
        {
            if (GameMain.mainPlayer == null || UIRoot.instance == null || UIRoot.instance.uiGame == null || UIRoot.instance.uiGame.globemap == null)
                return;
            if (!GameMain.isRunning)
                return;
            if (!LogisticsNetwork.IsInitted && GameMain.mainPlayer != null && GameMain.mainPlayer.factory != null)
            {
                PluginConfig.InitConfig(Config);
                ShippingManager.Init();

                Debug("Starting logistics network");
                LogisticsNetwork.Start();
                CrossSeedInventoryState.Init();
                if (!_initted)
                    InitUi();
            }
            if (VFInput.control && Input.GetKeyDown(KeyCode.F3))
            {
                UIRoot.ClearFatalError();
                GameMain.errored = false;
                var parent = GameObject.Find("UI Root/Overlay Canvas/In Game/");
                var componentsInParent = parent.GetComponentsInChildren<UIItemTip>();
                logger.LogDebug($"found {componentsInParent.Length} tip windows to close");
                foreach (var tipWindow in componentsInParent)
                {
                    if (UINetworkStatusTip.IsOurTip(tipWindow))
                    {
                        UINetworkStatusTip.CloseTipWindow(tipWindow);
                    }
                    else
                    {
                        Destroy(tipWindow.gameObject);
                    }
                }

                return;
            }

            if (InventoryManager.Instance != null)
                InventoryManager.Instance.ProcessInventoryActions();


            UINetworkStatusTip.UpdateAll();
            if (_inventorySyncWaited < _inventorySyncInterval && LogisticsNetwork.IsInitted && LogisticsNetwork.IsFirstLoadComplete)
            {
                _inventorySyncWaited += Time.deltaTime;
                if (_inventorySyncWaited >= _inventorySyncInterval)
                {
                    PersonalLogisticManager.SyncInventory();
                }
            }
            else
                _inventorySyncWaited = 0.0f;


            if (Time.frameCount % 100 == 0)
            {
                if (LogisticsNetwork.IsInitted && LogisticsNetwork.IsFirstLoadComplete)
                {
                    var itemLoadStates = ItemLoadState.GetLoadState();
                    if (itemLoadStates.Count == 0)
                    {
                        return;
                    }

                    var sb = new StringBuilder("Inbound: ");
                    foreach (var loadState in itemLoadStates)
                    {
                        sb.Append(loadState);
                        sb.Append($"{loadState.itemName} {loadState.percentLoaded}%\r\n");
                    }

                    LogAndPopupMessage(sb.ToString());
                }
            }

            if (Time.frameCount % 205 == 0)
            {
                TrashHandler.ProcessTasks();
                ShippingManager.Process();
            }
        }


        private void OnDestroy()
        {
            LogisticsNetwork.Stop();
            foreach (var gameObj in _objectsToDestroy)
            {
                try
                {
                    Destroy(gameObj);
                }
                catch (Exception e)
                {
                    Warn($"failed to destroy gameobject {e.Message}\n{e.StackTrace}");
                }
            }

            try
            {
                PUI.Unload();
            }
            catch (Exception e)
            {
                // ignored
            }

            _objectsToDestroy.Clear();
            _harmony.UnpatchSelf();
            LoadFromFile.UnloadAssetBundle();
        }

        public void OnGUI()
        {
            if (RequestWindow.visible)
                RequestWindow.OnGUI();
        }

        private void InitUi()
        {
            var buttonToCopy = UIRoot.instance.uiGame.gameMenu.button1;
            if (buttonToCopy == null || buttonToCopy.gameObject.GetComponent<RectTransform>() == null)
                return;

            var rectTransform = buttonToCopy.gameObject.GetComponent<RectTransform>();
            var newButton = PUI.CopyButton(rectTransform, (Vector2.right * 65 + Vector2.down * 20), LoadFromFile.LoadIconSprite(),
                (v) => { RequestWindow.visible = !RequestWindow.visible; });
            if (newButton != null)
            {
                _objectsToDestroy.Add(newButton.gameObject);
            }

            if (UIRoot.instance.uiGame.inventory != null && newButton != null)
            {
                _initted = true;
            }
        }


        [HarmonyPostfix, HarmonyPatch(typeof(UIItemTip), "SetTip")]
        public static void UIItemTip_SetTip_Postfix(UIItemTip __instance)
        {
            if (__instance != null && __instance.descText.text != null && instance != null && LogisticsNetwork.IsInitted && !UINetworkStatusTip.IsOurTip(__instance))
            {
                UINetworkStatusTip.Create(__instance);
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(GameMain), "End")]
        public static void OnGameEnd()
        {
            LogisticsNetwork.Stop();
            CrossSeedInventoryState.Save();
            CrossSeedInventoryState.Reset();
            InventoryManager.Reset();
            RequestWindow.Reset();
            ShippingManager.Save();
        }
        

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TrashSystem), "AddTrash")]
        public static void TrashSystem_AddTrash_Postfix(TrashSystem __instance, int itemId, int count, int objId)
        {
            if (instance == null || !PluginConfig.sendLitterToLogisticsNetwork.Value)
            {
                return;
            }

            if (!LogisticsNetwork.IsInitted || !LogisticsNetwork.IsFirstLoadComplete)
            {
                return;
            }

            if (!LogisticsNetwork.HasItem(itemId))
            {
                return;
            }

            TrashHandler.trashSystem = __instance;
            TrashHandler.player = __instance.player;
            TrashHandler.AddTask(itemId);
            Debug($"Added task to remove item {itemId} count {count} from trash system {objId}");
        }
    }
}

