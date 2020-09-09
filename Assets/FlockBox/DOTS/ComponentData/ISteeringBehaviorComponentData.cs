﻿using Unity.Entities;
using Unity.Mathematics;

namespace CloudFine.FlockBox.DOTS
{
    public interface ISteeringBehaviorComponentData
    {
        float3 CalculateSteering(AgentData mine, SteeringData steering, DynamicBuffer<NeighborData> neighbors);
        PerceptionData AddPerceptionRequirements(AgentData mine, PerceptionData perception);
    }
}