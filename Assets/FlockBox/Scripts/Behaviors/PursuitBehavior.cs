﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CloudFine.FlockBox
{
    [System.Serializable]
    public class PursuitBehavior : SeekBehavior
    {
        public override void GetSteeringBehaviorVector(out Vector3 steer, SteeringAgent mine, SurroundingsContainer surroundings)
        {
            if (!mine.HasAgentProperty(targetIDAttributeName)) mine.SetAgentProperty(targetIDAttributeName, -1);
            int chosenTargetID = mine.GetAgentProperty<int>(targetIDAttributeName);

            HashSet<Agent> allTargets = GetFilteredAgents(surroundings, this);

            if (allTargets.Count == 0)
            {
                if (mine.HasPursuitTarget())
                {
                    mine.DisengagePursuit(chosenTargetID);
                }
                steer = Vector3.zero;
                return;
            }

            Agent closestTarget = ClosestPursuableTarget(allTargets, mine);

            if (!closestTarget || !closestTarget.CanBeCaughtBy(mine))
            {
                if (mine.HasPursuitTarget())
                {
                    mine.DisengagePursuit(chosenTargetID);
                }
                steer = Vector3.zero;
                return;
            }

            if (closestTarget.agentID != chosenTargetID)
            {
                mine.DisengagePursuit(chosenTargetID);
                mine.EngagePursuit(closestTarget);
            }

            Vector3 distance = closestTarget.Position - mine.Position;
            float est_timeToIntercept = distance.magnitude / mine.activeSettings.maxSpeed;
            Vector3 predictedInterceptPosition = closestTarget.Position + closestTarget.Velocity * est_timeToIntercept;

            mine.AttemptCatch(closestTarget);

            mine.GetSeekVector(out steer, predictedInterceptPosition);
        }
    }
}