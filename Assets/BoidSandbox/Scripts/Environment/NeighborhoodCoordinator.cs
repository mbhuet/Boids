﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


//Should not be a static class because I want to be able to define boundaries without editing the script
//Class will manage itself if there are no instances by creating an instance
//


namespace CloudFine
{
    public class NeighborhoodCoordinator : MonoBehaviour
    {
        private static NeighborhoodCoordinator m_instance;
        public static NeighborhoodCoordinator Instance
        {
            get
            {
                if (m_instance == null)
                {
                    m_instance = FindObjectOfType<NeighborhoodCoordinator>();
                    if (m_instance == null)
                    {
                        GameObject neighborhoodObj = new GameObject("NeighborhoodCoordinator");
                        m_instance = neighborhoodObj.AddComponent<NeighborhoodCoordinator>();
                    }
                }
                return m_instance;
            }
            private set
            {
                m_instance = value;
            }
        }

        private Dictionary<int, List<Agent>> bucketToAgents = new Dictionary<int, List<Agent>>(); //get all agents in a bucket
        private Dictionary<Agent, List<int>> agentToBuckets = new Dictionary<Agent, List<int>>(); //get all buckets an agent is in

        public bool displayGizmos;
        [SerializeField]
        private Vector3Int dimensions = Vector3Int.one * 10;
        [SerializeField]
        private float cellSize = 10;

        public bool wrapEdges = true;

        public static bool HasMoved
        {
            get; private set;
        }

        private List<int> bucketsToDraw = new List<int>();

        void Awake()
        {
            if (Instance != null && Instance != this) GameObject.Destroy(this);
            else Instance = this;
        }

        private void Update()
        {
            bucketsToDraw.Clear();
        }


        public void GetSurroundings(Vector3 position, Vector3 velocity, float radius, float secondsAhead, ref List<int> buckets, out List<AgentWrapped> neighbors)
        {
            neighbors = new List<AgentWrapped>();
            if(buckets == null) buckets = new List<int>();
            else buckets.Clear();

            if (radius > 0)
            {
                GetBucketsOverlappingSphere(position, radius, ref buckets);
            }
            if (secondsAhead > 0)
            {
                GetBucketsOverlappingLine(position, position + velocity * secondsAhead, 0, ref buckets);
            }

            for (int i = 0; i < buckets.Count; i++)
            {
                if (bucketToAgents.ContainsKey(buckets[i]))
                {
                    foreach (Agent agent in bucketToAgents[buckets[i]])
                    {
                        neighbors.Add(new AgentWrapped(agent, wrapEdges ? WrapPositionRelative(agent.Position, position) : agent.Position));
                    }
                }
            }
        }


        public void BorderRepelForce(out Vector3 steer, SteeringAgent agent)
        {
            if (wrapEdges)
            {
                steer = Vector3.zero;
                return;
            }
            Vector3 futurePosition = agent.Position + agent.Velocity;
            futurePosition.x = dimensions.x > 0 ? Mathf.Clamp(futurePosition.x, cellSize, dimensions.x * cellSize - cellSize) : 0;
            futurePosition.y = dimensions.y > 0 ? Mathf.Clamp(futurePosition.y, cellSize, dimensions.y * cellSize - cellSize) : 0;
            futurePosition.z = dimensions.z > 0 ? Mathf.Clamp(futurePosition.z, cellSize, dimensions.z * cellSize - cellSize) : 0;

            float distanceToBorder = Mathf.Min(
                dimensions.x > 0 ? agent.Position.x : float.MaxValue,
                dimensions.y > 0 ? agent.Position.y : float.MaxValue,
                dimensions.z > 0 ? agent.Position.z : float.MaxValue,
                dimensions.x > 0 ? dimensions.x * cellSize - agent.Position.x : float.MaxValue,
                dimensions.y > 0 ? dimensions.y * cellSize - agent.Position.y : float.MaxValue,
                dimensions.z > 0 ? dimensions.z * cellSize - agent.Position.z : float.MaxValue);
            if (distanceToBorder <= 0) distanceToBorder = .001f;

            agent.GetSeekVector(out steer, futurePosition);
            steer *= cellSize / distanceToBorder;
        }


        public void UpdateAgentBuckets(Agent agent, out List<int> buckets)
        {
            RemoveAgentFromBuckets(agent, out buckets);
            AddAgentToBuckets(agent, out buckets);
        }

        public void RemoveAgentFromBuckets(Agent agent, out List<int> buckets)
        {
            if (agentToBuckets.TryGetValue(agent, out buckets))
            {
                for (int i = 0; i < buckets.Count; i++)
                {
                    if (bucketToAgents.ContainsKey(buckets[i]))
                    {
                        bucketToAgents[buckets[i]].Remove(agent);
                    }
                }
                agentToBuckets[agent].Clear();
            }

        }

