﻿using BepInEx.Logging;
using DrakiaXYZ.BigBrain.Brains;
using EFT;
using Movement.Components;
using UnityEngine;
using static Movement.UserSettings.Debug;
using SAIN_Helpers;

namespace SAIN.Movement.Layers
{
    namespace DogFight
    {
        internal class DogFightLogic : CustomLogic
        {
            public DogFightLogic(BotOwner bot) : base(bot)
            {
                Logger = BepInEx.Logging.Logger.CreateLogSource(this.GetType().Name);
                Targeting = new UpdateTarget(bot);
                Move = new UpdateMove(bot);
                Steering = new UpdateSteering(bot);
                Decisions = new BotDecision(bot);
            }

            public override void Start()
            {
                BotOwner.PatrollingData.Pause();
            }

            public override void Stop()
            {
                BotOwner.PatrollingData.Unpause();
            }

            public override void Update()
            {
                if (VisionCheckTimer < Time.time)
                {
                    VisionCheckTimer = Time.time + 0.1f;
                    CanShootEnemy = ShootCheck;
                    CanSeeEnemy = SeeCheck;
                }

                Decisions.GetDecision();

                Move.ManualUpdate(CanSeeEnemy, CanShootEnemy);

                Steering.ManualUpdate();

                if (!BotOwner.WeaponManager.HaveBullets)
                {
                    BotOwner.WeaponManager.Reload.TryReload();
                }

                if (BotOwner.WeaponManager.Reload.Reloading || BotOwner.Medecine.FirstAid.Using || !BotOwner.WeaponManager.HaveBullets)
                {
                    return;
                }

                if (CanSeeEnemy)
                {
                    Targeting.ManualUpdate();
                }
            }

            private float VisionCheckTimer = 0f;
            private readonly UpdateTarget Targeting;
            private readonly UpdateMove Move;
            private readonly UpdateSteering Steering;
            public readonly BotDecision Decisions;

            public bool CanSeeEnemy;
            public bool CanShootEnemy;
            public bool ShootCheck
            {
                get
                {
                    if (BotOwner.Memory.GoalEnemy != null)
                    {
                        Vector3 weaponPos = BotOwner.WeaponRoot.position;
                        foreach (var part in BotOwner.Memory.GoalEnemy.AllActiveParts.Keys)
                        {
                            Vector3 direction = part.Position - weaponPos;
                            if (!Physics.Raycast(weaponPos, direction, direction.magnitude, LayerMaskClass.HighPolyWithTerrainMaskAI))
                            {
                                if (DebugMode)
                                {
                                    DebugDrawer.Line(weaponPos, part.Position, 0.025f, Color.red, 0.25f);
                                }

                                return true;
                            }
                        }
                    }
                    return false;
                }
            }
            public bool SeeCheck
            {
                get
                {
                    if (BotOwner.Memory.GoalEnemy != null)
                    {
                        Vector3 headPos = BotOwner.LookSensor._headPoint;
                        foreach (var part in BotOwner.Memory.GoalEnemy.AllActiveParts.Keys)
                        {
                            Vector3 direction = part.Position - headPos;
                            if (!Physics.Raycast(headPos, direction, direction.magnitude, LayerMaskClass.HighPolyWithTerrainMaskAI))
                            {
                                if (DebugMode)
                                {
                                    DebugDrawer.Line(headPos, part.Position, 0.025f, Color.cyan, 0.25f);
                                }

                                return true;
                            }
                        }
                    }
                    return false;
                }
            }
            public bool EnemyIsNull => BotOwner.Memory.GoalEnemy == null;
            public bool CanShootEnemyAndVisible => ShootCheck && SeeCheck;
            public bool DebugMode => DebugDogFightLayer.Value;
            public bool DebugDrawPoints => DebugDogFightLayerDraw.Value;

            public ManualLogSource Logger;
        }
    }
}