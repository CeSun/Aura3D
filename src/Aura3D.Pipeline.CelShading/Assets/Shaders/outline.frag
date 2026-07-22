#version 300 es
precision mediump float;
out vec4 outColor;



in vec2 vTexCoord;
in vec3 vFragPosition;
in mat3 vTBN;
in vec3 vNormal;
in vec3 debugLineColor;

uniform vec4 BaseColor;

uniform vec3 cameraPosition;


void main()
{
	outColor = BaseColor;
	// outColor = vec4(debugLineColor, 1.0);
}