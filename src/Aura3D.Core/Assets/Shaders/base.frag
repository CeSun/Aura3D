#version 300 es
precision mediump float;
out vec4 outColor;

//{{defines}}

#define MAX_DIRECTIONAL_LIGHTS 4
#define MAX_POINT_LIGHTS 4
#define MAX_SPOT_LIGHTS 4


#define DL_SHADOW_ASSIGN(index) if (DirectionalLights[index].castShadow == 1.0) \
	shadows[index] = CalculateShadow(DirectionalLights[index].shadowMapMatrix, DirectionalLightShadowMaps[index]); \
	else \
	shadows[index] = 1.0;

#define PL_SHADOW_ASSIGN(index) if (PointLights[index].castShadow == 1.0) \
	shadows[index] = CalculatePointLightShadow(PointLights[index].position, PointLights[index].shadowMapMatrices, PointLightShadowMaps[index]); \
		else \
	shadows[index] = 1.0;

#define SP_SHADOW_ASSIGN(index) if (SpotLights[index].castShadow == 1.0) \
	shadows[index] = CalculateShadow(SpotLights[index].shadowMapMatrix, SpotLightShadowMaps[index]); \
	else \
	shadows[index] = 1.0;

#define REPEAT_DL_SHADOW_ASSIGN_1 DL_SHADOW_ASSIGN(0)
#define REPEAT_DL_SHADOW_ASSIGN_2 REPEAT_DL_SHADOW_ASSIGN_1; DL_SHADOW_ASSIGN(1)
#define REPEAT_DL_SHADOW_ASSIGN_3 REPEAT_DL_SHADOW_ASSIGN_2; DL_SHADOW_ASSIGN(2)
#define REPEAT_DL_SHADOW_ASSIGN_4 REPEAT_DL_SHADOW_ASSIGN_3; DL_SHADOW_ASSIGN(3)
#define REPEAT_DL_SHADOW_ASSIGN_5 REPEAT_DL_SHADOW_ASSIGN_4; DL_SHADOW_ASSIGN(4)
#define REPEAT_DL_SHADOW_ASSIGN_6 REPEAT_DL_SHADOW_ASSIGN_5; DL_SHADOW_ASSIGN(5)
#define REPEAT_DL_SHADOW_ASSIGN_7 REPEAT_DL_SHADOW_ASSIGN_6; DL_SHADOW_ASSIGN(6)
#define REPEAT_DL_SHADOW_ASSIGN_8 REPEAT_DL_SHADOW_ASSIGN_7; DL_SHADOW_ASSIGN(7)
#define REPEAT_DL_SHADOW_ASSIGN_9 REPEAT_DL_SHADOW_ASSIGN_8; DL_SHADOW_ASSIGN(8)
#define REPEAT_DL_SHADOW_ASSIGN_10 REPEAT_DL_SHADOW_ASSIGN_9; DL_SHADOW_ASSIGN(9)


#define REPEAT_PL_SHADOW_ASSIGN_1 PL_SHADOW_ASSIGN(0)
#define REPEAT_PL_SHADOW_ASSIGN_2 REPEAT_PL_SHADOW_ASSIGN_1; PL_SHADOW_ASSIGN(1)
#define REPEAT_PL_SHADOW_ASSIGN_3 REPEAT_PL_SHADOW_ASSIGN_2; PL_SHADOW_ASSIGN(2)
#define REPEAT_PL_SHADOW_ASSIGN_4 REPEAT_PL_SHADOW_ASSIGN_3; PL_SHADOW_ASSIGN(3)
#define REPEAT_PL_SHADOW_ASSIGN_5 REPEAT_PL_SHADOW_ASSIGN_4; PL_SHADOW_ASSIGN(4)
#define REPEAT_PL_SHADOW_ASSIGN_6 REPEAT_PL_SHADOW_ASSIGN_5; PL_SHADOW_ASSIGN(5)
#define REPEAT_PL_SHADOW_ASSIGN_7 REPEAT_PL_SHADOW_ASSIGN_6; PL_SHADOW_ASSIGN(6)
#define REPEAT_PL_SHADOW_ASSIGN_8 REPEAT_PL_SHADOW_ASSIGN_7; PL_SHADOW_ASSIGN(7)
#define REPEAT_PL_SHADOW_ASSIGN_9 REPEAT_PL_SHADOW_ASSIGN_8; PL_SHADOW_ASSIGN(8)
#define REPEAT_PL_SHADOW_ASSIGN_10 REPEAT_PL_SHADOW_ASSIGN_9; PL_SHADOW_ASSIGN(9)


