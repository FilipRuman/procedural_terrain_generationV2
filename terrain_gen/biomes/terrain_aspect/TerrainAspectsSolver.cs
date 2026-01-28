using Godot;
[GlobalClass, Tool]
public partial class TerrainAspectsSolver : Node3D
{

    [Export] public NoiseComponent elevation_noise;
    [Export] public NoiseComponent temperature_noise;
    [Export] public NoiseComponent moisture_noise;

    [Export] public float ruggedness_calculation_step_size;

    [Export] public float moisture_roughness_effect;
    [Export] public float elevation_roughness_effect;
    [Export] public float ruggedness_roughness_effect;



    public class TerrainAspects
    {
        // all values should be in a 0..1 range
        public float moisture;
        public float temperature;
        public float elevation;
        public float ruggedness;
        public float roughness;

        public TerrainAspects(float moisture, float temperature, float elevation, float ruggedness, float roughness)
        {
            this.moisture = moisture;
            this.temperature = temperature;
            this.elevation = elevation;
            this.ruggedness = ruggedness;
            this.roughness = roughness;
        }
    }
    public TerrainAspects SolveForPos(Vector2 pos)
    {
        var elevation = elevation_noise.SampleNormalized(pos);
        var temperature = temperature_noise.SampleNormalized(pos);
        var moisture = moisture_noise.SampleNormalized(pos);
        var ruggedness = CalculateRuggedness(pos);
        var roughness = Mathf.Clamp(elevation_roughness_effect * elevation + moisture_roughness_effect * moisture + ruggedness_roughness_effect * ruggedness, 0, 1);

        return new(moisture, temperature, elevation, ruggedness, roughness);
    }
    private float CalculateRuggedness(Vector2 pos)
    {
        var e_current = elevation_noise.SampleNormalized(pos);

        var e_x = elevation_noise.SampleNormalized(pos + Vector2.Right * ruggedness_calculation_step_size);
        var delta_x = (e_x - e_current) / ruggedness_calculation_step_size;

        var e_y = elevation_noise.SampleNormalized(pos + Vector2.Up * ruggedness_calculation_step_size);
        var delta_y = (e_y - e_current) / ruggedness_calculation_step_size;
        // Change this so something faster
        return Mathf.Sqrt(delta_x * delta_x + delta_y * delta_y);

    }
}

