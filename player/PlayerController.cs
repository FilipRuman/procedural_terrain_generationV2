using Godot;
public partial class PlayerController : CharacterBody3D
{
    [Export] float movement_speed;
    [Export] float sprint_mod;
    [Export] float gravity;
    [Export] Curve drag;
    [Export] float static_drag;
    [Export] Node3D camera;
    [Export] Vector2 mouse_sens;

    private Vector2 mouse_motion = Vector2.Zero;

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseMotion mouseMotion)
        {
            mouse_motion = mouseMotion.Relative;
        }
    }
    public override void _PhysicsProcess(double delta)
    {
        var input = Vector3.Zero;
        if (Input.IsActionPressed("Right"))
            input += Vector3.Right;
        if (Input.IsActionPressed("Left"))
            input += Vector3.Left;
        if (Input.IsActionPressed("Back"))
            input += Vector3.Back;
        if (Input.IsActionPressed("Forward"))
            input += Vector3.Forward;

        input.Normalized();
        if (Input.IsActionPressed("Sprint"))
            input *= sprint_mod;
        input = input.Rotated(Vector3.Up, Rotation.Y);
        input *= movement_speed;
        input += Vector3.Down * gravity;

        Velocity += input * (float)delta;
        Velocity -= Velocity.Normalized() * drag.SampleBaked(Velocity.Length()) * (float)delta;
        Vector3 velocity_with_static_drag = new(Mathf.Sign(Velocity.X) * Mathf.Max(Mathf.Abs(Velocity.X) - static_drag * (float)delta, 0), Mathf.Sign(Velocity.Y) * Mathf.Max(Mathf.Abs(Velocity.Y) - static_drag * (float)delta, 0), Mathf.Sign(Velocity.Z) * Mathf.Max(Mathf.Abs(Velocity.Z) - static_drag * (float)delta, 0));

        Input.MouseMode = Input.MouseModeEnum.Captured;

        Velocity = velocity_with_static_drag;
        MoveAndSlide();

        Rotation = new Vector3(0, Rotation.Y + mouse_sens.X * -mouse_motion.X, 0);
        camera.Rotation = new Vector3(Mathf.Clamp(camera.Rotation.X + mouse_sens.Y * -mouse_motion.Y, -Mathf.DegToRad(80), Mathf.DegToRad(80)), 0, 0);
        mouse_motion = Vector2.Zero;
    }

}
