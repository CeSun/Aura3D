#version 300 es
precision highp float;
precision highp sampler2D;
precision highp samplerCube;

//{{defines}}

layout(location = 0) out vec4 o_iblColor;

#ifdef ENBALE_DEFERRED_SHADING

in vec2 v_texCoord;
in vec4 v_clipPos;

uniform sampler2D gBufferBaseColor;
uniform sampler2D gBufferNormalRoughness;
uniform sampler2D gBufferMetallicEmissive;
uniform sampler2D depthTexture;


#else 

in vec2 vTexCoord;
in vec3 vFragPosition;
in mat3 vTBN;
uniform sampler2D Texture_BaseColor;
uniform sampler2D Texture_Normal;
uniform sampler2D Texture_MetallicRoughness;
uniform sampler2D Texture_Emissive;
uniform sampler2D Texture_Occlusion;

#endif

uniform samplerCube u_irradianceMap;
uniform samplerCube u_prefilterMap;
uniform sampler2D u_brdfLUT;

uniform mat4 u_viewMatrix;
uniform mat4 u_projMatrix;
uniform mat4 u_invViewProjMatrix;
uniform vec3 u_cameraPos;
uniform float u_max_mipmap;

const float PI = 3.14159265359;
const float EPSILON = 0.0001;

vec3 reconstructWorldPosition(vec2 texCoord, float depth) {
    vec3 clipPos = vec3(texCoord * 2.0 - 1.0, depth);
    vec4 ndcPos = vec4(clipPos, 1.0);
    vec4 worldPos = u_invViewProjMatrix * ndcPos;
    worldPos /= worldPos.w;
    return worldPos.xyz;
}

vec3 fresnelSchlickRoughness(float cosTheta, vec3 F0, float roughness) {
    return F0 + (max(vec3(1.0 - roughness), F0) - F0) * pow(1.0 - cosTheta, 5.0);
}

void main() {

#ifdef ENBALE_DEFERRED_SHADING
    // Sample base color (albedo)
    vec4 basecolor = texture(gBufferBaseColor, v_texCoord);
    vec3 albedo = basecolor.rgb;
    float alpha = basecolor.a;
    // Sample normal and roughness
    vec3 normal = normalize(texture(gBufferNormalRoughness, v_texCoord).rgb);
	normal = normalize(normal * 2.0 - 1.0);
    float roughness = texture(gBufferNormalRoughness, v_texCoord).a;

    // Sample metallic and emissive
    float metallic = texture(gBufferMetallicEmissive, v_texCoord).r;
    vec3 emissive = texture(gBufferMetallicEmissive, v_texCoord).gba;

    // Reconstruct world position from depth
    float depth = texture(depthTexture, v_texCoord).r;
    vec3 worldPos = reconstructWorldPosition(v_texCoord, depth);

#else

    vec4 baseColor = texture(Texture_BaseColor, vTexCoord);
    vec3 normal = texture(Texture_Normal, vTexCoord).xyz;
    vec4 metalness_roughness = texture(Texture_MetallicRoughness, vTexCoord);

    vec3 albedo = baseColor.rgb;
    float alpha = baseColor.a;
   
    normal = normalize(normal.xyz * 2.0 - 1.0);
    normal = normalize(vTBN * normal);

	if (!gl_FrontFacing) 
	{
		normal = -normal;
	}

    float roughness = metalness_roughness.b;
    float metallic = metalness_roughness.g;
    vec3 worldPos = vFragPosition;
#endif
    // Clamp parameters to valid range
    roughness = clamp(roughness, 0.01, 1.0);
    metallic = clamp(metallic, 0.0, 1.0);

    // Convert normal to world space if stored in view space
    vec3 worldNormal = normal;
    /*
    if (u_viewMatrix[0][0] != 0.0) {
        worldNormal = normalize(mat3(inverse(u_viewMatrix)) * normal);
    }
    */

    // Calculate view direction and reflection vector
    vec3 V = normalize(u_cameraPos - worldPos);
    vec3 R = reflect(-V, worldNormal);
    float NdotV = max(dot(worldNormal, V), EPSILON);

    // Calculate base reflectivity (F0)
    vec3 F0 = vec3(0.04);
    F0 = mix(F0, albedo, metallic);

    // Calculate diffuse IBL
    vec3 irradiance = texture(u_irradianceMap, worldNormal).rgb;
    vec3 diffuse = albedo * (1.0 - metallic) * irradiance;

    // Calculate specular IBL
    vec3 F = fresnelSchlickRoughness(NdotV, F0, roughness);
    vec3 prefilteredColor = textureLod(u_prefilterMap, R, roughness * u_max_mipmap).rgb;
    vec2 brdf = texture(u_brdfLUT, vec2(NdotV, roughness)).rg;
    vec3 specular = prefilteredColor * (F * brdf.x + brdf.y);

    // Apply ambient occlusion (default 1.0 if not available)
    float ao = 1.0;
    diffuse *= ao;
    specular *= ao;

    // Output final IBL color
    vec3 ambient = diffuse + specular;
    o_iblColor = vec4(ambient, alpha);
}