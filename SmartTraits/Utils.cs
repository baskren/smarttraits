using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using TypeInfo = Microsoft.CodeAnalysis.TypeInfo;

namespace SmartTraits
{
    public static class Utils
    {
        public static DiagnosticSeverity ToSeverity(this T4GeneratorVerbosity verbosity)
        {
            return verbosity switch
            {
                T4GeneratorVerbosity.Critical => DiagnosticSeverity.Error,
                T4GeneratorVerbosity.Error => DiagnosticSeverity.Error,
                T4GeneratorVerbosity.Warning => DiagnosticSeverity.Warning,
                T4GeneratorVerbosity.Info => DiagnosticSeverity.Info,
                _ => DiagnosticSeverity.Hidden,
            };
        }

        public static string GetPath(string directoryPath, string directoryName)
        {
            if (Directory.Exists(directoryPath + "/" + directoryName))
                return directoryPath + "/" + directoryName + "/";

            DirectoryInfo parentInfo = Directory.GetParent(directoryPath);
            if (parentInfo == null)
                return null;

            return GetPath(parentInfo.FullName, directoryName);
        }

        public static (string mscorlibLocation, string netstandardLocation) GetNetStandardAssemblyLocactions(SyntaxNode node, DiagnosticDescriptor diagnostics)
        {
            string coreDir = Path.GetDirectoryName(typeof(object).GetTypeInfo().Assembly.Location);
            if (coreDir == null)
                return (null, null);

            var mscorlibLocation = Path.Combine(coreDir, "mscorlib.dll");
            if (!File.Exists(mscorlibLocation))
            {
                var parentDir = Directory.GetParent(coreDir);

                string mscorlibLocation2 = parentDir.FullName + Path.DirectorySeparatorChar + "mscorlib.dll";
                if (!File.Exists(mscorlibLocation2))
                    return (null, null);

                mscorlibLocation = mscorlibLocation2;
            }

            var netstandardLocation = Path.Combine(coreDir, "netstandard.dll");
            if (!File.Exists(netstandardLocation))
                return (null, null);

            return (mscorlibLocation, netstandardLocation);
        }

        public static string RemoveNewLine(string s)
        {
            if (s == null)
                return "";

            return s.Replace("\n", " ").Replace("\r", " ");
        }

        public static string RemoveWhitespace(string s)
        {
            if (s == null)
                return "";

            return Regex.Replace(s, @"\s", "");
        }

        public static string GetUniqueFileName(HashSet<string> generatedFiles, string sourcefileName)
        {
            string fileName = $"{sourcefileName}.g.cs";
            if (!generatedFiles.Contains(fileName))
                return fileName;

            for (int i = 1; i < 1000; i++)
            {
                fileName = $"{sourcefileName}.{i}.g.cs";
                if (!generatedFiles.Contains(fileName))
                    return fileName;
            }

            throw (new Exception($"Cannot get unique file name for the {sourcefileName}"));
        }

        public static StringBuilder ReturnError(string errorMsg)
        {
            StringBuilder sb = new();

            sb.AppendLine($"#error {errorMsg}");

            return sb;
        }

        public static List<MemberNodeInfo> GetAllMembersInfo(TypeDeclarationSyntax node)
        {
            List<MemberNodeInfo> membersInfo = new();

            foreach (MemberDeclarationSyntax memberNode in node.Members)
            {
                if (memberNode is BaseFieldDeclarationSyntax fieldNode)
                {
                    foreach (VariableDeclaratorSyntax variableNode in fieldNode.Declaration.Variables)
                    {
                        membersInfo.Add(new MemberNodeInfo()
                        {
                            Kind = variableNode.Kind(),
                            Name = variableNode.Identifier.ToString(),
                        });
                    }
                }
                else
                {
                    var nodeInfo = GetMemberInfo(memberNode);
                    if (nodeInfo != null)
                        membersInfo.Add(nodeInfo);
                }
            }

            return membersInfo;
        }

