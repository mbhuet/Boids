﻿using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace CloudFine
{
    [System.Serializable]
    public class SeparationBehavior : RadialSteeringBehavior, IConvertToComponentData
    {


        public override void GetSteeringBehaviorVector(out Vector3 steer, SteeringAgent mine, SurroundingsContainer surroundings)
        {
            steer = Vector3.zero;
            int count = 0;
            foreach (Agent other in GetFilteredAgents(surroundings, this))
            {

                if (WithinEffectiveRadius(mine, other))
                {
                    Vector3 diff = mine.Position - other.Position;
                    if (diff.sqrMagnitude < .001f) diff = UnityEngine.Random.insideUnitCircle * .01f;
                    steer += (diff.normalized / diff.magnitude);
                    count++;
                }
            }
            if (count > 0)
            {
                steer /= ((float)count);
            }

            if (steer.magnitude > 0)
            {
                mine.GetSteerVector(out steer, steer);
            }
        }

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddComponentData(entity, new SeparationData { Radius = effectiveRadius, TagMask = TagMaskUtility.GetTagMask(filterTags) });
        }
    }
}