﻿using BepInEx.Logging;
using EFT;
using SAIN.Classes;
using SAIN.Helpers;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using static SAIN.UserSettings.CoverConfig;

namespace SAIN.Components
{
    public class CoverFinderComponent : MonoBehaviour
    {
        private Collider[] Colliders;
        public List<CoverPoint> CoverPoints { get; private set; } = new List<CoverPoint>();

        private void Awake()
        {
            SAIN = GetComponent<SAINComponent>();
            Logger = BepInEx.Logging.Logger.CreateLogSource(this.GetType().Name);
            Path = new NavMeshPath();
        }

        private void Update()
        {
            if (DebugCoverFinder.Value)
            {
                if (CoverPoints.Count > 0)
                {
                    DebugGizmos.SingleObjects.Line(CoverPoints.PickRandom().Position, SAIN.HeadPosition, Color.yellow, 0.035f, true, 0.1f);
                }
            }
        }

        public void LookForCover(Vector3 targetPosition, Vector3 originPoint)
        {
            TargetPosition = targetPosition;
            OriginPoint = originPoint;

            if (SAIN.Decision.CurrentSelfDecision == SAINSelfDecision.RunAwayGrenade)
            {
                MinObstacleHeight = 1.5f;
                MinEnemyDist = 10f;
            }
            else
            {
                MinObstacleHeight = CoverMinHeight.Value;
                MinEnemyDist = CoverMinEnemyDistance.Value;
            }

            if (TakeCoverCoroutine == null)
            {
                TakeCoverCoroutine = StartCoroutine(FindCover());
            }
        }

        public void StopLooking()
        {
            if (TakeCoverCoroutine != null)
            {
                StopCoroutine(TakeCoverCoroutine);
                TakeCoverCoroutine = null;
            }
        }

        public CoverPoint FallBackPoint { get; private set; }

        private IEnumerator FindCover()
        {
            while (true)
            {
                UpdateSpotted();
                ClearOldPoints();
                GetColliders(out int hits);

                int frameWait = 0;
                int totalChecked = 0;

                for (int i = 0; i < hits; i++)
                {
                    totalChecked++;
                    if (CheckCollider(Colliders[i], out var newPoint))
                    {
                        CoverPoints.Add(newPoint);
                    }

                    // Every 10 colliders checked, wait until the next frame before continuing.
                    if (frameWait == 5)
                    {
                        frameWait = 0;
                        yield return null;
                    }
                    frameWait++;

                    if (CoverPoints.Count > 2)
                    {
                        break;
                    }
                }

                if (CoverPoints.Count > 0)
                {
                    FindFallback();

                    if (DebugLogTimer < Time.time && DebugCoverFinder.Value)
                    {
                        DebugLogTimer = Time.time + 1f;
                        Logger.LogInfo($"[{BotOwner.name}] - Found [{CoverPoints.Count}] CoverPoints. Colliders checked: [{totalChecked}] Collider Array Size = [{hits}]");
                    }
                }
                else
                {
                    if (DebugLogTimer < Time.time && DebugCoverFinder.Value)
                    {
                        DebugLogTimer = Time.time + 1f;
                        Logger.LogWarning($"[{BotOwner.name}] - No Cover Found! Valid Colliders checked: [{totalChecked}] Collider Array Size = [{hits}]");
                    }
                }
                yield return new WaitForSeconds(CoverUpdateFrequency.Value);
            }
        }

        private void FindFallback()
        {
            if (CoverPoints.Count > 0)
            {
                float highest = 0f;
                int highestIndex = 0;
                for (int i = 0; i < CoverPoints.Count; i++)
                {
                    float Y = CoverPoints[i].Collider.bounds.size.y;
                    if (Y > highest)
                    {
                        highest = Y;
                        highestIndex = i;
                    }
                }
                FallBackPoint = CoverPoints[highestIndex];
            }
        }