        public static MemberNodeInfo GetMemberInfo(MemberDeclarationSyntax member)
        {
            if (member is ClassDeclarationSyntax classMember)
            {
                return new MemberNodeInfo()
                {
                    Name = classMember.Identifier.ToFullString(),
                    Kind = member.Kind(),
                };
            }

            if (member is MethodDeclarationSyntax methodMember)
            {
                return new MemberNodeInfo()
                {
                    Name = methodMember.Identifier.ToFullString(),
                    Kind = member.Kind(),
                    ReturnType = Utils.RemoveWhitespace(methodMember.ReturnType.ToFullString()),
                    GenericsCount = methodMember.TypeParameterList?.Parameters.Count() ?? 0,
                    Arguments = Utils.RemoveWhitespace(string.Join(";", methodMember.ParameterList.Parameters.Select(s => s.Type?.ToString()))),
                };
            }

            if (member is PropertyDeclarationSyntax propertyMember)
            {
                return new MemberNodeInfo()
                {
                    Name = propertyMember.Identifier.ToFullString(),
                    Kind = member.Kind(),
                    ReturnType = Utils.RemoveWhitespace(propertyMember.Type.ToString()),
                };
            }

            return null;
        }

        public static bool IsStrictMode(ClassDeclarationSyntax traitClass)
        {
            var classAttrs = traitClass.AttributeLists.SelectMany(s => s.Attributes).ToArray();
            var traitAttr = classAttrs.FirstOrDefault(w => Consts.TraitAttributes.Contains(w.Name.ToFullString()));

            return HasAttributeParameter(traitAttr, Consts.TraitOptionsEnums, "Strict");
        }

        public static InterfaceDeclarationSyntax GetTraitInterface(SemanticModel semanticModel, ClassDeclarationSyntax classNode)
        {
            if (classNode.BaseList == null)
                return null;

            foreach (var baseListType in classNode.BaseList.Types)
            {
                TypeInfo typeInfo = semanticModel.GetTypeInfo(baseListType.Type);
                if (typeInfo.Type == null)
                    return null;

                SyntaxNode typeNode = semanticModel.GetSymbolInfo(baseListType.Type).Symbol?.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
                if (typeNode == null)
                    continue;

                if (typeNode is InterfaceDeclarationSyntax interfaceNode)
                {
                    if (interfaceNode.AttributeLists.SelectMany(s => s.Attributes).Any(w => Consts.TraitInterfaceAttributes.Contains(w.Name.ToString())))
                        return interfaceNode;
                }
            }

            return null;
        }

        public static StringBuilder LogExceptionAsComments(Exception ex, string msg)
        {
            StringBuilder sb = new();

            sb.AppendLine($"#error {Utils.RemoveNewLine(msg)}");
            sb.AppendLine("");
            sb.AppendLine("// Exception message:");

            foreach (string s in ex.Message.Split('\n'))
            {
                sb.AppendLine($"// {Utils.RemoveNewLine(s)}");
            }

            sb.AppendLine("");
            sb.AppendLine("// Exception stack trace:");

            foreach (string s in ex.StackTrace.Split('\n'))
            {
                sb.AppendLine($"// {Utils.RemoveNewLine(s)}");
            }

            return sb;
        }

        public static bool HasAttributeOfType(this MemberDeclarationSyntax member, string[] possibleTypes)
        {
            return member.AttributeLists.Any(w => w.Attributes.Any(a => possibleTypes.Contains(a.Name.ToString())));
        }

        public static bool HasAttributeParameter(this AttributeSyntax attr, string[] possibleTypes, string name)
        {
            if (attr?.ArgumentList == null)
                return false;

            return attr.ArgumentList.Arguments
                .Where(w => w.Expression.Kind() == SyntaxKind.SimpleMemberAccessExpression)
                .Select(s => s.Expression)
                .OfType<MemberAccessExpressionSyntax>()
                .Any(w => possibleTypes.Contains(w.Expression.ToString()) && w.Name.ToString() == name);
        }

