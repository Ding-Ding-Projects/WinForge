using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Disassembler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace WinForge.Services;

/// <summary>
/// 原生 .NET 組件反編譯服務 · Native .NET assembly decompiler service.
/// 完全用受控嘅 ICSharpCode.Decompiler（ILSpy 自己嘅引擎）同 System.Reflection.Metadata，
/// 喺程序內做 IL→C# 反編譯同 IL 反組譯 —— 唔會啟動／shell／bundle 任何外部工具（ILSpy／ildasm）。
/// Fully managed: uses ICSharpCode.Decompiler (ILSpy's own engine) + System.Reflection.Metadata to
/// decompile IL→C# and disassemble IL in-process. No external tool is launched, shelled, or bundled.
/// </summary>
public sealed class DecompilerService : IDisposable
{
    private PEFile? _file;
    private CSharpDecompiler? _decompiler;
    private DecompilerTypeSystem? _typeSystem;
    private UniversalAssemblyResolver? _resolver;

    public string? Path { get; private set; }
    public bool IsLoaded => _file is not null;

    /// <summary>組件嘅後設資料 · Loaded-assembly metadata snapshot.</summary>
    public AssemblyMeta? Meta { get; private set; }

    // ===== Loading 載入 =====

    /// <summary>
    /// 載入一個受控組件（.dll／.exe）· Load a managed assembly. Throws if the file is not a
    /// managed PE (e.g. a native DLL has no CLR metadata).
    /// </summary>
    public void Load(string path)
    {
        Dispose();
        var settings = new DecompilerSettings(LanguageVersion.CSharp10_0)
        {
            ThrowOnAssemblyResolveErrors = false,
            RemoveDeadCode = false,
            ShowXmlDocumentation = true,
        };

        var file = new PEFile(path, PEStreamOptions.PrefetchEntireImage);
        var resolver = new UniversalAssemblyResolver(path, false, file.DetectTargetFrameworkId());
        // 同目錄做後備搜尋 · also probe the assembly's own directory for referenced assemblies.
        var dir = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) resolver.AddSearchDirectory(dir);

