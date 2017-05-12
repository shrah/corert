﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Interlocked = System.Threading.Interlocked;

namespace Internal.Runtime.CompilerHelpers
{
    /// <summary>
    /// These methods are used to throw exceptions from generated code.
    /// </summary>
    internal static class InteropHelpers
    {
        internal static unsafe byte* StringToAnsiString(String str, bool bestFit, bool throwOnUnmappableChar)
        {
            return PInvokeMarshal.StringToAnsiString(str, bestFit, throwOnUnmappableChar);
        }

        public static unsafe string AnsiStringToString(byte* buffer)
        {
            return PInvokeMarshal.AnsiStringToString(buffer);
        }


        internal static unsafe void StringToByValAnsiString(string str, byte* pNative, int charCount, bool bestFit, bool throwOnUnmappableChar)
        {
            // In CoreRT charCount = Min(SizeConst, str.Length). So we don't need to truncate again.
            PInvokeMarshal.StringToByValAnsiString(str, pNative, charCount, bestFit, throwOnUnmappableChar, truncate: false);
        }

        public static unsafe string ByValAnsiStringToString(byte* buffer, int length)
        {
            return PInvokeMarshal.ByValAnsiStringToString(buffer, length);
        }

        internal static unsafe void StringToUnicodeFixedArray(String str, UInt16* buffer, int length)
        {
            if (buffer == null)
                return;

            Debug.Assert(str.Length >= length);

            fixed (char* pStr = str)
            {
                int size = length * sizeof(char);
                Buffer.MemoryCopy(pStr, buffer, size, size);
                *(buffer + length) = 0;
            }
        }

        internal static unsafe string UnicodeToStringFixedArray(UInt16* buffer, int length)
        {
            if (buffer == null)
                return String.Empty;

            string result = String.Empty;

            if (length > 0)
            {
                result = new String(' ', length);

                fixed (char* pTemp = result)
                {
                    int size = length * sizeof(char);
                    Buffer.MemoryCopy(buffer, pTemp, size, size);
                }
            }
            return result;
        }

        internal static unsafe char* StringToUnicodeBuffer(String str)
        {
            if (str == null)
                return null;

            int stringLength = str.Length;

            char* buffer = (char*)PInvokeMarshal.CoTaskMemAlloc((UIntPtr)(sizeof(char) * (stringLength + 1))).ToPointer();

            fixed (char* pStr = str)
            {
                int size = stringLength * sizeof(char);
                Buffer.MemoryCopy(pStr, buffer, size, size);
                *(buffer + stringLength) = '\0';
            }
            return buffer;
        }

        public static unsafe string UnicodeBufferToString(char* buffer)
        {
            return new String(buffer);
        }

        public static unsafe byte* AllocMemoryForAnsiStringBuilder(StringBuilder sb)
        {
            if (sb == null)
            {
                return null;
            }
            return (byte *)CoTaskMemAllocAndZeroMemory(new IntPtr(checked((sb.Capacity + 2) * PInvokeMarshal.GetSystemMaxDBCSCharSize())));
        }

        public static unsafe char* AllocMemoryForUnicodeStringBuilder(StringBuilder sb)
        {
            if (sb == null)
            {
                return null;
            }
            return (char *)CoTaskMemAllocAndZeroMemory(new IntPtr(checked((sb.Capacity + 2) * 2)));
        }

        public static unsafe byte* AllocMemoryForAnsiCharArray(char[] chArray)
        {
            if (chArray == null)
            {
                return null;
            }
            return (byte*)CoTaskMemAllocAndZeroMemory(new IntPtr(checked((chArray.Length + 2) * PInvokeMarshal.GetSystemMaxDBCSCharSize())));
        }

        public static unsafe void AnsiStringToStringBuilder(byte* newBuffer, System.Text.StringBuilder stringBuilder)
        {
            if (stringBuilder == null)
                return;

            PInvokeMarshal.AnsiStringToStringBuilder(newBuffer, stringBuilder);
        }

