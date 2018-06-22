﻿using System;
using System.Linq.Expressions;
using System.Text;
using FluentAssertions;
using Im.Proxy.VclCore.Compiler;
using Im.Proxy.VclCore.Model;
using Im.Proxy.VclCore.UnitTests.Properties;
using Xunit;

namespace Im.Proxy.VclCore.UnitTests
{
    [Trait("", "")]
    public class VclGrammar_should
    {
        public static TheoryData<string, string[]> IncludeTestData()
        {
            return new TheoryData<string, string[]>
            {
                {
                    "include \"backends.vcl\";",
                    new[]
                    {
                        "Include backends.vcl"
                    }
                },
                {
                    "include \"backends.vcl\"; include \"scarface.vcl\"",
                    new[]
                    {
                        "Include backends.vcl",
                        "Include scarface.vcl"
                    }
                },
                {
                    "include \"backends.vcl\"; include \"scarface.vcl\"; include \"smartchicken.vcl\";",
                    new[]
                    {
                        "Include backends.vcl",
                        "Include scarface.vcl",
                        "Include smartchicken.vcl"
                    }
                }
            };
        }

        public static TheoryData<string, string[]> MethodTestData()
        {
            return new TheoryData<string, string[]>
            {
                {
                    "sub my-weird-sub { if (req.ip == \"192.168.0.1\") { } }",
                    new[]
                    {
                        "Function my-weird-sub",
                        "Enter compound statement",
                        "If req.ip==\"192.168.0.1\"",
                        "Enter compound statement",
                        "Leave compound statement",
                        "Leave compound statement",
                    }
                },
                {
                    "sub my-weird-sub { if (req.ip == \"192.168.0.1\") { return(nuts); } }",
                    new[]
                    {
                        "Function my-weird-sub",
                        "Enter compound statement",
                        "If req.ip==\"192.168.0.1\"",
                        "Enter compound statement",
                        "Return nuts",
                        "Leave compound statement",
                        "Leave compound statement",
                    }
                },
                {
                    "sub my-weird-sub { if (req.ip == \"192.168.0.1\") { return(nuts); } else { return (foo); } }",
                    new[]
                    {
                        "Function my-weird-sub",
                        "Enter compound statement",
                        "If req.ip==\"192.168.0.1\"",
                        "Enter compound statement",
                        "Return nuts",
                        "Leave compound statement",
                        "Enter compound statement",
                        "Return foo",
                        "Leave compound statement",
                        "Leave compound statement",
                    }
                }
            };
        }

        [Theory]
        [MemberData(nameof(IncludeTestData))]
        public void EvaluateIncludeStatements(string vclText, params string[] expectedOperations)
        {
            // Arrange
            var visitor = new VclTestVisitor();

            // Act
            new VclCompiler().CompileAndVisit(vclText, visitor);

            // Assert
            visitor.Operations.Should().BeEquivalentTo(expectedOperations);
        }

        [Theory]
        [MemberData(nameof(MethodTestData))]
        public void EvaluateMethods(string vclText, params string[] expectedOperations)
        {
            // Arrange
            var visitor = new VclTestVisitor();

            // Act
            new VclCompiler().CompileAndVisit(vclText, visitor);

            // Assert
            visitor.Operations.Should().BeEquivalentTo(expectedOperations);
        }

        [Fact(DisplayName = "Given named probe definition, When compiled, Then valid probe object is created.")]
        public void GenerateNamedProbeExpression()
        {
            var vclText =
                "probe myprobe {" +
                "    .url = \"/healthcheck/\";" +
                "    .expected_response = 201;" +
                "    .timeout = 5s;" +
                "    .window = 10;" +
                "    .interval = 1m;" +
                "    .threshold = 7;" +
                "    .initial = 6;" +
                "}";

            // Arrange
            var visitor = new VclCompileNamedProbeObjects();

            // Act
            new VclCompiler().CompileAndVisit(vclText, visitor);

            // Assert
            Assert.True(visitor.ProbeExpressions.ContainsKey("myprobe"));

            var probe = Expression.Lambda<Func<VclProbe>>(
                visitor.ProbeExpressions["myprobe"]).Compile()();
            probe.Should().BeEquivalentTo(
                new
                {
                    Name = "myprobe",
                    Url = "/healthcheck/",
                    ExpectedResponse = 201,
                    Window = 10,
                    Threshold = 7,
                    Initial = 6,
                    Timeout = TimeSpan.FromSeconds(5),
                    Interval = TimeSpan.FromMinutes(1)
                });
        }

        [Fact]
        public void EvaluateHitchedUat()
        {
            // Arrange
            var vclText = Encoding.UTF8.GetString(Resources.hitcheduat);
            var visitor = new VclTestVisitor();

            // Act
            new VclCompiler().CompileAndVisit(vclText, visitor);

            // Assert
            visitor.Operations.Should().BeEquivalentTo(
                "",
                "",
                "");
        }
    }
}