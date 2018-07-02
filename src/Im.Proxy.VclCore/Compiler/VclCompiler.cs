﻿using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Antlr4.Runtime;
using Im.Proxy.VclCore.Model;
using Microsoft.Extensions.Caching.Memory;

namespace Im.Proxy.VclCore.Compiler
{
    public class VclCompiler
    {
        private readonly IMemoryCache _memCache;
        private readonly IVclTextFileProvider _fileProvider;

        public VclCompiler(
            IMemoryCache memCache,
            IVclTextFileProvider fileProvider)
        {
            _memCache = memCache;
            _fileProvider = fileProvider;
        }

        public void CompileAndBuildModule(string filename, string outputAssembly)
        {
            //var compilerResult = Compile(filename);

            // TODO: Create CodeDOM compile unit and namespace

            // TODO: Inject handler class

            // TODO: Inject assembly information attributes
            
            // TODO: Write assembly

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
                var vclContent = _fileProvider.GetFileContent(filename);

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

        public TResult CompileAndVisit<TResult>(string vclContent, IVclVisitor<TResult> visitor)
        {
            var cacheKey = $"CompiledParseTree:{vclContent.GetHashCode():X}";
            if (!_memCache.TryGetValue(cacheKey, out VclParser parser))
            {
                using (var textStream = new StringReader(vclContent))
                {
                    // Pass text stream through lexer for tokenising
                    var tokenStream = new AntlrInputStream(textStream);
                    var lexer = new VclLexer(tokenStream);

                    // Pass token stream through parser to product AST
                    var stream = new CommonTokenStream(lexer);
                    parser = new VclParser(stream);

                    // Cache parse tree for 120 seconds on sliding expiration
                    _memCache.Set(cacheKey, parser, TimeSpan.FromSeconds(120));
                }
            }

            return visitor.Visit(parser.compileUnit());
        }
    }

    public class VclCompilerContext
    {
        public CodeTypeDeclaration HandlerClass { get; set; }

        public IDictionary<string, CodeFieldReferenceExpression> ProbeReferences { get; } =
            new Dictionary<string, CodeFieldReferenceExpression>(StringComparer.OrdinalIgnoreCase);

        public IDictionary<string, CodeFieldReferenceExpression> AclReferences { get; } =
            new Dictionary<string, CodeFieldReferenceExpression>(StringComparer.OrdinalIgnoreCase);

        public IDictionary<string, CodeFieldReferenceExpression> BackendReferences { get; } =
            new Dictionary<string, CodeFieldReferenceExpression>(StringComparer.OrdinalIgnoreCase);

        public IDictionary<string, CodeStatementCollection> MethodStatements { get; } =
            new Dictionary<string, CodeStatementCollection>(StringComparer.OrdinalIgnoreCase)
            {
                { "vcl_init", new CodeStatementCollection() }
            };

        public CodeStatementCollection InitStatements => MethodStatements["vcl_init"];
    }
}
