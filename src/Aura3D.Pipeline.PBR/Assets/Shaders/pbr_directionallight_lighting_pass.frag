#version 300 es
precision highp float;
precision mediump sampler2DArray;
//{{defines}}


layout (location = 0) out vec4 FragColor;


#ifdef ENBALE_DEFERRED_SHADING

in vec2 TexCoords;
uniform sampler2D gBufferBaseColor;
uniform sampler2D gBufferNormalRoughness;
uniform sampler2D gBufferMetallicEmissive;
uniform sampler2D depthTexture;

uniform mat4 invProjection;
uniform mat4 invView;

#else

uniform sampler2D Texture_BaseColor;
uniform sampler2D Texture_Normal;
uniform sampler2D Texture_MetallicRoughness;
uniform sampler2D Texture_Emissive;
uniform sampler2D Texture_Occlusion;


in vec2 vTexCoord;
in vec3 vFragPosition;
in mat3 vTBN;
#endif

uniform vec3 viewPos;

#ifdef ENABLE_DIR_LIGHT
uniform vec3 dirLightDirection;
uniform vec3 dirLightColor;
uniform float dirLightIntensity;
#ifdef ENABLE_CSM
#define MAX_CSM_CASCADES 4
uniform mat4 dirLightCSMMatrices[MAX_CSM_CASCADES];
uniform sampler2DArray dirLightCSMMap;
uniform float dirLightCascadeSplitDepths[MAX_CSM_CASCADES + 1];
uniform int dirLightCascadeCount;
#else
uniform mat4 dirLightshadowMapMatrix;
uniform sampler2D dirLightshadowMap;
#endif
#endif

uniform mat4 viewMatrix;

#ifdef ENABLE_POINT_LIGHT
uniform vec3 pointLightPosition;
uniform vec3 pointLightColor;
uniform float radius;
uniform float softRadius;
uniform float pointLightIntensity;
uniform mat4 pointShadowMapMatrices[6];
uniform samplerCube pointLightShadowMap;
#endif

#ifdef ENABLE_SPOT_LIGHT
uniform vec3 spotLightPosition;
uniform vec3 spotLightDirection;
uniform vec3 spotLightColor;
uniform float spotLightIntensity;
uniform float spotLightCutOff;
uniform float radius;
uniform float softRadius;
uniform float spotLightOuterCutOff;
uniform mat4 spotLightshadowMapMatrix;
uniform sampler2D spotLightshadowMap;
#endif

const float PI = 3.14159265359;

#ifdef ENBALE_DEFERRED_SHADING
vec3 reconstructWorldPosFromDepth(vec2 texCoords) {
    float depth = texture(depthTexture, texCoords).r;
    vec3 ndc;
    ndc.xy = texCoords * 2.0 - 1.0;
    ndc.z = depth * 2.0 - 1.0;
    vec4 clipPos = vec4(ndc, 1.0);
    vec4 viewPos = invProjection * clipPos;
    viewPos /= viewPos.w;
    vec4 worldPos = invView * viewPos;
    return worldPos.xyz;
}
#endif

float DistributionGGX(vec3 N, vec3 H, float roughness) {
    float a = roughness * roughness;
    float a2 = a * a;
    float NdotH = max(dot(N, H), 0.0);
    float NdotH2 = NdotH * NdotH;
    float nom   = a2;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = PI * denom * denom;
    return nom / max(denom, 1e-7);
}

float GeometrySchlickGGX(float NdotV, float roughness) {
    float r = (roughness + 1.0);
    float k = (r * r) / 8.0;
    float nom   = NdotV;
    float denom = NdotV * (1.0 - k) + k;
    return nom / max(denom, 1e-7);
}

float GeometrySmith(vec3 N, vec3 V, vec3 L, float roughness) {
    float NdotV = max(dot(N, V), 0.0);
    float NdotL = max(dot(N, L), 0.0);
    float ggx2 = GeometrySchlickGGX(NdotV, roughness);
    float ggx1 = GeometrySchlickGGX(NdotL, roughness);
    return ggx1 * ggx2;
}

