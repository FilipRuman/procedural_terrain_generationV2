shader_type spatial;

struct NormalAlbedoRoughness{
	vec3 normal;
	vec3 albedo;
	vec3 roughness;
};

// Implementing this shit myself seems like a torture, but thankfully there is a really nice implementation for Godot!
// https://github.com/acegiak/Godot4TerrainShader/blob/main/addons/terrain-shader/StochasticTexture.gdshaderh
vec2 hash( vec2 p )
{
	return fract( sin( p * mat2( vec2( 127.1, 311.7 ), vec2( 269.5, 183.3 ) ) ) * 43758.5453 );
}
// 3 birds with one stone
NormalAlbedoRoughness triple_stochastic_sample(sampler2D normal_texture,sampler2D albedo_texture,sampler2D roughness_texture, vec2 uv){
	vec2 skewV = mat2(vec2(1.0,1.0),vec2(-0.57735027 , 1.15470054))*uv * 3.464;

	vec2 vxID = floor(skewV);
	vec2 fracV = fract(skewV);
	vec3 barry = vec3(fracV.x,fracV.y,1.0-fracV.x-fracV.y);

	mat4 bw_vx = barry.z>0.0?
		mat4(vec4(vxID,0.0,0.0),vec4((vxID+vec2(0.0,1.0)),0.0,0.0),vec4(vxID+vec2(1.0,0.0),0,0),vec4(barry.zyx,0)):
		mat4(vec4(vxID+vec2(1.0,1.0),0.0,0.0),vec4((vxID+vec2(1.0,0.0)),0.0,0.0),vec4(vxID+vec2(0.0,1.0),0,0),vec4(-barry.z,1.0-barry.y,1.0-barry.x,0));

	vec2 ddx = dFdx(uv);
	vec2 ddy = dFdy(uv);


	vec2 uv_x = uv+hash(bw_vx[0].xy);
	vec2 uv_y = uv+hash(bw_vx[1].xy);
	vec2 uv_z = uv+hash(bw_vx[2].xy);

	vec4 normal = (textureGrad(normal_texture,uv_x,ddx,ddy)*bw_vx[3].x) +
	(textureGrad(normal_texture,uv_y,ddx,ddy)*bw_vx[3].y) +
	(textureGrad(normal_texture,uv_z,ddx,ddy)*bw_vx[3].z);

	vec4 albedo = (textureGrad(albedo_texture,uv_x,ddx,ddy)*bw_vx[3].x) +
	(textureGrad(albedo_texture,uv_y,ddx,ddy)*bw_vx[3].y) +
	(textureGrad(albedo_texture,uv_z,ddx,ddy)*bw_vx[3].z);

	vec4 roughness = (textureGrad(roughness_texture,uv_x,ddx,ddy)*bw_vx[3].x) +
	(textureGrad(roughness_texture,uv_y,ddx,ddy)*bw_vx[3].y) +
	(textureGrad(roughness_texture,uv_z,ddx,ddy)*bw_vx[3].z);

	return NormalAlbedoRoughness(normal.xyz,albedo.xyz,roughness.xyz);

}

const int BIOMES_COUNT = 8;
const int chunk_data_maps_count = 100;

uniform float global_brightness;
uniform float global_saturation;

uniform float rock_saturation;
uniform float rock_scale;
uniform sampler2D rock_texture;
uniform sampler2D rock_normal_map;
uniform sampler2D rock_roughness;

uniform sampler2D uv_noise_texture_1;
uniform float uv_noise_frequency_1;
uniform vec2 uv_noise_strength_1;

uniform sampler2D uv_noise_texture_2;
uniform float uv_noise_frequency_2;
uniform vec2 uv_noise_strength_2;

instance uniform int chunk_data_map_index;
uniform sampler2D[chunk_data_maps_count] map_1:repeat_disable;
uniform sampler2D[chunk_data_maps_count] map_2:repeat_disable;

uniform vec3[BIOMES_COUNT] texture_tint;
uniform float[BIOMES_COUNT] texture_saturation;
uniform float[BIOMES_COUNT] texture_scale;
uniform sampler2D[BIOMES_COUNT] biome_albedo_textures;
uniform sampler2D[BIOMES_COUNT] biome_normal_textures;
uniform sampler2D[BIOMES_COUNT] biome_roughness_textures;

uniform sampler2D other_noise;
uniform float metallic;
uniform float spectacular;
uniform float other_noise_scale;

uniform float uv_margin;

