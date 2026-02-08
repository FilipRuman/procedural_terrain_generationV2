using System.Collections.Generic;
using Godot;
[Tool]
public partial class ObjectsGenerator : Node3D
{

	[Export] float minimal_tree_spacing_sqrt;
	[Export] int tree_count;

	public void SpawnObjects(ObjectTypeSpawnData[] input, Node3D parent_for_objects)
	{
		foreach (var spawn_data in input)
		{
			SpawnObjectType(spawn_data, parent_for_objects);
		}
	}
	private class ObjectsSpacingGrid
	{
		// 1'st dimension -> x + y * grid_width 
		// 2'nd dimension -> all objects in this grid cell
		private List<ObjectsSpacingData>[] grid;
		private int grid_width;
#nullable enable
		public List<ObjectsSpacingData>? this[Vector2I pos]
		{
			get
			{
				if (pos.X < -1 || pos.X >= grid_width - 1 || pos.Y < -1 || pos.Y >= grid_width - 1)
					return null;

				int idx = (pos.X + grid_padding / 2) + (pos.Y + grid_padding / 2) * grid_width;
				return grid[idx];
			}
		}
		public ObjectsSpacingGrid(int width_height)
		{
			grid_width = width_height;
			grid = new List<ObjectsSpacingData>[width_height * width_height];
			for (int x = 0; x < width_height; x++)
			{
				for (int y = 0; y < width_height; y++)
				{
					grid[x + y * width_height] = new();
				}
			}
		}

	}
	public struct ObjectsSpacingData
	{
		public Vector2 pos;
		public float min_distance_sqrt;

		public ObjectsSpacingData(Vector2 pos, float min_distance_sqrt)
		{
			this.pos = pos;
			this.min_distance_sqrt = min_distance_sqrt;
		}
	}
	private float GridCellWidth()
	{
		var dist_max = minimal_tree_spacing_sqrt;
		return dist_max / Mathf.Sqrt2;
	}

