#version 300 es
precision mediump float;
out vec4 outColor;

const float brightnessLevels[16] = float[](0.7, 0.85, 0.85, 0.85, 0.85, 0.85, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0);
const float baseAmbient = 0.;

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
uniform sampler2D RampTexture;

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

float mapToDiscreteLevels(float value) {
    int index = int(value * 15.0);
    index = clamp(index, 0, 15);
    return brightnessLevels[index];
}

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


	vec3 ambient = ambientIntensity * baseColor.xyz;
	float diffuse = 0.0f;
	vec3 mainLightColor = vec3(1.0);
	if(MAX_DIRECTIONAL_LIGHTS > 0){
		diffuse = max(0.0f, dot(-DirectionalLights[0].direction, normal));
		mainLightColor = DirectionalLights[0].color;
	}
	// diffuse = mapToDiscreteLevels(diffuse);
	diffuse = texture(RampTexture, vec2(diffuse, diffuse)).x;
	// vec3 finalColor = vec3(0.0, 0.0, 0.0);
	vec3 finalColor = diffuse * baseColor.xyz * mainLightColor + ambient;

#ifdef BLENDMODE_TRANSLUCENT

	outColor = vec4(finalColor, baseColor.a);
#else

	outColor = vec4(finalColor, 1.0);
#endif
	
}