        private void AddAgentToBuckets(Agent agent, out List<int> buckets)
        {
            if (!agentToBuckets.ContainsKey(agent))
            {
                agentToBuckets.Add(agent, new List<int>());
            }
            buckets = new List<int>();

            switch (agent.neighborType)
            {
                case Agent.NeighborType.SHERE:
                    GetBucketsOverlappingSphere(agent.Position, agent.Radius, ref buckets);
                    break;
                case Agent.NeighborType.POINT:
                    buckets = new List<int>() { GetBucketOverlappingPoint(agent.Position) };
                    break;
                case Agent.NeighborType.LINE:
                    GetBucketsOverlappingLine(agent.Position, agent.Position + agent.Forward, agent.Radius, ref buckets);
                    break;
                default:
                    buckets = new List<int>() { GetBucketOverlappingPoint(agent.Position) };
                    break;
            }
            for (int i = 0; i < buckets.Count; i++)
            {
                if (!bucketToAgents.ContainsKey(buckets[i]))
                {
                    bucketToAgents.Add(buckets[i], new List<Agent>());
                }
                bucketToAgents[buckets[i]].Add(agent);
                agentToBuckets[agent].Add(buckets[i]);
            }


        }

        public int GetBucketOverlappingPoint(Vector3 point)
        {
            return WorldPositionToHash(wrapEdges ? WrapPosition(point) : point);
        }

        public void GetBucketsOverlappingLine(Vector3 start, Vector3 end, float thickness, ref List<int> buckets)
        {
            int x0 = CellFloor(start.x);
            int x1 = CellFloor(end.x);

            int y0 = CellFloor(start.y);
            int y1 = CellFloor(end.y);

            int z0 = CellFloor(start.z);
            int z1 = CellFloor(end.z);

            float wd = thickness;
            buckets.Add(CellPositionToHash(x0, y0, z0));

            if (buckets == null) buckets = new List<int>();

            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int dz = Math.Abs(z1 - z0), sz = z0 < z1 ? 1 : -1;
            int dm = Mathf.Max(dx, dy, dz), i = dm; /* maximum difference */
            x1 = y1 = z1 = dm / 2; /* error offset */

            for (; ; )
            {  /* loop */
                int bucket = -1;
                if (!wrapEdges)
                {
                    if (x0 < 0 || x0 >= dimensions.x) break;
                    if (y0 < 0 || y0 >= dimensions.y) break;
                    if (z0 < 0 || z0 >= dimensions.z) break;
                    bucket = CellPositionToHash(x0, y0, z0);
                }

                else
                {
                    
                    bucket = (CellPositionToHash(
                        (int)Mathf.Repeat(x0, dimensions.x),
                        (int)Mathf.Repeat(y0, dimensions.y),
                        (int)Mathf.Repeat(z0, dimensions.z)
                        ));

                }
                bucketsToDraw.Add(bucket);
                buckets.Add(bucket);

                if (i-- == 0) break;


                x1 -= dx; if (x1 < 0) { x1 += dm; x0 += sx; }
                y1 -= dy; if (y1 < 0) { y1 += dm; y0 += sy; }
                z1 -= dz; if (z1 < 0) { z1 += dm; z0 += sz; }

               // Debug.Log("After " + x1 + " " + y1 + " " + z1 + " " + GetHash(x0, y0, z0));

            }

        }

        public void GetBucketsOverlappingSphere(Vector3 center, float radius, ref List<int> buckets)
        {
            int neighborhoodRadius = 1 + Mathf.FloorToInt((radius - .01f) / cellSize);
            if(buckets == null) buckets = new List<int>();
            Vector3 positionContainer;

            for (int xOff = -neighborhoodRadius; xOff < neighborhoodRadius; xOff++)
            {
                for (int yOff = -neighborhoodRadius; yOff < neighborhoodRadius; yOff++)
                {
                    for (int zOff = -neighborhoodRadius; zOff < neighborhoodRadius; zOff++)
                    {

                        positionContainer.x = (center.x + xOff * cellSize);
                        positionContainer.y = (center.y + yOff * cellSize);
                        positionContainer.z = (center.z + zOff * cellSize);
                        if (!wrapEdges)
                        {
                            if (positionContainer.x < 0 || positionContainer.x > dimensions.x * cellSize
                                || positionContainer.y < 0 || positionContainer.y > dimensions.y * cellSize
                                || positionContainer.z < 0 || positionContainer.z > dimensions.z * cellSize)
                            {
                                continue;
                            }
                            else
                            {
                                buckets.Add(WorldPositionToHash(positionContainer));
                            }
                        }
                        else
                        {
                            buckets.Add(WorldPositionToHash(WrapPosition(positionContainer)));
                        }
                    }
                }
            }

        }


