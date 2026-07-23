#version 300 es
precision mediump float;

#define MAX_BONES 256
#define MAX_DIRECTIONAL_LIGHTS 4
#define MAX_POINT_LIGHTS 4
#define MAX_SPOT_LIGHTS 4

//{{defines}}

layout(location = 0) in vec3 position;
layout(location = 1) in vec2 texCoord;
layout(location = 2) in vec4 color;
layout(location = 3) in vec3 normal;
layout(location = 4) in vec3 tangent;
layout(location = 5) in vec3 bitangent;
layout(location = 6) in vec4 boneIndices;
layout(location = 7) in vec4 boneWeights;


#ifdef INSTANCED_MESH
layout(location = 8) in mat4 modelMatrix;
layout(location = 12) in mat4 normalMatrix;
#endif


#ifdef SKINNED_MESH
layout(std140) uniform BoneBlock {
    mat4 BoneMatrices[MAX_BONES];
};
#endif

#ifndef INSTANCED_MESH
uniform mat4 modelMatrix;
uniform mat4 normalMatrix;
#endif

uniform mat4 viewMatrix;
uniform mat4 projectionMatrix;

out vec2 vTexCoord;
out vec3 vFragPosition;
out mat3 vTBN;


void main()
{
	vTexCoord = texCoord;

#ifdef SKINNED_MESH

		int idx0 = int(boneIndices.x);
	    int idx1 = int(boneIndices.y);
	    int idx2 = int(boneIndices.z);
	    int idx3 = int(boneIndices.w);

		float sum = boneWeights.x + boneWeights.y + boneWeights.z + boneWeights.w;
	    vec4 w = (sum > 0.0001) ? boneWeights / sum : vec4(1.0, 0.0, 0.0, 0.0);

		mat4 skinMatrix = w.x * BoneMatrices[idx0];
	    skinMatrix      += w.y * BoneMatrices[idx1];
	    skinMatrix      += w.z * BoneMatrices[idx2];
	    skinMatrix      += w.w * BoneMatrices[idx3];

		vec4 worldPosition = modelMatrix * skinMatrix * vec4(position, 1.0);
		mat3 skinNormalMatrix = mat3(normalMatrix) * mat3(skinMatrix);
		vec3 T = normalize(skinNormalMatrix * tangent);
		vec3 B = normalize(skinNormalMatrix * bitangent);
		vec3 N = normalize(skinNormalMatrix * normal);

#else
		vec4 worldPosition = modelMatrix * vec4(position, 1.0);
		vec3 T = normalize(mat3(normalMatrix) * tangent);
		vec3 B = normalize(mat3(normalMatrix) * bitangent);
		vec3 N = normalize(mat3(normalMatrix) * normal);
#endif

		mat3 TBN = mat3(T, B, N);
		vTBN = TBN;

		vFragPosition = worldPosition.xyz;
		gl_Position = projectionMatrix * viewMatrix * worldPosition;
}