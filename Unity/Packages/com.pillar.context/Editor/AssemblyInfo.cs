using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("PILLAR.Context.Editor.Tests")]
// Pipeline tests drive device-ness through ContextMetadataRegistry.OverrideProviders, which is
// internal. Without it their results would differ between the CI legs that have a twin framework
// installed and the ones that do not.
[assembly: InternalsVisibleTo("PILLAR.Context.Pipeline.Tests")]
