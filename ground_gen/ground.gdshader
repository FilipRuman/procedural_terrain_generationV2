shader_type spatial;
const int BIOMES_COUNT = 256;

uniform float global_brightness;
uniform float global_saturation;

uniform float rock_saturation;
uniform float rock_scale;
uniform sampler2D rock_texture; 

uniform sampler2D biome_type_map;
uniform sampler2D biome_influence_map;

uniform vec3[BIOMES_COUNT] texture_tint;
uniform float[BIOMES_COUNT] texture_saturation; 
uniform float[BIOMES_COUNT] texture_scale;
uniform sampler2D[BIOMES_COUNT] biome_textures;

vec3 triplanar (vec3 position, vec3 worldNormal,vec3 adjusted_normal, sampler2D sampler,float scale){
	vec3 weights = adjusted_normal / (adjusted_normal.x + adjusted_normal.y + adjusted_normal.z) * 3.0;
	
	vec2 uv_x = position.zy;
	vec2 uv_y = position.xz;
	vec2 uv_z = position.xy;
	
	vec3 color = vec3(0,0,0); 
	if (weights.x > 0.01){
		color += texture(rock_texture, uv_x * rock_scale).rgb * weights.x  * rock_saturation;
	}
	if (weights.y > 0.01){
		color += texture(sampler, uv_y * scale).rgb * weights.y;
	}	
	if (weights.z > 0.01){
		color += texture(rock_texture, uv_z * rock_scale).rgb * weights.z  * rock_saturation;
	}	
	
	return color / 3.0;
}

vec3 handle_biome_color(vec3 input_color, float type_float, float influence, vec3 position, vec3 worldNormal, vec3 adjusted_normal){
	int type = int(type_float * 255.0);

	if (type == 0){
	return input_color;
	}

	int biome = type - 1;
	vec3 texture_color = triplanar(position, worldNormal, adjusted_normal, biome_textures[biome], texture_scale[biome]);
	vec3 biome_color = (texture_color + texture_tint[biome] - vec3(1) * texture_saturation[biome]) * influence;
	
	return input_color + biome_color;
}

void fragment(){
	vec3 world_pos = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
	vec3 world_normal = normalize((INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz);
	vec3 adjusted_normal = pow(abs(world_normal), vec3(8.0));
	
	vec4 biome_types = texture(biome_type_map,UV);
	vec4 biome_influences = texture(biome_influence_map,UV);
	vec3 color = vec3(0,0,0);
	color = handle_biome_color(color, biome_types.r, biome_influences.r, world_pos, world_normal, adjusted_normal);
	color = handle_biome_color(color, biome_types.g, biome_influences.g, world_pos, world_normal, adjusted_normal);
	color = handle_biome_color(color, biome_types.b, biome_influences.b, world_pos, world_normal, adjusted_normal);
	color = handle_biome_color(color, biome_types.a, biome_influences.a, world_pos, world_normal, adjusted_normal);

	ALBEDO = color;

	ALBEDO *= global_brightness;
	ALBEDO -= vec3(1) * global_saturation;
}
