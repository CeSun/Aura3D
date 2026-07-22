#version 300 es
precision mediump float;
out vec4 outColor;

const float brightnessLevels[16] = float[](0.7, 0.85, 0.85, 0.85, 0.85, 0.85, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0);
const float baseAmbient = 0.;

//{{defines}}

#define MAX_DIRECTIONAL_LIGHTS 4

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

//Texture flags 
// Bit value represent texture exist or not, start from low side: BaseColorTexture | NormalTexture | ILMTextures | ShadowRamp | SpecularRamp
uniform int TexturesFlags;

// SDF (Face Shadow Map)
uniform sampler2D SDFTextures;
uniform int _UseFaceLightMapChannel_R;

// ILM
uniform sampler2D ILMTextures;

// Ramp 
uniform sampler2D ShadowRamp;
uniform sampler2D SpecularRamp;
uniform int _UseCoolShadowColorOrTex;
uniform float _RampIndex0;
uniform float _RampIndex1;
uniform float _RampIndex2;
uniform float _RampIndex3;
uniform float _RampIndex4;
uniform float _BrightFac;
uniform float _GreyFac;
uniform float _DarkFac;

//Shadow
uniform vec4 _LightAreaColorTint;
uniform float _FaceShadowOffset;
uniform vec4 _DarkShadowColor;
uniform vec4 _CoolDarkShadowColor;
uniform float _BrightAreaShadowFac;
uniform float _FaceShadowTransitionSoftness;

uniform sampler2D BaseColorTexture;

uniform sampler2D NormalTexture;

uniform vec4 BaseColor;

uniform float ambientIntensity;

uniform vec3 cameraPosition;

uniform mat4 modelMatrix;

#ifdef FACE_RENDER
// Model matrix for face SDF calculation
uniform mat4 faceModelMatrix;
#endif


#if defined(BLENDMODE_MASKED) || defined(BLENDMODE_TRANSLUCENT)

uniform float alphaCutoff;

#endif

uniform s_directional_light_info DirectionalLights[MAX_DIRECTIONAL_LIGHTS];

uniform sampler2D DirectionalLightShadowMaps[MAX_DIRECTIONAL_LIGHTS];

float mapToDiscreteLevels(float value) {
    int index = int(value * 15.0);
    index = clamp(index, 0, 15);
    return brightnessLevels[index];
}


const int BaseColorBit = 1;
const int NormalBit = 2;
const int ILMBit = 4;
const int SDFBit = 8;
const int ShadowRampBit = 16;
const int SpecularRampBit = 32;

// Calculate shadow attenuation with normal-based lighting and ambient occlusion
float GetShadow(vec3 normalWS, vec3 lightDirection, float aoFactor, float shadowAttenuation)
{
    float NDotL = dot(normalWS, lightDirection);
    // Apply smoothstep to create soft transition for half Lambert
    float halfLambert = smoothstep(0.0, _GreyFac, NDotL + _DarkFac);
    // Combine half Lambert with AO and shadow attenuation
    float shadow = clamp(2.0 * halfLambert * aoFactor, 0.0, 1.0) * shadowAttenuation;
    // Return 1.0 when AO factor is high (>= 0.9), otherwise return calculated shadow
    return mix(shadow, 1.0, step(0.9, aoFactor));
	// return shadow;
}

