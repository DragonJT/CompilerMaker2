using System.Collections;
using System.Collections.Generic;
using System;

namespace CompilerMaker2
{
    class HTMLWriter
    {
        public List<char> html = new List<char>();

        public void Write(WASMWriter wasm)
        {
            html.Add('[');
            for (int i = 0; i < wasm.bytes.Count; i++)
            {
                var b = "0x" + BitConverter.ToString(new byte[] { wasm.bytes[i] });
                html.AddRange(b);
                if (i < wasm.bytes.Count - 1)
                    html.Add(',');
            }
            html.AddRange("];\n");
        }

        public void Write(string value)
        {
            html.AddRange(value);
        }

        public void Write(AsmImportFunction func)
        {
            html.AddRange(func.name);
            html.AddRange(":(");
            for (int i = 0; i < func.parameters.Length; i++)
            {
                html.AddRange(func.parameters[i].name);
                if (i < func.parameters.Length - 1)
                    html.Add(',');
            }
            html.AddRange(")=>{");
            html.AddRange(func.body);
            html.AddRange("},\n");
        }

        public override string ToString()
        {
            return new string(html.ToArray());
        }
    }

    class WASMWriter
    {
        public List<byte> bytes = new List<byte>();

        public void LEB128Signed(long value)
        {
            bool more = true;

            while (more)
            {
                byte chunk = (byte)(value & 0x7fL); // extract a 7-bit chunk
                value >>= 7;

                bool signBitSet = (chunk & 0x40) != 0; // sign bit is the msb of a 7-bit byte, so 0x40
                more = !((value == 0 && !signBitSet) || (value == -1 && signBitSet));
                if (more) { chunk |= 0x80; } // set msb marker that more bytes are coming

                bytes.Add(chunk);
            };
        }

        public void LEB128Unsigned(ulong value)
        {
            bool more = true;

            while (more)
            {
                byte chunk = (byte)(value & 0x7fUL); // extract a 7-bit chunk
                value >>= 7;

                more = value != 0;
                if (more) { chunk |= 0x80; } // set msb marker that more bytes are coming

                bytes.Add(chunk);
            };
        }

        public void Ieee754(float value)
        {
            bytes.AddRange(BitConverter.GetBytes(value));
        }

        public void String(string value)
        {
            LEB128Unsigned((ulong)value.Length);
            foreach (var c in value)
            {
                bytes.Add((byte)c);
            }
        }

        public void Bytes(params byte[] value)
        {
            bytes.AddRange(value);
        }

        public void MagicModuleHeader()
        {
            Bytes(0x00, 0x61, 0x73, 0x6d);
        }

        public void ModuleVersion()
        {
            Bytes(0x01, 0x00, 0x00, 0x00);
        }

        public void Writer(WASMWriter writer)
        {
            LEB128Unsigned((ulong)writer.bytes.Count);
            bytes.AddRange(writer.bytes);
        }

        public void Vector(WASMVector vector)
        {
            LEB128Unsigned((ulong)vector.count);
            bytes.AddRange(vector.bytes);
        }

        public void Section(Section section, WASMVector vector)
        {
            var writer = new WASMWriter();
            writer.Vector(vector);
            bytes.Add((byte)section);
            Writer(writer);
        }

        public void Valtype(Valtype valtype)
        {
            bytes.Add((byte)valtype);
        }

        public void ExportType(ExportType exportType)
        {
            bytes.Add((byte)exportType);
        }

        public void Opcode(Opcode opcode)
        {
            bytes.Add((byte)opcode);
        }

        public void FunctionType()
        {
            bytes.Add(0x60);
        }

        public void EmptyArray()
        {
            bytes.Add(0x0);
        }

        public void EncodeLocal(Valtype type, ulong count)
        {
            LEB128Unsigned(count);
            bytes.Add((byte)type);
        }
    }

    class WASMVector : WASMWriter
    {
        public int count;

        public void Increment()
        {
            count++;
        }
    }

    // https://webassembly.github.io/spec/core/binary/modules.html#sections
    enum Section : byte
    {
        custom = 0,
        type = 1,
        import = 2,
        func = 3,
        table = 4,
        memory = 5,
        global = 6,
        export = 7,
        start = 8,
        element = 9,
        code = 10,
        data = 11
    }