#define REPEAT_SP_SHADOW_ASSIGN_1 SP_SHADOW_ASSIGN(0)
#define REPEAT_SP_SHADOW_ASSIGN_2 REPEAT_SP_SHADOW_ASSIGN_1; SP_SHADOW_ASSIGN(1)
#define REPEAT_SP_SHADOW_ASSIGN_3 REPEAT_SP_SHADOW_ASSIGN_2; SP_SHADOW_ASSIGN(2)
#define REPEAT_SP_SHADOW_ASSIGN_4 REPEAT_SP_SHADOW_ASSIGN_3; SP_SHADOW_ASSIGN(3)
#define REPEAT_SP_SHADOW_ASSIGN_5 REPEAT_SP_SHADOW_ASSIGN_4; SP_SHADOW_ASSIGN(4)
#define REPEAT_SP_SHADOW_ASSIGN_6 REPEAT_SP_SHADOW_ASSIGN_5; SP_SHADOW_ASSIGN(5)
#define REPEAT_SP_SHADOW_ASSIGN_7 REPEAT_SP_SHADOW_ASSIGN_6; SP_SHADOW_ASSIGN(6)
#define REPEAT_SP_SHADOW_ASSIGN_8 REPEAT_SP_SHADOW_ASSIGN_7; SP_SHADOW_ASSIGN(7)
#define REPEAT_SP_SHADOW_ASSIGN_9 REPEAT_SP_SHADOW_ASSIGN_8; SP_SHADOW_ASSIGN(8)
#define REPEAT_SP_SHADOW_ASSIGN_10 REPEAT_SP_SHADOW_ASSIGN_9; SP_SHADOW_ASSIGN(9)



struct s_directional_light_info
{
	vec3 color;
	vec3 direction;	
	mat4 shadowMapMatrix;
	float castShadow;
};

struct s_point_light_info
{
	vec3 color;
	vec3 position;
	float radius; 
	float castShadow;
	mat4 shadowMapMatrices[6];
};

struct s_spot_light_info
{
	vec3 color;
	vec3 position;
	vec3 direction;
	float radius; 
	float inner_cone_cos;
	float outer_cone_cos;
	float castShadow;
	mat4 shadowMapMatrix;
};


in vec2 vTexCoord;
in vec3 vFragPosition;
in mat3 vTBN;
in vec3 vNormal;


uniform sampler2D BaseColorTexture;
uniform sampler2D NormalTexture;

uniform int HasNormalTexture;

uniform vec4 BaseColor;

uniform int HasBaseColorTexture;

uniform float ambientIntensity;

uniform vec3 cameraPosition;

#if defined(BLENDMODE_MASKED) || defined(BLENDMODE_TRANSLUCENT)

uniform float alphaCutoff;

#endif

uniform s_directional_light_info DirectionalLights[MAX_DIRECTIONAL_LIGHTS];
uniform s_point_light_info PointLights[MAX_POINT_LIGHTS];
uniform s_spot_light_info SpotLights[MAX_SPOT_LIGHTS];


uniform sampler2D DirectionalLightShadowMaps[MAX_DIRECTIONAL_LIGHTS];
uniform samplerCube PointLightShadowMaps[MAX_POINT_LIGHTS];
uniform sampler2D SpotLightShadowMaps[MAX_SPOT_LIGHTS];

vec3 CalculateDirectionalLight(vec3 lightDirection, vec3 lightColor, vec3 baseColor, vec3 normal);
vec3 CalculatePointLight(vec3 lightPosition, vec3 lightColor, float radius, vec3 baseColor, vec3 normal);
vec3 CalculateSpotLight(vec3 lightPosition, vec3 lightColor, vec3 lightDirection, float radius, float inner_cone_cos, float outer_cone_cos, vec3 baseColor, vec3 normal);
float CalculateShadow(mat4 shadowMatrix, sampler2D shadowMap);
float CalculatePointLightShadow(vec3 position, mat4 shadowMapMatrices[6], samplerCube shadowMapTexture);



