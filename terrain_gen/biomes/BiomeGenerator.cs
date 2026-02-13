using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Godot;
[Tool]
public partial class BiomeGenerator : Node
{

        private const int margin_pixels = 0;
        private const int COLOR_CHANNELS = 4;
        [Export] int biome_map_resolution;
        [Export] int backup_biome_index;
        /// map float (expected 0..1) to byte 0..255.
        static byte FloatToByte(float v)
        {
                return (byte)MathF.Round(v * 255f);
        }

        static float ByteToFloat(byte v)
        {
                return v / 255f;
        }

        public class OutputData
        {
                readonly byte[][] biome_maps;

                public int map_resolution;

                public OutputData(int map_resolution, byte[][] biome_maps_array)
                {
                        this.biome_maps = biome_maps_array;
                        this.map_resolution = map_resolution;
                }
                public List<BiomeInfluenceOutput> GetBiomeInfluenceForUV(Vector2 uv, int biome_count)
                {
                        int x = Mathf.FloorToInt(map_resolution * uv.X);
                        int y = Mathf.FloorToInt(map_resolution * uv.Y);
                        int base_index = x + margin_pixels + (y + margin_pixels) * (map_resolution + 2 * margin_pixels);
                        List<BiomeInfluenceOutput> output = new();
                        for (int biome = 0; biome < biome_count; biome++)
                        {
                                HandleBiomeInfluenceSampling(base_index, biome, ref output);
                        }
                        return output;

                }
                private void HandleBiomeInfluenceSampling(int base_map_index, int biome_index, ref List<BiomeInfluenceOutput> output)
                {
                        int biome_map = biome_index / COLOR_CHANNELS;
                        int index_inside_biome_map = biome_index % COLOR_CHANNELS;
                        float influence = ByteToFloat(biome_maps[biome_map][index_inside_biome_map]);
                        if (influence < 0.1f)
                        {
                                return;
                        }
                        output.Add(new(biome_index, influence));
                }


                public ImageTexture GetTexture(int map_index)
                {
                        if (map_index >= biome_maps.Length)
                        {
                                GD.PushWarning($"The requested biome map texture index was outside of the array, returning blank texture\n requested-{map_index} length- {biome_maps.Length}");

                                var image = Image.CreateEmpty(map_resolution + margin_pixels * 2, map_resolution + margin_pixels * 2, false, Image.Format.Rgba8);
                                return ImageTexture.CreateFromImage(image);
                        }
                        else
                        {
                                var data = biome_maps[map_index];
                                var image = Image.CreateFromData(map_resolution + margin_pixels * 2, map_resolution + margin_pixels * 2, false, Image.Format.Rgba8, data);
                                return ImageTexture.CreateFromImage(image);

                        }
                }
                public struct BiomeInfluenceOutput
                {
                        public int biome_type_index;
                        public float influence;

                        public BiomeInfluenceOutput(int biome_index, float influence)
                        {
                                this.biome_type_index = biome_index;
                                this.influence = influence;
                        }
                }


                private static void ReadFromMapAndAddToOutput(List<BiomeInfluenceOutput> output, int index_inside_biome_map, int biome_index, byte[] map)
                {
                        var influence = map[index_inside_biome_map];
                        if (influence == 0) return;
                        output.Add(new(biome_index, ByteToFloat(influence)));
                }
        }
        class BiomeInfluence
        {
                public Biome biome;
                public float influence;

                public BiomeInfluence(Biome biome, float influence)
                {
                        this.biome = biome;
                        this.influence = influence;
                }
        }
        [Export] float biome_transitions_smoothness;
        public float get_aspect_influence(FloatRange preferred, float value)
        {
                return Mathf.SmoothStep(preferred.min - biome_transitions_smoothness, preferred.max, value)
                         * (1 - Mathf.SmoothStep(preferred.min, preferred.max + biome_transitions_smoothness, value));
        }
        private List<BiomeInfluence> GetBiomeInfluences(TerrainAspectsSolver.TerrainAspects terrain_aspects, Biome[] biomes)
        {
                float influence_sum = 0;
                List<BiomeInfluence> output = new();

                foreach (var biome in biomes)
                {

                        float influence =
                                    get_aspect_influence(biome.preferred_moisture, terrain_aspects.moisture) *
                                    get_aspect_influence(biome.preferred_elevation, terrain_aspects.elevation) *
                                    get_aspect_influence(biome.preferred_temperature, terrain_aspects.temperature);
                        if (influence == 0)
                        {
                                continue;
                        }
                        influence_sum += influence;
                        output.Add(new(biome, influence));
                }
                if (influence_sum == 0)
                {
                        GD.PrintErr($"There is no valid biome for this terrain, using the backup biome! terrain aspects: moisture{Mathf.RoundToInt(terrain_aspects.moisture * 100) / 100f} elevation{Mathf.RoundToInt(terrain_aspects.elevation * 100) / 100f} temperature{Mathf.RoundToInt(terrain_aspects.temperature * 100) / 100f}");
                        return [new BiomeInfluence(biomes[backup_biome_index], 1)];
                }
                var normalization_factor = 1f / influence_sum;
                foreach (var biome in output)
                {
                        biome.influence *= normalization_factor;
                }

                return output;
        }
        public static byte[][] InitializeBiomeMapArray(int maps_count, int points_per_axis)
        {

                int map_size = (points_per_axis + margin_pixels * 2) * (points_per_axis + margin_pixels * 2) * COLOR_CHANNELS;
                byte[][] biome_maps = new byte[maps_count][];

                for (int i = 0; i < maps_count; i++)
                {
                        biome_maps[i] = new byte[map_size];
                }

                return biome_maps;
        }
        public OutputData GenerateMaps(TerrainAspectsSolver terrain_aspects_solver, Vector2 base_world_position, int width_height, Biome[] biomes)
        {

                int points_per_axis = biome_map_resolution;

                float point_size = width_height / biome_map_resolution;

                var biome_maps_count = Mathf.CeilToInt((float)biomes.Length / COLOR_CHANNELS);
                byte[][] biome_maps = InitializeBiomeMapArray(biome_maps_count, points_per_axis);

                for (int x = -margin_pixels; x < points_per_axis + margin_pixels; x++)
                {
                        for (int y = -margin_pixels; y < points_per_axis + margin_pixels; y++)
                        {
                                Vector2 world_pos = new Vector2(x, y) * point_size + base_world_position;


                                List<BiomeInfluence> biome_influences = GetBiomeInfluences(terrain_aspects_solver.SolveForPos(world_pos), biomes);
                                int base_index = (x + margin_pixels + (y + margin_pixels) * (points_per_axis + 2 * margin_pixels)) * COLOR_CHANNELS;

                                foreach (var biome_influence in biome_influences)
                                {
                                        var map_index = biome_influence.biome.index_in_biomes_array / COLOR_CHANNELS;
                                        int color_channel_index = biome_influence.biome.index_in_biomes_array % COLOR_CHANNELS;
                                        // So that the Byte doesn't overflow
                                        byte clamped_value = FloatToByte(Math.Clamp(ByteToFloat(biome_maps[map_index][base_index + color_channel_index]) + biome_influence.influence, 0, 1));
                                        biome_maps[map_index][base_index + color_channel_index] = clamped_value;
                                }
                        }
                }

                return new(points_per_axis, biome_maps);
        }

}