        private bool CheckCollider(Collider collider, out CoverPoint newPoint)
        {
            const float ExtendLengthThresh = 1.5f;

            newPoint = null;
            if (collider == null || collider.bounds.size.y < MinObstacleHeight || !ColliderDirection(collider))
            {
                return false;
            }

            Vector3 colliderPos = collider.transform.position;

            // The botToCorner from the target to the collider
            Vector3 colliderDir = (colliderPos - TargetPosition).normalized;
            colliderDir.y = 0f;

            if (collider.bounds.size.z > ExtendLengthThresh && collider.bounds.size.x > ExtendLengthThresh)
            {
                colliderDir *= ExtendLengthThresh;
            }

            // a farPoint on opposite side of the target
            Vector3 farPoint = colliderPos + colliderDir;

            // the closest edge to that farPoint
            if (NavMesh.SamplePosition(farPoint, out var hit, 1f, -1))
            {
                if (CheckPath(hit.position))
                {
                    if (CheckPosition(hit.position))
                    {
                        newPoint = new CoverPoint(BotOwner, hit.position, collider);
                    }
                }
            }

            return newPoint != null;
        }

        private bool ColliderDirection(Collider collider)
        {
            Vector3 pos = collider.transform.position;
            Vector3 target = TargetPosition;
            Vector3 bot = BotOwner.Position;

            Vector3 directionToTarget = target - bot;
            float targetDist = directionToTarget.magnitude;

            Vector3 directionToCollider = pos - bot;
            float colliderDist = directionToCollider.magnitude;

            float dot = Vector3.Dot(directionToTarget.normalized, directionToCollider.normalized);

            if (dot <= 0.33f)
            {
                return true;
            }
            if (dot <= 0.6f)
            {
                return colliderDist < targetDist * 2f;
            }
            if (dot <= 0.8f)
            {
                return colliderDist < targetDist * 1.5f;
            }
            return colliderDist < targetDist;
        }

        private float MinEnemyDist;

