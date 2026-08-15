#include <stddef.h>

/*
 * Foreign-platform SDL entry points retained by the pinned FNA managed
 * bindings under full static AOT. Apple targets never call these functions;
 * their presence completes the static native closure without carrying any
 * tvOS product behavior into iOS.
 */

void SDL_SetWindowsMessageHook(void *callback, void *userdata) { }
int SDL_Direct3D9GetAdapterIndex(int displayIndex) { return 0; }
void *SDL_RenderGetD3D9Device(void *renderer) { return NULL; }
int SDL_DXGIGetOutputInfo(int displayIndex, int *adapterIndex, int *outputIndex) { return 0; }
int SDL_LinuxSetThreadPriority(long int threadID, int priority) { return 0; }
void *SDL_AndroidGetJNIEnv(void) { return NULL; }
void *SDL_AndroidGetActivity(void) { return NULL; }
int SDL_GetAndroidSDKVersion(void) { return 0; }
int SDL_IsAndroidTV(void) { return 0; }
int SDL_IsChromebook(void) { return 0; }
int SDL_IsDeXMode(void) { return 0; }
void SDL_AndroidBackButton(void) { }
const char *SDL_AndroidGetInternalStoragePath(void) { return NULL; }
int SDL_AndroidGetExternalStorageState(void) { return 0; }
void *INTERNAL_SDL_AndroidGetExternalStoragePath(void) { return NULL; }
const char *SDL_AndroidGetExternalStoragePath(void) { return NULL; }
const void *SDL_WinRTGetFSPathUNICODE(int pathType) { return NULL; }
const char *SDL_WinRTGetFSPathUTF8(int pathType) { return NULL; }
int SDL_WinRTGetDeviceFamily(void) { return 0; }
int SDL_WinRTRunApp(void *mainFunction, void *reserved) { return 0; }
int SDL_AndroidRequestPermission(const char *permission) { return 0; }
void *SDL_RenderGetD3D11Device(void *renderer) { return NULL; }
int SDL_AndroidShowToast(void *message, int duration, int gravity, int xOffset, int yOffset) { return 0; }
void emscripten_set_main_loop(void *function, int fps, int simulateInfiniteLoop) { }
void emscripten_cancel_main_loop(void) { }
