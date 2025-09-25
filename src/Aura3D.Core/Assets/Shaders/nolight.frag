#version 300 es
precision mediump float;
out vec4 outColor;




in vec2 vTexCoord;

uniform sampler2D BaseColorTexture;
uniform vec4 BaseColor;
uniform int HasBaseColorTexture;
uniform float alphaCutoff;

void main()
{
	vec4 baseColor = BaseColor;

	if (HasBaseColorTexture == 1)
	{
		baseColor = texture(BaseColorTexture, vTexCoord);
	}

	#if defined(BLENDMODE_MASKED) || defined(BLENDMODE_TRANSLUCENT)
		if (baseColor.a <= alphaCutoff)
			discard;
	#endif
	outColor = baseColor;
}