        _file = file;
        _resolver = resolver;
        _typeSystem = new DecompilerTypeSystem(file, resolver, settings);
        _decompiler = new CSharpDecompiler(_typeSystem, settings);
        Path = path;
        Meta = BuildMeta(file, path);
    }

    private static AssemblyMeta BuildMeta(PEFile file, string path)
    {
        var md = file.Metadata;
        var asmDef = md.GetAssemblyDefinition();
        string name = md.GetString(asmDef.Name);
        var ver = asmDef.Version;

        // Public key token (last 8 bytes of SHA1 of public key, reversed) — derived by the reader.
        string pkt;
        try
        {
            var token = md.GetPublicKeyToken();
            pkt = (string.IsNullOrEmpty(token) || token == "null") ? "(none)" : token;
        }
        catch { pkt = "(none)"; }

        string tfm;
        try { tfm = file.DetectTargetFrameworkId(); }
        catch { tfm = ""; }
        if (string.IsNullOrWhiteSpace(tfm)) tfm = "(unknown)";

        // Referenced assemblies.
        var refs = new List<string>();
        foreach (var handle in md.AssemblyReferences)
        {
            var r = md.GetAssemblyReference(handle);
            refs.Add($"{md.GetString(r.Name)}, {r.Version}");
        }
        refs.Sort(StringComparer.OrdinalIgnoreCase);

        bool isExe;
        try { isExe = (file.Reader.PEHeaders.PEHeader?.Subsystem ?? Subsystem.Unknown) != Subsystem.WindowsCui
                       ? file.Reader.PEHeaders.CorHeader?.EntryPointTokenOrRelativeVirtualAddress is not 0 and not null
                       : true; }
        catch { isExe = string.Equals(System.IO.Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase); }

        return new AssemblyMeta(
            Name: name,
            Version: ver?.ToString() ?? "0.0.0.0",
            TargetFramework: tfm,
            PublicKeyToken: pkt,
            FullName: file.FullName,
            FilePath: path,
            Architecture: ArchOf(file),
            IsExecutable: isExe,
            ReferencedAssemblies: refs);
    }

    private static string ArchOf(PEFile file)
    {
        try
        {
            var m = file.Reader.PEHeaders.CoffHeader.Machine;
            return m switch
            {
                Machine.Amd64 => "x64",
                Machine.I386 => "x86 / AnyCPU",
                Machine.Arm64 => "ARM64",
                Machine.Arm => "ARM",
                _ => m.ToString(),
            };
        }
        catch { return "(unknown)"; }
    }

    // ===== Tree 樹狀結構 =====

    /// <summary>
    /// 建立「命名空間 → 型別 → 成員」嘅樹 · Build the namespace → type → member tree from the type system.
    /// </summary>
    public IReadOnlyList<TreeNode> BuildTree()
    {
        if (_typeSystem is null) return Array.Empty<TreeNode>();
        var asm = _typeSystem.MainModule;

        var byNs = new SortedDictionary<string, List<ITypeDefinition>>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in asm.TypeDefinitions)
        {
            if (t.DeclaringType is not null) continue; // nested handled under their parent
            string ns = string.IsNullOrEmpty(t.Namespace) ? "(global)" : t.Namespace;
            if (!byNs.TryGetValue(ns, out var list)) byNs[ns] = list = new();
            list.Add(t);
        }

        var roots = new List<TreeNode>();
        foreach (var (ns, types) in byNs)
        {
            var nsNode = new TreeNode(ns, NodeKind.Namespace, null);
            foreach (var t in types.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase))
                nsNode.Children.Add(BuildTypeNode(t));
            roots.Add(nsNode);
        }
        return roots;
    }

    private static TreeNode BuildTypeNode(ITypeDefinition t)
    {
        var kind = t.Kind switch
        {
            TypeKind.Interface => NodeKind.Interface,
            TypeKind.Enum => NodeKind.Enum,
            TypeKind.Struct => NodeKind.Struct,
            TypeKind.Delegate => NodeKind.Delegate,
            _ => NodeKind.Class,
        };
        var node = new TreeNode(TypeLabel(t), kind, t) { FullName = t.FullName };

        // Nested types first.
        foreach (var nt in t.NestedTypes.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase))
            node.Children.Add(BuildTypeNode(nt));

        foreach (var m in t.Members.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase))
        {
            var mk = m switch
            {
                IMethod im => im.IsConstructor ? NodeKind.Method : NodeKind.Method,
                IProperty => NodeKind.Property,
                IField => NodeKind.Field,
                IEvent => NodeKind.Event,
                _ => NodeKind.Method,
            };
            node.Children.Add(new TreeNode(MemberLabel(m), mk, m) { FullName = m.FullName });
        }
        return node;
    }

    private static string TypeLabel(ITypeDefinition t)
    {
        if (t.TypeParameterCount > 0)
            return $"{t.Name}<{string.Join(", ", t.TypeParameters.Select(p => p.Name))}>";
        return t.Name;
    }

    private static string MemberLabel(IMember m)
    {
        if (m is IMethod im)
        {
            string ps = string.Join(", ", im.Parameters.Select(p => p.Type.Name));
            string nm = im.IsConstructor ? (im.IsStatic ? ".cctor" : ".ctor") : im.Name;
            return $"{nm}({ps}) : {im.ReturnType.Name}";
        }
        if (m is IProperty ip) return $"{ip.Name} : {ip.ReturnType.Name}";
        if (m is IField ifld) return $"{ifld.Name} : {ifld.ReturnType.Name}";
        if (m is IEvent ie) return $"{ie.Name} : {ie.ReturnType.Name}";
        return m.Name;
    }

    // ===== Resources 資源 =====

    public IReadOnlyList<string> Resources()
    {
        if (_file is null) return Array.Empty<string>();
        var list = new List<string>();
        try
        {
            foreach (var r in _file.Resources)
                list.Add($"{r.Name}  ({r.ResourceType})");
        }
        catch { /* none */ }
        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list;
    }

    // ===== Decompile to C# C# 反編譯 =====

    public Task<string> DecompileWholeModuleAsync(CancellationToken ct = default) =>
        Task.Run(() =>
        {
            if (_decompiler is null) return "";
            ct.ThrowIfCancellationRequested();
            return _decompiler.DecompileWholeModuleAsString();
        }, ct);

    /// <summary>反編譯一個樹節點（型別或成員）做 C# · Decompile a tree node (type or member) to C#.</summary>
    public Task<string> DecompileNodeAsync(TreeNode node, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            if (_decompiler is null || node.Symbol is null)
                return "// 揀一個型別或成員 · Select a type or member to decompile.";
            ct.ThrowIfCancellationRequested();
            try
            {
                if (node.Symbol is ITypeDefinition td)
                    return _decompiler.DecompileTypeAsString(new FullTypeName(td.FullTypeName.ToString()));

                if (node.Symbol is IMember member)
                {
                    var eh = member.MetadataToken;
                    if (!eh.IsNil)
                        return _decompiler.DecompileAsString(eh);
                }
                return "// 呢個節點冇可反編譯嘅內容 · Nothing decompilable for this node.";
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return $"// 反編譯失敗 · Decompilation failed:\n// {ex.Message}";
            }
        }, ct);

    // ===== Disassemble IL IL 反組譯 =====

    public Task<string> DisassembleNodeAsync(TreeNode node, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            if (_file is null) return "";
            ct.ThrowIfCancellationRequested();
            try
            {
                EntityHandle handle = node.Symbol switch
                {
                    ITypeDefinition td => td.MetadataToken,
                    IMember m => m.MetadataToken,
                    _ => default,
                };
                if (handle.IsNil)
                    return "// 揀一個型別或成員 · Select a type or member to disassemble.";

                var output = new PlainTextOutput();
                var dis = new ReflectionDisassembler(output, ct)
                {
                    DetectControlStructure = true,
                    ShowSequencePoints = false,
                };
                var md = _file.Metadata;
                switch (handle.Kind)
                {
                    case HandleKind.TypeDefinition:
                        dis.DisassembleType(_file, (TypeDefinitionHandle)handle);
                        break;
                    case HandleKind.MethodDefinition:
                        dis.DisassembleMethod(_file, (MethodDefinitionHandle)handle);
                        break;
                    case HandleKind.FieldDefinition:
                        dis.DisassembleField(_file, (FieldDefinitionHandle)handle);
                        break;
                    case HandleKind.PropertyDefinition:
                        dis.DisassembleProperty(_file, (PropertyDefinitionHandle)handle);
                        break;
                    case HandleKind.EventDefinition:
                        dis.DisassembleEvent(_file, (EventDefinitionHandle)handle);
                        break;
                    default:
                        return "// 呢個節點冇 IL · No IL for this node.";
                }
                return output.ToString();
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return $"// IL 反組譯失敗 · IL disassembly failed:\n// {ex.Message}";
            }
        }, ct);

    public void Dispose()
    {
        _file?.Dispose();
        _file = null;
        _decompiler = null;
        _typeSystem = null;
        _resolver = null;
        Meta = null;
        Path = null;
    }
}

/// <summary>樹節點種類（決定圖示）· Tree-node kind (drives the icon).</summary>
public enum NodeKind
{
    Namespace, Class, Struct, Interface, Enum, Delegate,
    Method, Property, Field, Event,
}

/// <summary>組件樹一個節點 · One node in the assembly tree.</summary>
public sealed class TreeNode
{
    public string Label { get; }
    public NodeKind Kind { get; }
    public object? Symbol { get; }      // ITypeDefinition or IMember (null for namespaces)
    public string? FullName { get; set; }
    public List<TreeNode> Children { get; } = new();

    public TreeNode(string label, NodeKind kind, object? symbol)
    {
        Label = label;
        Kind = kind;
        Symbol = symbol;
    }

    public bool IsTypeOrMember => Symbol is not null;
}

/// <summary>組件後設資料 · Assembly metadata snapshot.</summary>
public sealed record AssemblyMeta(
    string Name,
    string Version,
    string TargetFramework,
    string PublicKeyToken,
    string FullName,
    string FilePath,
    string Architecture,
    bool IsExecutable,
    IReadOnlyList<string> ReferencedAssemblies);
