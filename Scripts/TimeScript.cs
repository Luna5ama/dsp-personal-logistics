﻿using System;
using System.Collections;
using System.Text;
using PersonalLogistics.PlayerInventory;
using PersonalLogistics.UI;
using PersonalLogistics.Util;
using UnityEngine;

namespace PersonalLogistics.Scripts
{
    public class TimeScript : MonoBehaviour
    {
        private StringBuilder timeText;
        private string positionText;
        private GUIStyle fontSize;
        private GUIStyle style;
        private bool loggedException = false;
        private static int yOffset = 0;
        private static int xOffset = 0;

        void Awake()
        {
            StartCoroutine(Loop());
            fontSize = new GUIStyle(GUI.skin.GetStyle("label"))
            {
                fontSize = UiScaler.ScaleToDefault(12, false)
            };
            style = new GUIStyle
            {
                normal = new GUIStyleState { textColor = Color.white },
                wordWrap = false,
            };
        }

        public void OnGUI()
        {
            if ((timeText == null || timeText.Length == 0) && string.IsNullOrEmpty(positionText))
            {
                return;
            }

            var uiGame = UIRoot.instance.uiGame;
            if (uiGame == null)
            {
                return;
            }

            if (uiGame.starmap.active || uiGame.dysonmap.active || uiGame.globemap.active || uiGame.escMenu.active || uiGame.techTree.active)
            {
                return;
            }

            var text = (timeText == null ? "" : timeText.ToString()) + (positionText ?? "");
            var minWidth = UiScaler.ScaleToDefault(600);
            var height = style.CalcHeight(new GUIContent(text), minWidth) + 10;

            var rect = GUILayoutUtility.GetRect(minWidth, height * 1.25f);

            if (yOffset == 0)
            {
                DetermineYOffset();
            }

            DetermineXOffset();
            GUI.Label(new Rect(xOffset, yOffset, rect.width, rect.height), text, fontSize);
        }

        private void DetermineYOffset()
        {
            yOffset = (int)(Screen.height / 10f);
        }

        private void DetermineXOffset()
        {
            var manualResearch = GameObject.Find("UI Root/Overlay Canvas/In Game/Windows/Mini Lab Panel");
            if (manualResearch != null && manualResearch.activeSelf)
            {
                xOffset = UiScaler.ScaleToDefault(250);
            }
            else
                xOffset = UiScaler.ScaleToDefault(150);
        }

        private void UpdateIncomingItems()
        {
            try
            {
                var itemLoadStates = ItemLoadState.GetLoadState();
                if (itemLoadStates == null)
                {
                    return;
                }

                if (itemLoadStates.Count > 0)
                {
                    timeText = new StringBuilder();
                    foreach (var loadState in itemLoadStates)
                    {
                        timeText.Append($"{loadState.itemName} arriving in {loadState.secondsRemaining + 5} seconds\r\n");
                    }
                }
                else
                {
                    timeText = null;
                }
            }
            catch (Exception e)
            {
                if (!loggedException)
                {
                    loggedException = true;
                    Log.Warn($"failure while updating incoming items {e.Message} {e.StackTrace}");
                }
            }
        }

        IEnumerator Loop()
        {
            while (true)
            {
                yield return new WaitForSeconds(2);
                if (!PluginConfig.inventoryManagementPaused.Value && PluginConfig.showIncomingItemProgress.Value)
                    UpdateIncomingItems();
                else
                    timeText = null;

                yield return new WaitForSeconds(2);
                if (!PluginConfig.inventoryManagementPaused.Value && PluginConfig.showNearestBuildGhostIndicator.Value)
                    AddGhostStatus();
                else
                    positionText = null;
            }
        }

        private void AddGhostStatus()
        {
            if (GameMain.localPlanet == null || GameMain.localPlanet.factory == null)
            {
                positionText = null;
                return;
            }

            var ctr = 0;
            var playerPosition = GameMain.mainPlayer.position;
            Vector3 closest = Vector3.zero;
            var closestDist = float.MaxValue;
            string closestItemName = "";
            int closestItemId = 0;
            foreach (var prebuildData in GameMain.localPlanet.factory.prebuildPool)
            {
                if (prebuildData.id < 1)
                {
                    continue;
                }

                ctr++;
                var distance = Vector3.Distance(prebuildData.pos, playerPosition);
                if (distance < closestDist && OutOfBuildRange(distance))
                {
                    closest = prebuildData.pos;
                    closestDist = distance;
                    closestItemName = ItemUtil.GetItemName(prebuildData.protoId);
                    closestItemId = prebuildData.protoId;
                }
            }

            if (closestDist < float.MaxValue && OutOfBuildRange(closestDist))
            {
                var coords = PositionToLatLonString(closest);
                var parensPart = $"(total: {ctr})";
                if (closestItemId > 0 && !InventoryManager.IsItemInInventoryOrInbound(closestItemId))
                    parensPart = "(Not available)";
                
                positionText = $"Nearest ghost at {coords}, {closestItemName} {parensPart}\r\n";
            }
            else
            {
                positionText = null;
            }
        }

        private bool OutOfBuildRange(float closestDist)
        {
            var localPlanet = GameMain.localPlanet;
            var mechaBuildArea = GameMain.mainPlayer?.mecha?.buildArea;
            if (localPlanet == null || localPlanet.type == EPlanetType.Gas)
            {
                return false;
            }

            return closestDist > mechaBuildArea;
        }

        public static string PositionToLatLonString(Vector3 position)
        {
            Maths.GetLatitudeLongitude(position, out int latd, out int latf, out int logd, out int logf, out bool north, out _, out _,
                out bool east);
            string latDir = north ? "N" : "S";
            string lonDir = east ? "E" : "W";
            var latCoord = $"{latd}° {latf}' {latDir}";

            string lonCoord = $"{logd}° {logf}' {lonDir}";
            return $"{latCoord}, {lonCoord}";
        }

        public static void ClearOffset()
        {
            yOffset = 0;
        }

        public void Unload()
        {
        }
    }
}