// Calculate shadow ramp color using half Lambert and lightmap data
vec3 GetShadowRampColor(float halfLambert, vec4 lightmap)
{
    // Create bright mask for areas above brightness threshold
    float brightMask = step(_BrightFac, halfLambert);
    
    // Determine day or night mode
    float rampSampling = 0.0;
    if(_UseCoolShadowColorOrTex == 1) { 
        rampSampling = 0.5; 
    }
    
    // Calculate ramp sampling positions based on ramp indices
    float ramp0 = _RampIndex0 * -0.1 + 1.05 - rampSampling;  // 0.95
    float ramp1 = _RampIndex1 * -0.1 + 1.05 - rampSampling;  // 0.65
    float ramp2 = _RampIndex2 * -0.1 + 1.05 - rampSampling;  // 0.75
    float ramp3 = _RampIndex3 * -0.1 + 1.05 - rampSampling;  // 0.55
    float ramp4 = _RampIndex4 * -0.1 + 1.05 - rampSampling;  // 0.85
    
    // Separate lightmap.a into different material regions
    float lightmapA1 = step(0.0, lightmap.a);   // 0.0
    float lightmapA2 = step(0.25, lightmap.a);  // 0.3
    float lightmapA3 = step(0.45, lightmap.a);  // 0.5
    float lightmapA4 = step(0.65, lightmap.a);  // 0.7
    float lightmapA5 = step(0.95, lightmap.a);  // 1.0
    
    // Reconstruct lightmap.a by blending ramp values
    float rampV = 0.0;
    rampV = mix(rampV, ramp0, lightmapA1);  // 0.0
    rampV = mix(rampV, ramp1, lightmapA2);  // 0.3
    rampV = mix(rampV, ramp2, lightmapA3);  // 0.5
    rampV = mix(rampV, ramp3, lightmapA4);  // 0.7
    rampV = mix(rampV, ramp4, lightmapA5);  // 1.0
    
    // Sample ramp texture using halfLambert as U and rampV as V coordinate
    vec3 rampCol = texture(ShadowRamp, vec2(halfLambert, rampV)).rgb;
    
    // Apply bright mask to blend between ramp color and halfLambert
    vec3 shadowRamp = mix(rampCol, vec3(halfLambert), brightMask);
    
    return shadowRamp;
}


#ifdef FACE_RENDER
// Calculate face shadow using SDF (Signed Distance Field)
// This creates smooth, art-directable shadows on character faces
float GetFaceShadow(vec2 uv, vec3 lightDirection)
{
    // Extract head directions from model matrix
    // The character head bone is rotated, so we extract the transformed axes
    vec3 headDirWSUp = normalize(vec3(faceModelMatrix[1]));
    vec3 headDirWSForward = -normalize(vec3(faceModelMatrix[2]));
    vec3 headDirWSRight = normalize(vec3(faceModelMatrix[0]));

	// vec3 headDirWSUp = normalize(vec3(modelMatrix[1]));
    // vec3 headDirWSForward = -normalize(vec3(modelMatrix[2]));
    // vec3 headDirWSRight = normalize(vec3(modelMatrix[0]));


    // Project light direction onto head plane (removing up component)
    vec3 lightDirProj = normalize(lightDirection - dot(lightDirection, headDirWSUp) * headDirWSUp);

    // Determine if light is coming from right side
    bool isRight = dot(lightDirProj, headDirWSRight) > 0.0;

    // Flip U coordinate when light is from right side
    // This is because the SDF map is authored for right-side lighting
    float sdfUVx = isRight ? (1.0 - uv.x) : uv.x;
    vec2 sdfUV = vec2(sdfUVx, uv.y);

    // Sample SDF value from face shadow map
    float sdfValue = 0.0;
    if (_UseFaceLightMapChannel_R == 1) {
        sdfValue = texture(SDFTextures, sdfUV).r;
    } else {
        sdfValue = texture(SDFTextures, sdfUV).a;
    }
    sdfValue += _FaceShadowOffset;

    // Calculate forward-light dot product, remap from [-1,1] to [0,1]
    float FoL01 = dot(headDirWSForward, lightDirProj) * 0.5 + 0.5;

    // Compare SDF value with light angle to determine shadow
    // Uses smoothstep for soft shadow transition
    float sdfShadow = smoothstep(
        FoL01 - _FaceShadowTransitionSoftness,
        FoL01 + _FaceShadowTransitionSoftness,
        1.0 - sdfValue
    );

	// debug
	//return sdfValue;
    return sdfShadow;
}
#endif

