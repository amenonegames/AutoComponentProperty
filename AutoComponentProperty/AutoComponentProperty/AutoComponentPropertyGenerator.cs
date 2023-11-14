using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace AutoComponentProperty
{
    [Generator]
    public class AutoComponentPropertyGenerator : ISourceGenerator
    {
        
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForPostInitialization(x => SetDefaultAttribute(x));
            context.RegisterForSyntaxNotifications( () => new SyntaxReceiver() );
        }
        
        private void SetDefaultAttribute(GeneratorPostInitializationContext context)
        {
            // AutoPropertyAttributeのコード本体
            const string AttributeText = @"
using System;
namespace AutoComponentProperty
{
    [AttributeUsage(AttributeTargets.Field,
                    Inherited = false, AllowMultiple = false)]
        sealed class CompoPropAttribute : Attribute
    {
    
        public GetFrom from { get; set; }

        public CompoPropAttribute(GetFrom from)
        {   
            this.from = from;
        }
        
    }

    [Flags]
    internal enum GetFrom
    {
        This  = 1,
        Children = 1 << 1,
        Parent = 1 << 2,
    }

}
";            
            //コンパイル時に参照するアセンブリを追加
            context.AddSource
            (
                "CompoPropAttribute.cs",
                SourceText.From(AttributeText,Encoding.UTF8)
            );
        }

        public void Execute(GeneratorExecutionContext context)
        {
            //Context.SyntaxReceiverというプロパティに格納されているので
            //それを取得する
            var receiver = context.SyntaxReceiver as SyntaxReceiver;
            if (receiver == null) return;
            
            var fieldSymbols = new List<(IFieldSymbol field, ITypeSymbol sourceType , GetFrom from)>();

            foreach (var field in receiver.TargetFields)
            {
                var model = context.Compilation.GetSemanticModel(field.field.SyntaxTree);
                foreach (var variable in field.field.Declaration.Variables)
                {
                    var fieldSymbol = model.GetDeclaredSymbol(variable) as IFieldSymbol;
                    //フィールド属性からAutoProperty属性があるかを確認

                    var arg = field.attr.ArgumentList.Arguments[0];
                    var expr = arg.Expression;
                    var parsed = Enum.ToObject(typeof(GetFrom), model.GetConstantValue(expr).Value);
                    
                    // 'Type' プロパティの値を取得
                    //int from = attribute.ConstructorArguments[0].Value is short ? (short)attribute.ConstructorArguments[0].Value : 0;
                    ITypeSymbol sourceType = fieldSymbol.Type;
                    
                    fieldSymbols.Add((fieldSymbol,sourceType, (GetFrom)parsed));
                    
                }
            }
            
            //クラス単位にまとめて、そこからpartialなクラスを生成したいので、
            //クラス名をキーにしてグループ化する
            foreach (var group in fieldSymbols.GroupBy(field=>field.field.ContainingType))
            {
                //classSourceにクラス定義のコードが入る
                var classSource = ProcessClass(group.Key, group.ToList());
                //クラス名.Generated.csという名前でコード生成
                context.AddSource
                    (
                        $"{group.Key.Name}.Generated.cs",
                        SourceText.From(classSource,Encoding.UTF8)
                    );

            }
            
        }
        
        private string ProcessClass(INamedTypeSymbol classSymbol, List<(IFieldSymbol field, ITypeSymbol sourceType , GetFrom from)> fieldSymbols)
        {
            var namespaceName = classSymbol.ContainingNamespace.IsGlobalNamespace ? "" : $"namespace {classSymbol.ContainingNamespace.ToDisplayString()}\n{{\n";
            var classDeclaration = $"public partial class {classSymbol.Name}\n{{\n";

            var builder = new StringBuilder();
            builder.Append(namespaceName);
            builder.Append(classDeclaration);

            foreach (var (field, sourceType , from) in fieldSymbols)
            {
                var className = sourceType.ToDisplayString();
                var propertyName = GetPropertyName(field.Name);
                bool isArray = sourceType is IArrayTypeSymbol;
                var rowClassName = className;
                if (className.EndsWith("[]"))
                {
                    rowClassName = className.Substring(0, className.Length - 2);
                }

                builder.Append($@"
    private {className} {propertyName} => {field.Name} is null 
                ? ({field.Name} = ");
                    
                if(from == GetFrom.This && !isArray)
                    builder.Append($"GetComponent<{className}>())");
                else if(from == GetFrom.Children && !isArray)
                    builder.Append($"GetComponentInChildren<{className}>(true))");
                else if(from == GetFrom.Parent && !isArray)
                    builder.Append($"GetComponentInParent<{className}>(true))");
                else if(from == GetFrom.This && isArray)
                    builder.Append($"GetComponents<{rowClassName}>())");
                else if(from == GetFrom.Children && isArray)
                    builder.Append($"GetComponentsInChildren<{rowClassName}>(true))");
                else if(from == GetFrom.Parent && isArray)
                    builder.Append($"GetComponentsInParent<{rowClassName}>(true))");
                else
                    builder.Append($"GetComponent<{className}>())");

                builder.Append($@"
                : {field.Name};");
            }

            builder.Append("\n}\n"); // Close class
            if (!classSymbol.ContainingNamespace.IsGlobalNamespace)
            {
                builder.Append("}\n"); // Close namespace
            }

            return builder.ToString();
        }

        
        private string GetPropertyName(string fieldName)
        {
            
            // 最初の大文字に変換可能な文字を探す
            for (int i = 0; i < fieldName.Length; i++)
            {
                if (char.IsLower(fieldName[i]))
                {
                    // 大文字に変換して、残りの文字列を結合
                    return char.ToUpper(fieldName[i]) + fieldName.Substring(i + 1);
                }
            }

            // 大文字に変換可能な文字がない場合
            return "NoLetterCanUppercase";
        }
        

    }

    class SyntaxReceiver : ISyntaxReceiver
    {
        public List<(FieldDeclarationSyntax field, AttributeSyntax attr)> TargetFields { get; } = new List<(FieldDeclarationSyntax field, AttributeSyntax attr)>();

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (syntaxNode is FieldDeclarationSyntax field)
            {
                foreach (var attributeList in field.AttributeLists)
                {
                    foreach (var attribute in attributeList.Attributes)
                    {
                        // ここで属性の名前をチェックします
                        if (attribute.Name.ToString().EndsWith("CompoPropAttribute") ||
                            attribute.Name.ToString().EndsWith("CompoProp")) // 短縮形も考慮
                        {
                            TargetFields.Add((field,attribute));
                            return; // 一致する属性が見つかったら、他の属性はチェックしない
                        }
                    }
                }
            }
        }
    }
}