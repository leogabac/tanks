using UnityEngine;

using Debug = UnityEngine.Debug;

namespace CE6127.Tanks.AI
{
    /// <summary>
    /// Class <c>IdleState</c> represents the state of the tank when it is idle.
    /// </summary>
    internal class ChaseState : BaseState
    {
        private TankSM m_TankSM; // Reference to the tank state machine.

        /// <summary>
        /// Constructor <c>IdleState</c> is the constructor of the class.
        /// </summary>
        public ChaseState(TankSM tankStateMachine) : base("Chase", tankStateMachine) => m_TankSM = (TankSM)m_StateMachine;

        /// <summary>
        /// Method <c>Enter</c> is called when the state is entered.
        /// </summary>
        public override void Enter()
        {
            base.Enter();
            // in case i want to add coroutines, this is how to do it
            // m_TankSM.SetStopDistanceToZero();
            // m_TankSM.StartCoroutine(Patrolling());
        }
        

        /// <summary>
        /// Method <c>Update</c> is called each frame.
        /// </summary>
        
    public override void Update()
    {
        base.Update();

        // If we have no target, go back to patrolling (or just return)
        if (m_TankSM.Target == null)
        {
            m_StateMachine.ChangeState(m_TankSM.m_States.Patrolling);
            return;
        }

        // Distance check (if target is too far, stop chasing)
        var targetPos = m_TankSM.Target.position;
        var dist = Vector3.Distance(m_TankSM.transform.position, targetPos);

        if (dist > m_TankSM.TargetDistance)
        {
            m_StateMachine.ChangeState(m_TankSM.m_States.Patrolling);
            return;
        }

        // Smoothly rotate to face the target
        var lookPos = targetPos - m_TankSM.transform.position;
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

        // CHASE destination: go to the target
        var m_Destination = targetPos;

        // Update navmesh destination at a fixed rate (prevents jitter + saves CPU)
        if (Time.time >= m_TankSM.NavMeshUpdateDeadline)
        {
            m_TankSM.NavMeshUpdateDeadline = Time.time + m_TankSM.PatrolNavMeshUpdate;

            if (m_TankSM.NavMeshAgent != null && m_TankSM.NavMeshAgent.isOnNavMesh)
            {
                m_TankSM.NavMeshAgent.SetDestination(m_Destination);
            }
        }
        
        // then here would be cool to update to go into firing and engaging mode
        // the basic idea would be to change between firing and chasing mode
    }
}
}
