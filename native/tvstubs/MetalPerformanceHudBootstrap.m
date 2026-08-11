#import <Foundation/Foundation.h>

/*
 * Make Apple's Metal Performance HUD facility available before SDL/FNA creates
 * Celeste's CAMetalLayer. Visibility remains disabled unless the host applies
 * the user's separate Performance HUD preference to that exact layer.
 *
 * MetalHUDForceEnabled is the current documented NSUserDefaults spelling.
 * MetalForceHudEnabled is the spelling in Apple's official Metal HUD Tech
 * Talk and remains required by the accepted physical tvOS release. Retain both
 * until physical replacement evidence establishes that the compatibility key
 * can be removed.
 */
__attribute__((constructor))
static void CelesteTvOSMetalHudBootstrapConstructor(void)
{
    @autoreleasepool {
        NSUserDefaults *defaults = [NSUserDefaults standardUserDefaults];
        [defaults setBool:YES forKey:@"MetalHUDForceEnabled"];
        [defaults setBool:YES forKey:@"MetalForceHudEnabled"];
        [defaults synchronize];
    }
}

void CelesteTvOSMetalHudBootstrapForceLink(void)
{
}
