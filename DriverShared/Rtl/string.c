/*
    EasyHook - The reinvention of Windows API hooking
 
    Copyright (C) 2009 Christoph Husse

    This library is free software; you can redistribute it and/or
    modify it under the terms of the GNU Lesser General Public
    License as published by the Free Software Foundation; either
    version 2.1 of the License, or (at your option) any later version.

    This library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
    Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public
    License along with this library; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301  USA

    Please visit http://www.codeplex.com/easyhook for more information
    about the project and latest updates.
*/
#include "stdafx.h"

ULONG RtlAnsiLength(CHAR* InString)
{
    ULONG       Length = 0;

    while(*InString != 0)
    {
        Length++;
        InString++;
    }

    return Length;
}

ULONG RtlUnicodeLength(WCHAR* InString)
{
    ULONG       Length = 0;

    while(*InString != 0)
    {
        Length++;
        InString++;
    }

    return Length;
}

LONG RtlAnsiIndexOf(
            CHAR* InString,
            CHAR InChar)
{
    ULONG       Index = 0;

    while(*InString != 0)
    {
        if(*InString == InChar)
            return Index;

        Index++;
        InString++;
    }

    return -1;
}

LONG RtlAnsiSubString(
            CHAR* InString,
            ULONG InOffset,
            ULONG InCount,
            CHAR* InTarget,
            ULONG InTargetMaxLen)
{
    ULONG       Index = 0;
    ULONG       Result = 0;

    while(*InString != 0)
    {
        if(Index >= InOffset + InCount)
        {
            *InTarget = 0;

            return Result;
        }

        if(Index >= InOffset)
        {
            Result++;

            if(InTargetMaxLen < Result)
                return -1;

            *InTarget = *InString;

            InTarget++;
        }

        Index++;
        InString++;
    }

    return -1;
}

BOOL RtlAnsiHexToLongLong(
            CHAR* InHexString,
            ULONG InMinStrLen,
            LONGLONG* OutValue)
{
    ULONG       Index = 0;
    CHAR        c;
    LONGLONG    Result = 0;
    LONGLONG    Tmp;

    while((c = *InHexString) != 0)
    {
        if((c >= '0') && (c <= '9'))
            Tmp = c - '0';
        else if ((c >= 'a') && (c <= 'f'))
            Tmp = c - 'a' + 10;
        else if ((c >= 'A') && (c <= 'F'))
            Tmp = c - 'A' + 10;
        else
            return FALSE;

        if(Index >= 16)
            return FALSE;

        Result += Tmp << (60 - (Index * 4));
        
        Index++;
        InHexString++;
    }

    if(Index < InMinStrLen)
        return FALSE;

    *OutValue = Result;

    return TRUE;
}

void RtlLongLongToUnicodeHex(LONGLONG InValue, WCHAR* InBuffer)
{
    ULONG           Index;
    ULONG           iChar;
    WCHAR           c;

    for(Index = 0, iChar = 0; Index < 64; Index += 4, iChar++)
    {
#ifdef _M_X64
        c = ((LONGLONG)InValue >> Index) & 0x0F;
#else
        if(Index < 32)
            c = (WCHAR)(((LONG)InValue >> Index) & 0x0F);
        else
            c = 0;
#endif

        if(c < 10)
            c += '0';
        else
            c += 'A' - 10;

        InBuffer[15 - iChar] = c;
    }

    InBuffer[16] = 0;
}