#version 300 es
precision mediump float;
out vec4 outColor;

//{{defines}}


in vec2 vTexCoord;

#ifdef SKYBOX
	uniform samplerCube uSkybox;
	uniform mat4 invViewProj;
    #ifdef ORTHOGRAPHIC
	    uniform mat4 viewRot;
        uniform vec2 orthoSize;
        uniform float farPlane;
    #endif
#endif

#ifdef BACKGROUND_TEXTURE
	uniform sampler2D uBackgroundTexture;
#endif



void main() 
{
#ifdef SKYBOX 
    #ifdef ORTHOGRAPHIC
        float x = (vTexCoord.x * 2.0 - 1.0) * orthoSize.x / 2.0;
        float y = (vTexCoord.y * 2.0 - 1.0) * orthoSize.y / 2.0;
        vec3 worldPos = vec3(x, y, -farPlane);
        vec3 dir = normalize(worldPos);
        dir = (viewRot * vec4(dir, 0.0)).xyz;
    #else
	    vec3 ndc = vec3(vTexCoord * 2.0 - 1.0, 1.0);
	    vec4 worldPos = invViewProj * vec4(ndc, 1.0);
        vec3 dir = normalize(worldPos.xyz / worldPos.w);
    #endif
        outColor = texture(uSkybox, dir);
#endif

#ifdef BACKGROUND_TEXTURE
	outColor = texture(uBackgroundTexture, vTexCoord);
#endif
	
}