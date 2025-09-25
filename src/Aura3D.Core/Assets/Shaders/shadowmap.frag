#version 300 es
precision mediump float;

//{{defines}}

#ifdef BLENDMODE_MASKED

in vec2 vTexCoord;

uniform sampler2D BaseColorTexture;

#endif

void main()
{

#ifdef BLENDMODE_MASKED
	vec4 outColor = texture(BaseColorTexture, vTexCoord);
	if (outColor.a <= 0.0)
		discard;
#endif
}