#version 300 es
precision mediump float;

#define BONE_NUMBER 150

#define MAX_DIRECTIONAL_LIGHTS 4
#define MAX_POINT_LIGHTS 4
#define MAX_SPOT_LIGHTS 4

//{{defines}}

layout(location = 0) in vec3 position;
layout(location = 1) in vec2 texCoord;
layout(location = 2) in vec3 normal;
layout(location = 3) in vec3 tangent;
layout(location = 4) in vec3 bitangent;

#ifdef SKINNED_MESH
layout(location = 5) in vec4 boneIndices;
layout(location = 6) in vec4 boneWeights;

uniform mat4 BoneMatrices[BONE_NUMBER];

#endif

uniform mat4 modelMatrix;
uniform mat4 viewMatrix;
uniform mat4 projectionMatrix;

uniform mat4 normalMatrix;

out vec2 vTexCoord;
out vec3 vFragPosition;
out mat3 vTBN;
out vec3 vNormal;


void main()
{
	vTexCoord = texCoord;

    vec3 T = normalize(mat3(normalMatrix) * tangent);
    vec3 B = normalize(mat3(normalMatrix) * bitangent);
    vec3 N = normalize(mat3(normalMatrix) * normal); 
	mat3 TBN = mat3(T, B, N);
	vTBN = TBN;
	vNormal =  N;


#ifdef SKINNED_MESH
		
	mat4 skinMatrix = boneWeights.x * BoneMatrices[int(boneIndices.x)];
	skinMatrix += boneWeights.y * BoneMatrices[int(boneIndices.y)];
	skinMatrix += boneWeights.z * BoneMatrices[int(boneIndices.z)];
	skinMatrix += boneWeights.w * BoneMatrices[int(boneIndices.w)];

	vec4 worldPosition = modelMatrix * skinMatrix * vec4(position, 1.0);

#else
	vec4 worldPosition = modelMatrix * vec4(position, 1.0);
#endif

	vFragPosition = worldPosition.xyz;
	gl_Position = projectionMatrix * viewMatrix * worldPosition;
}