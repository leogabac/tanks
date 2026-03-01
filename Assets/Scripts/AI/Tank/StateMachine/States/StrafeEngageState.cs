using UnityEngine;
using UnityEngine.AI;

namespace CE6127.Tanks.AI
{
    // here the idea is to have a state where the tank is firing while moving
    // how they do it is to fire, then move to a point in a circle around the target at some stop distance
    // it is not really conencted to anything rn, but probably it is a good idea to have different styles of engage
    // so that the gamplay becomes smoother
    internal class StrafeEngageState : BaseState
    {
        private TankSM m_TankSM;

        // firing
        private float m_NextFireTime = -1f;

        // reposition
        private Vector3 m_RepositionDestination;
        private bool m_HasDestination;

        // some paraemeters
        private const float ExitToChaseBuffer = 6.0f;      // hysteresis so it doesn't thrash
        private const float ArriveTolerance = 1.2f;
        private const float RepositionMaxDuration = 2.5f;  // failsafe
        private float m_RepositionDeadline;

        // circle sampling 
        private const float MinAngleStepDeg = 35f;         // avoids tiny moves
        private const float MaxAngleStepDeg = 110f;
        private float m_LastAngleDeg;                      // around target, in degrees

        public StrafeEngageState(TankSM tankStateMachine) : base("Engage", tankStateMachine)
            => m_TankSM = (TankSM)m_StateMachine;

        public override void Enter()
        {
            base.Enter();

            m_HasDestination = false;

            if (m_TankSM.NavMeshAgent != null && m_TankSM.NavMeshAgent.isOnNavMesh)
                m_TankSM.NavMeshAgent.ResetPath();

            // shot (idk, it was buggy and this solved it)
            if (m_NextFireTime < 0f) m_NextFireTime = Time.time + 0.25f;

            // initialize angle based on current relative pos
            if (m_TankSM.Target != null)
            {
                Vector3 fromTarget = (m_TankSM.transform.position - m_TankSM.Target.position);
                fromTarget.y = 0f;
                if (fromTarget.sqrMagnitude > 0.001f)
                {
                    fromTarget.Normalize();
                    m_LastAngleDeg = Mathf.Atan2(fromTarget.z, fromTarget.x) * Mathf.Rad2Deg;
                }
                else
                {
                    m_LastAngleDeg = Random.Range(0f, 360f);
                }
            }
        }

        public override void Update()
        {
            base.Update();

            if (m_TankSM.Target == null)
            {
                m_StateMachine.ChangeState(m_TankSM.m_States.Patrolling);
                return;
            }
            
            // Debug.Log("Strafe Engage");

            var tankPos = m_TankSM.transform.position;
            var targetPos = m_TankSM.Target.position;

            AimAt(targetPos);

            float dist = Vector3.Distance(tankPos, targetPos);

            // chase if too far
            if (dist > m_TankSM.StopDistance + ExitToChaseBuffer)
            {
                m_StateMachine.ChangeState(m_TankSM.m_States.Chase);
                return;
            }

            // move to the orbit
            UpdateDestinationStatus();

            // fire if need after cooldown
            if (Time.time >= m_NextFireTime)
            {
                FireAtDistance(dist);
                ScheduleNextShot();

                // orbit in a circle around the player
                PickAndMoveToCirclePoint(targetPos);
            }
        }

        private void AimAt(Vector3 targetPos)
        {
            var tankPos = m_TankSM.transform.position;
            var look = targetPos - tankPos;
            look.y = 0f;

            if (look.sqrMagnitude > 0.001f)
            {
                var rot = Quaternion.LookRotation(look);
                m_TankSM.transform.rotation = Quaternion.Slerp(
                    m_TankSM.transform.rotation,
                    rot,
                    m_TankSM.OrientSlerpScalar
                );
            }
        }

        private void FireAtDistance(float distToTarget)
        {
            // simple firing, choose force proportional to distance
            float t = Mathf.InverseLerp(m_TankSM.StopDistance * 0.5f, m_TankSM.TargetDistance, distToTarget);
            float launchForce = Mathf.Lerp(m_TankSM.LaunchForceMinMax.x, m_TankSM.LaunchForceMinMax.y, t);

            m_TankSM.LaunchProjectile(launchForce);
        }

        private void ScheduleNextShot()
        {
            float interval = Random.Range(m_TankSM.FireInterval.x, m_TankSM.FireInterval.y);
            m_NextFireTime = Time.time + interval;
        }

        private void PickAndMoveToCirclePoint(Vector3 targetPos)
        {
            if (m_TankSM.NavMeshAgent == null || !m_TankSM.NavMeshAgent.isOnNavMesh)
                return;

            float radius = m_TankSM.StopDistance;

            // choose a new angle on the circle
            float delta = Random.Range(MinAngleStepDeg, MaxAngleStepDeg);
            if (Random.value < 0.5f) delta = -delta;
            m_LastAngleDeg += delta;

            // determine which position that has
            float rad = m_LastAngleDeg * Mathf.Deg2Rad;
            Vector3 circleOffset = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * radius;

            Vector3 desired = targetPos + circleOffset;

            // put it into the navmesh
            if (NavMesh.SamplePosition(desired, out NavMeshHit hit, 4.0f, NavMesh.AllAreas))
            {
                m_RepositionDestination = hit.position;
                m_TankSM.NavMeshAgent.SetDestination(m_RepositionDestination);

                m_HasDestination = true;
                m_RepositionDeadline = Time.time + RepositionMaxDuration;
            }
            else
            {
                // if for some reason it crashes, try some random points quickly to keep moving
                for (int i = 0; i < 4; i++)
                {
                    float a = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                    Vector3 alt = targetPos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;

                    if (NavMesh.SamplePosition(alt, out hit, 6.0f, NavMesh.AllAreas))
                    {
                        m_RepositionDestination = hit.position;
                        m_TankSM.NavMeshAgent.SetDestination(m_RepositionDestination);

                        m_HasDestination = true;
                        m_RepositionDeadline = Time.time + RepositionMaxDuration;
                        break;
                    }
                }
            }
        }

        private void UpdateDestinationStatus()
        {
            if (!m_HasDestination) return;

            // timout: don't get stuck, sometimes the tanks would just keep looking at the player without doing anything
            // so they need to keep moving
            if (Time.time >= m_RepositionDeadline)
            {
                m_HasDestination = false;
                if (m_TankSM.NavMeshAgent != null && m_TankSM.NavMeshAgent.isOnNavMesh)
                    m_TankSM.NavMeshAgent.ResetPath();
                return;
            }

            if (m_TankSM.NavMeshAgent == null || !m_TankSM.NavMeshAgent.isOnNavMesh)
            {
                m_HasDestination = false;
                return;
            }

            if (!m_TankSM.NavMeshAgent.pathPending)
            {
                float stop = Mathf.Max(ArriveTolerance, m_TankSM.NavMeshAgent.stoppingDistance + 0.2f);
                if (m_TankSM.NavMeshAgent.remainingDistance <= stop)
                {
                    m_HasDestination = false;
                    // let it keep its path cleared so next reposition is clean
                    m_TankSM.NavMeshAgent.ResetPath();
                }
            }
        }
    }
}