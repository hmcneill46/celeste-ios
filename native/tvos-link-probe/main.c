#define SDL_MAIN_HANDLED 1

#include <FAudio.h>
#include <FNA3D.h>
#include <SDL.h>
#include <theorafile.h>
#include <vulkan/vulkan.h>
#include <tvStubs.h>

int main(void)
{
    /* References prove that each public archive participates in the link. */
    volatile const void *symbols[] = {
        (const void *) SDL_Init,
        (const void *) FNA3D_CreateDevice,
        (const void *) FAudioCreate,
        (const void *) tf_fopen,
        (const void *) vkGetInstanceProcAddr,
        (const void *) SDL_AndroidGetJNIEnv,
    };
    return symbols[0] == 0;
}
