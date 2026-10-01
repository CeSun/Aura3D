#version 300 es
precision mediump float;
out vec4 outColor;




in vec2 vTexCoord;

uniform sampler2D BaseColorTexture;
uniform float alphaCutoff;
// Pushed to 0 by RenderPass when the material does not set it (see base.frag).
uniform float uUseVertexColor;
in vec4 vColor;

void main()
{
	vec4 baseColor = texture(BaseColorTexture, vTexCoord);

	baseColor.rgb = mix(baseColor.rgb, vColor.rgb, uUseVertexColor);

	// Cutoff discard belongs to Masked only; Translucent blends by alpha (see base.frag).
	#ifdef BLENDMODE_MASKED
		if (baseColor.a <= alphaCutoff)
			discard;
	#endif
	outColor = baseColor;
}