// Usage: dotnet apidump.dll <dir with assemblies> <Assembly.dll> <type-name regex> [<regex> ...]
// Lists fields (with FieldOffset when present), properties, events, methods and constructors; enums list values.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

var dir = args[0];
var asmName = args[1];
var paths = Directory.GetFiles(dir, "*.dll").Concat(Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll"))
    .GroupBy(Path.GetFileName).Select(g => g.First());
var mlc = new MetadataLoadContext(new PathAssemblyResolver(paths));
var asm = mlc.LoadFromAssemblyPath(Path.Combine(dir, asmName));
Type[] types;
try { types = asm.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
foreach (var pat in args.Skip(2)) {
    var re = new Regex(pat);
    foreach (var t in types.Where(t => re.IsMatch(t.FullName ?? ""))) {
        Console.WriteLine($"=== {(t.IsEnum ? "enum" : t.IsInterface ? "interface" : t.IsValueType ? "struct" : "class")} {t.FullName} : {t.BaseType?.FullName} [{string.Join(", ", t.GetInterfaces().Select(i => i.Name))}]");
        const BindingFlags bf = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        if (t.IsEnum) {
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Static)) Console.WriteLine($"  {f.Name} = {f.GetRawConstantValue()}");
            continue;
        }
        foreach (var f in t.GetFields(bf)) {
            string off = "";
            try { var a = f.CustomAttributes.FirstOrDefault(c => c.AttributeType.Name == "FieldOffsetAttribute"); if (a != null) off = $" @0x{(int)a.ConstructorArguments[0].Value:X}"; } catch {}
            Console.WriteLine($"  F {(f.IsStatic ? "static " : "")}{N(f.FieldType)} {f.Name}{off}");
        }
        foreach (var p in t.GetProperties(bf)) Console.WriteLine($"  P {N(p.PropertyType)} {p.Name} {{{(p.CanRead ? " get;" : "")}{(p.CanWrite ? " set;" : "")} }}");
        foreach (var e in t.GetEvents(bf)) Console.WriteLine($"  E {N(e.EventHandlerType)} {e.Name}");
        foreach (var m in t.GetMethods(bf).Where(m => !m.IsSpecialName)) Console.WriteLine($"  M {(m.IsStatic ? "static " : "")}{N(m.ReturnType)} {m.Name}({string.Join(", ", m.GetParameters().Select(p => N(p.ParameterType) + " " + p.Name))})");
        foreach (var c in t.GetConstructors(bf)) Console.WriteLine($"  C ({string.Join(", ", c.GetParameters().Select(p => N(p.ParameterType) + " " + p.Name))})");
    }
}
static string N(Type t) {
    if (t.IsGenericType) return t.Name.Split('`')[0] + "<" + string.Join(",", t.GetGenericArguments().Select(N)) + ">";
    return t.Name;
}
