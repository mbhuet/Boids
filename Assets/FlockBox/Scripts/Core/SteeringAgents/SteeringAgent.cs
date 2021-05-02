﻿using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using CloudFine.FlockBox.DOTS;

namespace CloudFine.FlockBox
{
    [System.Serializable]
    public class SteeringAgent : Agent, IConvertGameObjectToEntity
    {

        public Vector3 Acceleration { get; private set; }

        protected float speedThrottle = 1;

        public BehaviorSettings activeSettings;
        private bool freezePosition = false;
        private bool sleeping = false;
        private SurroundingsContainer mySurroundings = new SurroundingsContainer();

        protected virtual void Update()
        {
            if (!isAlive) return;
            if (activeSettings == null) return;
            if (freezePosition) return;
            sleeping = (UnityEngine.Random.value < FlockBox.sleepChance);
            if(!sleeping){
                Acceleration *= 0;
                activeSettings.AddPerceptions(this, mySurroundings);
                FlockBox.GetSurroundings(Position, Velocity, cells, mySurroundings);
                Flock(mySurroundings);
            }
            Contain();
            ApplySteeringAcceleration();
        }


        protected override void LateUpdate()
        {
            if (!isAlive) return;
            if (sleeping) return;
            FindOccupyingCells();
        }


        void ApplyForce(Vector3 force)
        {
            // We could add mass here if we want A = F / M
            Acceleration += (force);
        }


        private Vector3 steerBuffer = Vector3.zero;

        void Flock(SurroundingsContainer surroundings)
        {
            foreach (SteeringBehavior behavior in activeSettings.Behaviors)
            {
                if (!behavior.IsActive) continue;
                behavior.GetSteeringBehaviorVector(out steerBuffer, this, surroundings);
                steerBuffer *= behavior.weight;
                if (behavior.DrawSteering) Debug.DrawRay(transform.position, FlockBox.transform.TransformDirection(steerBuffer), behavior.debugColor);
                ApplyForce(steerBuffer);
            }
        }

        void Contain()
        {
            if (!FlockBox.wrapEdges)
            {
                activeSettings.Containment.GetSteeringBehaviorVector(out steerBuffer, this, FlockBox.WorldDimensions, FlockBox.boundaryBuffer);
                if (activeSettings.Containment.DrawSteering) Debug.DrawRay(transform.position, FlockBox.transform.TransformDirection(steerBuffer), activeSettings.Containment.debugColor);
                ApplyForce(steerBuffer);
            }
        }

        public void GetSeekVector(out Vector3 steer, Vector3 target)
        {
            GetSteerVector(out steer, target - Position);
        }

        public void GetSteerVector(out Vector3 steer, Vector3 desiredForward)
        {
            steer = desiredForward.normalized * activeSettings.maxSpeed - Velocity;
            steer = steer.normalized * Mathf.Min(steer.magnitude, activeSettings.maxForce);
        }

        /// <summary>
        /// Apply the results of all steering behavior calculations to this object.
        /// </summary>
        protected virtual void ApplySteeringAcceleration()
        {
            Velocity += (Acceleration) * Time.deltaTime;
            Velocity = Velocity.normalized * Mathf.Min(Velocity.magnitude, activeSettings.maxSpeed * speedThrottle);
            ValidateVelocity();

            Position += (Velocity * Time.deltaTime);
            ValidatePosition();

            UpdateTransform();
        }

        protected override void FindOccupyingCells()
        {
            if (FlockBox)
                FlockBox.UpdateAgentCells(this, cells, false);
        }

        public override void Spawn(FlockBox flockBox, Vector3 position)
        {
            base.Spawn(flockBox, position);
            LockPosition(false);
            speedThrottle = 1;
            Acceleration = Vector3.zero;
            if (activeSettings)
            {
                Velocity = UnityEngine.Random.insideUnitSphere * activeSettings.maxSpeed;
            }
            else
            {
                Debug.LogWarning("No BehaviorSettings for SteeringAgent " + this.name);
            }
        }

        public void LockPosition(bool isLocked)
        {
            freezePosition = isLocked;
        }
       
        protected Quaternion LookRotation(Vector3 desiredForward)
        {
            return Quaternion.LookRotation(desiredForward);
        }

        protected override void UpdateTransform()
        {
            transform.localPosition = (Position);

            if (Velocity.magnitude > 0)
            {
                transform.localRotation = LookRotation(Velocity.normalized);
                Forward = Velocity;
            }

            else
            {
                Forward = transform.localRotation * Vector3.forward;
            }
        }

        void IConvertGameObjectToEntity.Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            //BehaviorSettingsUpdateSystem will fill in the rest of the neccessary componentData when the change is detected

            dstManager.AddComponent<SteeringData>(entity);
            dstManager.AddSharedComponentData(entity, new BehaviorSettingsData { Settings = null });

            if (activeSettings)
            {
                activeSettings.ApplyToEntity(entity, dstManager);
            }

            //AgentData holds everything a behavior needs to react to another Agent
            dstManager.AddComponentData(entity, new AgentData
            {
                Position = Position,
                Velocity = Velocity,
                Forward = Forward,
                Tag = TagMaskUtility.TagToInt(tag),
                Radius = shape.radius,
                Fill = shape.type == Shape.ShapeType.SPHERE,
            }) ;
            dstManager.AddComponentData(entity, new AccelerationData { Value  = float3.zero});
            dstManager.AddComponentData(entity, new PerceptionData());

            //give entity a buffer to hold info about surroundings
            dstManager.AddBuffer<NeighborData>(entity);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = this.transform.localToWorldMatrix;

            if (debugDrawShape)
            {
                Gizmos.color = Color.grey;
                UnityEditor.Handles.matrix = this.transform.localToWorldMatrix;
                UnityEditor.Handles.color = Color.grey;
                shape.DrawGizmo();
            }

            if (UnityEditor.Selection.activeGameObject != transform.gameObject)
            {
                return;
            }
        }

        void OnDrawGizmos()
        {
            if (activeSettings)
            {
                activeSettings.DrawPropertyGizmos(this);
            }
        }
#endif

    }
}