//using Microsoft.CodeAnalysis.CSharp;
//using Microsoft.CodeAnalysis.Emit;
//using Microsoft.CodeAnalysis;
//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Reflection;
//using System.Text;
//using System.Threading.Tasks;
//using WinterRose.WinterForgeSerializing.Workers;

//namespace WinterRose.WinterForgeSerializing.Formatting
//{
//    public class OpcodeCLICompiler
//    {
//        private record diffiredAssignment(string id, Func<string> method);
//        private record Dispatched();

//        Stack<string> varnameStack = new();
//        Stack<string> varStack = new();

//        int listNum = 0;

//        private string Var => varnameStack.Peek();

//        private static List<PortableExecutableReference> assemblyReferences;

//        private List<diffiredAssignment> diffiredAssignments = [];

//        static OpcodeCLICompiler()
//        {
//            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();

//            assemblyReferences = loadedAssemblies.Where(a =>
//            {
//                if (string.IsNullOrWhiteSpace(a.Location))
//                    return false;
//                if (!File.Exists(a.Location))
//                    return false;
//                return true;
//            }).
//            Select(a => MetadataReference.CreateFromFile(a.Location)).ToList();
//        }

//        private static int ParseRef(string raw)
//        {
//            var inner = raw[5..^1];
//            return int.Parse(inner);
//        }

//        private void Dispatch(int refID, Func<string> method)
//        {
//            diffiredAssignments.Add(new("var" + refID.ToString(), method));
//        }

//        private void Dispatch(string name, Func<string> method)
//        {
//            diffiredAssignments.Add(new(name, method));
//        }

//        public object validateValue(string val, string fieldName)
//        {
//            switch (val)
//            {
//                case string s when s.StartsWith("_ref("):
//                    {
//                        int refID = ParseRef(s);
//                        string str = refID.ToString();
//                        string varName = Var;
//                        if (!varnameStack.Any(var => var.EndsWith(str)))
//                        {
//                            Dispatch(refID, () =>
//                            {
//                                return $"{varName}.{fieldName} = var{refID};";
//                            });
//                            return new Dispatched();
//                        }
//                        return $"var{refID}";
//                    }

//                case string s when s.StartsWith("_stack"):
//                    {
//                        string stackvar = varStack.Pop();
//                        return $"{stackvar}";
//                    }
//            }

//            return val;
//        }

//        public MethodInfo GenerateCSharpFromOpcodes(List<Instruction> instructions, Stream assemblyDestinationStream)
//        {
//            var generatedCode = new StringBuilder();
//            string line;
//            string currentObjectName = string.Empty;

//            // Start the C# class generation
//            generatedCode.AppendLine("namespace WinterForge.GENERATED.DESERIALIZECLI;\n");
//            generatedCode.AppendLine("public class DeserializedObject {");
//            generatedCode.AppendLine("public static object Deserialize() {");

//            // Read the opcodes and convert to C# code
//            foreach (var instr in instructions)
//            {
//                switch (instr.OpCode)
//                {
//                    case OpCode.DEFINE:
//                        {
//                            // Define a new object type
//                            currentObjectName = instr.Args[0];
//                            varnameStack.Push("var" + instr.Args[1]);
//                            generatedCode.AppendLine($"{currentObjectName} {Var} = new {currentObjectName}();");
//                            break;
//                        }

//                    case OpCode.SET:
//                        {
//                            // Set property or field values
//                            var fieldName = instr.Args[0];
//                            var value = validateValue(instr.Args[1], fieldName);
//                            if (value is Dispatched)
//                                break;
//                            generatedCode.AppendLine($"    {Var}.{fieldName} = {value};");
//                            break;
//                        }

//                    case OpCode.LIST_START:
//                        {
//                            // Start a list of elements
//                            var listType = instr.Args[0];
//                            varnameStack.Push(Var + $"_{listNum++}");
//                            generatedCode.AppendLine($"    var {Var} = new System.Collections.Generic.List<{listType}>();");
//                            break;
//                        }

//                    case OpCode.ELEMENT:
//                        {
//                            // Add an element to the list
//                            object value = validateValue(instr.Args[0], "");
//                            if (value is Dispatched)
//                                break;
//                            generatedCode.AppendLine($"    {Var}.Add({value});");
//                            break;
//                        }

//                    case OpCode.RET:
//                        {
//                            string number = instr.Args[0];
//                            generatedCode.AppendLine($"return var{number};");
//                            break;
//                        }

//                    case OpCode.END:
//                    case OpCode.LIST_END:
//                        string var = varnameStack.Pop();
//                        varStack.Push(var);
//                        break;
//                    default:
//                        Console.WriteLine($"Unknown opcode: {instr.OpCode}");
//                        break;
//                }
//            }
//            generatedCode.AppendLine("}");
//            // End the C# class generation
//            generatedCode.AppendLine("}");

//            string code = generatedCode.ToString();

//            // Step 1: Parse the C# code string into a syntax tree
//            var syntaxTree = CSharpSyntaxTree.ParseText(code);

//            // Step 3: Create a compilation object
//            var compilation = CSharpCompilation.Create(
//                "DynamicAssembly", // Assembly name
//                syntaxTrees: new[] { syntaxTree }, // List of syntax trees
//                references: assemblyReferences,
//                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary) // Specify dynamic library
//            );

//            Assembly a;
//            using var memoryStream = new MemoryStream();

//            EmitResult result = compilation.Emit(assemblyDestinationStream);

//            if (!result.Success)
//            {
//                // If compilation fails, print out errors
//                foreach (var diagnostic in result.Diagnostics)
//                {
//                    Console.WriteLine(diagnostic.ToString());
//                }
//                throw new InvalidOperationException("Compilation failed.");
//            }

//            assemblyDestinationStream.Seek(0, SeekOrigin.Begin);
//            assemblyDestinationStream.CopyTo(memoryStream);
//            memoryStream.Seek(0, SeekOrigin.Begin);
//            a = Assembly.Load(memoryStream.ToArray());

//            return a.GetTypes()[0].GetMethod("Deserialize");
//        }
//    }
//}