        private bool CheckPosition(Vector3 position)
        {
            if (SpottedPoints.Count > 0)
            {
                foreach (var point in SpottedPoints)
                {
                    if (!point.IsValidAgain && point.TooClose(position))
                    {
                        return false;
                    }
                }
            }
            if (CheckPositionVsOtherBots(position))
            {
                if ((position - TargetPosition).magnitude > MinEnemyDist)
                {
                    if (VisibilityCheck(position))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool CheckPath(Vector3 position)
        {
            Path.ClearCorners();
            if (NavMesh.CalculatePath(OriginPoint, position, -1, Path) && Path.status == NavMeshPathStatus.PathComplete)
            {
                if (PathToEnemy(Path))
                {
                    return true;
                }
            }

            return false;
        }

        private NavMeshPath Path;

        private bool PathToEnemy(NavMeshPath path)
        {
            for (int i = 1; i < path.corners.Length - 1; i++)
            {
                var corner = path.corners[i];
                Vector3 cornerToTarget = TargetPosition - corner;
                Vector3 botToTarget = TargetPosition - OriginPoint;
                Vector3 botToCorner = corner - OriginPoint;

                if (cornerToTarget.magnitude < 0.5f)
                {
                    if (DebugCoverFinder.Value)
                    {
                        //DebugGizmos.SingleObjects.Ray(OriginPoint, corner - OriginPoint, Color.red, (corner - OriginPoint).magnitude, 0.05f, true, 30f);
                    }

                    return false;
                }

                if (i == 1)
                {
                    if (Vector3.Dot(botToCorner.normalized, botToTarget.normalized) > 0.75f)
                    {
                        if (DebugCoverFinder.Value)
                        {
                            //DebugGizmos.SingleObjects.Ray(corner, cornerToTarget, Color.red, cornerToTarget.magnitude, 0.05f, true, 30f);
                        }
                        return false;
                    }
                }
                else if (i < path.corners.Length - 2)
                {
                    Vector3 cornerB = path.corners[i + 1];
                    Vector3 directionToNextCorner = cornerB - corner;

                    if (Vector3.Dot(cornerToTarget.normalized, directionToNextCorner.normalized) > 0.75f)
                    {
                        if (directionToNextCorner.magnitude > cornerToTarget.magnitude)
                        {
                            if (DebugCoverFinder.Value)
                            {
                                //DebugGizmos.SingleObjects.Ray(corner, cornerToTarget, Color.red, cornerToTarget.magnitude, 0.05f, true, 30f);
                            }
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        private float DebugLogTimer = 0f;

        public List<SpottedCoverPoint> SpottedPoints { get; private set; } = new List<SpottedCoverPoint>();
        private readonly List<SpottedCoverPoint> ReCheckList = new List<SpottedCoverPoint>();

        private void UpdateSpotted()
        {
            if (SpottedPoints.Count > 0)
            {
                ReCheckList.AddRange(SpottedPoints);
                for (int j = ReCheckList.Count - 1; j >= 0; j--)
                {
                    var spotted = ReCheckList[j];
                    if (spotted != null)
                    {
                        if (spotted.IsValidAgain)
                        {
                            SpottedPoints.RemoveAt(j);
                        }
                    }
                }
                ReCheckList.Clear();
            }
        }

        private void ClearOldPoints()
        {
            CoverPoint PointToSave = null;
            if (CoverPoints.Count > 0)
            {
                for (int i = 0; i < CoverPoints.Count; i++)
                {
                    var point = CoverPoints[i];
                    if (point != null)
                    {
                        if (point.Spotted)
                        {
                            SpottedPoints.Add(new SpottedCoverPoint(point.Position));
                            continue;
                        }
                        if (point.BotIsUsingThis)
                        {
                            if (CheckCollider(point.Collider, out var newPoint))
                            {
                                PointToSave = newPoint;
                                PointToSave.BotIsUsingThis = true;
                                PointToSave.HitInCoverCount = point.HitInCoverCount;
                                if (PointToSave.Collider.bounds.size.y >= 1.5f)
                                {
                                    FallBackPoint = newPoint;
                                }
                                else
                                {
                                    FallBackPoint = null;
                                }
                            }
                        }
                    }
                }
            }
            CoverPoints.Clear();
            if (PointToSave != null)
            {
                CoverPoints.Add(PointToSave);
            }
        }

        public bool CheckPositionVsOtherBots(Vector3 position)
        {
            if (SAIN.Squad.SquadLocations == null || SAIN.Squad.SquadMembers == null || SAIN.Squad.SquadMembers.Count < 2)
            {
                return true;
            }

            const float DistanceToBotCoverThresh = 1f;

            foreach (var member in SAIN.Squad.SquadMembers.Values)
            {
                if (member != null && member.BotOwner != BotOwner)
                {
                    if (member.Cover.CurrentCoverPoint != null)
                    {
                        if (Vector3.Distance(position, member.Cover.CurrentCoverPoint.Position) < DistanceToBotCoverThresh)
                        {
                            return false;
                        }
                    }
                    if (member.Cover.FallBackPoint != null)
                    {
                        if (Vector3.Distance(position, member.Cover.FallBackPoint.Position) < DistanceToBotCoverThresh)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        private bool VisibilityCheck(Vector3 position)
        {
            const float offset = 0.15f;

            Vector3 target = TargetPosition;

            if (CheckRayCast(position, target))
            {
                Vector3 enemyDirection = target - position;
                enemyDirection = enemyDirection.normalized * offset;

                Quaternion right = Quaternion.Euler(0f, 90f, 0f);
                Vector3 rightPoint = right * enemyDirection;
                rightPoint += position;

                if (CheckRayCast(rightPoint, target))
                {
                    Quaternion left = Quaternion.Euler(0f, -90f, 0f);
                    Vector3 leftPoint = left * enemyDirection;
                    leftPoint += position;

                    if (CheckRayCast(leftPoint, target))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool CheckRayCast(Vector3 point, Vector3 target, float distance = 3f)
        {
            point.y += 0.66f;
            target.y += 0.66f;
            Vector3 direction = target - point;
            return Physics.Raycast(point, direction, distance, LayerMaskClass.HighPolyWithTerrainMask);
        }

        private void GetColliders(out int hits)
        {
            const float CheckDistThresh = 100f;
            const float ColliderSortDistThresh = 25f;

            float distance = (LastCheckPos - OriginPoint).sqrMagnitude;
            if (Colliders == null || distance > CheckDistThresh)
            {
                if (Colliders == null)
                {
                    Colliders = new Collider[500];
                }
                LastCheckPos = OriginPoint;
                GetNewColliders(out hits);
                LastHitCount = hits;
                return;
            }
            else if (distance > ColliderSortDistThresh)
            {
                System.Array.Sort(Colliders, ColliderArraySortComparer);
            }

            hits = LastHitCount;
        }

        private Vector3 LastCheckPos;

        private void ClearColliders()
        {
            for (int i = 0; i < Colliders.Length; i++)
            {
                Colliders[i] = null;
            }
        }

        private void GetNewColliders(out int hits)
        {
            var mask = LayerMaskClass.HighPolyWithTerrainMask;
            var orientation = Quaternion.identity;

            const float width = 25f;
            const float height = 3f;

            Vector3 box = new Vector3(width, height, width);
            ClearColliders();
            hits = Physics.OverlapBoxNonAlloc(OriginPoint, box, Colliders, orientation, mask);
            hits = FilterColliders(hits);

            System.Array.Sort(Colliders, ColliderArraySortComparer);
        }

        private int FilterColliders(int hits)
        {
            int hitReduction = 0;
            for (int i = 0; i < hits; i++)
            {
                if (Colliders[i].bounds.size.y < 0.66)
                {
                    Colliders[i] = null;
                    hitReduction++;
                }
                else if (Colliders[i].bounds.size.x < 0.1f && Colliders[i].bounds.size.z < 0.1f)
                {
                    Colliders[i] = null;
                    hitReduction++;
                }
            }
            hits -= hitReduction;
            return hits;
        }

        private int LastHitCount = 0;

        public int ColliderArraySortComparer(Collider A, Collider B)
        {
            if (A == null && B != null)
            {
                return 1;
            }
            else if (A != null && B == null)
            {
                return -1;
            }
            else if (A == null && B == null)
            {
                return 0;
            }
            else
            {
                float AMag = (OriginPoint - A.transform.position).sqrMagnitude;
                float BMag = (OriginPoint - B.transform.position).sqrMagnitude;
                return AMag.CompareTo(BMag);
            }
        }

        public void OnDestroy()
        {
            StopAllCoroutines();
        }

        private BotOwner BotOwner => SAIN.BotOwner;
        private SAINComponent SAIN;
        protected ManualLogSource Logger;
        private float MinObstacleHeight;
        private Coroutine TakeCoverCoroutine;
        private Vector3 OriginPoint;
        public Vector3 TargetPosition { get; private set; }

        public class SpottedCoverPoint
        {
            public SpottedCoverPoint(Vector3 position, float expireTime = 2f)
            {
                ExpireTime = expireTime;
                Position = position;
                TimeCreated = Time.time;
            }

            public bool TooClose(Vector3 newPos, float sqrdist = 3f)
            {
                return (Position - newPos).sqrMagnitude > sqrdist;
            }

            public Vector3 Position { get; private set; }
            public float TimeCreated { get; private set; }
            public float TimeSinceCreated => Time.time - TimeCreated;

            private readonly float ExpireTime;
            public bool IsValidAgain => TimeSinceCreated > ExpireTime;
        }
    }
}