        public static IEnumerable<AttributeSyntax> GetAttributesOfType(this MemberDeclarationSyntax memberNode, string[] possibleTypes)
        {
            return memberNode.AttributeLists.SelectMany(s => s.Attributes).Where(w => possibleTypes.Contains(w.Name.ToString()));
        }

        public static List<MemberNodeInfo> GetMemberNodeInfo(MemberDeclarationSyntax memberNode)
        {
            List<MemberNodeInfo> result = new();
            if (memberNode is BaseFieldDeclarationSyntax fieldNodes)
            {
                foreach (var variableNode in fieldNodes.Declaration.Variables)
                {
                    result.Add(new MemberNodeInfo()
                    {
                        Kind = variableNode.Kind(),
                        Name = variableNode.Identifier.ToString(),
                    });
                }
            }
            else
            {
                MemberNodeInfo nodeInfo = Utils.GetMemberInfo(memberNode);
                if (nodeInfo != null)
                    result.Add(nodeInfo);
            }

            return result;
        }

        public static MemberDeclarationSyntax RemoveAttributes(MemberDeclarationSyntax memberNode, string[] removeAttributes)
        {
            while (true)
            {
                if (memberNode == null)
                    break;

                var removeAttr = memberNode.AttributeLists.SelectMany(s => s.Attributes.Where(w => removeAttributes.Contains(w.Name.ToString()))).FirstOrDefault();
                if (removeAttr == null)
                    break;

                memberNode = memberNode.RemoveNode(removeAttr.Ancestors().OfType<AttributeListSyntax>().First(), SyntaxRemoveOptions.KeepNoTrivia);
            }

            return memberNode;
        }

        public static void AddToGeneratedSources(GeneratorExecutionContext context, HashSet<string> generatedFiles, MemberDeclarationSyntax memberNode, StringBuilder sb, string defaultFileName = "Common", AttributeSyntax addTraitAttr = null, SemanticModel semanticModel = null, ClassDeclarationSyntax traitClassNode = null)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            string fileName = defaultFileName;
            if (memberNode != null)
                fileName = Path.GetFileNameWithoutExtension(memberNode.SyntaxTree.FilePath);
            if (semanticModel != null)
            {
                var traitClassName = string.Empty;
                if (addTraitAttr != null)
                {
                    var firstParam = addTraitAttr.ArgumentList?.Arguments.FirstOrDefault();

                    var typeofTraitNode = firstParam?.DescendantNodes().OfType<TypeOfExpressionSyntax>().FirstOrDefault();
                    if (typeofTraitNode == null)
                        return;

                    TypeInfo addTraitType = semanticModel.GetTypeInfo(typeofTraitNode.Type);
                    traitClassName = addTraitType.Type?.ToDisplayString();
                }
                else if (traitClassNode is ClassDeclarationSyntax traitClass)
                    traitClassName = traitClassNode.Identifier.ToFullString();
                fileName = traitClassName.Trim() + "." + fileName;
            }

            string generatedFileName = Utils.GetUniqueFileName(generatedFiles, fileName);
            context.AddSource(generatedFileName, sb.ToString());

            generatedFiles.Add(generatedFileName);
        }

        public static string GetSpanText(this SyntaxNode node, Microsoft.CodeAnalysis.Text.TextSpan span)
        {
            while (node.FullSpan.Start > 0)
                node = node.Parent;
            return node.ToFullString().Substring(span.Start, span.Length);
        }

        public static string GetSpanText(this SyntaxNode node, int start, int length = -1)
        {
            while (node.FullSpan.Start > 0)
                node = node.Parent;
            if (length < 0)
                return node.ToFullString().Substring(start);
            else
                return node.ToFullString().Substring(start, length);
        }

