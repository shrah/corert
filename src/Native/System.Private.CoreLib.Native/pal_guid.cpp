// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pal_common.h"

#if HAVE_LIBUUID_H
#include <uuid/uuid.h>
#endif

#include "pal_endian.h"

typedef struct _GUID {
    uint32_t Data1;    // NOTE: diff from Win32, for LP64
    uint16_t Data2;
    uint16_t Data3;
    uint8_t  Data4[8];
} GUID;

extern "C" void CoreLibNative_CreateGuid(GUID* pGuid)
{
#if HAVE_LIBUUID_H
    uuid_generate_random(*(uuid_t*)pGuid);

    // Change the byte order of the Data1, 2 and 3, since the uuid_generate_random
    // generates them with big endian while GUIDS need to have them in little endian.
    pGuid->Data1 = SWAP32(pGuid->Data1);
    pGuid->Data2 = SWAP16(pGuid->Data2);
    pGuid->Data3 = SWAP16(pGuid->Data3);
#else
#error Don't know how to generate UUID on this platform
#endif
}
