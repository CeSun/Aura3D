#version 300 es
precision mediump float;
#define BONE_NUMBER 150

//{{defines}}

layout(location = 0) in vec3 position;

#ifdef BLENDMODE_MASKED
layout(location = 1) in vec2 texCoord;
#endif
#ifdef SKINNED_MESH

layout(location = 5) in vec4 boneIndices;
layout(location = 6) in vec4 boneWeights;

uniform mat4 BoneMatrices[BONE_NUMBER];

#endif

uniform mat4 modelMatrix;
uniform mat4 viewMatrix;
uniform mat4 projectionMatrix;

#ifdef BLENDMODE_MASKED
out vec2 vTexCoord;
#endif

void main()
{
#ifdef BLENDMODE_MASKED
	vTexCoord = texCoord;
#endif

#ifdef SKINNED_MESH
		
	mat4 skinMatrix = boneWeights.x * BoneMatrices[int(boneIndices.x)];
	skinMatrix += boneWeights.y * BoneMatrices[int(boneIndices.y)];
	skinMatrix += boneWeights.z * BoneMatrices[int(boneIndices.z)];
	skinMatrix += boneWeights.w * BoneMatrices[int(boneIndices.w)];

	vec4 worldPosition = modelMatrix * skinMatrix * vec4(position, 1.0);

#else
	vec4 worldPosition = modelMatrix * vec4(position, 1.0);
#endif

	gl_Position = projectionMatrix * viewMatrix * worldPosition;
}