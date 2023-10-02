﻿using BepInEx.Logging;
using EFT;
using HarmonyLib;
using SAIN.Components;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using System;
using SAIN.Preset.GlobalSettings;
using SAIN.SAINComponent;
using SAIN.SAINComponent.Classes.Decision;
using SAIN.SAINComponent.Classes.Talk;
using SAIN.SAINComponent.Classes.WeaponFunction;
using SAIN.SAINComponent.Classes.Mover;
using SAIN.SAINComponent.Classes;
using SAIN.SAINComponent.SubComponents;
using Comfort.Common;
using UnityEngine.Audio;
using System.IO;
using System.Threading.Tasks;
using SAIN.Helpers;
using UnityEngine.Networking;

namespace SAIN.Components
{
    public class PropertyNames
    {
        public static string PlayerSpirit = "PlayerSpiritAura";
        public static string Memory = "Memory";
        public static string GoalEnemy = "GoalEnemy";
        public static string ShootData = "ShootData";
        public static string CanShootByState = "CanShootByState";
        public static string IsVisible = "IsVisible";
    }

    public class SAINNoBushESP : MonoBehaviour
    {
        static SAINNoBushESP()
        {
            Logger = BepInEx.Logging.Logger.CreateLogSource(nameof(SAINNoBushESP));
            NoBushMask = LayerMaskClass.HighPolyWithTerrainMaskAI | (1 << LayerMask.NameToLayer(PropertyNames.PlayerSpirit));

            Type botType = typeof(BotOwner);

            Type memoryType = AccessTools.Field(
                botType, PropertyNames.Memory).FieldType;

            GoalEnemyProp = AccessTools.Property(
                memoryType, PropertyNames.GoalEnemy);

            IsVisibleProp = AccessTools.Property(
                GoalEnemyProp.PropertyType, PropertyNames.IsVisible);

            Type shootDataType = AccessTools.Property(
                botType, PropertyNames.ShootData).PropertyType;

            CanShootByState = AccessTools.PropertySetter(
                shootDataType, PropertyNames.CanShootByState);
            try
            {
            }
            catch (Exception e)
            {
                Logger.LogError(e);
                return;
            }
        }

        private static readonly PropertyInfo GoalEnemyProp;
        private static readonly PropertyInfo IsVisibleProp;
        private static readonly MethodInfo CanShootByState;

        private BotOwner BotOwner;
        private SAINComponentClass SAIN;

        private AudioClip SixthSenseAudioClip;

        public void Init(BotOwner botOwner, SAINComponentClass sain = null)
        {
            BotOwner = botOwner;
            SAIN = sain;
            SixthSenseAudioClip = null;

            LoadAudioClip();
        }

        async private void LoadAudioClip()
        {
            string path = Path.Combine(JsonUtility.GetSAINPluginPath(), "SixthSense.ogg");
            SixthSenseAudioClip = await RequestAudioClip(path);

            if (SixthSenseAudioClip == null && DebugMode)
            {
                Logger.LogDebug($"Unable to find AudioClip at [{path}].");
            }
            else
            {
                Logger.LogDebug($"Audio clip at path [{path}] loaded.");
            }
        }

        async static Task<AudioClip> RequestAudioClip(string path)
        {
            AudioType audioType = AudioType.OGGVORBIS;

            UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(path, audioType);
            var SendWeb = request.SendWebRequest();

            while(!SendWeb.isDone)
            {
                await Task.Yield();
            }

            if (request.isNetworkError || request.isHttpError)
            {
                return null;
            }
            else
            {
                AudioClip audioClip = DownloadHandlerAudioClip.GetContent(request);
                return audioClip;
            }
        }

        private static GeneralSettings GeneralSettings => SAINPlugin.LoadedPreset?.GlobalSettings?.General;
        private static bool UserToggle => GeneralSettings?.NoBushESPToggle == true;
        private static bool EnhancedChecks => GeneralSettings?.NoBushESPEnhanced == true;
        private static float EnhancedRatio => GeneralSettings == null ? 0.5f : GeneralSettings.NoBushESPEnhancedRatio;
        private static float Frequency => GeneralSettings == null ? 0.1f : GeneralSettings.NoBushESPFrequency;
        private static bool DebugMode => GeneralSettings?.NoBushESPDebugMode == true;
        private static bool SixthSense => GeneralSettings?.SixthSense == true;
        private static float SixthSenseCooldown => GeneralSettings == null ? 5f : GeneralSettings.SixthSenseCooldownTimer;
        private static float SixthSenseVolume => GeneralSettings == null ? 25f : GeneralSettings.SixthSenseVolume;

