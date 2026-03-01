using UnityEngine;

namespace CE6127.Tanks.AI
{
    /// <summary>
    /// tank aims at the target and fires periodically.
    /// leaves Engage if target is too far (back to Chase) or missing (Patrolling).
    /// </summary>
    internal class EngageState : BaseState
    {
        private TankSM m_TankSM;

        private float m_NextFireTime;
        private float m_CurrentInterval;
        private int m_ShotsSinceFlee;
        private int m_ShotsBeforeFlee;

        public EngageState(TankSM tankStateMachine) : base("Engage", tankStateMachine)
            => m_TankSM = (TankSM)m_StateMachine;

        public override void Enter()
        {
            base.Enter();

            m_ShotsSinceFlee = 0;
            m_ShotsBeforeFlee = Random.Range(2, 5); // flee after 2–4 shots

            // don't move while engaging
            if (m_TankSM.NavMeshAgent != null && m_TankSM.NavMeshAgent.isOnNavMesh)
            {
                m_TankSM.NavMeshAgent.ResetPath();
                m_TankSM.NavMeshAgent.velocity = Vector3.zero;
            }

            // shot scheduling: chooses some random interval and adds it to a timer
            m_CurrentInterval = Random.Range(m_TankSM.FireInterval.x, m_TankSM.FireInterval.y);
            m_NextFireTime = Time.time + m_CurrentInterval;
        }

        public override void Update()
        {
            base.Update();

            Debug.Log("Engage");
            

            // fallback to patrolling if target is missing
            if (m_TankSM.Target == null)
            {
                m_StateMachine.ChangeState(m_TankSM.m_States.Patrolling);
                return;
            }

            var tankPos = m_TankSM.transform.position;
            var targetPos = m_TankSM.Target.position;

            // keep aiming
            var lookPos = targetPos - tankPos;
            lookPos.y = 0f;

            if (lookPos.sqrMagnitude > 0.001f)
            {
                var rot = Quaternion.LookRotation(lookPos);
                m_TankSM.transform.rotation = Quaternion.Slerp(
                    m_TankSM.transform.rotation,
                    rot,
                    m_TankSM.OrientSlerpScalar
                );
            }

            // go to chasing if the target is too far
            float distToTarget = Vector3.Distance(tankPos, targetPos);

            // leave Engage if beyond stop distance (with hysteresis to avoid flicker)
            float chaseBuffer = 2.0f;
            if (distToTarget > m_TankSM.StopDistance + chaseBuffer)
            {
                m_StateMachine.ChangeState(m_TankSM.m_States.Chase);
                return;
            }

            // fire according to the scheduling (this prevents simultaneous shots, but still some randomness in the firing)
            if (Time.time >= m_NextFireTime)
            {
                // simple force selection, probably it is a good idea to make it better later
                float t = Mathf.InverseLerp(m_TankSM.StopDistance * 0.5f, m_TankSM.TargetDistance, distToTarget);
                float launchForce = Mathf.Lerp(m_TankSM.LaunchForceMinMax.x, m_TankSM.LaunchForceMinMax.y, t);

                m_TankSM.LaunchProjectile(launchForce);

                // re-schedule the shot
                m_CurrentInterval = Random.Range(m_TankSM.FireInterval.x, m_TankSM.FireInterval.y);
                m_NextFireTime = Time.time + m_CurrentInterval;

                m_ShotsSinceFlee++;
                if (m_ShotsSinceFlee >= m_ShotsBeforeFlee)
                {
                    m_StateMachine.ChangeState(m_TankSM.m_States.Flee);
                    return;
                }
            }
        }

        public override void Exit()
        {
            base.Exit();
        }
    }
}