NormalAlbedoRoughness triple_stochastic_triplanar (vec3 position, vec3 worldNormal, vec3 adjusted_normal, float biome_scale,
 sampler2D biome_normal_map, sampler2D biome_texture, sampler2D biome_roughness){
	vec3 weights = adjusted_normal / (adjusted_normal.x + adjusted_normal.y + adjusted_normal.z) * 3.0;

	vec2 uv_x = position.zy;
	vec2 uv_y = position.xz;
	vec2 uv_z = position.xy;

	NormalAlbedoRoughness output = NormalAlbedoRoughness(vec3(0), vec3(0), vec3(0));
	NormalAlbedoRoughness partial;
	if (weights.x > 0.01){
		partial = triple_stochastic_sample(rock_normal_map, rock_texture, rock_roughness, uv_x * rock_scale);
		output.normal += partial.normal * weights.x;
		output.albedo += partial.albedo * rock_saturation * weights.x;
		output.roughness += partial.roughness * weights.x;
	}
	if (weights.y > 0.01){
		partial = triple_stochastic_sample(biome_normal_map, biome_texture, biome_roughness, uv_y * biome_scale);
		output.normal += partial.normal * weights.y;
		output.albedo += partial.albedo * rock_saturation * weights.y;
		output.roughness += partial.roughness * weights.y;
	}
	if (weights.z > 0.01){
		partial = triple_stochastic_sample(rock_normal_map, rock_texture, rock_roughness, uv_z * rock_scale);
		output.normal += partial.normal * weights.z;
		output.albedo += partial.albedo * rock_saturation * weights.z;
		output.roughness += partial.roughness * weights.z;
	}

	return output;
}

void handle_biome(int biome, float influence, vec3 position, vec3 worldNormal, vec3 adjusted_normal, inout vec3 output_color, inout vec3 output_normal, inout vec3 output_roughness){
	if (influence < 0.01){
		return ;
	}

	NormalAlbedoRoughness output = triple_stochastic_triplanar(position, worldNormal, adjusted_normal, texture_scale[biome],biome_normal_textures[biome], biome_albedo_textures[biome],biome_roughness_textures[biome]);

	output_color += (output.albedo + texture_tint[biome] - vec3(1) * texture_saturation[biome]) * influence;
	output_normal += output.normal * influence;
	output_roughness += output.roughness * influence;
}

NormalAlbedoRoughness collect_biome_data(vec4 biome_data_1, vec4 biome_data_2,vec3 world_pos, vec3 world_normal, vec3 adjusted_normal){
	vec3 output_color = vec3(0, 0, 0);
	vec3 output_normal = vec3(0, 0, 0);
	vec3 output_roughness = vec3(0, 0, 0);

	handle_biome(0, biome_data_1.r, world_pos, world_normal, adjusted_normal, output_color, output_normal, output_roughness);
	handle_biome(1, biome_data_1.g, world_pos, world_normal, adjusted_normal, output_color, output_normal, output_roughness);
	handle_biome(2, biome_data_1.b, world_pos, world_normal, adjusted_normal, output_color, output_normal, output_roughness);
	handle_biome(3, biome_data_1.a, world_pos, world_normal, adjusted_normal, output_color, output_normal, output_roughness);
	handle_biome(4, biome_data_2.r, world_pos, world_normal, adjusted_normal, output_color, output_normal, output_roughness);
	handle_biome(5, biome_data_2.g, world_pos, world_normal, adjusted_normal, output_color, output_normal, output_roughness);
	handle_biome(6, biome_data_2.b, world_pos, world_normal, adjusted_normal, output_color, output_normal, output_roughness);
	handle_biome(7, biome_data_2.a, world_pos, world_normal, adjusted_normal, output_color, output_normal, output_roughness);

	return NormalAlbedoRoughness(output_normal, output_color, output_roughness);
}

void fragment(){
	vec3 world_pos = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
	vec3 world_normal = normalize((INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz);
	vec3 adjusted_normal = pow(abs(world_normal), vec3(8.0));

 	vec2 uv_with_margin_included = vec2(uv_margin, uv_margin) + UV * (1.0 - 2.0 * uv_margin);
	vec2 uv_noise_1 = texture(uv_noise_texture_1, world_pos.xz * uv_noise_frequency_1).r * uv_noise_strength_1;
	vec2 uv_noise_2 = texture(uv_noise_texture_2, world_pos.xz * uv_noise_frequency_2).r * uv_noise_strength_2;
	vec2 noisy_uv = uv_with_margin_included + uv_noise_1 + uv_noise_2 ;

	vec4 biome_data_1 = texture(map_1[chunk_data_map_index],noisy_uv);
	vec4 biome_data_2 = texture(map_2[chunk_data_map_index],noisy_uv);
	NormalAlbedoRoughness output = collect_biome_data(biome_data_1, biome_data_2, world_pos, world_normal, adjusted_normal);
	ALBEDO = output.albedo;
	NORMAL_MAP = output.normal;
	ROUGHNESS  = output.roughness.r;


	//apply processing

	ALBEDO *= global_brightness;

	ALBEDO *= mix(0.8, 1.2, texture(other_noise, world_pos.xz*other_noise_scale).r);
	ALBEDO -= vec3(1) * global_saturation;
	METALLIC = texture(other_noise, world_pos.xz*other_noise_scale).r * metallic;
	SPECULAR = texture(other_noise, world_pos.xz*other_noise_scale).r* spectacular;
}
