#version 300 es
precision highp float;
//{{defines}}
layout (location = 0) out vec4 FragColor;

#ifdef ENBALE_DEFERRED_SHADING

in vec2 TexCoords;
uniform sampler2D gBufferBaseColor;

#else

uniform sampler2D Texture_BaseColor;

in vec2 vTexCoord;

#endif

uniform vec3 ambientColor;
uniform float ambientIntensity;


void main()
{
#ifdef ENBALE_DEFERRED_SHADING
    vec4 baseColor = texture(gBufferBaseColor, TexCoords);
    vec3 albedo = baseColor.xyz;

#else
    vec4 baseColor = texture(Texture_BaseColor, vTexCoord);
    vec3 albedo = baseColor.rgb;
#endif

    vec3 lightContribution = ambientIntensity * ambientColor* albedo;

#ifdef ENBALE_DEFERRED_SHADING
    float alpha = baseColor.a;
#endif

#ifdef BLENDMODE_TRANSLUCENT
#ifdef IS_FIRST_LIGHT
    float alpha = baseColor.a;
#else
    float alpha = 0.0;
#endif
    
#endif
    FragColor = vec4(lightContribution, alpha);
}

