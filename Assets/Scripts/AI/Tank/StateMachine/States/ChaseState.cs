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

        Debug.Log("Chase");


        // If we have no target, go back to patrolling (or just return)
        if (m_TankSM.Target == null)
        {
            m_StateMachine.ChangeState(m_TankSM.m_States.Patrolling);
            return;
        }

        var tankPos = m_TankSM.transform.position;
        var targetPos = m_TankSM.Target.position;

        // if the target becomes again too far, then go back to  patrolling
        var distToTarget = Vector3.Distance(tankPos, targetPos);
        if (distToTarget > m_TankSM.TargetDistance)
        {
            m_StateMachine.ChangeState(m_TankSM.m_States.Patrolling);
            return;
        }

        // keep some distance from the player when chasing so that the tanks dont get too close and collide
        // apparently the tanksm already had one variable for that
        float stopDistance = m_TankSM.StopDistance;
        float arriveTolerance = 0.5f; // small buffer so it doesn't jitter

        // direction from target to tank (so the point is "in front of" the tank relative to target)
        Vector3 dirFromTarget = tankPos - targetPos;
        dirFromTarget.y = 0f;

        // if we are exactly on top of the target (rare), pick a fallback direction
        if (dirFromTarget.sqrMagnitude < 0.0001f)
            dirFromTarget = -m_TankSM.transform.forward;

        dirFromTarget.Normalize();

        // destination is an offset point stopDistance away from the target
        Vector3 destination = targetPos + dirFromTarget * stopDistance;

        // smoothly rotate to face the target
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

        // update navmesh destination at a fixed rate
        if (Time.time >= m_TankSM.NavMeshUpdateDeadline)
        {
            m_TankSM.NavMeshUpdateDeadline = Time.time + m_TankSM.PatrolNavMeshUpdate;

            if (m_TankSM.NavMeshAgent != null && m_TankSM.NavMeshAgent.isOnNavMesh)
            {
                m_TankSM.NavMeshAgent.stoppingDistance = stopDistance;

                m_TankSM.NavMeshAgent.SetDestination(destination);
            }
        }

        // change to engage mode if we are within the stop distance (and tolerance)
        if (distToTarget <= stopDistance + arriveTolerance)
        {
            if (m_TankSM.NavMeshAgent != null && m_TankSM.NavMeshAgent.isOnNavMesh)
            {
                m_TankSM.NavMeshAgent.ResetPath();
            }

            m_StateMachine.ChangeState(m_TankSM.m_States.Engage);
            return;
        }
    }
}
}