        public static unsafe void UnicodeStringToStringBuilder(ushort* newBuffer, System.Text.StringBuilder stringBuilder)
        {
            if (stringBuilder == null)
                return;

            PInvokeMarshal.UnicodeStringToStringBuilder(newBuffer, stringBuilder);
        }

        public static unsafe void StringBuilderToAnsiString(System.Text.StringBuilder stringBuilder, byte* pNative,
            bool bestFit, bool throwOnUnmappableChar)
        {
            if (pNative == null)
                return;

            PInvokeMarshal.StringBuilderToAnsiString(stringBuilder, pNative, bestFit, throwOnUnmappableChar);
        }

        public static unsafe void StringBuilderToUnicodeString(System.Text.StringBuilder stringBuilder, ushort* destination)
        {
            if (destination == null)
                return;

            PInvokeMarshal.StringBuilderToUnicodeString(stringBuilder, destination);
        }

        public static unsafe void WideCharArrayToAnsiCharArray(char[] managedArray, byte* pNative, bool bestFit, bool throwOnUnmappableChar)
        {
            PInvokeMarshal.WideCharArrayToAnsiCharArray(managedArray, pNative, bestFit, throwOnUnmappableChar);
        }

        /// <summary>
        /// Convert ANSI ByVal byte array to UNICODE wide char array, best fit
        /// </summary>
        /// <remarks>
        /// * This version works with array instead to string, it means that the len must be provided and there will be NO NULL to
        /// terminate the array.
        /// * The buffer to the UNICODE wide char array must be allocated by the caller.
        /// </remarks>
        /// <param name="pNative">Pointer to the ANSI byte array. Could NOT be null.</param>
        /// <param name="lenInBytes">Maximum buffer size.</param>
        /// <param name="managedArray">Wide char array that has already been allocated.</param>
        public static unsafe void AnsiCharArrayToWideCharArray(byte* pNative, char[] managedArray)
        {
            PInvokeMarshal.AnsiCharArrayToWideCharArray(pNative, managedArray);
        }

        /// <summary>
        /// Convert a single UNICODE wide char to a single ANSI byte.
        /// </summary>
        /// <param name="managedArray">single UNICODE wide char value</param>
        public static unsafe byte WideCharToAnsiChar(char managedValue, bool bestFit, bool throwOnUnmappableChar)
        {
            return PInvokeMarshal.WideCharToAnsiChar(managedValue, bestFit, throwOnUnmappableChar);
        }

        /// <summary>
        /// Convert a single ANSI byte value to a single UNICODE wide char value, best fit.
        /// </summary>
        /// <param name="nativeValue">Single ANSI byte value.</param>
        public static unsafe char AnsiCharToWideChar(byte nativeValue)
        {
            return PInvokeMarshal.AnsiCharToWideChar(nativeValue);
        }

        internal static unsafe IntPtr ResolvePInvoke(MethodFixupCell* pCell)
        {
            if (pCell->Target != IntPtr.Zero)
                return pCell->Target;

            return ResolvePInvokeSlow(pCell);
        }

        internal static unsafe IntPtr ResolvePInvokeSlow(MethodFixupCell* pCell)
        {
            ModuleFixupCell* pModuleCell = pCell->Module;
            IntPtr hModule = pModuleCell->Handle;
            if (hModule == IntPtr.Zero)
            {
                FixupModuleCell(pModuleCell);
                hModule = pModuleCell->Handle;
            }

            FixupMethodCell(hModule, pCell);
            return pCell->Target;
        }