        private static readonly ManualLogSource Logger;

        private void Update()
        {
            if (BotOwner == null || !UserToggle)
            {
                NoBushESPActive = false;
                return;
            }

            if (NoBushTimer < Time.time)
            {
                NoBushTimer = Time.time + Frequency;
                bool active = NoBushESPCheck();
                SetCanShoot(active);
            }
        }

        public bool NoBushESPActive { get; private set; } = false;

        private float NoBushTimer = 0f;
        private float SixthSenseTimer = 0f;

        private Vector3 HeadPosition => BotOwner.LookSensor._headPoint;

        public bool NoBushESPCheck()
        {
            var enemy = BotOwner?.Memory?.GoalEnemy;
            if (enemy != null && (enemy.IsVisible || enemy.CanShoot))
            {
                Player player = enemy?.Person as Player;
                if (player?.IsYourPlayer == true && enemy.IsVisible)
                {
                    if (DebugMode)
                    {
                        Logger.LogDebug($"PLAYER IN SIGHT ! CHECKING SIXTH SENSE [{Time.time}/{SixthSenseTimer}]");
                    }
                    if (SixthSense && Time.time > SixthSenseTimer)
                    {
                        AudioSource source = player.GetOrAddComponent<AudioSource>();
                        SixthSenseTimer = Time.time + SixthSenseCooldown;

                        if (DebugMode)
                        {
                            Logger.LogDebug($"DRING DRING");
                            Logger.LogDebug($"Source : [{source}]");
                            Logger.LogDebug($"Clip : [{SixthSenseAudioClip}]");
                        }

                        source.PlayOneShot(SixthSenseAudioClip, ((float)SixthSenseVolume / 100f));
                    }
                    if (EnhancedChecks)
                    {
                        return NoBushESPCheckEnhanced(player);
                    }
                    else
                    {
                        return NoBushESPCheck(player);
                    }
                }
            }
            return false;
        }

        public bool NoBushESPCheck(IAIDetails player)
        {
            Vector3 partPos = player.MainParts[BodyPartType.body].Position;
            bool active = RayCast(partPos, HeadPosition);


            return active;
        }

        public bool NoBushESPCheckEnhanced(IAIDetails player)
        {
            int hitCount = 0;
            int partCount = player.MainParts.Count;
            Vector3 start = HeadPosition;
            foreach (var part in player.MainParts)
            {
                if (RayCast(part.Value.Position, start))
                {
                    hitCount++;
                }
            }
            float ratio = (float)hitCount / partCount;
            bool active = ratio >= EnhancedRatio;
            if (active && DebugMode)
            {
                Logger.LogDebug($"Enhanced Active: [{ratio}] visible from hit count: [{hitCount}] / [{partCount}]. Config Value: [{EnhancedRatio}]");
            }
            return active;
        }

        private static bool RayCast(Vector3 end, Vector3 start)
        {
            Vector3 direction = end - start;
            if (Physics.Raycast(start, direction.normalized, out var hit, direction.magnitude, NoBushMask))
            {
                GameObject hitObject = hit.transform?.parent?.gameObject;
                if (hitObject != null)
                {
                    string hitName = hitObject?.name?.ToLower();
                    foreach (string exclusion in ExclusionList)
                    {
                        if (hitName.Contains(exclusion))
                        {
                            if (DebugMode)
                            {
                                Logger.LogDebug(exclusion);
                            }
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public void SetCanShoot(bool blockShoot)
        {
            NoBushESPActive = blockShoot;
            if (blockShoot)
            {
                var enemy = BotOwner?.Memory?.GoalEnemy;
                if (enemy != null)
                {
                    if (DebugMode)
                    {
                        Logger.LogDebug("No Bush ESP active");
                    }

                    enemy.SetCanShoot(false);

                    IsVisibleProp.SetValue(enemy, false);

                    BotOwner.AimingData?.LoseTarget();
                    BotOwner.ShootData?.EndShoot();

                    // Use reflection to set the blockShoot of the property
                    var shoot = BotOwner.ShootData;
                    if (shoot != null)
                    {
                        //CanShootByState.Invoke(shoot, new object[] { false });
                    }
                    var vision = SAIN?.Enemy?.Vision;
                    if (vision != null)
                    {
                        vision.UpdateCanShoot(false);
                        vision.UpdateVisible(false);
                    }
                }
            }
        }

        private static readonly LayerMask NoBushMask;
        private static readonly List<string> ExclusionList = new List<string> 
        { "filbert", "fibert", "tree", "pine", "plant", "birch", "collider", "timber", "spruce", "bush", "metal", "wood", "grass" };
    }
}