vec3 fresnelSchlick(float cosTheta, vec3 F0) {
    return F0 + (1.0 - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

float CalcPointLightAttenuation(float d, float r, float softRatio) {
    if (d > r) return 0.0;
    
    float nd = d / r;
    float atten = 1.0 / (1.0 + 25.0 * nd * nd);
    float softThresh = r * softRatio;
    float soft = smoothstep(r, softThresh, d);
    return atten * soft;
}

float CalculateShadow(vec3 fragPos, mat4 shadowMatrix, sampler2D shadowMap)
{
	vec4 shadowCoord = shadowMatrix * vec4(fragPos, 1.0);
	

	if (shadowCoord.x < -shadowCoord.w || shadowCoord.x > shadowCoord.w ||
        shadowCoord.y < -shadowCoord.w || shadowCoord.y > shadowCoord.w ||
        shadowCoord.z < -shadowCoord.w || shadowCoord.z > shadowCoord.w)
        return 1.0;
		
    shadowCoord /= shadowCoord.w;

	shadowCoord.xyz = shadowCoord.xyz * 0.5 + 0.5;


	float shadowValue = texture(shadowMap, shadowCoord.xy).x;
	float bias = 0.001;
	if (shadowValue < shadowCoord.z - bias)
		return 0.0;
	else
		return 1.0;
}

#ifdef ENABLE_CSM
	float CalculateCSMShadow(vec3 fragPosWorld, mat4 csmMatrices[4], sampler2DArray csmMap, float splits[5], int cascadeCount)
	{
		vec4 viewPos4 = viewMatrix * vec4(fragPosWorld, 1.0);
		float viewZ = -viewPos4.z;

		int cascade = 0;
		for (int i = 0; i < cascadeCount - 1; i++) {
			if (viewZ > splits[i + 1])
				cascade = i + 1;
		}

		vec4 shadowCoord = csmMatrices[cascade] * vec4(fragPosWorld, 1.0);

		if (shadowCoord.x < -shadowCoord.w || shadowCoord.x > shadowCoord.w ||
			shadowCoord.y < -shadowCoord.w || shadowCoord.y > shadowCoord.w ||
			shadowCoord.z < -shadowCoord.w || shadowCoord.z > shadowCoord.w)
			return 1.0;

		shadowCoord /= shadowCoord.w;
		shadowCoord.xyz = shadowCoord.xyz * 0.5 + 0.5;

		float shadowValue = texture(csmMap, vec3(shadowCoord.xy, float(cascade))).x;
		float bias = 0.001;
		if (shadowValue < shadowCoord.z - bias)
			return 0.0;
		return 1.0;
	}
#endif

float CalculatePointLightShadow(vec3 fragPos, vec3 lightPos, mat4 shadowMapMatrices[6], samplerCube shadowMapTexture)
{
    vec3 fragToLight = fragPos - lightPos;
    int face = 0;
    float maxComp = 0.0;
    if(abs(fragToLight.x) > maxComp) {
        face = fragToLight.x > 0.0 ? 0 : 1;
        maxComp = abs(fragToLight.x);
    }
    if(abs(fragToLight.y) > maxComp) {
        face = fragToLight.y > 0.0 ? 2 : 3;
        maxComp = abs(fragToLight.y);
    }
    if(abs(fragToLight.z) > maxComp) {
        face = fragToLight.z > 0.0 ? 4 : 5;
        maxComp = abs(fragToLight.z);
    }

    vec4 shadowCoord = shadowMapMatrices[face] * vec4(fragPos, 1.0);
    shadowCoord /= shadowCoord.w;
    shadowCoord.xyz = shadowCoord.xyz * 0.5 + 0.5;

    vec3 sampleDir = normalize(fragToLight);
    float shadowValue = texture(shadowMapTexture, sampleDir).r;

    float bias = 0.001;

    if (shadowValue < shadowCoord.z - bias)
        return 0.0;
    else
        return 1.0;

}


#ifdef ENABLE_DIR_LIGHT
vec3 calcSingleDirLight(vec3 N, vec3 V, vec3 fragPos, vec3 albedo, float metalness, float roughness) {
    vec3 L = normalize(-dirLightDirection);
    vec3 H = normalize(V + L);
    vec3 F0 = mix(vec3(0.04), albedo, metalness);

    float NDF = DistributionGGX(N, H, roughness);
    float G = GeometrySmith(N, V, L, roughness);
    vec3 F = fresnelSchlick(max(dot(H, V), 0.0), F0);

    vec3 specular = (NDF * G * F) / max(4.0 * max(dot(N, V), 0.0) * max(dot(N, L), 0.0), 1e-7);
    vec3 kS = F;
    vec3 kD = (vec3(1.0) - kS) * (1.0 - metalness);
    vec3 diffuse = kD * albedo / PI;
    float shadow = 1.0;

#ifdef ENABLE_CSM
    shadow = CalculateCSMShadow(fragPos, dirLightCSMMatrices, dirLightCSMMap, dirLightCascadeSplitDepths, dirLightCascadeCount);
#elif defined(ENABLE_SHADOWS)
    shadow = CalculateShadow(fragPos, dirLightshadowMapMatrix, dirLightshadowMap);
#endif
    float NdotL = max(dot(N, L), 0.0);
    return (diffuse + specular) * dirLightColor * dirLightIntensity * NdotL * shadow;
}
#endif

#ifdef ENABLE_POINT_LIGHT
vec3 calcSinglePointLight(vec3 N, vec3 V, vec3 fragPos, vec3 albedo, float metalness, float roughness) {
    vec3 L = normalize(pointLightPosition - fragPos);
    float distance = length(pointLightPosition - fragPos);
    float attenuation = CalcPointLightAttenuation(distance, radius, softRadius); 

    vec3 H = normalize(V + L);
    vec3 F0 = mix(vec3(0.04), albedo, metalness);

    float NDF = DistributionGGX(N, H, roughness);
    float G = GeometrySmith(N, V, L, roughness);
    vec3 F = fresnelSchlick(max(dot(H, V), 0.0), F0);

    vec3 specular = (NDF * G * F) / max(4.0 * max(dot(N, V), 0.0) * max(dot(N, L), 0.0), 1e-7);
    vec3 kS = F;
    vec3 kD = (vec3(1.0) - kS) * (1.0 - metalness);
    vec3 diffuse = kD * albedo / PI;

    float NdotL = max(dot(N, L), 0.0);
    vec3 radiance = pointLightColor * pointLightIntensity * attenuation;

    float shadow = 1.0;
    
#ifdef ENABLE_SHADOWS
    shadow = CalculatePointLightShadow(fragPos, pointLightPosition, pointShadowMapMatrices, pointLightShadowMap);
#endif

    return (diffuse + specular) * radiance * NdotL * shadow;
}
#endif

#ifdef ENABLE_SPOT_LIGHT
vec3 calcSingleSpotLight(vec3 N, vec3 V, vec3 fragPos, vec3 albedo, float metalness, float roughness) {
    vec3 L = normalize(spotLightPosition - fragPos);
    float distance = length(spotLightPosition - fragPos);
    float distanceAttenuation = CalcPointLightAttenuation(distance, radius, softRadius); 
    
    float theta = dot(L, normalize(-spotLightDirection));
    float epsilon = spotLightCutOff - spotLightOuterCutOff;
    float angleAttenuation = clamp((theta - spotLightOuterCutOff) / epsilon, 0.0, 1.0);
    
    vec3 H = normalize(V + L);
    vec3 F0 = mix(vec3(0.04), albedo, metalness);

    float NDF = DistributionGGX(N, H, roughness);
    float G = GeometrySmith(N, V, L, roughness);
    vec3 F = fresnelSchlick(max(dot(H, V), 0.0), F0);

    vec3 specular = (NDF * G * F) / max(4.0 * max(dot(N, V), 0.0) * max(dot(N, L), 0.0), 1e-7);
    vec3 kS = F;
    vec3 kD = (vec3(1.0) - kS) * (1.0 - metalness);
    vec3 diffuse = kD * albedo / PI;

    float totalAttenuation = distanceAttenuation * angleAttenuation;
    float NdotL = max(dot(N, L), 0.0);
    vec3 radiance = spotLightColor * spotLightIntensity * totalAttenuation;
    float shadow = 1.0;
#ifdef ENABLE_SHADOWS
    shadow = CalculateShadow(fragPos, spotLightshadowMapMatrix, spotLightshadowMap);
#endif
    return (diffuse + specular) * radiance * NdotL * shadow;;
}
#endif

void main() {
#ifdef ENBALE_DEFERRED_SHADING
    vec4 baseColor = texture(gBufferBaseColor, TexCoords);
    vec4 metallicEmissive = texture(gBufferMetallicEmissive, TexCoords);
    vec3 albedo = baseColor.xyz;
    float metalness = metallicEmissive.x;

    vec4 normalRough = texture(gBufferNormalRoughness, TexCoords);
    vec3 N = normalize(normalRough.rgb * 2.0 - 1.0);
    float roughness = clamp(normalRough.a, 0.05, 1.0);

    vec3 fragPosWorld = reconstructWorldPosFromDepth(TexCoords);

#else
    vec4 baseColor = texture(Texture_BaseColor, vTexCoord);
    vec3 normal = texture(Texture_Normal, vTexCoord).xyz;
    vec4 metalness_roughness = texture(Texture_MetallicRoughness, vTexCoord);
    
    normal = normalize(normal.xyz * 2.0 - 1.0);
    normal = normalize(vTBN * normal);
    
	if (!gl_FrontFacing) 
	{
		normal = -normal;
	}
    vec3 N = normal;
    vec3 albedo = baseColor.rgb;
    float metalness = metalness_roughness.b;
    float roughness = metalness_roughness.g;

    vec3 fragPosWorld = vFragPosition;

#endif
    vec3 V = normalize(viewPos - fragPosWorld);

    vec3 lightContribution = vec3(0.0);
    #ifdef ENABLE_DIR_LIGHT
    lightContribution = calcSingleDirLight(N, V, fragPosWorld, albedo, metalness, roughness);
    #endif
    #ifdef ENABLE_POINT_LIGHT
    lightContribution = calcSinglePointLight(N, V, fragPosWorld, albedo, metalness, roughness);
    #endif
    #ifdef ENABLE_SPOT_LIGHT
    lightContribution = calcSingleSpotLight(N, V, fragPosWorld, albedo, metalness, roughness);
    #endif
#ifdef ENBALE_DEFERRED_SHADING
    float alpha = baseColor.a;
#endif

#ifdef BLENDMODE_TRANSLUCENT
#ifdef IS_FIRST_LIGHT
    float alpha = baseColor.a;
#else
    float alpha = 0.0;
#endif
    
#endif
    FragColor = vec4(lightContribution, alpha);
}