using UnityEngine;

using Debug = UnityEngine.Debug;

namespace CE6127.Tanks.AI
{
    /// <summary>
    /// Class <c>IdleState</c> represents the state of the tank when it is idle (spawn state).
    /// </summary>
    internal class IdleState : BaseState
    {
        private TankSM m_TankSM; // Reference to the tank state machine.
        private float m_lateGameTime;

        /// <summary>
        /// Constructor <c>IdleState</c> is the constructor of the class.
        /// </summary>
        public IdleState(TankSM tankStateMachine) : base("Idle", tankStateMachine)
            => m_TankSM = (TankSM)m_StateMachine;

        /// <summary>
        /// Method <c>Enter</c> is called when the state is entered.
        /// </summary>
        public override void Enter()
        {
            base.Enter();

            m_lateGameTime = 0.5f*(60*m_TankSM.GameManager.MinutesPerRound);
            // Idle should not "stop early" from navmesh perspective; usually we don't move here anyway.
            // But clearing any leftover path is safe.
            if (m_TankSM.NavMeshAgent != null && m_TankSM.NavMeshAgent.isOnNavMesh)
                m_TankSM.NavMeshAgent.ResetPath();
        }

        /// <summary>
        /// Method <c>Update</c> is called each frame.
        /// </summary>
        public override void Update()
        {
            base.Update();

            // if we have no target, just patrol
            if (m_TankSM.Target == null)
            {
                m_StateMachine.ChangeState(m_TankSM.m_States.Patrolling);
                return;
            }

            var tankPos = m_TankSM.transform.position;
            var targetPos = m_TankSM.Target.position;
            var dist = Vector3.Distance(tankPos, targetPos);

            // Always face the target while idle (nice spawn behavior)
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

            // transitions to other states, since this is the spawn state, then i want to do multiple things here
            // if very close, go straight to engage
            if (dist <= m_TankSM.StopDistance)
            {
                // clear movement, so that tanks dont bug out when engage start
                if (m_TankSM.NavMeshAgent != null && m_TankSM.NavMeshAgent.isOnNavMesh)
                    m_TankSM.NavMeshAgent.ResetPath();

                // change to hard or easy mode depending on the current match timer
                if (m_TankSM.UseStrafeEngagePermanently || m_TankSM.GameManager.m_RoundTimeLeft <= m_lateGameTime)
                    m_StateMachine.ChangeState(m_TankSM.m_States.StrafeEngage);
                else
                    m_StateMachine.ChangeState(m_TankSM.m_States.Engage);

                return;
            }

            // if within range, chase
            if (dist <= m_TankSM.TargetDistance)
            {
                m_StateMachine.ChangeState(m_TankSM.m_States.Chase);
                return;
            }

            // otherwise, just patrol
            m_StateMachine.ChangeState(m_TankSM.m_States.Patrolling);
        }
    }
}