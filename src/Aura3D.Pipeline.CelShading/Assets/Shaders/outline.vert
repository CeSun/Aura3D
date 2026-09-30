#version 300 es
precision mediump float;

#define MAX_BONES 256
//{{defines}}

layout(location = 0) in vec3 position;
layout(location = 1) in vec2 texCoord;
layout(location = 2) in vec4 color;
layout(location = 3) in vec3 normal;
layout(location = 4) in vec3 tangent;
layout(location = 5) in vec3 bitangent;

#ifdef SKINNED_MESH
layout(location = 6) in vec4 boneIndices;
layout(location = 7) in vec4 boneWeights;

layout(std140) uniform BoneBlock {
    mat4 BoneMatrices[MAX_BONES];
};

#endif

uniform mat4 modelMatrix;
uniform mat4 viewMatrix;
uniform mat4 projectionMatrix;
uniform mat4 normalMatrix;
uniform mat4 normalPrjMatrix;

// Outline width in NDC, computed on CPU from the mesh's on-screen coverage
uniform float outlineWidthNdc;

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

#else
		vec4 worldPosition = modelMatrix * vec4(position, 1.0);
#endif

		vec4 outVertex = projectionMatrix * viewMatrix * worldPosition;

		// Inflate along the normal in clip-space xy:
		// offset is scaled by outVertex.w so the NDC offset (i.e. pixel width) stays
		// constant after perspective divide. The pixel width itself is computed on the
		// CPU from the mesh's on-screen coverage: bigger on screen -> capped max width,
		// smaller on screen -> thinner outline.
		vec2 normalClipXY = (mat3(normalPrjMatrix) * normal).xy;
		float dirLen = length(normalClipXY);
		vec2 offsetDir = dirLen > 0.00001 ? normalClipXY / dirLen : vec2(0.0);

		outVertex.xy += offsetDir * outlineWidthNdc * outVertex.w;

		vFragPosition = worldPosition.xyz;
		gl_Position = outVertex;

}