    // https://webassembly.github.io/spec/core/binary/types.html
    enum Valtype : byte
    {
        i32 = 0x7f,
        f32 = 0x7d
    }

    // http://webassembly.github.io/spec/core/binary/modules.html#export-section
    enum ExportType : byte
    {
        func = 0x00,
        table = 0x01,
        mem = 0x02,
        global = 0x03
    }

    // https://pengowray.github.io/wasm-ops/
    // https://webassembly.github.io/spec/core/binary/instructions.html
    enum Opcode : byte
    {
        block = 0x02,
        loop = 0x03,
        br = 0x0c,
        br_if = 0x0d,
        end = 0x0b,
        call = 0x10,
        get_local = 0x20,
        set_local = 0x21,
        i32_store_8 = 0x3a,
        i32_const = 0x41,
        f32_const = 0x43,
        i32_eqz = 0x45,
        i32_eq = 0x46,
        i32_add = 0x6a,
        i32_sub = 0x6b,
        i32_mul = 0x6c,
        i32_div_s = 0x6d,
        f32_eq = 0x5b,
        f32_lt = 0x5d,
        f32_gt = 0x5e,
        i32_and = 0x71,
        f32_add = 0x92,
        f32_sub = 0x93,
        f32_mul = 0x94,
        f32_div = 0x95,
        i32_trunc_f32_s = 0xa8,
        f32_convert_i32_s = 0xb2,
    }

    interface IInstruction
    {
        void Emit(WASMWriter wasm);
    }

    class AsmOp:IInstruction
    {
        Opcode opcode;

        public AsmOp(Opcode opcode)
        {
            this.opcode = opcode;
        }

        public void Emit(WASMWriter wasm)
        {
            wasm.Opcode(opcode);
        }
    }

    class AsmCall:IInstruction
    {
        IFunction function;

        public AsmCall(IFunction function)
        {
            this.function = function;
        }

        public void Emit(WASMWriter wasm)
        {
            wasm.Opcode(Opcode.call);
            wasm.LEB128Unsigned(function.funcID);
        }
    }

    class AsmConstF32:IInstruction
    {
        float value;

        public AsmConstF32(float value)
        {
            this.value = value;
        }


        public void Emit(WASMWriter wasm)
        {
            wasm.Opcode(Opcode.f32_const);
            wasm.Ieee754(value);
        }
    }

    class AsmParameter
    {
        public Valtype type;
        public string name;

        public AsmParameter(Valtype type, string name)
        {
            this.type = type;
            this.name = name;
        }
    }

    class IFunction
    {
        public ulong funcID;
        public ulong typeID;
        public string typeSignature;
        public AsmParameter[] parameters;
        public Valtype? returnType;
        public string name;

        static char ValtypeToChar(Valtype? type)
        {
            if (type == null)
                return 'v';
            switch (type)
            {
                case Valtype.f32: return 'f';
                case Valtype.i32: return 'i';
                default: throw new Exception("ValtypeToChar defaulted:" + type);
            }
        }

        static string GetTypeSignature(Valtype? returnType, AsmParameter[] parameters)
        {
            string typeSignature = "" + ValtypeToChar(returnType);
            foreach (var p in parameters)
                typeSignature += ValtypeToChar(p.type);
            return typeSignature;
        }

        public IFunction(Valtype? returnType, string name, AsmParameter[] parameters)
        {
            this.parameters = parameters;
            this.returnType = returnType;
            this.name = name;
            typeSignature = GetTypeSignature(returnType, parameters);
        }
    }

    class AsmImportFunction:IFunction
    {
        public string body;

        public AsmImportFunction(Valtype? returnType, string name, AsmParameter[] parameters, string body)
            :base(returnType, name, parameters)
        {
            this.body = body;
        }
    }

    class AsmFunction: IFunction
    {
        public bool export;
        public List<IInstruction> instructions = new();

        public AsmFunction(bool export, Valtype? returnType, string name, AsmParameter[] parameters)
            :base(returnType, name, parameters)
        {
            this.export = export;
        }

        public void Add(IInstruction instruction)
        {
            instructions.Add(instruction);
        }

