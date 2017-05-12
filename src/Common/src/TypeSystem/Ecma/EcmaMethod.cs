// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Reflection.Metadata;
using System.Threading;
using System.Collections.Generic;

using Internal.TypeSystem;

namespace Internal.TypeSystem.Ecma
{
    public sealed partial class EcmaMethod : MethodDesc, EcmaModule.IEntityHandleObject
    {
        private static class MethodFlags
        {
            public const int BasicMetadataCache     = 0x0001;
            public const int Virtual                = 0x0002;
            public const int NewSlot                = 0x0004;
            public const int Abstract               = 0x0008;
            public const int Final                  = 0x0010;
            public const int NoInlining             = 0x0020;
            public const int AggressiveInlining     = 0x0040;
            public const int RuntimeImplemented     = 0x0080;
            public const int InternalCall           = 0x0100;
            public const int Synchronized           = 0x0200;

            public const int AttributeMetadataCache = 0x1000;
            public const int Intrinsic              = 0x2000;
            public const int NativeCallable         = 0x4000;
            public const int RuntimeExport          = 0x8000;
        };

        private EcmaType _type;
        private MethodDefinitionHandle _handle;

        // Cached values
        private ThreadSafeFlags _methodFlags;
        private MethodSignature _signature;
        private string _name;
        private TypeDesc[] _genericParameters; // TODO: Optional field?

        internal EcmaMethod(EcmaType type, MethodDefinitionHandle handle)
        {
            _type = type;
            _handle = handle;

#if DEBUG
            // Initialize name eagerly in debug builds for convenience
            this.ToString();
#endif
        }

        EntityHandle EcmaModule.IEntityHandleObject.Handle
        {
            get
            {
                return _handle;
            }
        }

        public override TypeSystemContext Context
        {
            get
            {
                return _type.Module.Context;
            }
        }

        public override TypeDesc OwningType
        {
            get
            {
                return _type;
            }
        }

        private MethodSignature InitializeSignature()
        {
            var metadataReader = MetadataReader;
            BlobReader signatureReader = metadataReader.GetBlobReader(metadataReader.GetMethodDefinition(_handle).Signature);

            EcmaSignatureParser parser = new EcmaSignatureParser(Module, signatureReader);
            var signature = parser.ParseMethodSignature();
            return (_signature = signature);
        }

        public override MethodSignature Signature
        {
            get
            {
                if (_signature == null)
                    return InitializeSignature();
                return _signature;
            }
        }

        public EcmaModule Module
        {
            get
            {
                return _type.EcmaModule;
            }
        }

        public MetadataReader MetadataReader
        {
            get
            {
                return _type.MetadataReader;
            }
        }