	const int grid_padding = 2;
	public ObjectTypeSpawnData[] GenerateObjectsData(int chunk_size, ThreadSafeGroundMeshGen ground_mesh_gen,
			BiomeGenerator.OutputData biome_data, Vector2 base_world_position, Biome[] biomes)
	{
		float grid_cell_width = GridCellWidth();
		chunk_size -= 2;
		// var grid_cell_widt = GridCellWidth();
		var grid_width = Mathf.CeilToInt((float)chunk_size / grid_cell_width) + grid_padding;
		var grid = new ObjectsSpacingGrid(grid_width);

		var seed = RNG.GenerateSeed(base_world_position);
		Dictionary<TerrainObject, List<ObjectInstantiationData>> instances_data_for_object_type = new();
		for (int x = -1; x < grid_width - 1; x++)
		{
			for (int y = -1; y < grid_width - 1; y++)
			{
				var base_cell_world_pos = base_world_position + new Vector2(x, y) * grid_cell_width;
				bool is_margin = x == -1 || y == -1 || x == grid_width - 2 || y == grid_width - 2;
				GenerateForGridCell(base_cell_world_pos, new(x, y), grid_cell_width, is_margin,
						biomes, ground_mesh_gen, biome_data, ref grid, ref instances_data_for_object_type);
				GD.Seed(seed);
			}
		}


		{
			var output = new ObjectTypeSpawnData[instances_data_for_object_type.Count];
			int i = 0;
			foreach (var instances_for_type in instances_data_for_object_type)
			{
				output[i] = new(instances_for_type.Key.mesh, instances_for_type.Key.collision_shape, instances_for_type.Value);
				i++;
			}

			return output;
		}
	}
	private void GenerateForGridCell(Vector2 base_cell_world_pos, Vector2I grid_pos, float grid_cell_width, bool is_margin,
		Biome[] biomes, ThreadSafeGroundMeshGen ground_mesh_gen, BiomeGenerator.OutputData biome_data,
		ref ObjectsSpacingGrid grid, ref Dictionary<TerrainObject, List<ObjectInstantiationData>> instances_data_for_object_type)
	{

		for (int i = 0; i < tree_count; i++)
		{
			Vector2 uv = new(GD.Randf(), GD.Randf());
			var world_pos_2d = uv * grid_cell_width + base_cell_world_pos;
			if (!IsPosValid(world_pos_2d, minimal_tree_spacing_sqrt, grid_pos, grid))
			{
				continue;
			}
			var height = ground_mesh_gen.CalculateHeight(world_pos_2d, out var terrain_aspects);
			var biomes_influence = biome_data.GetBiomeInfluenceForUV(uv, biomes.Length);
			Vector3 world_pos_3d = new(world_pos_2d.X, height, world_pos_2d.Y);
			foreach (var biome_influence in biomes_influence)
			{
				if (RNG.Float(uv) > biome_influence.influence)
					continue;
				var biome = biomes[biome_influence.biome_type_index];
				var object_inst_data = biome.objects_data.GetTree(world_pos_3d, terrain_aspects);

				if (object_inst_data == null)
					continue;

				if (!is_margin)
				{
					if (instances_data_for_object_type.TryGetValue(object_inst_data.Value.obj, out var object_type_array))
					{
						object_type_array.Add(object_inst_data.Value);
					}
					else
					{
						List<ObjectInstantiationData> list = new();
						list.Add(object_inst_data.Value);
						instances_data_for_object_type.Add(object_inst_data.Value.obj, list);
					}
				}

				grid[grid_pos]!.Add(new(world_pos_2d, minimal_tree_spacing_sqrt));
				break;
			}

		}
	}
	private bool IsPosValid(Vector2 pos, float main_min_distance_sqrt, Vector2I grid_pos, ObjectsSpacingGrid grid)
	{
		for (int x = -1; x <= 1; x++)
		{
			for (int y = -1; y <= 1; y++)
			{
				var cell = grid[grid_pos + new Vector2I(x, y)];
				if (cell == null)
					continue;
				if (!IsFarEnoughtFromObjects(pos, main_min_distance_sqrt, cell))
					return false;

			}
		}
		return true;
	}
	private bool IsFarEnoughtFromObjects(Vector2 pos, float main_min_distance_sqrt, List<ObjectsSpacingData> to_check)
	{
		foreach (var obj in to_check)
		{
			var spacing = Mathf.Max(main_min_distance_sqrt, obj.min_distance_sqrt);
			if (pos.DistanceSquaredTo(obj.pos) < spacing * spacing)
				return false;
		}
		return true;
	}
	public class ObjectTypeSpawnData
	{
		public int instance_count;
		public Mesh mesh;
		public float[] mesh_buffer;
		public PackedScene collision_shape;
		public Vector4[] colliders_pos_scale;
		public ObjectTypeSpawnData(Mesh mesh, PackedScene collision_shape, List<ObjectInstantiationData> object_instances)
		{
			this.mesh = mesh;
			this.collision_shape = collision_shape;

			instance_count = object_instances.Count;
			mesh_buffer = new float[object_instances.Count * 12];
			colliders_pos_scale = new Vector4[object_instances.Count];
			for (int i = 0; i < object_instances.Count; i++)
			{
				var object_data = object_instances[i];
				colliders_pos_scale[i] = new Vector4(object_data.pos.X, object_data.pos.Y, object_data.pos.Z, object_data.scale.X);
				Basis basis = new Basis(Godot.Quaternion.FromEuler(object_data.rotation * Mathf.DegToRad(1.0f)));
				basis = basis.Scaled(object_data.scale);

				var base_idx = i * 12;

				mesh_buffer[base_idx] = basis.X.X;
				mesh_buffer[base_idx + 1] = basis.Y.X;
				mesh_buffer[base_idx + 2] = basis.Z.X;
				mesh_buffer[base_idx + 3] = object_data.pos.X;

				mesh_buffer[base_idx + 4] = basis.X.Y;
				mesh_buffer[base_idx + 5] = basis.Y.Y;
				mesh_buffer[base_idx + 6] = basis.Z.Y;
				mesh_buffer[base_idx + 7] = object_data.pos.Y;

				mesh_buffer[base_idx + 8] = basis.X.Z;
				mesh_buffer[base_idx + 9] = basis.Y.Z;
				mesh_buffer[base_idx + 10] = basis.Z.Z;
				mesh_buffer[base_idx + 11] = object_data.pos.Z;
			}
		}

	}
	private void SpawnObjectType(ObjectTypeSpawnData spawn_data, Node3D parent_for_objects)
	{
		var mesh_instance = new MultiMeshInstance3D();
		parent_for_objects.AddChild(mesh_instance);
		var multi_mesh = new MultiMesh();
		multi_mesh.Mesh = spawn_data.mesh;
		multi_mesh.TransformFormat = MultiMesh.TransformFormatEnum.Transform3D;
		multi_mesh.InstanceCount = spawn_data.instance_count;
		multi_mesh.Buffer = spawn_data.mesh_buffer;

		mesh_instance.Multimesh = multi_mesh;
		foreach (var collider_data in spawn_data.colliders_pos_scale)
		{
			var pos = new Vector3(collider_data.X, collider_data.Y, collider_data.Z);
			var scale = collider_data.W;

			var collision_node = (Node3D)spawn_data.collision_shape.Instantiate();
			collision_node.Scale = Vector3.One * scale;
			collision_node.Position = pos;
			parent_for_objects.AddChild(collision_node);
		}
	}

}