void main()
{
	vec4 baseColor = BaseColor;

	if (HasBaseColorTexture == 1)
	{
		baseColor = texture(BaseColorTexture, vTexCoord);
	}


	vec3 normal = vNormal;

	if (HasNormalTexture == 1)
	{
		normal = texture(NormalTexture, vTexCoord).xyz;
		normal = normalize(normal * 2.0 - 1.0);
		normal = normalize(vTBN * normal);
	}
	
	if (!gl_FrontFacing) 
	{
		normal = -normal;
	}
	#if defined(BLENDMODE_MASKED) || defined(BLENDMODE_TRANSLUCENT)
		if (baseColor.a <= alphaCutoff)
			discard;
	#endif
	
	vec3 finalColor = vec3(0.0);


	finalColor += baseColor.xyz * ambientIntensity;
	float shadows[10];
	REPEAT_DL_SHADOW_ASSIGN_4//
	for(int i = 0; i < MAX_DIRECTIONAL_LIGHTS; ++i)
	{
		vec3 color = CalculateDirectionalLight(DirectionalLights[i].direction, DirectionalLights[i].color, baseColor.xyz, normal);
		
		if (DirectionalLights[i].castShadow == 0.0)
			shadows[i] = 1.0;

		finalColor += (color * shadows[i]);
	}
	
	REPEAT_PL_SHADOW_ASSIGN_4//
	for(int i = 0; i < MAX_POINT_LIGHTS; ++i)
	{
		vec3 color = CalculatePointLight(PointLights[i].position, PointLights[i].color,PointLights[i].radius, baseColor.xyz, normal);

		if (PointLights[i].castShadow == 0.0)
			shadows[i] = 1.0;

		finalColor += (color * shadows[i]);
	}
	
	REPEAT_SP_SHADOW_ASSIGN_4//
	for(int i = 0; i < MAX_SPOT_LIGHTS; ++i)
	{
		vec3 color = CalculateSpotLight(SpotLights[i].position, SpotLights[i].color, SpotLights[i].direction, SpotLights[i].radius, SpotLights[i].inner_cone_cos, SpotLights[i].outer_cone_cos, baseColor.xyz, normal);
		
		if (SpotLights[i].castShadow == 0.0)
			shadows[i] = 1.0;

		finalColor += (color * shadows[i]);
	}

#ifdef BLENDMODE_TRANSLUCENT

	outColor = vec4(finalColor, baseColor.a);
#else

	outColor = vec4(finalColor, 1.0);
#endif
	
}


vec3 CalculateDirectionalLight(vec3 lightDirection, vec3 lightColor, vec3 baseColor, vec3 normal)
{
	float diff = max(dot(normal, -lightDirection), 0.0);

	vec3 viewDir = normalize(cameraPosition - vFragPosition);

	vec3 halfVector = normalize(viewDir - lightDirection);

	float specular = pow(max(dot(normal, halfVector), 0.0), 32.0);
	
	float F0 = 0.02; 

	return (diff + specular) * lightColor * baseColor;
}

vec3 CalculatePointLight(vec3 lightPosition, vec3 lightColor, float radius, vec3 baseColor, vec3 normal)
{
	vec3 lightDir = normalize(lightPosition - vFragPosition);

	float diff = max(dot(normal, lightDir), 0.0);

	float distance = length(lightPosition - vFragPosition);
	
	float attenuation = 1.0 / (distance * (1.0/radius) * distance* (1.0/radius));

	
	vec3 viewDir = normalize(cameraPosition - vFragPosition);

	vec3 halfVector = normalize(viewDir + lightDir);

	float specular = pow(max(dot(normal, halfVector), 0.0), 32.0);

	return (diff + specular) * attenuation * lightColor * baseColor;
}

vec3 CalculateSpotLight(vec3 lightPosition, vec3 lightColor, vec3 lightDirection, float radius, float inner_cone_cos, float outer_cone_cos, vec3 baseColor, vec3 normal)
{
	vec3 lightDir = normalize(lightPosition - vFragPosition);

	float diff = max(dot(normal, lightDir), 0.0);

	float distance = length(lightPosition - vFragPosition);
	
	float attenuation = 1.0 / (distance * (1.0/radius) * distance* (1.0/radius));


    float theta = dot(lightDir, normalize(-lightDirection)); 

    float epsilon = (inner_cone_cos - outer_cone_cos);

    float intensity = clamp((theta - outer_cone_cos) / epsilon, 0.0, 1.0);


	
	vec3 viewDir = normalize(cameraPosition - vFragPosition);

	vec3 halfVector = normalize(viewDir + lightDir);

	float specular = pow(max(dot(normal, halfVector), 0.0), 32.0);


	return (diff + specular) * intensity * attenuation * lightColor * baseColor;
}

float CalculateShadow(mat4 shadowMatrix, sampler2D shadowMap)
{
	vec4 shadowCoord = shadowMatrix * vec4(vFragPosition, 1.0);
	

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




float CalculatePointLightShadow(vec3 position, mat4 shadowMapMatrices[6], samplerCube shadowMapTexture)
{
    vec3 fragToLight = vFragPosition - position;
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

    vec4 shadowCoord = shadowMapMatrices[face] * vec4(vFragPosition, 1.0);
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