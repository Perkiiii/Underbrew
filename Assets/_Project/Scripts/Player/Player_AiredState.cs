using UnityEngine;

public class Player_AiredState : EntityState
{
    public Player_AiredState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }


    public override void Update()
    {
        base.Update();

        player.ApplyHorizontalMovement(player.moveInput.x * player.inAirMoveMultiplier, grounded: false);

        if (player.ConsumeBufferedGroundJump())
        {
            stateMachine.ChangeState(player.jumpState);
            return;
        }

        if (player.CanUseAttackInput() && input.Player.Attack.WasPressedThisFrame())
        {
            stateMachine.ChangeState(player.jumpAttackState);
        }
    }
}
