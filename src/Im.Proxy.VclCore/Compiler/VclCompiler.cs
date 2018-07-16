using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Antlr4.Runtime;
using Im.Proxy.VclCore.Model;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;

namespace Im.Proxy.VclCore.Compiler
{
    public class VclCompiler
    {
        private readonly IMemoryCache _memCache;
        private readonly IFileProvider _fileProvider;

        public VclCompiler(
            IMemoryCache memCache,
            IFileProvider fileProvider)
        {
            _memCache = memCache;
            _fileProvider = fileProvider;
        }

        public void CompileAndBuildModule(string filename, string outputAssembly)
        {
            // Compile the file
            var compilerContext = Compile(filename);

            // Insert derived class methods
            foreach (var method in compilerContext.MethodDefinitions)
            {
                var systemMethodName = SystemFunctionToMethodInfoFactory
                    .GetSystemMethodName(method.Key);

                // System method overrides need a slightly different definition
                if (!string.IsNullOrWhiteSpace(systemMethodName))
                {
                    method.Value.Statements.Add(
                        new CodeMethodReturnStatement(
                            new CodeMethodInvokeExpression(
                                new CodeBaseReferenceExpression(),
                                systemMethodName)));
                }
                else
                {
                    method.Value.Statements.Add(
                        new CodeMethodReturnStatement(
                            new CodePrimitiveExpression(VclFrontendAction.NoOp)));
                }
            }

            // Create namespace Im.Proxy.Handlers
            var codeNamespace = new CodeNamespace("Im.Proxy.Handlers");
            codeNamespace.Types.Add(compilerContext.HandlerClass);

            // Create code unit
            var unit = new CodeCompileUnit();
            unit.Namespaces.Add(codeNamespace);
            unit.ReferencedAssemblies.Add(typeof(VclCompiler).Assembly.GetName().FullName);

            // TODO: Inject assembly information attributes

            // Write assembly
            var options =
                new CompilerParameters
                {
                    OutputAssembly = outputAssembly
                };
            var compileResults = CodeDomProvider
                .CreateProvider("CSharp")
                .CompileAssemblyFromDom(
                    options,
                    unit);

            // TODO: Sign assembly using our signing key

            // Finally return assembly name
        }

        public VclCompilerContext Compile(string filename)
        {
            var compilerContext = new VclCompilerContext();
            CompileFile(filename, compilerContext);
            return compilerContext;
        }

        private void CompileFile(string filename, VclCompilerContext compilerContext)
        {
            // Track files we compile so we only compile each file once
            // All files we bring in via include directive are added to queue
            var seenFiles = new List<string>();
            var filenameQueue = new Queue<string>();
            filenameQueue.Enqueue(filename);

            // Walk queue of files until it is empty
            while (filenameQueue.Any())
            {
                // Retrieve next file from the queue and skip if already processed
                filename = filenameQueue.Dequeue();
                if (seenFiles.Contains(filename))
                {
                    continue;
                }

                // Add to list of seen files and get file content
                seenFiles.Add(filename);
                var fileInfo = _fileProvider.GetFileInfo(filename);
                using (var fileStream = fileInfo.CreateReadStream())
                {
                    using (var reader = new StreamReader(fileStream))
                    {
                        // Read file content from reader
                        var vclContent = reader.ReadToEnd();

                        // Process include directives
                        var includeDirectiveCompiler = new VclCompileIncludes();
                        CompileAndVisit(vclContent, includeDirectiveCompiler);

                        // Queue any include directives we have found
                        foreach (var include in includeDirectiveCompiler.Files)
                        {
                            filenameQueue.Enqueue(include);
                        }

                        // Process all other content
                        CompileFileContent(vclContent, compilerContext);
                    }
                }
            }
        }

        private void CompileFileContent(string vclContent, VclCompilerContext compilerContext)
        {
            // Compile named probe entries
            var probeCompiler = new VclCompileNamedProbeObjects(compilerContext);
            CompileAndVisit(vclContent, probeCompiler);

            // Ensure we have a default probe in the named probe entries
            if (!compilerContext.ProbeReferences.ContainsKey("default"))
            {
                var fieldName = "default".SafeIdentifier("_probe");
                compilerContext.ProbeReferences.Add(
                    "default", new CodeFieldReferenceExpression(
                        new CodeThisReferenceExpression(), fieldName));

                // Create field and add to handler class
                compilerContext.HandlerClass.Members.Add(
                    new CodeMemberField(typeof(VclProbe), fieldName)
                    {
                        Attributes = MemberAttributes.Private,
                        InitExpression =
                            new CodeObjectCreateExpression(
                                typeof(VclProbe),
                                new CodePrimitiveExpression("default"))
                    });
            }

            // Compile named backend entries
            var backendCompiler = new VclCompileNamedBackendObjects(compilerContext);
            CompileAndVisit(vclContent, backendCompiler);

            // Compile named ACL entries
            var aclCompiler = new VclCompileNamedAclObjects(compilerContext);
            CompileAndVisit(vclContent, aclCompiler);

            // TODO: Compile named director entries (if we bother to implement)

            // Compile subroutines entries
            var subCompiler = new VclCompileNamedSubroutine(compilerContext);
            CompileAndVisit(vclContent, subCompiler);
        }

        public TResult CompileAndVisit<TResult>(string vclContent, IVclLangVisitor<TResult> visitor)
        {
            var cacheKey = $"CompiledParseTree:{vclContent.GetHashCode():X}";
            if (!_memCache.TryGetValue(cacheKey, out VclLangParser parser))
            {
                using (var textStream = new StringReader(vclContent))
                {
                    // Pass text stream through lexer for tokenising
                    var tokenStream = new AntlrInputStream(textStream);
                    var lexer = new VclLangLexer(tokenStream);

                    // Pass token stream through parser to product AST
                    var stream = new CommonTokenStream(lexer);
                    parser = new VclLangParser(stream);

                    // Cache parse tree for 120 seconds on sliding expiration
                    _memCache.Set(cacheKey, parser, TimeSpan.FromSeconds(120));
                }
            }

            return visitor.Visit(parser.compileUnit());
        }
    }
}
