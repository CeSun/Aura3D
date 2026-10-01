#version 300 es
precision mediump float;
out vec4 outColor;




in vec2 vTexCoord;

uniform sampler2D BaseColorTexture;
uniform float alphaCutoff;

void main()
{
	vec4 baseColor = texture(BaseColorTexture, vTexCoord);

	// Cutoff discard belongs to Masked only; Translucent blends by alpha (see base.frag).
	#ifdef BLENDMODE_MASKED
		if (baseColor.a <= alphaCutoff)
			discard;
	#endif
	outColor = baseColor;
}