#ifndef CELESTE_TVOS_TVSTUBS_H
#define CELESTE_TVOS_TVSTUBS_H

/*
 * Compatibility exports for platform-specific declarations retained in the
 * pinned SDL2-CS bindings. tvOS code must not call these functions. Their
 * complete, verified symbol list is generated from stubs.c by Stage 1.
 */

#ifdef __cplusplus
extern "C" {
#endif

void SDL_SetWindowsMessageHook(void *callback, void *userdata);
int SDL_LinuxSetThreadPriority(long int threadID, int priority);
void *SDL_AndroidGetJNIEnv(void);
void *SDL_AndroidGetActivity(void);
void emscripten_set_main_loop(void *function, int fps, int simulate_infinite_loop);
void emscripten_cancel_main_loop(void);

#ifdef __cplusplus
}
#endif

#endif
