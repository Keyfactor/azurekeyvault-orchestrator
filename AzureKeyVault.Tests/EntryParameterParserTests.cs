// Copyright 2025 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0

using System.Collections.Generic;
using FluentAssertions;
using Keyfactor.Logging;
using Xunit;

namespace Keyfactor.Extensions.Orchestrator.AzureKeyVault.Tests
{
    public class EntryParameterParserTests
    {
        private static readonly Microsoft.Extensions.Logging.ILogger Logger =
            LogHandler.GetClassLogger<EntryParameterParserTests>();

        [Fact]
        public void Present_NonCritical_UsesProvidedValue()
        {
            var jobProperties = new Dictionary<string, object> { { "Foo", true } };
            var definitions = new[] { new EntryParameterDefinition("Foo", critical: false, defaultValue: false) };

            var ok = EntryParameterParser.TryParse(jobProperties, definitions, Logger, out var values, out var error);

            ok.Should().BeTrue();
            error.Should().BeNull();
            values["Foo"].Should().Be(true);
        }

        [Fact]
        public void Missing_NonCritical_DoesNotThrow_AndUsesDefault()
        {
            var jobProperties = new Dictionary<string, object>(); // Foo not provided
            var definitions = new[] { new EntryParameterDefinition("Foo", critical: false, defaultValue: "the-default") };

            var act = () => EntryParameterParser.TryParse(jobProperties, definitions, Logger, out var values, out var error);

            act.Should().NotThrow();
            EntryParameterParser.TryParse(jobProperties, definitions, Logger, out var result, out var err)
                .Should().BeTrue();
            err.Should().BeNull();
            result["Foo"].Should().Be("the-default");
        }

        [Fact]
        public void Missing_Critical_ReturnsFalseWithClearError_DoesNotThrow()
        {
            var jobProperties = new Dictionary<string, object>(); // Required missing
            var definitions = new[] { new EntryParameterDefinition("Required", critical: true) };

            var act = () => EntryParameterParser.TryParse(jobProperties, definitions, Logger, out _, out _);
            act.Should().NotThrow("a missing critical parameter should fail cleanly, not throw");

            var ok = EntryParameterParser.TryParse(jobProperties, definitions, Logger, out var values, out var error);

            ok.Should().BeFalse();
            error.Should().NotBeNullOrEmpty();
            error.Should().Contain("Required", "the error should name the missing parameter");
        }

        [Fact]
        public void Present_Critical_Succeeds()
        {
            var jobProperties = new Dictionary<string, object> { { "Required", "supplied-value" } };
            var definitions = new[] { new EntryParameterDefinition("Required", critical: true) };

            var ok = EntryParameterParser.TryParse(jobProperties, definitions, Logger, out var values, out var error);

            ok.Should().BeTrue();
            error.Should().BeNull();
            values["Required"].Should().Be("supplied-value");
        }

        [Fact]
        public void ExtraUnrecognizedField_DoesNotThrow_AndStillParsesKnownFields()
        {
            var jobProperties = new Dictionary<string, object>
            {
                { "Foo", true },
                { "SomeFutureFieldWeDontKnowAbout", "whatever" }
            };
            var definitions = new[] { new EntryParameterDefinition("Foo", critical: false, defaultValue: false) };

            var act = () => EntryParameterParser.TryParse(jobProperties, definitions, Logger, out _, out _);
            act.Should().NotThrow("an unrecognized extra entry parameter should be ignored, not fatal");

            var ok = EntryParameterParser.TryParse(jobProperties, definitions, Logger, out var values, out var error);

            ok.Should().BeTrue();
            error.Should().BeNull();
            values.Should().ContainKey("Foo");
            values.Should().NotContainKey("SomeFutureFieldWeDontKnowAbout",
                "unrecognized fields are ignored, not carried through");
        }

        [Fact]
        public void MixOfMissingNonCriticalAndExtraField_StillSucceeds()
        {
            var jobProperties = new Dictionary<string, object>
            {
                { "UnknownExtra", 123 }
                // "Foo" intentionally omitted
            };
            var definitions = new[] { new EntryParameterDefinition("Foo", critical: false, defaultValue: "default-foo") };

            var ok = EntryParameterParser.TryParse(jobProperties, definitions, Logger, out var values, out var error);

            ok.Should().BeTrue();
            error.Should().BeNull();
            values["Foo"].Should().Be("default-foo");
        }

        [Fact]
        public void NullJobProperties_TreatedAsEmpty_DoesNotThrow()
        {
            var definitions = new[] { new EntryParameterDefinition("Foo", critical: false, defaultValue: false) };

            var act = () => EntryParameterParser.TryParse(null, definitions, Logger, out var values, out var error);

            act.Should().NotThrow();
        }
    }
}