        public Vector3 WrapPositionRelative(Vector3 position, Vector3 relativeTo)
        {
            // |-* |   |   |   | *-|
            if (relativeTo.x > position.x && (relativeTo.x - position.x > (position.x + dimensions.x * cellSize) - relativeTo.x))
            {
                position.x = position.x + dimensions.x * cellSize;
            }
            else if (relativeTo.x < position.x && (position.x - relativeTo.x > (relativeTo.x + dimensions.x * cellSize) - position.x))
            {
                position.x = position.x - dimensions.x * cellSize;
            }

            if (relativeTo.y > position.y && (relativeTo.y - position.y > (position.y + dimensions.y * cellSize) - relativeTo.y))
            {
                position.y = position.y + dimensions.y * cellSize;
            }
            else if (relativeTo.y < position.y && (position.y - relativeTo.y > (relativeTo.y + dimensions.y * cellSize) - position.y))
            {
                position.y = position.y - dimensions.y * cellSize;
            }

            if (relativeTo.z > position.z && (relativeTo.z - position.z > (position.z + dimensions.z * cellSize) - relativeTo.z))
            {
                position.z = position.z + dimensions.z * cellSize;
            }
            else if (relativeTo.z < position.z && (position.z - relativeTo.z > (relativeTo.z + dimensions.z * cellSize) - position.z))
            {
                position.z = position.z - dimensions.z * cellSize;
            }
            return position;
        }


        public void ValidatePosition(ref Vector3 position)
        {
            if (wrapEdges) position = WrapPosition(position);
            else
            {
                if (position.x < 0) position.x = 0;
                else if (position.x > dimensions.x * cellSize) position.x = dimensions.x * cellSize;
                if (position.y < 0) position.y = 0;
                else if (position.y > dimensions.y * cellSize) position.y = dimensions.y * cellSize;
                if (position.z < 0) position.z = 0;
                else if (position.z > dimensions.z * cellSize) position.z = dimensions.z * cellSize;
            }
        }

        private Vector3 WrapPosition(Vector3 position)
        {
            if (dimensions.x == 0)
            {
                position.x = 0;
            }
            else if (position.x < 0)
            {
                position.x = dimensions.x * cellSize + position.x;
            }
            else if (position.x > dimensions.x * cellSize)
            {
                position.x = position.x % (dimensions.x * cellSize);
            }

            if (dimensions.y == 0)
            {
                position.y = 0;
            }
            else if (position.y < 0)
            {
                position.y = dimensions.y * cellSize + position.y;
            }
            else if (position.y > dimensions.y * cellSize)
            {
                position.y = position.y % (dimensions.y * cellSize);
            }

            if (dimensions.z == 0)
            {
                position.z = 0;
            }
            else if (position.z < 0)
            {
                position.z = dimensions.z * cellSize + position.z;
            }
            else if (position.z > dimensions.z * cellSize)
            {
                position.z = position.z % (dimensions.z * cellSize);
            }


            return position;
        }


        public Vector3 RandomPosition()
        {
            return new Vector3(
               UnityEngine.Random.Range(0, dimensions.x * cellSize),
               UnityEngine.Random.Range(0, dimensions.y * cellSize),
               UnityEngine.Random.Range(0, dimensions.z * cellSize)
             );
        }

        private int CellFloor(float p)
        {
            return Mathf.FloorToInt(p / cellSize);
        }

        private int CellPositionToHash(int x, int y, int z)
        {
            if (x < 0 || y < 0 || z < 0) return -1;

            return (
                 x
               + y * (dimensions.x + 1) // +1 in case dimension is 0, will still produce unique hash
               + z * (dimensions.x + 1) * (dimensions.y + 1));
        }

        private int WorldPositionToHash(float x, float y, float z)
        {
            if (x < 0 || y < 0 || z < 0) return -1;
            return (int)(
                 Mathf.Floor(x / cellSize)
               + Mathf.Floor(y / cellSize) * (dimensions.x + 1) // +1 in case dimension is 0, will still produce unique hash
               + Mathf.Floor(z / cellSize) * (dimensions.x + 1) * (dimensions.y + 1));
        }

        private int WorldPositionToHash(Vector3 position)
        {
            return WorldPositionToHash(position.x, position.y, position.z);
        }

#if UNITY_EDITOR

        private void OnDrawGizmos()
        {
            if (displayGizmos) DrawNeighborHoods();
        }

        void DrawNeighborHoods()
        {
            if (bucketToAgents == null) return;
            Gizmos.color = Color.grey;
            Gizmos.matrix = this.transform.localToWorldMatrix;
            Gizmos.DrawWireCube((Vector3)dimensions * (cellSize / 2f), (Vector3)dimensions * cellSize);
            Gizmos.color = Color.grey * .1f;


            for (int x = 0; x < (dimensions.x > 0 ? dimensions.x : 1); x++)
            {
                for (int y = 0; y < (dimensions.y > 0 ? dimensions.y : 1); y++)
                {
                    for (int z = 0; z < (dimensions.z > 0 ? dimensions.z : 1); z++)
                    {

                        Vector3 corner = new Vector3(x, y, z) * cellSize;
                        int bucket = WorldPositionToHash(corner);

                        if (bucketsToDraw.Contains(bucket))
                        {
                            Gizmos.color = Color.red * .8f;
                            Gizmos.DrawCube(corner + Vector3.one * (cellSize / 2f), Vector3.one * cellSize);
                        }
                        else if (bucketToAgents.ContainsKey(bucket) && bucketToAgents[bucket].Count > 0)
                        {
                            Gizmos.color = Color.grey * .1f;
                            Gizmos.DrawCube(corner + Vector3.one * (cellSize / 2f), Vector3.one * cellSize);
                        }
                    }
                }
            }
        }
#endif

    }
}
