using UnityEngine;
using UnityEngine.InputSystem;

public class Player_InteractState : EntityState
{
    private IInteractable currentInteractable;
    private EntityState previousState;
    private InputAction interactAction;

    public Player_InteractState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
        interactAction = player.input.Player.Interact;
    }

    public void Setup(IInteractable interactable, EntityState returnState)
    {
        currentInteractable = interactable;
        previousState = returnState;
    }

    public override void Enter()
    {
        base.Enter();

        stateTimer = player.interactHoldDuration;
        player.SetVelocity(0, rb.linearVelocity.y);
    }

    public override void Update()
    {
        if (currentInteractable == null)
        {
            CancelAndReturn();
            return;
        }

        if (player.currentInteractable != currentInteractable)
        {
            CancelAndReturn();
            return;
        }

        if (interactAction == null || interactAction.IsPressed() == false)
        {
            CancelAndReturn();
            return;
        }

        stateTimer -= Time.deltaTime;
        player.SetVelocity(player.moveInput.x * (player.moveSpeed * player.interactMoveSlowMultiplier), rb.linearVelocity.y);

        if (stateTimer <= 0)
        {
            currentInteractable.Interact();
            ReturnToPreviousState();
        }
    }

    public override void Exit()
    {
        base.Exit();

        currentInteractable = null;
        previousState = null;
    }

    private void CancelAndReturn()
    {
        currentInteractable?.CancelInteract();
        ReturnToPreviousState();
    }

    private void ReturnToPreviousState()
    {
        if (previousState != null && previousState != this)
            stateMachine.ChangeState(previousState);
        else
            stateMachine.ChangeState(player.idleState);
    }
}
