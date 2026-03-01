using System.Collections;
using UnityEngine;
using UnityEngine.AI;

using Random = UnityEngine.Random;
using Debug = UnityEngine.Debug;

namespace CE6127.Tanks.AI
{
    /// <summary>
    /// Class <c>FleeState</c> represents the state of the tank when it is fleeing from the target.
    /// </summary>
    internal class FleeState : BaseState
    {
        private TankSM m_TankSM;               // Reference to the tank state machine.
        private Vector3 m_Destination;         // Current flee destination.
        private Coroutine m_FleeCoroutine;     // Coroutine handle so we can stop it safely.

        // --- Tunables ---
        private const float FleeRepathMin = 0.6f;      // how often to choose a new flee point
        private const float FleeRepathMax = 1.4f;

        private const float FleeStepMin = 12f;         // how far to try to run each time (min)
        private const float FleeStepMax = 22f;         // how far to try to run each time (max)

        private const float NavmeshSampleRadius = 6f;  // how far we allow snapping to navmesh
        private const float FaceAwaySlerp = 0.25f;     // separate from OrientSlerpScalar; tweak if needed

        public FleeState(TankSM tankStateMachine) : base("Flee", tankStateMachine)
            => m_TankSM = (TankSM)m_StateMachine;

        public override void Enter()
        {
            base.Enter();

            // While fleeing, do not stop early.
            m_TankSM.SetStopDistanceToZero();

            // Reset any existing path to avoid sticky behavior.
            if (m_TankSM.NavMeshAgent != null && m_TankSM.NavMeshAgent.isOnNavMesh)
                m_TankSM.NavMeshAgent.ResetPath();

            // Start coroutine that periodically picks a new destination away from target.
            m_FleeCoroutine = m_TankSM.StartCoroutine(Fleeing());
        }

        public override void Update()
        {
            base.Update();

            // If no target, we can't flee "from" anything; do nothing here.
            // (You can later transition to Patrolling/Idle when integrating.)
            if (m_TankSM.Target == null)
                return;

            // (Optional) Face away from target while fleeing.
            // Remove this block if you prefer to keep facing the target.
            Vector3 tankPos = m_TankSM.transform.position;
            Vector3 targetPos = m_TankSM.Target.position;

            Vector3 away = (tankPos - targetPos);
            away.y = 0f;

            if (away.sqrMagnitude > 0.001f)
            {
                Quaternion rot = Quaternion.LookRotation(away.normalized);
                m_TankSM.transform.rotation = Quaternion.Slerp(
                    m_TankSM.transform.rotation,
                    rot,
                    FaceAwaySlerp
                );
            }

            // Update navmesh destination at a fixed rate (like PatrollingState).
            if (Time.time >= m_TankSM.NavMeshUpdateDeadline)
            {
                m_TankSM.NavMeshUpdateDeadline = Time.time + m_TankSM.TargetNavMeshUpdate;

                if (m_TankSM.NavMeshAgent != null && m_TankSM.NavMeshAgent.isOnNavMesh)
                {
                    m_TankSM.NavMeshAgent.SetDestination(m_Destination);
                }
            }
        }

        public override void Exit()
        {
            base.Exit();

            if (m_FleeCoroutine != null)
            {
                m_TankSM.StopCoroutine(m_FleeCoroutine);
                m_FleeCoroutine = null;
            }
        }

        /// <summary>
        /// Coroutine that periodically chooses a new flee destination away from the target.
        /// </summary>
        private IEnumerator Fleeing()
        {
            // Safety: wait a frame so all references are ready
            yield return null;

            while (true)
            {
                if (distToTarget > m_TankSM.TargetDistance)
                {
                    m_StateMachine.ChangeState(m_TankSM.m_States.Patrolling);
                    return;
                }
                
                if (m_TankSM.Target != null && m_TankSM.NavMeshAgent != null && m_TankSM.NavMeshAgent.isOnNavMesh)
                {
                    Vector3 tankPos = m_TankSM.transform.position;
                    Vector3 targetPos = m_TankSM.Target.position;

                    // direction away
                    Vector3 away = (tankPos - targetPos);
                    away.y = 0f;

                    if (away.sqrMagnitude < 0.001f)
                        away = -m_TankSM.transform.forward; // fallback

                    away.Normalize();

                    // add some randomness to not flee in a perfect straight line
                    // (small lateral offset)
                    Vector3 lateral = Vector3.Cross(Vector3.up, away).normalized;
                    float lateralJitter = Random.Range(-0.8f, 0.8f);

                    float step = Random.Range(FleeStepMin, FleeStepMax);
                    Vector3 desired = tankPos + (away + lateral * lateralJitter).normalized * step;

                    // snap to navmesh
                    if (NavMesh.SamplePosition(desired, out NavMeshHit hit, NavmeshSampleRadius, NavMesh.AllAreas))
                    {
                        m_Destination = hit.position;
                    }
                    else
                    {
                        // if we cant sample, just try some random direction
                        // (rotate away vector a bit)
                        float angle = Random.Range(-60f, 60f);
                        Vector3 rotatedAway = Quaternion.Euler(0f, angle, 0f) * away;
                        Vector3 alt = tankPos + rotatedAway.normalized * step;

                        if (NavMesh.SamplePosition(alt, out hit, NavmeshSampleRadius * 1.5f, NavMesh.AllAreas))
                            m_Destination = hit.position;
                        else
                            m_Destination = tankPos; // worst-case: no move
                    }
                }

                // wait a bit before choosing a new flee point
                float wait = Random.Range(FleeRepathMin, FleeRepathMax);
                yield return new WaitForSeconds(wait);
            }

        }
    }
}