void main()
{
	// BaseColor
	vec4 baseColor = BaseColor;
	if ((TexturesFlags & BaseColorBit) != 0)
	{
		baseColor = texture(BaseColorTexture, vTexCoord);
	}
	#if defined(BLENDMODE_MASKED) || defined(BLENDMODE_TRANSLUCENT)
		if (baseColor.a <= alphaCutoff)
			discard;
	#endif

	// Normal
 	vec3 normal = vec3(0.0, 1.0, 0.0);
 	if ((TexturesFlags & NormalBit) != 0)
 	{
 		normal = texture(NormalTexture, vTexCoord).xyz;
 		normal = normalize(normal * 2.0 - 1.0);
 		normal = normalize(vTBN * normal);
 	}
	else{
		normal = vTBN[2];
	}
	if (!gl_FrontFacing) 
	{
		normal = -normal;
	}

	// Main Light Direction
	vec3 mainLightDirection = vec3(0.25 * 3.14, 0, 0);
	vec3 mainLightColor = vec3(1.0);
	if(MAX_DIRECTIONAL_LIGHTS > 0){
		mainLightColor = DirectionalLights[0].color;
		mainLightDirection = -DirectionalLights[0].direction;
	}

	// ILM Preprocess
	bool HasILMTexture = (TexturesFlags & ILMBit) != 0;
	vec4 ilmTexCol = vec4(1.0, 1.0, 1.0, 1.0);
	if(HasILMTexture)
	{
		ilmTexCol = texture(ILMTextures, vTexCoord);
	}
	float aoFactor = ilmTexCol.y;
	//debug
	float shadow = GetShadow(normal, mainLightDirection, aoFactor, 1.0);

	// Diffuse
	vec3 diffuseColor = vec3(0);

#ifdef FACE_RENDER
	// Face rendering with SDF shadow
	bool HasSDFTexture = (TexturesFlags & SDFBit) != 0;

	if (HasSDFTexture)
	{
		// Calculate SDF-based face shadow
		float sdfShadow = GetFaceShadow(vTexCoord, mainLightDirection);
		float shadowAttenuation = 1.0; // Can be replaced with actual shadow map attenuation
		float brightAreaMask = (1.0 - sdfShadow) * shadowAttenuation;

		// Get ramp color
		vec3 rampTexCol = GetShadowRampColor(sdfShadow, ilmTexCol);

		// Calculate shadow colors
		vec3 brightAreaColor = rampTexCol * _LightAreaColorTint.rgb;
		vec3 darkShadowColor = rampTexCol * mix(_DarkShadowColor.rgb, _CoolDarkShadowColor.rgb, float(_UseCoolShadowColorOrTex));
		vec3 ShadowColorTint = mix(darkShadowColor, brightAreaColor, brightAreaMask);

		diffuseColor = ShadowColorTint * baseColor.xyz;

		// ILM red channel masks areas affected by lighting
		// Areas with ilmTexCol.r = 0 won't be affected by lighting (like eyes)
		diffuseColor = mix(baseColor.xyz, diffuseColor, ilmTexCol.r);

		// debug
		// diffuseColor = vec3(brightAreaMask, brightAreaMask, brightAreaMask);
		// diffuseColor = rampTexCol;
	}
	else
	{
		// Fallback to regular body rendering if no SDF texture
		vec3 rampTexCol = GetShadowRampColor(shadow, ilmTexCol);
		vec3 brightAreaColor = rampTexCol * _LightAreaColorTint.rgb;
		vec3 darkShadowColor = rampTexCol * mix(_DarkShadowColor.rgb, _CoolDarkShadowColor.rgb, float(_UseCoolShadowColorOrTex));
		vec3 ShadowColorTint = mix(darkShadowColor.rgb, brightAreaColor, _BrightAreaShadowFac);
		diffuseColor = ShadowColorTint * baseColor.xyz;
	}
#else
	// Body rendering (original logic)
	vec3 rampTexCol = GetShadowRampColor(shadow, ilmTexCol);
	vec3 brightAreaColor = rampTexCol * _LightAreaColorTint.rgb;
	vec3 darkShadowColor = rampTexCol * mix(_DarkShadowColor.rgb, _CoolDarkShadowColor.rgb, float(_UseCoolShadowColorOrTex));
    vec3 ShadowColorTint = mix(darkShadowColor.rgb, brightAreaColor, _BrightAreaShadowFac);
	diffuseColor = ShadowColorTint * baseColor.xyz;

#endif


	vec3 ambient = ambientIntensity * baseColor.xyz;
	vec3 finalColor = diffuseColor;


#ifdef BLENDMODE_TRANSLUCENT

	outColor = vec4(finalColor, baseColor.a);
#else

	outColor = vec4(finalColor, 1.0);
#endif
	
}