        internal static unsafe IntPtr TryResolveModule(string moduleName, PInvokeAttributes pinvokeAttributes)
        {
            IntPtr hModule = IntPtr.Zero;

            // Try original name first
            hModule = LoadLibrary(moduleName);
            if (hModule != IntPtr.Zero) return hModule;

            bool searchAssemblyDirectory = true;
            bool isRelativePath = !System.IO.Path.IsPathRooted(moduleName);

            if ((pinvokeAttributes & PInvokeAttributes.DllImportSearchPathMask) != 0)
            {
                searchAssemblyDirectory = (pinvokeAttributes & PInvokeAttributes.DllImportSearchPathAssemblyDirectory) != 0;
            }

            string path = String.Empty;

            if (searchAssemblyDirectory && isRelativePath)
            {
                path = AppContext.BaseDirectory;
#if PLATFORM_UNIX
                path += "/";
#else
                path += "\\";
#endif
                hModule = LoadLibrary(path + moduleName);
                if (hModule != IntPtr.Zero) return hModule;
            }

#if PLATFORM_UNIX
            const string PAL_SHLIB_PREFIX = "lib";
#if PLATFORM_OSX
            const string PAL_SHLIB_SUFFIX = ".dylib";
#else
            const string PAL_SHLIB_SUFFIX = ".so";
#endif

            string []combinations = new string [] {
                    PAL_SHLIB_PREFIX + moduleName + PAL_SHLIB_SUFFIX,       // Try prefix+name+suffix
                    moduleName + PAL_SHLIB_SUFFIX,                          // Try name+suffix
                    PAL_SHLIB_PREFIX + moduleName                           // Try prefix+name
            };

            if (isRelativePath)
            {
                for(int i=0; i < combinations.Length; i++)
                {
                    if (searchAssemblyDirectory)
                    {
                        // look for the library in the assembly directory
                        hModule = LoadLibrary(path + combinations[i]);
                        if (hModule != IntPtr.Zero) return hModule;
                    }

                    hModule = LoadLibrary(combinations[i]);
                    if (hModule != IntPtr.Zero) return hModule;
                }
            }
#endif
            return IntPtr.Zero;
        }

        internal static unsafe IntPtr LoadLibrary(string moduleName)
        {
            IntPtr hModule;

#if !PLATFORM_UNIX
            hModule = Interop.mincore.LoadLibraryEx(moduleName, IntPtr.Zero, 0);
#else
            hModule = Interop.Sys.LoadLibrary(moduleName);
#endif

            return hModule;
        }

        internal static unsafe void FreeLibrary(IntPtr hModule)
        {
#if !PLATFORM_UNIX
            Interop.mincore.FreeLibrary(hModule);
#else
            Interop.Sys.FreeLibrary(hModule);
#endif
        }

        private static unsafe string GetModuleName(ModuleFixupCell* pCell)
        {
            byte* pModuleName = (byte*)pCell->ModuleName;
            return Encoding.UTF8.GetString(pModuleName, strlen(pModuleName));
        }

        internal static unsafe void FixupModuleCell(ModuleFixupCell* pCell)
        {
            string moduleName = GetModuleName(pCell);
            IntPtr hModule = TryResolveModule(moduleName, pCell->Attributes);
            if (hModule != IntPtr.Zero)
            {
                var oldValue = Interlocked.CompareExchange(ref pCell->Handle, hModule, IntPtr.Zero);
                if (oldValue != IntPtr.Zero)
                {
                    // Some other thread won the race to fix it up.
                    FreeLibrary(hModule);
                }
            }
            else
            {
                throw new DllNotFoundException(SR.Format(SR.Arg_DllNotFoundExceptionParameterized, moduleName));
            }
        }

        internal static unsafe void FixupMethodCell(IntPtr hModule, MethodFixupCell* pCell)
        {
            byte* methodName = (byte*)pCell->MethodName;

#if !PLATFORM_UNIX
            pCell->Target = Interop.mincore.GetProcAddress(hModule, methodName);
#else
            pCell->Target = Interop.Sys.GetProcAddress(hModule, methodName);
#endif
            if (pCell->Target == IntPtr.Zero)
            {
                string entryPointName = Encoding.UTF8.GetString(methodName, strlen(methodName));
                throw new EntryPointNotFoundException(SR.Format(SR.Arg_EntryPointNotFoundExceptionParameterized, entryPointName, GetModuleName(pCell->Module)));
            }
        }