        public void Emit(WASMWriter wasm)
        {
            foreach(var i in instructions)
            {
                i.Emit(wasm);
            }
            wasm.Opcode(Opcode.end);
        }

    }

    class Asm
    {
        List<IFunction> functions = new();

        public AsmImportFunction ImportFunction(Valtype? returnType, string name, AsmParameter[] parameters, string body)
        {
            var importFunction = new AsmImportFunction(returnType, name, parameters, body);
            functions.Add(importFunction);
            return importFunction;
        }

        public AsmFunction Function(bool export, Valtype? returnType, string name, AsmParameter[] parameters)
        {
            var function = new AsmFunction(export, returnType, name, parameters);
            functions.Add(function);
            return function;
        }

        public IFunction FindFunction(string name)
        {
            return functions.First(f => f.name == name);
        }
        
        WASMWriter Encode()
        {
            var functions = this.functions.OfType<AsmFunction>().ToArray();
            var importFunctions = this.functions.OfType<AsmImportFunction>().ToArray();
            ulong id = 0;
            foreach (var f in importFunctions)
            {
                f.funcID = id;
                id++;
            }
            foreach(var f in functions)
            {
                f.funcID = id;
                id++;
            }

            var wasm = new WASMWriter();
            wasm.MagicModuleHeader();
            wasm.ModuleVersion();

            var typeVector = new WASMVector();
            var typeSignatures = new Dictionary<string, ulong>();
            foreach (var f in this.functions)
            {
                if (!typeSignatures.ContainsKey(f.typeSignature))
                {
                    typeVector.FunctionType();
                    var paramVector = new WASMVector();
                    foreach(var p in f.parameters)
                    {
                        paramVector.Valtype(p.type);
                        paramVector.Increment();
                    }
                    typeVector.Vector(paramVector);
                    var returnVector = new WASMVector();
                    if (f.returnType != null)
                    {
                        returnVector.Valtype(f.returnType.Value);
                        returnVector.Increment();
                    }
                    typeVector.Vector(returnVector);
                    typeVector.Increment();
                    f.typeID = (ulong)typeSignatures.Count;
                    typeSignatures.Add(f.typeSignature, f.typeID);
                }
                else
                {
                    f.typeID = typeSignatures[f.typeSignature];
                }
            }
            wasm.Section(Section.type, typeVector);
            
            var importVector = new WASMVector();
            foreach (var f in importFunctions)
            {
                importVector.String("env");
                importVector.String(f.name);
                importVector.ExportType(ExportType.func);
                importVector.LEB128Unsigned(f.typeID);
                importVector.Increment();
            }
            wasm.Section(Section.import, importVector);

            var funcVector = new WASMVector();
            foreach (var f in functions)
            {
                funcVector.LEB128Unsigned(f.typeID);
                funcVector.Increment();
            }
            wasm.Section(Section.func, funcVector);

            var exportVector = new WASMVector();
            foreach(var f in functions)
            {
                if (f.export)
                {
                    exportVector.String(f.name);
                    exportVector.ExportType(ExportType.func);
                    exportVector.LEB128Unsigned(f.funcID);
                    exportVector.Increment();
                }
            }
            wasm.Section(Section.export, exportVector);

            var functionVector = new WASMVector();
            foreach (var f in functions)
            {
                var fwasm = new WASMWriter();
                fwasm.EmptyArray();
                f.Emit(fwasm);
                functionVector.Writer(fwasm);
                functionVector.Increment();
            }
            wasm.Section(Section.code, functionVector);
            return wasm;
        }

        void WriteImportFunctions(HTMLWriter html)
        {
            foreach (var f in this.functions.OfType<AsmImportFunction>())
            {
                html.Write(f);
            }
        }

        public string Emit()
        {
            var wasm = Encode();
            var html = new HTMLWriter();

            html.Write("        <script>");
            html.Write("var wasm = ");
            html.Write(wasm);
            html.Write("var importObject = {env:{");
            WriteImportFunctions(html);
            html.Write("}};\n");
            html.Write(@"WebAssembly.instantiate(Uint8Array.from(wasm), importObject).then(
    (obj) => {
        obj.instance.exports.Main();
    }
);");
            html.Write("       </script>");
            return html.ToString();
        }
    }
}