        public static string PopulatePlaceholder(string text, string placeholder, string type)
        {
            text += " ";
            var typeBounds = new[] { ' ', '\t', '\r', '\n', '<', '>', ',', '(', ':' };
            var sb = new StringBuilder();

            var index = 0;
            var lastWasBounds = false;
            while (index < text.Length - placeholder.Length)
            {
                var postChar = text[index + placeholder.Length];
                if (lastWasBounds &&
                    text.Substring(index, placeholder.Length) == placeholder &&
                    typeBounds.Contains(postChar))
                {
                    sb.Append(type);
                    sb.Append(postChar);
                    lastWasBounds = true;
                    index += placeholder.Length + 1;
                }
                else
                {
                    var c = text[index];
                    sb.Append(c);
                    lastWasBounds = typeBounds.Contains(c);
                    index++;
                }
            }
            //sb.Append(text.Substring(text.Length - placeholder.Length - 2));
            return sb.ToString();
        }

        public static bool InheritsFrom(this INamedTypeSymbol symbol, ITypeSymbol type)
        {
            var baseType = symbol;
            while (baseType != null)
            {
                if (type.Equals(baseType))
                    return true;

                baseType = baseType.BaseType;
            }
            return false;
        }

        public static bool Implements(this INamedTypeSymbol symbol, ITypeSymbol type)
        {
            return symbol.AllInterfaces.Any(i => type.Equals(i));
        }

        public static List<string> GetNamespaceCandidates(this CSharpSyntaxNode tpcs)
        {
            var result = new List<string>();

            var syntaxNode = tpcs as CSharpSyntaxNode;
            do
            {
                syntaxNode = (CSharpSyntaxNode)syntaxNode.Parent;
                if (syntaxNode is NamespaceDeclarationSyntax namespaceNode)
                    result.Insert(0, namespaceNode.Name.ToFullString().Trim());
                if (syntaxNode.GetType().GetProperty("Usings") is PropertyInfo usingsProperty)
                {
                    var usings = (SyntaxList<UsingDirectiveSyntax>)usingsProperty.GetValue(syntaxNode);
                    foreach (var usingNode in usings)
                        result.Add(usingNode.ToFullString().Trim());
                }
            } while (syntaxNode is not CompilationUnitSyntax);
            return result;
        }

        public static INamedTypeSymbol GetNamedTypeSymbol(this TypeParameterConstraintSyntax tpcs, SemanticModel semanticModel)
        {
            var namespaces = GetNamespaceCandidates(tpcs);
            var constraintTypeName = tpcs.ToFullString();
            foreach (var namespaceName in namespaces)
                if (semanticModel.Compilation.GetTypeByMetadataName(namespaceName + "." + constraintTypeName) is INamedTypeSymbol constraintType)
                    return constraintType;
            return null;
        }
    }

    public static class TypeDeclarationSyntaxExtensions
    {
        const char NESTED_CLASS_DELIMITER = '+';
        const char NAMESPACE_CLASS_DELIMITER = '.';
        const char TYPEPARAMETER_CLASS_DELIMITER = '`';

        public static string GetFullName(this TypeDeclarationSyntax source)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));

            var namespaces = new LinkedList<NamespaceDeclarationSyntax>();
            var types = new LinkedList<TypeDeclarationSyntax>();
            for (var parent = source.Parent; parent is object; parent = parent.Parent)
            {
                if (parent is NamespaceDeclarationSyntax @namespace)
                {
                    namespaces.AddFirst(@namespace);
                }
                else if (parent is TypeDeclarationSyntax type)
                {
                    types.AddFirst(type);
                }
            }

            var result = new StringBuilder();
            for (var item = namespaces.First; item is object; item = item.Next)
            {
                result.Append(item.Value.Name).Append(NAMESPACE_CLASS_DELIMITER);
            }
            for (var item = types.First; item is object; item = item.Next)
            {
                var type = item.Value;
                AppendName(result, type);
                result.Append(NESTED_CLASS_DELIMITER);
            }
            AppendName(result, source);

            return result.ToString();
        }

        static void AppendName(StringBuilder builder, TypeDeclarationSyntax type)
        {
            builder.Append(type.Identifier.Text);
            var typeArguments = type.TypeParameterList?.ChildNodes()
                .Count(node => node is TypeParameterSyntax) ?? 0;
            if (typeArguments != 0)
                builder.Append(TYPEPARAMETER_CLASS_DELIMITER).Append(typeArguments);
        }

    }
}