        internal static unsafe int strlen(byte* pString)
        {
            byte* p = pString;
            while (*p != 0) p++;
            return checked((int)(p - pString));
        }

        internal unsafe static void* CoTaskMemAllocAndZeroMemory(global::System.IntPtr size)
        {
            void* ptr;
            ptr = PInvokeMarshal.CoTaskMemAlloc((UIntPtr)(void*)size).ToPointer();

            // PInvokeMarshal.CoTaskMemAlloc will throw OOMException if out of memory
            Debug.Assert(ptr != null);

            Buffer.ZeroMemory((byte*)ptr, size.ToInt64());
            return ptr;
        }

        internal unsafe static void CoTaskMemFree(void* p)
        {
            PInvokeMarshal.CoTaskMemFree((IntPtr)p);
        }
        /// <summary>
        /// Returns the stub to the pinvoke marshalling stub
        /// </summary>
        public static IntPtr GetStubForPInvokeDelegate(Delegate del)
        {
            return PInvokeMarshal.GetStubForPInvokeDelegate(del);
        }

        /// <summary>
        /// Retrieve the corresponding P/invoke instance from the stub
        /// </summary>
        public static Delegate GetPInvokeDelegateForStub(IntPtr pStub, RuntimeTypeHandle delegateType)
        {
            return PInvokeMarshal.GetPInvokeDelegateForStub(pStub, delegateType);
        }

        /// <summary>
        /// Retrieves the function pointer for the current open static delegate that is being called
        /// </summary>
        public static IntPtr GetCurrentCalleeOpenStaticDelegateFunctionPointer()
        {
            return PInvokeMarshal.GetCurrentCalleeOpenStaticDelegateFunctionPointer();
        }

        /// <summary>
        /// Retrieves the current delegate that is being called
        /// </summary>
        public static T GetCurrentCalleeDelegate<T>() where T : class // constraint can't be System.Delegate
        {
            return PInvokeMarshal.GetCurrentCalleeDelegate<T>();
        }

        [Flags]
        public enum PInvokeAttributes : int
        {
            None = 0,
            ExactSpelling = 1,
            CharSetAnsi = 2,
            CharSetUnicode = 4,
            CharSetAuto = 6,
            CharSetMask = 6,
            BestFitMappingEnable = 16,
            BestFitMappingDisable = 32,
            BestFitMappingMask = 48,
            SetLastError = 64,
            CallingConventionWinApi = 256,
            CallingConventionCDecl = 512,
            CallingConventionStdCall = 768,
            CallingConventionThisCall = 1024,
            CallingConventionFastCall = 1280,
            CallingConventionMask = 1792,
            ThrowOnUnmappableCharEnable = 4096,
            ThrowOnUnmappableCharDisable = 8192,
            ThrowOnUnmappableCharMask = 12288,
            DllImportSearchPathLegacyBehavior = 16384,
            DllImportSearchPathAssemblyDirectory = 32768,
            DllImportSearchPathUseDllDirectoryForDependencies = 65536,
            DllImportSearchPathApplicationDirectory = 131072,
            DllImportSearchPathUserDirectories = 262144,
            DllImportSearchPathSystem32 = 524288,
            DllImportSearchPathSafeDirectories = 1048576,
            DllImportSearchPathMask = 2080768
        }

        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct ModuleFixupCell
        {
            public IntPtr Handle;
            public IntPtr ModuleName;
            public PInvokeAttributes Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct MethodFixupCell
        {
            public IntPtr Target;
            public IntPtr MethodName;
            public ModuleFixupCell* Module;
        }
    }
}