        public MethodDefinitionHandle Handle
        {
            get
            {
                return _handle;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private int InitializeMethodFlags(int mask)
        {
            int flags = 0;

            if ((mask & MethodFlags.BasicMetadataCache) != 0)
            {
                var methodAttributes = Attributes;
                var methodImplAttributes = ImplAttributes;

                if ((methodAttributes & MethodAttributes.Virtual) != 0)
                    flags |= MethodFlags.Virtual;

                if ((methodAttributes & MethodAttributes.NewSlot) != 0)
                    flags |= MethodFlags.NewSlot;

                if ((methodAttributes & MethodAttributes.Abstract) != 0)
                    flags |= MethodFlags.Abstract;

                if ((methodAttributes & MethodAttributes.Final) != 0)
                    flags |= MethodFlags.Final;

                if ((methodImplAttributes & MethodImplAttributes.NoInlining) != 0)
                    flags |= MethodFlags.NoInlining;

                if ((methodImplAttributes & MethodImplAttributes.AggressiveInlining) != 0)
                    flags |= MethodFlags.AggressiveInlining;

                if ((methodImplAttributes & MethodImplAttributes.Runtime) != 0)
                    flags |= MethodFlags.RuntimeImplemented;

                if ((methodImplAttributes & MethodImplAttributes.InternalCall) != 0)
                    flags |= MethodFlags.InternalCall;

                if ((methodImplAttributes & MethodImplAttributes.Synchronized) != 0)
                    flags |= MethodFlags.Synchronized;

                flags |= MethodFlags.BasicMetadataCache;
            }

            // Fetching custom attribute based properties is more expensive, so keep that under
            // a separate cache that might not be accessed very frequently.
            if ((mask & MethodFlags.AttributeMetadataCache) != 0)
            {
                var metadataReader = this.MetadataReader;
                var methodDefinition = metadataReader.GetMethodDefinition(_handle);

                foreach (var attributeHandle in methodDefinition.GetCustomAttributes())
                {
                    StringHandle namespaceHandle, nameHandle;
                    if (!metadataReader.GetAttributeNamespaceAndName(attributeHandle, out namespaceHandle, out nameHandle))
                        continue;

                    if (metadataReader.StringComparer.Equals(namespaceHandle, "System.Runtime.CompilerServices"))
                    {
                        if (metadataReader.StringComparer.Equals(nameHandle, "IntrinsicAttribute"))
                        {
                            flags |= MethodFlags.Intrinsic;
                        }
                    }
                    else
                    if (metadataReader.StringComparer.Equals(namespaceHandle, "System.Runtime.InteropServices"))
                    {
                        if (metadataReader.StringComparer.Equals(nameHandle, "NativeCallableAttribute"))
                        {
                            flags |= MethodFlags.NativeCallable;
                        }
                    }
                    else
                    if (metadataReader.StringComparer.Equals(namespaceHandle, "System.Runtime"))
                    {
                        if (metadataReader.StringComparer.Equals(nameHandle, "RuntimeExportAttribute"))
                        {
                            flags |= MethodFlags.RuntimeExport;
                        }
                    }
                }

                flags |= MethodFlags.AttributeMetadataCache;
            }

            Debug.Assert((flags & mask) != 0);
            _methodFlags.AddFlags(flags);

            return flags & mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetMethodFlags(int mask)
        {
            int flags = _methodFlags.Value & mask;
            if (flags != 0)
                return flags;
            return InitializeMethodFlags(mask);
        }

        public override bool IsVirtual
        {
            get
            {
                return (GetMethodFlags(MethodFlags.BasicMetadataCache | MethodFlags.Virtual) & MethodFlags.Virtual) != 0;
            }
        }

        public override bool IsNewSlot
        {
            get
            {
                return (GetMethodFlags(MethodFlags.BasicMetadataCache | MethodFlags.NewSlot) & MethodFlags.NewSlot) != 0;
            }
        }

        public override bool IsAbstract
        {
            get
            {
                return (GetMethodFlags(MethodFlags.BasicMetadataCache | MethodFlags.Abstract) & MethodFlags.Abstract) != 0;
            }
        }

        public override bool IsFinal
        {
            get
            {
                return (GetMethodFlags(MethodFlags.BasicMetadataCache | MethodFlags.Final) & MethodFlags.Final) != 0;
            }
        }

        public override bool IsNoInlining
        {
            get
            {
                return (GetMethodFlags(MethodFlags.BasicMetadataCache | MethodFlags.NoInlining) & MethodFlags.NoInlining) != 0;
            }
        }

        public override bool IsAggressiveInlining
        {
            get
            {
                return (GetMethodFlags(MethodFlags.BasicMetadataCache | MethodFlags.AggressiveInlining) & MethodFlags.AggressiveInlining) != 0;
            }
        }

        public override bool IsRuntimeImplemented
        {
            get
            {
                return (GetMethodFlags(MethodFlags.BasicMetadataCache | MethodFlags.RuntimeImplemented) & MethodFlags.RuntimeImplemented) != 0;
            }
        }

        public override bool IsIntrinsic
        {
            get
            {
                return (GetMethodFlags(MethodFlags.AttributeMetadataCache | MethodFlags.Intrinsic) & MethodFlags.Intrinsic) != 0;
            }
        }

        public override bool IsInternalCall
        {
            get
            {
                return (GetMethodFlags(MethodFlags.BasicMetadataCache | MethodFlags.InternalCall) & MethodFlags.InternalCall) != 0;
            }
        }

        public override bool IsSynchronized
        {
            get
            {
                return (GetMethodFlags(MethodFlags.BasicMetadataCache | MethodFlags.Synchronized) & MethodFlags.Synchronized) != 0;
            }
        }

        public override bool IsNativeCallable
        {
            get
            {
                return (GetMethodFlags(MethodFlags.AttributeMetadataCache | MethodFlags.NativeCallable) & MethodFlags.NativeCallable) != 0;
            }
        }

        public override bool IsRuntimeExport
        {
            get
            {
                return (GetMethodFlags(MethodFlags.AttributeMetadataCache | MethodFlags.RuntimeExport) & MethodFlags.RuntimeExport) != 0;
            }
        }

        public override bool IsDefaultConstructor
        {
            get
            {
                MethodAttributes attributes = Attributes;
                return attributes.IsRuntimeSpecialName() 
                    && attributes.IsPublic()
                    && Signature.Length == 0
                    && Name == ".ctor"
                    && !_type.IsAbstract;
            }
        }

        public MethodAttributes Attributes
        {
            get
            {
                return MetadataReader.GetMethodDefinition(_handle).Attributes;
            }
        }

        public MethodImplAttributes ImplAttributes
        {
            get
            {
                return MetadataReader.GetMethodDefinition(_handle).ImplAttributes;
            }
        }

        private string InitializeName()
        {
            var metadataReader = MetadataReader;
            var name = metadataReader.GetString(metadataReader.GetMethodDefinition(_handle).Name);
            return (_name = name);
        }

        public override string Name
        {
            get
            {
                if (_name == null)
                    return InitializeName();
                return _name;
            }
        }

        private void ComputeGenericParameters()
        {
            var genericParameterHandles = MetadataReader.GetMethodDefinition(_handle).GetGenericParameters();
            int count = genericParameterHandles.Count;
            if (count > 0)
            {
                TypeDesc[] genericParameters = new TypeDesc[count];
                int i = 0;
                foreach (var genericParameterHandle in genericParameterHandles)
                {
                    genericParameters[i++] = new EcmaGenericParameter(Module, genericParameterHandle);
                }
                Interlocked.CompareExchange(ref _genericParameters, genericParameters, null);
            }
            else
            {
                _genericParameters = TypeDesc.EmptyTypes;
            }
        }

        public override Instantiation Instantiation
        {
            get
            {
                if (_genericParameters == null)
                    ComputeGenericParameters();
                return new Instantiation(_genericParameters);
            }
        }

        public override bool HasCustomAttribute(string attributeNamespace, string attributeName)
        {
            return !MetadataReader.GetCustomAttributeHandle(MetadataReader.GetMethodDefinition(_handle).GetCustomAttributes(),
                attributeNamespace, attributeName).IsNil;
        }

        public override string ToString()
        {
            return _type.ToString() + "." + Name;
        }

        public override bool IsPInvoke
        {
            get
            {
                return (((int)Attributes & (int)MethodAttributes.PinvokeImpl) != 0);
            }
        }

        public override PInvokeMetadata GetPInvokeMethodMetadata()
        {
            if (!IsPInvoke)
                return default(PInvokeMetadata);

            MetadataReader metadataReader = MetadataReader;
            MethodImport import = metadataReader.GetMethodDefinition(_handle).GetImport();
            string name = metadataReader.GetString(import.Name);

            ModuleReference moduleRef = metadataReader.GetModuleReference(import.Module);
            string moduleName = metadataReader.GetString(moduleRef.Name);

            // Spot check the enums match
            Debug.Assert((int)MethodImportAttributes.CallingConventionStdCall == (int)PInvokeAttributes.CallingConventionStdCall);
            Debug.Assert((int)MethodImportAttributes.CharSetAuto == (int)PInvokeAttributes.CharSetAuto);
            Debug.Assert((int)MethodImportAttributes.CharSetUnicode == (int)PInvokeAttributes.CharSetUnicode);
            Debug.Assert((int)MethodImportAttributes.SetLastError == (int)PInvokeAttributes.SetLastError);

            PInvokeAttributes pinvokeAttribtues = (PInvokeAttributes)import.Attributes;

            DllImportSearchPath? dllImportSearchPath = this.GetDllImportSearchPath();

            // if DefaultDllImportSearchPathAttribute is not assigned on the method
            // check to see whether it is assigned on the containing assembly
            if (!dllImportSearchPath.HasValue)
            {
                Debug.Assert(Module is IAssemblyDesc, "Multi-module assemblies");
                dllImportSearchPath = ((EcmaAssembly)Module).GetDllImportSearchPath();
            }

            if (dllImportSearchPath.HasValue)
            {
                switch (dllImportSearchPath.Value)
                {
                    case DllImportSearchPath.ApplicationDirectory:
                        pinvokeAttribtues |= PInvokeAttributes.DllImportSearchPathApplicationDirectory;
                        break;
                    case DllImportSearchPath.AssemblyDirectory:
                        pinvokeAttribtues |= PInvokeAttributes.DllImportSearchPathAssemblyDirectory;
                        break;
                    case DllImportSearchPath.LegacyBehavior:
                        pinvokeAttribtues |= PInvokeAttributes.DllImportSearchPathLegacyBehavior;
                        break;
                    case DllImportSearchPath.SafeDirectories:
                        pinvokeAttribtues |= PInvokeAttributes.DllImportSearchPathSafeDirectories;
                        break;
                    case DllImportSearchPath.System32:
                        pinvokeAttribtues |= PInvokeAttributes.DllImportSearchPathSystem32;
                        break;
                    case DllImportSearchPath.UseDllDirectoryForDependencies:
                        pinvokeAttribtues |= PInvokeAttributes.DllImportSearchPathUseDllDirectoryForDependencies;
                        break;
                    case DllImportSearchPath.UserDirectories:
                        pinvokeAttribtues |= PInvokeAttributes.DllImportSearchPathUserDirectories;
                        break;
                    default:
                        Debug.Assert(false, "Unexpected DllImportSearchPath");
                        break;
                }
            }
            return new PInvokeMetadata(moduleName, name, pinvokeAttribtues);
        }

        public override ParameterMetadata[] GetParameterMetadata()
        {
            MetadataReader metadataReader = MetadataReader;
            
            // Spot check the enums match
            Debug.Assert((int)ParameterAttributes.In == (int)ParameterMetadataAttributes.In);
            Debug.Assert((int)ParameterAttributes.Out == (int)ParameterMetadataAttributes.Out);
            Debug.Assert((int)ParameterAttributes.Optional == (int)ParameterMetadataAttributes.Optional);
            Debug.Assert((int)ParameterAttributes.HasDefault == (int)ParameterMetadataAttributes.HasDefault);
            Debug.Assert((int)ParameterAttributes.HasFieldMarshal == (int)ParameterMetadataAttributes.HasFieldMarshal);

            ParameterHandleCollection parameterHandles = metadataReader.GetMethodDefinition(_handle).GetParameters();
            ParameterMetadata[] parameterMetadataArray = new ParameterMetadata[parameterHandles.Count];
            int index = 0;
            foreach (ParameterHandle parameterHandle in parameterHandles)
            {
                Parameter parameter = metadataReader.GetParameter(parameterHandle);
                MarshalAsDescriptor marshalAsDescriptor = GetMarshalAsDescriptor(parameter);
                ParameterMetadata data = new ParameterMetadata(parameter.SequenceNumber, (ParameterMetadataAttributes)parameter.Attributes, marshalAsDescriptor);
                parameterMetadataArray[index++] = data;
            }
            return parameterMetadataArray;
        }

        private MarshalAsDescriptor GetMarshalAsDescriptor(Parameter parameter)
        {
            if ((parameter.Attributes & ParameterAttributes.HasFieldMarshal) == ParameterAttributes.HasFieldMarshal)
            {
                MetadataReader metadataReader = MetadataReader;
                BlobReader marshalAsReader = metadataReader.GetBlobReader(parameter.GetMarshallingDescriptor());
                EcmaSignatureParser parser = new EcmaSignatureParser(Module, marshalAsReader);
                MarshalAsDescriptor marshalAs = parser.ParseMarshalAsDescriptor();
                Debug.Assert(marshalAs != null);
                return marshalAs;
            }
            return null;
        }
    }
}
