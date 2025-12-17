shader_type spatial;
const int BIOMES_COUNT = 8;

uniform float global_brightness;
uniform float global_saturation;

uniform float rock_saturation;
uniform float rock_scale;
uniform sampler2D rock_texture;
uniform sampler2D uv_noise_texture;
uniform sampler2D texture_repetition_noise;
uniform vec2 uv_noise_strength; 
uniform vec3 texture_noise_strenght;

uniform sampler2D roughness_gradient;
uniform sampler2D noise;
uniform sampler2D map_1;
uniform sampler2D map_2;

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
	if (weights.x > 0.01)
		color += texture(rock_texture, uv_x * rock_scale).rgb * weights.x  * rock_saturation;
	if (weights.y > 0.01)
		color += texture(sampler, uv_y * scale).rgb * weights.y;
	if (weights.z > 0.01)
		color += texture(rock_texture, uv_z * rock_scale).rgb * weights.z  * rock_saturation;

	return color / 3.0;
}

vec3 handle_biome_color(vec3 input_color, int type, float influence, vec3 position, vec3 worldNormal, vec3 adjusted_normal){

	if (type == 0 || influence < 0.01) return input_color;
	

	int biome = type - 1;
	vec3 texture_color = triplanar(position, worldNormal, adjusted_normal, biome_textures[biome], texture_scale[biome]);
	vec3 biome_color = (texture_color + texture_tint[biome] - vec3(1) * texture_saturation[biome]) * influence;

	return input_color + biome_color;
}

void fragment(){
	vec3 world_pos = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
	vec3 world_normal = normalize((INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz);
	vec3 adjusted_normal = pow(abs(world_normal), vec3(8.0));
	world_pos += texture(texture_repetition_noise,UV).r * texture_noise_strenght;

	float uv_noise = texture(uv_noise_texture,UV).r;
	
	vec2 fixed_uv = UV+uv_noise* uv_noise_strength;
	vec4 color_1 = texture(map_1,fixed_uv);
	vec4 color_2 = texture(map_2,fixed_uv);


	vec3 output_color = vec3(0,0,0);
	output_color = handle_biome_color(output_color, 0, color_1.r, world_pos, world_normal, adjusted_normal);
	output_color = handle_biome_color(output_color, 1, color_1.g, world_pos, world_normal, adjusted_normal);
	output_color = handle_biome_color(output_color, 2, color_1.b, world_pos, world_normal, adjusted_normal);
	output_color = handle_biome_color(output_color, 3, color_1.a, world_pos, world_normal, adjusted_normal);
	output_color = handle_biome_color(output_color, 4, color_2.r, world_pos, world_normal, adjusted_normal);
	output_color = handle_biome_color(output_color, 5, color_2.g, world_pos, world_normal, adjusted_normal);
	output_color = handle_biome_color(output_color, 6, color_2.b, world_pos, world_normal, adjusted_normal);
	output_color = handle_biome_color(output_color, 7, color_2.a, world_pos, world_normal, adjusted_normal);
	ALBEDO =output_color;

	ALBEDO *= global_brightness;

	ALBEDO *= mix(0.9, 1.1, texture(noise,UV).r);
	ALBEDO -= vec3(1) * global_saturation; 
	ROUGHNESS =texture(roughness_gradient,texture(noise,UV).rg).r ;
	METALLIC = 0.0;
}
