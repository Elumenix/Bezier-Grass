#ifndef GRASSBLADE
#define GRASSBLADE

struct GrassBlade
{
    float3 position;
    float width;
    float height;
    float2 facing;
    float tilt;
    float bend;
    float3 nearestClumpPosition;
};

#endif