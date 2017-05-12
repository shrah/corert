// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a single PInvoke MethodFixupCell as defined in the core library.
    /// </summary>
    public class PInvokeMethodFixupNode : ObjectNode, ISymbolDefinitionNode
    {
        private PInvokeMetadata _metadata;

        public PInvokeMethodFixupNode(PInvokeMetadata metadata)
        {
            _metadata = metadata;
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("__pinvoke_");
            sb.Append(_metadata.Module);
            sb.Append("__");
            sb.Append(_metadata.Name);
        }
        public int Offset => 0;
        public override bool IsShareable => true;

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public override ObjectNodeSection Section => ObjectNodeSection.DataSection;

        public override bool StaticDependenciesAreComputed => true;

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder builder = new ObjectDataBuilder(factory, relocsOnly);
            builder.AddSymbol(this);

            //
            // Emit a MethodFixupCell struct
            //

            // Address (to be fixed up at runtime)
            builder.EmitZeroPointer();

            // Entry point name
            if (factory.Target.IsWindows && _metadata.Name.StartsWith("#", StringComparison.OrdinalIgnoreCase))
            {
                // Windows-specific ordinal import
                // CLR-compatible behavior: Strings that can't be parsed as a signed integer are treated as zero.
                int entrypointOrdinal;
                if (!int.TryParse(_metadata.Name.Substring(1), out entrypointOrdinal))
                    entrypointOrdinal = 0;

                // CLR-compatible behavior: Ordinal imports are 16-bit on Windows. Discard rest of the bits.
                builder.EmitNaturalInt((ushort)entrypointOrdinal);
            }
            else
            {
                // Import by name
                builder.EmitPointerReloc(factory.ConstantUtf8String(_metadata.Name));
            }

            // Module fixup cell
            builder.EmitPointerReloc(factory.PInvokeModuleFixup(_metadata.Module, _metadata.Flags.Attributes));

            return builder.ToObjectData();
        }
    }
}
