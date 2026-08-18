#if IOS_CELESTE_RUNTIME_HOST
using System;
using CelesteIOSFoundation;

namespace Celeste;

public static partial class IOSStorageHooks
{
    public static bool ValidateLogicalFile(string logicalName, ReadOnlyMemory<byte> payload)
    {
        ValidateLogicalName(logicalName);
        return payload.Length is > 0 and <= CelesteFileDurabilityStore.MaximumLogicalFileBytes &&
               new CanonicalCelesteValidator().IsValid(logicalName, payload);
    }

    public static bool HasPreviousGood(string logicalName)
    {
        ValidateLogicalName(logicalName);
        return DurableStore.LoadPreviousGood(logicalName) is not null;
    }

    public static CelesteFileRestoreResult RestorePreviousGood(string logicalName)
    {
        ValidateLogicalName(logicalName);
        return DurableStore.RestorePreviousGood(logicalName);
    }
}
#endif
