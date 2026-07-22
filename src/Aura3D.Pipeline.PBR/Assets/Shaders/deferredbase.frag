#version 300 es
precision mediump float;
layout (location = 0) out vec4 Buffer_BaseColor;
layout (location = 1) out vec4 Buffer_Normal_Roughness;
layout (location = 2) out vec4 Buffer_Metalness_Emissive;

//{{defines}}

uniform sampler2D Texture_BaseColor;
uniform sampler2D Texture_Normal;
uniform sampler2D Texture_MetallicRoughness;
uniform sampler2D Texture_Emissive;
uniform sampler2D Texture_Occlusion;


in vec2 vTexCoord;
in vec3 vFragPosition;
in mat3 vTBN;

void main() 
{
    vec4 baseColor = texture(Texture_BaseColor, vTexCoord);
    vec3 normal = texture(Texture_Normal, vTexCoord).xyz;
    vec4 metalness_roughness = texture(Texture_MetallicRoughness, vTexCoord);
    vec4 emissive = texture(Texture_Emissive, vTexCoord);
    vec4 occlusion = texture(Texture_Occlusion, vTexCoord);
    
    normal = normalize(normal.xyz * 2.0 - 1.0);
    normal = normalize(vTBN * normal);
    
	if (!gl_FrontFacing) 
	{
		normal = -normal;
	}
    
    normal = normal * 0.5 + 0.5;

    Buffer_BaseColor = baseColor;
    Buffer_Normal_Roughness = vec4(normal, metalness_roughness.g);
    Buffer_Metalness_Emissive = vec4(metalness_roughness.b, emissive.rgb);

}